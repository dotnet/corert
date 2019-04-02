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
using ReflectionMapBlob = Internal.Runtime.ReflectionMapBlob;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing stub methods for interop
    /// </summary>
    public sealed class CompilerGeneratedInteropStubManager : InteropStubManager
    {
        internal HashSet<TypeDesc> _delegateMarshalingTypes = new HashSet<TypeDesc>();
        private HashSet<TypeDesc> _structMarshallingTypes = new HashSet<TypeDesc>();

        public CompilerGeneratedInteropStubManager(CompilationModuleGroup compilationModuleGroup, CompilerTypeSystemContext typeSystemContext, InteropStateManager interopStateManager) : 
            base(compilationModuleGroup, typeSystemContext, interopStateManager)
        {
        }

        private MethodDesc GetOpenStaticDelegateMarshallingStub(TypeDesc delegateType)
        {
            var stub = InteropStateManager.GetOpenStaticDelegateMarshallingThunk(delegateType);
            Debug.Assert(stub != null);

            _delegateMarshalingTypes.Add(delegateType);
            return stub;
        }

        private MethodDesc GetClosedDelegateMarshallingStub(TypeDesc delegateType)
        {
            var stub = InteropStateManager.GetClosedDelegateMarshallingThunk(delegateType);
            Debug.Assert(stub != null);

            _delegateMarshalingTypes.Add(delegateType);
            return stub;
        }
        private MethodDesc GetForwardDelegateCreationStub(TypeDesc delegateType)
        {
            var stub = InteropStateManager.GetForwardDelegateCreationThunk(delegateType);
            Debug.Assert(stub != null);

            _delegateMarshalingTypes.Add(delegateType);
            return stub;
        }

        private MethodDesc GetStructMarshallingManagedToNativeStub(TypeDesc structType)
        {
            MethodDesc stub = InteropStateManager.GetStructMarshallingManagedToNativeThunk(structType);
            Debug.Assert(stub != null);

            _structMarshallingTypes.Add(structType);
            return stub;
        }

        private MethodDesc GetStructMarshallingNativeToManagedStub(TypeDesc structType)
        {
            MethodDesc stub = InteropStateManager.GetStructMarshallingNativeToManagedThunk(structType);
            Debug.Assert(stub != null);

            _structMarshallingTypes.Add(structType);
            return stub;
        }

        private MethodDesc GetStructMarshallingCleanupStub(TypeDesc structType)
        {
            MethodDesc stub = InteropStateManager.GetStructMarshallingCleanupThunk(structType);
            Debug.Assert(stub != null);

            _structMarshallingTypes.Add(structType);
            return stub;
        }

        private TypeDesc GetInlineArrayType(InlineArrayCandidate candidate)
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
            foreach (var structType in _structMarshallingTypes)
            {
                yield return
                    new StructMarshallingThunks()
                    {
                        StructType = structType,
                        NativeStructType = InteropStateManager.GetStructMarshallingNativeType(structType),
                        MarshallingThunk = InteropStateManager.GetStructMarshallingManagedToNativeThunk(structType),
                        UnmarshallingThunk = InteropStateManager.GetStructMarshallingNativeToManagedThunk(structType),
                        CleanupThunk = InteropStateManager.GetStructMarshallingCleanupThunk(structType)
                    };
            }
        }
        
        private void AddDependenciesDueToPInvokeStructDelegateField(ref DependencyList dependencies, NodeFactory factory, TypeDesc typeDesc)
        {
            if (typeDesc is ByRefType)
            {
                typeDesc = typeDesc.GetParameterType();
            }

            MetadataType metadataType = typeDesc as MetadataType;
            if (metadataType != null) 
            {
                foreach (FieldDesc field in metadataType.GetFields())
                {
                    if (field.IsStatic)
                    {
                        continue;
                    }
                    TypeDesc fieldType = field.FieldType;

                    if (fieldType.IsDelegate)
                    {
                        AddDependenciesDueToPInvokeDelegate(ref dependencies, factory, fieldType);
                    }
                    else if (MarshalHelpers.IsStructMarshallingRequired(fieldType))
                    {
                        AddDependenciesDueToPInvokeStructDelegateField(ref dependencies, factory, fieldType);
                    }
                }
            }
        }

        public override void AddDependeciesDueToPInvoke(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            if (method.IsPInvoke)
            {
                dependencies = dependencies ?? new DependencyList();

                MethodSignature methodSig = method.Signature;
                AddDependenciesDueToPInvokeDelegate(ref dependencies, factory, methodSig.ReturnType);

                // struct may contain delegate fields, hence we need to add dependencies for it
                if (MarshalHelpers.IsStructMarshallingRequired(methodSig.ReturnType))
                {
                    AddDependenciesDueToPInvokeStructDelegateField(ref dependencies, factory, methodSig.ReturnType);
                }

                for (int i = 0; i < methodSig.Length; i++)
                {
                    AddDependenciesDueToPInvokeDelegate(ref dependencies, factory, methodSig[i]);
                    if (MarshalHelpers.IsStructMarshallingRequired(methodSig[i]))
                    {
                        AddDependenciesDueToPInvokeStructDelegateField(ref dependencies, factory, methodSig[i]);
                    }
                }
            }

            if (method.HasInstantiation)
            {
                dependencies = dependencies ?? new DependencyList();
                AddMarshalAPIsGenericDependencies(ref dependencies, factory, method);
            }
        }

        public override void AddInterestingInteropConstructedTypeDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.IsDelegate)
            {
                var delegateType = (MetadataType)type;
                if (delegateType.HasCustomAttribute("System.Runtime.InteropServices", "UnmanagedFunctionPointerAttribute"))
                {
                    AddDependenciesDueToPInvokeDelegate(ref dependencies, factory, delegateType);
                }
            }
        }

        /// <summary>
        /// For Marshal generic APIs(eg. Marshal.StructureToPtr<T>, GetFunctionPointerForDelegate) we add
        /// the generic parameter as dependencies so that we can generate runtime data for them
        /// </summary>
        public override void AddMarshalAPIsGenericDependencies(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
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
                            AddDependenciesDueToPInvokeDelegate(ref dependencies, factory, type);
                            AddDependenciesDueToPInvokeStruct(ref dependencies, factory, type, methodName == "OffsetOf");
                        }
                    }
                }
            }
        }

        private void AddDependenciesDueToPInvokeDelegate(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.IsDelegate)
            {
                dependencies.Add(factory.NecessaryTypeSymbol(type), "Delegate Marshalling Stub");

                dependencies.Add(factory.MethodEntrypoint(GetOpenStaticDelegateMarshallingStub(type)), "Delegate Marshalling Stub");
                dependencies.Add(factory.MethodEntrypoint(GetClosedDelegateMarshallingStub(type)), "Delegate Marshalling Stub");
                dependencies.Add(factory.MethodEntrypoint(GetForwardDelegateCreationStub(type)), "Delegate Marshalling Stub");
            }
        }

        private void AddDependenciesDueToPInvokeStruct(ref DependencyList dependencies, NodeFactory factory, TypeDesc type, bool fieldOffsetsRequired)
        {
            dependencies.Add(factory.NecessaryTypeSymbol(type), "Struct Marshalling Stub");

            if (MarshalHelpers.IsStructMarshallingRequired(type))
            {
                dependencies.Add(factory.MethodEntrypoint(GetStructMarshallingManagedToNativeStub(type)), "Struct Marshalling stub");
                dependencies.Add(factory.MethodEntrypoint(GetStructMarshallingNativeToManagedStub(type)), "Struct Marshalling stub");
                dependencies.Add(factory.MethodEntrypoint(GetStructMarshallingCleanupStub(type)), "Struct Marshalling stub");

                AddDependenciesDueToPInvokeStructDelegateField(ref dependencies, factory, type);
            }

            if (fieldOffsetsRequired)
            {
                _structMarshallingTypes.Add(type);
            }
        }

        public override void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
            var delegateMapNode = new DelegateMarshallingStubMapNode(commonFixupsTableNode);
            header.Add(MetadataManager.BlobIdToReadyToRunSection(ReflectionMapBlob.DelegateMarshallingStubMap), delegateMapNode, delegateMapNode, delegateMapNode.EndSymbol);

            var structMapNode = new StructMarshallingStubMapNode(commonFixupsTableNode);
            header.Add(MetadataManager.BlobIdToReadyToRunSection(ReflectionMapBlob.StructMarshallingStubMap), structMapNode, structMapNode, structMapNode.EndSymbol);
        }
    }
}
