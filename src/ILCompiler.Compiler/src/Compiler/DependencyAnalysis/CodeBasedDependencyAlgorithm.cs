// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem.Interop;

using DependencyList=ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;
using DependencyListEntry=ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyListEntry;

namespace ILCompiler.DependencyAnalysis
{
    public static class CodeBasedDependencyAlgorithm
    {
        public static void AddDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            // TODO: https://github.com/dotnet/corert/issues/3224
            // Reflection invoke stub handling is here because in the current reflection model we reflection-enable
            // all methods that are compiled. Ideally the list of reflection enabled methods should be known before
            // we even start the compilation process (with the invocation stubs being compilation roots like any other).
            // The existing model has it's problems: e.g. the invocability of the method depends on inliner decisions.
            if (factory.MetadataManager.IsReflectionInvokable(method))
            {
                if (dependencies == null)
                    dependencies = new DependencyList();

                if (factory.MetadataManager.HasReflectionInvokeStubForInvokableMethod(method)
                    && ((factory.Target.Abi != TargetAbi.ProjectN) || !method.IsCanonicalMethod(CanonicalFormKind.Any)))
                {
                    MethodDesc canonInvokeStub = factory.MetadataManager.GetCanonicalReflectionInvokeStub(method);
                    if (canonInvokeStub.IsSharedByGenericInstantiations)
                    {
                        dependencies.Add(new DependencyListEntry(factory.MetadataManager.DynamicInvokeTemplateData, "Reflection invoke template data"));
                        factory.MetadataManager.DynamicInvokeTemplateData.AddDependenciesDueToInvokeTemplatePresence(ref dependencies, factory, canonInvokeStub);
                    }
                    else
                        dependencies.Add(new DependencyListEntry(factory.MethodEntrypoint(canonInvokeStub), "Reflection invoke"));
                }

                bool skipUnboxingStubDependency = false;
                if (factory.Target.Abi == TargetAbi.ProjectN)
                {
                    // ProjectN compilation currently computes the presence of these stubs independent from dependency analysis here
                    // TODO: fix that issue and remove this odd treatment of unboxing stubs
                    if (!method.HasInstantiation && method.OwningType.IsValueType && method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any) && !method.Signature.IsStatic)
                        skipUnboxingStubDependency = true;
                }

                if (method.OwningType.IsValueType && !method.Signature.IsStatic && !skipUnboxingStubDependency)
                    dependencies.Add(new DependencyListEntry(factory.MethodEntrypoint(method, unboxingStub: true), "Reflection unboxing stub"));

                // If the method is defined in a different module than this one, a metadata token isn't known for performing the reference
                // Use a name/sig reference instead.
                if (!factory.MetadataManager.WillUseMetadataTokenToReferenceMethod(method))
                {
                    dependencies.Add(new DependencyListEntry(factory.NativeLayout.PlacedSignatureVertex(factory.NativeLayout.MethodNameAndSignatureVertex(method.GetTypicalMethodDefinition())),
                        "Non metadata-local method reference"));
                }

                if (method.HasInstantiation && method.IsCanonicalMethod(CanonicalFormKind.Universal))
                {
                    dependencies.Add(new DependencyListEntry(factory.NativeLayout.PlacedSignatureVertex(factory.NativeLayout.MethodNameAndSignatureVertex(method)),
                        "UniversalCanon signature of method"));
                }

                ReflectionVirtualInvokeMapNode.GetVirtualInvokeMapDependencies(ref dependencies, factory, method);
            }
        }

        public static void AddDependenciesDueToMethodCodePresence(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            factory.MetadataManager.GetDependenciesDueToReflectability(ref dependencies, factory, method);

            if (method.HasInstantiation)
            {
                ExactMethodInstantiationsNode.GetExactMethodInstantiationDependenciesForMethod(ref dependencies, factory, method);
                
                if (method.IsVirtual)
                {
                    // Generic virtual methods dependency tracking
                    dependencies = dependencies ?? new DependencyList();
                    dependencies.Add(new DependencyListEntry(factory.GVMDependencies(method), "GVM Dependencies Support"));
                }

                GenericMethodsTemplateMap.GetTemplateMethodDependencies(ref dependencies, factory, method);
                AddMarshalAPIsGenericDependencies(ref dependencies, factory, method);
            }
            else
            {
                TypeDesc owningTemplateType = method.OwningType;

                // Unboxing and Instantiating stubs use a different type as their template
                if (factory.TypeSystemContext.IsSpecialUnboxingThunk(method))
                    owningTemplateType = factory.TypeSystemContext.GetTargetOfSpecialUnboxingThunk(method).OwningType;

                GenericTypesTemplateMap.GetTemplateTypeDependencies(ref dependencies, factory, owningTemplateType);
            }

            // On Project N, the compiler doesn't generate the interop code on the fly
            if (method.Context.Target.Abi != TargetAbi.ProjectN)
            {
                if (method.IsPInvoke)
                {
                    if (dependencies == null)
                        dependencies = new DependencyList();

                    MethodSignature methodSig = method.Signature;
                    AddPInvokeParameterDependencies(ref dependencies, factory, methodSig.ReturnType);

                    for (int i = 0; i < methodSig.Length; i++)
                    {
                        AddPInvokeParameterDependencies(ref dependencies, factory, methodSig[i]);
                    }
                }
            }
        }

        public static void AddInterestingInteropConstructedTypeDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.IsDelegate)
            {
                var delegateType = type as MetadataType;
                if (delegateType != null && delegateType.HasCustomAttribute("System.Runtime.InteropServices", "UnmanagedFunctionPointerAttribute"))
                {
                    AddDependenciesDuePInvokeDelegate(ref dependencies, factory, delegateType);
                }
            }
            else if (type.IsValueType && type.IsTypeDefinition && !(type is NativeStructType))
            {
                var structType = type as MetadataType;
                if (structType != null && structType.HasCustomAttribute("System.Runtime.InteropServices", "StructLayoutAttribute"))
                {
                    AddDependenciesDuePInvokeStruct(ref dependencies, factory, type);
                }
            }
        }


        /// <summary>
        /// For Marshal generic APIs(eg. Marshal.StructureToPtr<T>, GetFunctionPointerForDelegate) we add
        /// the generic parameter as dependencies so that we can generate runtime data for them
        /// </summary>
        public static void AddMarshalAPIsGenericDependencies(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            TypeDesc owningType = method.OwningType;
            MetadataType metadataType = owningType as MetadataType;

            if (metadataType != null)
            {
                if (metadataType.Name == "Marshal" && metadataType.Namespace == "System.Runtime.InteropServices")
                {
                    foreach (TypeDesc type in method.Instantiation)
                    {
                        AddPInvokeParameterDependencies(ref dependencies, factory, type);
                    }
                }
            }
        }

        public static void AddPInvokeParameterDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc parameter)
        {
            AddDependenciesDuePInvokeDelegate(ref dependencies, factory, parameter);
            AddDependenciesDuePInvokeStruct(ref dependencies, factory, parameter);
        }

        public static void AddDependenciesDuePInvokeDelegate(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.IsDelegate)
            {
                dependencies.Add(factory.NecessaryTypeSymbol(type), "Delegate Marshalling Stub");

                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetOpenStaticDelegateMarshallingStub(type)), "Delegate Marshalling Stub");
                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetClosedDelegateMarshallingStub(type)), "Delegate Marshalling Stub");
                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetForwardDelegateCreationStub(type)), "Delegate Marshalling Stub");
            }
        }

        public static void AddDependenciesDuePInvokeStruct(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (MarshalHelpers.IsStructMarshallingRequired(type))
            {
                dependencies.Add(factory.NecessaryTypeSymbol(type), "Struct Marshalling Stub");

                var stub = (Internal.IL.Stubs.StructMarshallingThunk)factory.InteropStubManager.GetStructMarshallingManagedToNativeStub(type);
                dependencies.Add(factory.ConstructedTypeSymbol(factory.InteropStubManager.GetStructMarshallingType(type)), "Struct Marshalling Type");
                dependencies.Add(factory.MethodEntrypoint(stub), "Struct Marshalling stub");
                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetStructMarshallingNativeToManagedStub(type)), "Struct Marshalling stub");
                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetStructMarshallingCleanupStub(type)), "Struct Marshalling stub");

                foreach (var inlineArrayCandidate in stub.GetInlineArrayCandidates())
                {
                    dependencies.Add(factory.ConstructedTypeSymbol(factory.InteropStubManager.GetInlineArrayType(inlineArrayCandidate)), "Struct Marshalling Type");
                    foreach (var method in inlineArrayCandidate.ElementType.GetMethods())
                    {
                        dependencies.Add(factory.MethodEntrypoint(method), "inline array marshalling stub");
                    }
                }
            }
        }
    }
}
