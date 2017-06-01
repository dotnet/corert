// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;
using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing stub methods for interop
    /// </summary>
    public sealed class InteropStubManager
    {
        private readonly CompilationModuleGroup _compilationModuleGroup;
        private readonly CompilerTypeSystemContext _typeSystemContext;
        internal HashSet<TypeDesc> _delegateMarshalingTypes = new HashSet<TypeDesc>();
        private HashSet<NativeStructType> _structMarshallingTypes = new HashSet<NativeStructType>();
        private ModuleDesc _interopModule;
        private const string _interopModuleName = "System.Private.Interop";

        public InteropStateManager InteropStateManager
        {
            get;
        }

        public InteropStubManager(CompilationModuleGroup compilationModuleGroup, CompilerTypeSystemContext typeSystemContext, InteropStateManager interopStateManager)
        {
            _compilationModuleGroup = compilationModuleGroup;
            _typeSystemContext = typeSystemContext;
            InteropStateManager = interopStateManager;
            _interopModule = typeSystemContext.GetModuleForSimpleName(_interopModuleName);
        }
    
        internal MethodDesc GetOpenStaticDelegateMarshallingStub(TypeDesc delegateType)
        {
            var stub = InteropStateManager.GetOpenStaticDelegateMarshallingThunk(delegateType);
            Debug.Assert(stub != null);

            _delegateMarshalingTypes.Add(delegateType);
            return stub;
        }

        internal MethodDesc GetClosedDelegateMarshallingStub(TypeDesc delegateType)
        {
            var stub = InteropStateManager.GetClosedDelegateMarshallingThunk(delegateType);
            Debug.Assert(stub != null);

            _delegateMarshalingTypes.Add(delegateType);
            return stub;
        }
        internal MethodDesc GetForwardDelegateCreationStub(TypeDesc delegateType)
        {
            var stub = InteropStateManager.GetForwardDelegateCreationThunk(delegateType);
            Debug.Assert(stub != null);

            _delegateMarshalingTypes.Add(delegateType);
            return stub;
        }

        internal TypeDesc GetStructMarshallingType(TypeDesc structType)
        {
            NativeStructType nativeType = InteropStateManager.GetStructMarshallingNativeType(structType);
            Debug.Assert(nativeType != null);
            _structMarshallingTypes.Add(nativeType);
            return nativeType;
        }

        internal MethodDesc GetStructMarshallingManagedToNativeStub(TypeDesc structType)
        {
            MethodDesc stub = InteropStateManager.GetStructMarshallingManagedToNativeThunk(structType);
            Debug.Assert(stub != null);
            return stub;
        }

        internal MethodDesc GetStructMarshallingNativeToManagedStub(TypeDesc structType)
        {
            MethodDesc stub = InteropStateManager.GetStructMarshallingNativeToManagedThunk(structType);
            Debug.Assert(stub != null);
            return stub;
        }

        internal MethodDesc GetStructMarshallingCleanupStub(TypeDesc structType)
        {
            MethodDesc stub = InteropStateManager.GetStructMarshallingCleanupThunk(structType);
            Debug.Assert(stub != null);
            return stub;
        }

        internal TypeDesc GetInlineArrayType(InlineArrayCandidate candidate)
        {
            TypeDesc inlineArrayType = InteropStateManager.GetInlineArrayType(candidate);
            Debug.Assert(inlineArrayType != null);
            return inlineArrayType;
        }

        internal struct DelegateMarshallingThunks
        {
            public TypeDesc DelegateType;
            public MethodDesc OpenStaticDelegateMarshallingThunk;
            public MethodDesc ClosedDelegateMarshallingThunk;
            public MethodDesc DelegateCreationThunk;
        }

        internal IEnumerable<DelegateMarshallingThunks> GetDelegateMarshallingThunks()
        {
            foreach (var delegateType in _delegateMarshalingTypes)
            {
                yield return 
                    new DelegateMarshallingThunks()
                    {
                        DelegateType = delegateType,
                        OpenStaticDelegateMarshallingThunk = InteropStateManager.GetOpenStaticDelegateMarshallingThunk(delegateType),
                        ClosedDelegateMarshallingThunk = InteropStateManager.GetClosedDelegateMarshallingThunk(delegateType),
                        DelegateCreationThunk = InteropStateManager.GetForwardDelegateCreationThunk(delegateType)
                    };
            }
        }

        internal struct StructMarshallingThunks
        {
            public TypeDesc StructType;
            public NativeStructType NativeStructType;
            public MethodDesc MarshallingThunk;
            public MethodDesc UnmarshallingThunk;
            public MethodDesc CleanupThunk;
        }

        internal IEnumerable<StructMarshallingThunks> GetStructMarshallingTypes()
        {
                foreach (var nativeStuctType in _structMarshallingTypes)
            {
                yield return
                    new StructMarshallingThunks()
                    {
                        StructType = nativeStuctType.ManagedStructType,
                        NativeStructType = nativeStuctType,
                        MarshallingThunk = InteropStateManager.GetStructMarshallingManagedToNativeThunk(nativeStuctType.ManagedStructType),
                        UnmarshallingThunk = InteropStateManager.GetStructMarshallingNativeToManagedThunk(nativeStuctType.ManagedStructType),
                        CleanupThunk = InteropStateManager.GetStructMarshallingCleanupThunk(nativeStuctType.ManagedStructType)
                    };
            }
        }

        public void AddDependeciesDueToPInvoke(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            // On Project N, the compiler doesn't generate the interop code on the fly
            if (method.Context.Target.Abi != TargetAbi.ProjectN)
            {
                if (method.IsPInvoke)
                {
                    dependencies = dependencies ?? new DependencyList();

                    MethodSignature methodSig = method.Signature;
                    AddPInvokeParameterDependencies(ref dependencies, factory, methodSig.ReturnType);

                    for (int i = 0; i < methodSig.Length; i++)
                    {
                        AddPInvokeParameterDependencies(ref dependencies, factory, methodSig[i]);
                    }
                }

                if (method.HasInstantiation)
                {
                    dependencies = dependencies ?? new DependencyList();
                    AddMarshalAPIsGenericDependencies(ref dependencies, factory, method);
                }
            }
        }

        public void AddInterestingInteropConstructedTypeDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.IsDelegate)
            {
                var delegateType = type as MetadataType;
                if (delegateType != null && delegateType.HasCustomAttribute("System.Runtime.InteropServices", "UnmanagedFunctionPointerAttribute"))
                {
                    AddDependenciesDueToPInvokeDelegate(ref dependencies, factory, delegateType);
                }
            }

            //
            //  https://github.com/dotnet/corert/issues/3763
            // TODO: Add an attribute which indicates a struct is interesting for interop
            //
            //else if (type.IsValueType && type.IsTypeDefinition && !(type is NativeStructType))
            //{
            //    var structType = type as MetadataType;
            //    if (structType != null && structType.HasCustomAttribute("System.Runtime.InteropServices", "StructLayoutAttribute"))
            //    {
            //        AddDependenciesDuePInvokeStruct(ref dependencies, factory, type);
            //    }
            //}
        }


        /// <summary>
        /// For Marshal generic APIs(eg. Marshal.StructureToPtr<T>, GetFunctionPointerForDelegate) we add
        /// the generic parameter as dependencies so that we can generate runtime data for them
        /// </summary>
        public void AddMarshalAPIsGenericDependencies(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            Debug.Assert(method.HasInstantiation);

            TypeDesc owningType = method.OwningType;
            MetadataType metadataType = owningType as MetadataType;
            if (metadataType != null && metadataType.Module == _interopModule)
            {
                if (metadataType.Name == "Marshal" && metadataType.Namespace == "System.Runtime.InteropServices")
                {
                    string methodName = method.Name;
                    if (methodName == "GetFunctionPointerForDelegate" ||
                        methodName == "GetDelegateForFunctionPointer" ||
                        methodName == "PtrToStructure" ||
                        methodName == "StructureToPtr" ||
                        methodName == "SizeOf" ||
                        methodName == "OffsetOf")
                    {
                        foreach (TypeDesc type in method.Instantiation)
                        {
                            AddPInvokeParameterDependencies(ref dependencies, factory, type);
                        }
                    }
                }
            }
        }

        public void AddPInvokeParameterDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc parameter)
        {
            AddDependenciesDueToPInvokeDelegate(ref dependencies, factory, parameter);
            AddDependenciesDueToPInvokeStruct(ref dependencies, factory, parameter);
        }

        public void AddDependenciesDueToPInvokeDelegate(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.IsDelegate)
            {
                dependencies.Add(factory.NecessaryTypeSymbol(type), "Delegate Marshalling Stub");

                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetOpenStaticDelegateMarshallingStub(type)), "Delegate Marshalling Stub");
                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetClosedDelegateMarshallingStub(type)), "Delegate Marshalling Stub");
                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetForwardDelegateCreationStub(type)), "Delegate Marshalling Stub");
            }
        }

        public void AddDependenciesDueToPInvokeStruct(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (MarshalHelpers.IsStructMarshallingRequired(type))
            {
                dependencies.Add(factory.NecessaryTypeSymbol(type), "Struct Marshalling Stub");

                var stub = (Internal.IL.Stubs.StructMarshallingThunk)factory.InteropStubManager.GetStructMarshallingManagedToNativeStub(type);
                dependencies.Add(factory.NecessaryTypeSymbol(factory.InteropStubManager.GetStructMarshallingType(type)), "Struct Marshalling Type");
                dependencies.Add(factory.MethodEntrypoint(stub), "Struct Marshalling stub");
                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetStructMarshallingNativeToManagedStub(type)), "Struct Marshalling stub");
                dependencies.Add(factory.MethodEntrypoint(factory.InteropStubManager.GetStructMarshallingCleanupStub(type)), "Struct Marshalling stub");

                foreach (var inlineArrayCandidate in stub.GetInlineArrayCandidates())
                {
                    foreach (var method in inlineArrayCandidate.ElementType.GetMethods())
                    {
                        dependencies.Add(factory.MethodEntrypoint(method), "inline array marshalling stub");
                    }
                }
            }
        }
    }
}
