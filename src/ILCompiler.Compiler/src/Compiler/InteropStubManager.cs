// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;


using Debug = System.Diagnostics.Debug;

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

        public InteropStateManager InteropStateManager
        {
            get;
        }

        public InteropStubManager(CompilationModuleGroup compilationModuleGroup, CompilerTypeSystemContext typeSystemContext, InteropStateManager interopStateManager)
        {
            _compilationModuleGroup = compilationModuleGroup;
            _typeSystemContext = typeSystemContext;
            InteropStateManager = interopStateManager;
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
    }
}
