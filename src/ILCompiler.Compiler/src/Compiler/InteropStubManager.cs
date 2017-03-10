// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;

using ILCompiler.Metadata;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;
using ReadyToRunSectionType = Internal.Runtime.ReadyToRunSectionType;
using ReflectionMapBlob = Internal.Runtime.ReflectionMapBlob;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing stub methods for interop
    /// </summary>
    public sealed class InteropStubManager
    {
        private readonly CompilationModuleGroup _compilationModuleGroup;
        private readonly CompilerTypeSystemContext _typeSystemContext;
        private Dictionary<DelegateInvokeMethodSignature, DelegateMarshallingMethodThunk> _delegateMarshallingThunks = new Dictionary<DelegateInvokeMethodSignature, DelegateMarshallingMethodThunk>();
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
    
        internal MethodDesc GetDelegateMarshallingStub(TypeDesc delegateType)
        {
            DelegateMarshallingMethodThunk thunk;
            var lookupSig = new DelegateInvokeMethodSignature(delegateType);
            if (!_delegateMarshallingThunks.TryGetValue(lookupSig, out thunk))
            {
                string stubName = "ReverseDelegateStub__" + NodeFactory.NameManglerDoNotUse.GetMangledTypeName(delegateType);
                thunk = new DelegateMarshallingMethodThunk(_compilationModuleGroup.GeneratedAssembly.GetGlobalModuleType(), delegateType, InteropStateManager ,stubName);
                _delegateMarshallingThunks.Add(lookupSig, thunk);
            }
            return thunk;
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


        internal IReadOnlyDictionary<DelegateInvokeMethodSignature, DelegateMarshallingMethodThunk> GetDelegateMarshallingThunks()
        {
            return _delegateMarshallingThunks;
        }

        internal HashSet<NativeStructType> GetStructMarshallingTypes()
        {
            return _structMarshallingTypes;
        }
    }
}
