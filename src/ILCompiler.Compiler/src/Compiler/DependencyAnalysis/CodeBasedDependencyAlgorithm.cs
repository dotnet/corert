// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.DependencyAnalysis;

using DependencyList=ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;
using DependencyListEntry=ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyListEntry;

namespace ILCompiler.DependencyAnalysis
{
    public static class CodeBasedDependencyAlgorithm
    {
        public static void AddDependenciesDueToReflectability(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            if (factory.MetadataManager.IsReflectionInvokable(method))
            {
                if (dependencies == null)
                    dependencies = new DependencyList();

                // The fact we need to exclude Project N is likely a bug in Project N metadata manager
                if (factory.Target.Abi != TargetAbi.ProjectN)
                    dependencies.Add(factory.MaximallyConstructableType(method.OwningType), "Reflection invoke");

                if (factory.MetadataManager.HasReflectionInvokeStubForInvokableMethod(method)
                    && ((factory.Target.Abi != TargetAbi.ProjectN) || ProjectNDependencyBehavior.EnableFullAnalysis || !method.IsCanonicalMethod(CanonicalFormKind.Any)))
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
                if ((factory.Target.Abi == TargetAbi.ProjectN) && !ProjectNDependencyBehavior.EnableFullAnalysis)
                {
                    // ProjectN compilation currently computes the presence of these stubs independent from dependency analysis here
                    // TODO: fix that issue and remove this odd treatment of unboxing stubs
                    if (!method.HasInstantiation && method.OwningType.IsValueType && method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any) && !method.Signature.IsStatic)
                        skipUnboxingStubDependency = true;
                }

                if (method.OwningType.IsValueType && !method.Signature.IsStatic && !skipUnboxingStubDependency)
                    dependencies.Add(new DependencyListEntry(factory.ExactCallableAddress(method, isUnboxingStub: true), "Reflection unboxing stub"));

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
                GenericMethodsTemplateMap.GetTemplateMethodDependencies(ref dependencies, factory, method);
            }
            else
            {
                TypeDesc owningTemplateType = method.OwningType;

                // Unboxing and Instantiating stubs use a different type as their template
                if (factory.TypeSystemContext.IsSpecialUnboxingThunk(method))
                    owningTemplateType = factory.TypeSystemContext.GetTargetOfSpecialUnboxingThunk(method).OwningType;

                GenericTypesTemplateMap.GetTemplateTypeDependencies(ref dependencies, factory, owningTemplateType);
            }

            factory.InteropStubManager.AddDependeciesDueToPInvoke(ref dependencies, factory, method);

            if (method.IsIntrinsic && factory.Target.Abi != TargetAbi.ProjectN && factory.MetadataManager.SupportsReflection)
            {
                if (method.OwningType is MetadataType owningType)
                {
                    string name = method.Name;

                    switch (name)
                    {
                        // The general purpose code in Comparer/EqualityComparer Create method depends on the template
                        // type loader being able to load the necessary types at runtime.
                        case "Create":
                            if (method.IsSharedByGenericInstantiations
                                && owningType.Module == factory.TypeSystemContext.SystemModule
                                && owningType.Namespace == "System.Collections.Generic")
                            {
                                TypeDesc[] templateDependencies = null;

                                if (owningType.Name == "Comparer`1")
                                {
                                    templateDependencies = Internal.IL.Stubs.ComparerIntrinsics.GetPotentialComparersForType(
                                        owningType.Instantiation[0]);
                                }
                                else if (owningType.Name == "EqualityComparer`1")
                                {
                                    templateDependencies = Internal.IL.Stubs.ComparerIntrinsics.GetPotentialEqualityComparersForType(
                                        owningType.Instantiation[0]);
                                }

                                if (templateDependencies != null)
                                {
                                    dependencies = dependencies ?? new DependencyList();
                                    foreach (TypeDesc templateType in templateDependencies)
                                    {
                                        dependencies.Add(factory.NativeLayout.TemplateTypeLayout(templateType), "Generic comparer");
                                    }
                                }
                            }
                            break;
                    }
                }
            }
        }
    }
}
