﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.IL;
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
    public sealed class CompilerGeneratedInteropStubManager : InteropStubManager
    {
        private readonly HashSet<TypeDesc> _delegateMarshalingTypes = new HashSet<TypeDesc>();
        private readonly HashSet<TypeDesc> _structMarshallingTypes = new HashSet<TypeDesc>();
        private readonly PInvokeILEmitterConfiguration _pInvokeILEmitterConfiguration;

        public InteropStateManager InteropStateManager { get; }

        public CompilerGeneratedInteropStubManager(InteropStateManager interopStateManager, PInvokeILEmitterConfiguration pInvokeILEmitterConfiguration)
        {
            InteropStateManager = interopStateManager;
            _pInvokeILEmitterConfiguration = pInvokeILEmitterConfiguration;
        }

        public override PInvokeILProvider CreatePInvokeILProvider()
        {
            return new PInvokeILProvider(_pInvokeILEmitterConfiguration, InteropStateManager);
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
            if (metadataType != null && metadataType.Module == factory.TypeSystemContext.SystemModule)
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
                _delegateMarshalingTypes.Add(type);

                dependencies.Add(factory.NecessaryTypeSymbol(type), "Delegate Marshalling Stub");

                dependencies.Add(factory.MethodEntrypoint(InteropStateManager.GetOpenStaticDelegateMarshallingThunk(type)), "Delegate Marshalling Stub");
                dependencies.Add(factory.MethodEntrypoint(InteropStateManager.GetClosedDelegateMarshallingThunk(type)), "Delegate Marshalling Stub");
                dependencies.Add(factory.MethodEntrypoint(InteropStateManager.GetForwardDelegateCreationThunk(type)), "Delegate Marshalling Stub");
            }
        }

        private void AddDependenciesDueToPInvokeStruct(ref DependencyList dependencies, NodeFactory factory, TypeDesc type, bool fieldOffsetsRequired)
        {
            dependencies.Add(factory.NecessaryTypeSymbol(type), "Struct Marshalling Stub");

            if (MarshalHelpers.IsStructMarshallingRequired(type))
            {
                _structMarshallingTypes.Add(type);

                dependencies.Add(factory.MethodEntrypoint(InteropStateManager.GetStructMarshallingManagedToNativeThunk(type)), "Struct Marshalling stub");
                dependencies.Add(factory.MethodEntrypoint(InteropStateManager.GetStructMarshallingNativeToManagedThunk(type)), "Struct Marshalling stub");
                dependencies.Add(factory.MethodEntrypoint(InteropStateManager.GetStructMarshallingCleanupThunk(type)), "Struct Marshalling stub");

                AddDependenciesDueToPInvokeStructDelegateField(ref dependencies, factory, type);
            }

            if (fieldOffsetsRequired)
            {
                _structMarshallingTypes.Add(type);
            }
        }
    }
}
