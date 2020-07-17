// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;

using Internal.NativeFormat;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.TypeSystem.NativeFormat;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.Runtime.General;

namespace Internal.Runtime.TypeLoader
{
    // Extensibility api to allow a method execution strategy to be defined by a module that depends
    // on this module. The GlobalExecutionStrategy static variable is expected to be assigned once per process
    // with whatever the execution strategy is. 
    public abstract class MethodExecutionStrategy
    {
        public static MethodExecutionStrategy GlobalExecutionStrategy;
        public abstract IntPtr OnEntryPoint(MethodEntrypointPtr entrypointInfo, IntPtr callerArgumentsInfo);
    }

    internal struct MethodEntrypointData
    {
        public MethodEntrypointData(RuntimeMethodHandle methodIdentifier, IntPtr methodEntrypointThunk)
        {
            MethodIdentifier = methodIdentifier;
            MethodCode = IntPtr.Zero;
            MethodEntrypointThunk = methodEntrypointThunk;
        }

        public readonly RuntimeMethodHandle MethodIdentifier;
        public IntPtr MethodCode;
        public readonly IntPtr MethodEntrypointThunk;
    }

    public unsafe struct MethodEntrypointPtr
    {
        private static object s_thunkPoolHeap;
        internal static void SetThunkPool(object thunkPoolHeap) { s_thunkPoolHeap = thunkPoolHeap; }

        internal MethodEntrypointPtr(MethodEntrypointData *data)
        {
            _data = data;
        }

        internal MethodEntrypointPtr(IntPtr ptr)
        {
            _data = (MethodEntrypointData*)ptr.ToPointer();
        }

        private MethodEntrypointData *_data;
        public RuntimeMethodHandle MethodIdentifier { get { return _data->MethodIdentifier; } }
        public IntPtr MethodCode 
        { 
            get 
            { 
                return _data->MethodCode; 
            } 
            set 
            { 
                _data->MethodCode = value; 
                RuntimeAugments.SetThunkData(s_thunkPoolHeap, _data->MethodEntrypointThunk, value, new IntPtr(_data));
            }
        }

        public IntPtr MethodEntrypointThunk { get { return _data->MethodEntrypointThunk; } }

        public IntPtr ToIntPtr()
        {
            return new IntPtr(_data);
        }
    }

    public static class MethodEntrypointStubs
    {
        private static unsafe IntPtr RuntimeMethodHandleToIntPtr(RuntimeMethodHandle rmh)
        {
            return *(IntPtr*)&rmh;
        }
        private static unsafe RuntimeMethodHandle IntPtrToRuntimeMethodHandle(IntPtr rmh)
        {
            RuntimeMethodHandle handle = default(RuntimeMethodHandle);
            RuntimeMethodHandle* pRMH = &handle;
            *((IntPtr*)pRMH) = rmh;

            return handle;
        }

        private static unsafe RuntimeTypeHandle IntPtrToRuntimeTypeHandle(IntPtr rtth)
        {
            RuntimeTypeHandle handle = default(RuntimeTypeHandle);
            RuntimeTypeHandle* pRTTH = &handle;
            *((IntPtr*)pRTTH) = rtth;

            return handle;
        }

        private struct MethodEntrypointLookup
        {
            public MethodEntrypointLookup(MethodDesc method)
            {
                _method = method;

                RuntimeTypeHandle declaringTypeHandle = method.OwningType.GetRuntimeTypeHandle();
                RuntimeSignature methodSignature;
                if (!RuntimeSignatureHelper.TryCreate(method, out methodSignature))
                {
                    Environment.FailFast("Unable to create method signature");
                }
                RuntimeTypeHandle[] genericMethodArgs = null;
                if (method.Instantiation.Length != 0)
                {
                    genericMethodArgs = new RuntimeTypeHandle[method.Instantiation.Length];
                    for (int i = 0; i < genericMethodArgs.Length; i++)
                    {
                        genericMethodArgs[i] = method.Instantiation[i].GetRuntimeTypeHandle();
                    }
                }

                _rmh = TypeLoaderEnvironment.Instance.GetRuntimeMethodHandleForComponents(declaringTypeHandle,
                                    IntPtr.Zero,
                                    methodSignature,
                                    genericMethodArgs);
            }

            public MethodDesc Method => _method;
            public RuntimeMethodHandle MethodHandle => _rmh;

            private RuntimeMethodHandle _rmh;
            MethodDesc _method;
        }
        
        private unsafe class MethodEntrypointHash : LockFreeReaderHashtableOfPointers<MethodEntrypointLookup, MethodEntrypointPtr>
        {
            TypeLoaderEnvironment _tle = TypeLoaderEnvironment.Instance;

            /// <summary>
            /// Given a key, compute a hash code. This function must be thread safe.
            /// </summary>
            protected override int GetKeyHashCode(MethodEntrypointLookup key)
            {
                return key.MethodHandle.GetHashCode();
            }

            /// <summary>
            /// Given a value, compute a hash code which would be identical to the hash code
            /// for a key which should look up this value. This function must be thread safe.
            /// </summary>
            protected override unsafe int GetValueHashCode(MethodEntrypointPtr value)
            {
                return value.MethodIdentifier.GetHashCode();
            }

            /// <summary>
            /// Compare a key and value. If the key refers to this value, return true.
            /// This function must be thread safe.
            /// </summary>
            protected override bool CompareKeyToValue(MethodEntrypointLookup key, MethodEntrypointPtr value)
            {
                return value.MethodIdentifier.Equals(key.MethodHandle);
            }

            /// <summary>
            /// Compare a value with another value. Return true if values are equal.
            /// This function must be thread safe.
            /// </summary>
            protected override bool CompareValueToValue(MethodEntrypointPtr value1, MethodEntrypointPtr value2)
            {
                return value1.MethodIdentifier.Equals(value2.MethodIdentifier);
            }

            [DllImport("*", ExactSpelling = true, EntryPoint = "MethodEntrypointStubs_SetupPointers")]
            private unsafe extern static IntPtr MethodEntrypointStubs_SetupPointers(IntPtr universalTransitionThunk, IntPtr methodEntrypoint);

            private static object s_thunkPoolHeap;
            private static IntPtr s_entryPointStub = SetupMethodEntrypoints();

            private static IntPtr SetupMethodEntrypoints()
            {
                return MethodEntrypointStubs_SetupPointers(RuntimeAugments.GetUniversalTransitionThunk(),
                    Intrinsics.AddrOf<Func<IntPtr, IntPtr, IntPtr>>(EntrypointThunk));
            }

            unsafe private static IntPtr EntrypointThunk(IntPtr callerTransitionBlockParam, IntPtr entrypointData)
            {
                MethodEntrypointPtr entryPointPointer = new MethodEntrypointPtr(entrypointData);
                return MethodExecutionStrategy.GlobalExecutionStrategy.OnEntryPoint(entryPointPointer, callerTransitionBlockParam);
            }

            /// <summary>
            /// Create a new value from a key. Must be threadsafe. Value may or may not be added
            /// to collection. Return value must not be null.
            /// </summary>
            protected override unsafe MethodEntrypointPtr CreateValueFromKey(MethodEntrypointLookup key)
            {
                lock (this)
                {
                    IntPtr thunk = IntPtr.Zero;
                    if (s_thunkPoolHeap == null)
                    {
                        s_thunkPoolHeap = RuntimeAugments.CreateThunksHeap(s_entryPointStub);
                        MethodEntrypointPtr.SetThunkPool(s_thunkPoolHeap);
                        Debug.Assert(s_thunkPoolHeap != null);
                    }

                    thunk = RuntimeAugments.AllocateThunk(s_thunkPoolHeap);
                    Debug.Assert(thunk != IntPtr.Zero);
                    MethodEntrypointData *methodEntrypointData = (MethodEntrypointData*)MemoryHelpers.AllocateMemory(sizeof(MethodEntrypointData));

                    *methodEntrypointData = new MethodEntrypointData(key.MethodHandle, thunk);

                    RuntimeAugments.SetThunkData(s_thunkPoolHeap, thunk, IntPtr.Zero, new IntPtr(methodEntrypointData));

                    SerializedDebugData.RegisterTailCallThunk(thunk);

                    return new MethodEntrypointPtr(methodEntrypointData);
                }
            }

            /// <summary>
            /// Convert a value to an IntPtr for storage into the hashtable
            /// </summary>
            protected override IntPtr ConvertValueToIntPtr(MethodEntrypointPtr value)
            {
                return value.ToIntPtr();
            }

            /// <summary>
            /// Convert an IntPtr into a value for comparisions, or for returning.
            /// </summary>
            protected override MethodEntrypointPtr ConvertIntPtrToValue(IntPtr pointer)
            {
                return new MethodEntrypointPtr(pointer);
            }
        }

        private static MethodEntrypointHash s_methodEntrypointHash = new MethodEntrypointHash();

        public static bool TryGetMethodEntrypoint(MethodDesc methodOnType, out IntPtr entryPoint, out IntPtr unboxingStubAddress, out TypeLoaderEnvironment.MethodAddressType foundAddressType)
        {
            MethodDesc typicalMethod = methodOnType.GetTypicalMethodDefinition();

            if (!(typicalMethod is EcmaMethod))
            {
                foundAddressType = TypeLoaderEnvironment.MethodAddressType.None;
                entryPoint = IntPtr.Zero;
                unboxingStubAddress = IntPtr.Zero;
                return false;
            }

            // OK, this is a method entrypoint for an ecma method
            EcmaMethod ecmaTypicalMethod = (EcmaMethod)typicalMethod;

            // Canonicalize
            MethodDesc canonMethod = methodOnType.GetCanonMethodTarget(CanonicalFormKind.Specific);
            if (canonMethod != methodOnType)
                foundAddressType = TypeLoaderEnvironment.MethodAddressType.Canonical;
            else
                foundAddressType = TypeLoaderEnvironment.MethodAddressType.Exact;


            // Check to see if we should produce an unboxing stub entrypoint

            unboxingStubAddress = IntPtr.Zero; // Optimistically choose not to
            if (ecmaTypicalMethod.OwningType.IsValueType)
            {
                MethodSignature methodSig = ecmaTypicalMethod.Signature;
                if (!methodSig.IsStatic)
                    unboxingStubAddress = new IntPtr(5); // TODO Actually implement the unboxing stub logic
            }

            // Ensure RuntimeTypeHandles for owningType, and for instantiation types
            // They should be there, as the paths to this function should ensure it, but its a bit sketchy
            // as we don't have an opportunity to easily compute them now
            if (!canonMethod.OwningType.RetrieveRuntimeTypeHandleIfPossible())
                Environment.FailFast("Did not pre-allocate owning type typehandle");

            foreach (TypeDesc type in canonMethod.Instantiation)
            {
                if (!type.RetrieveRuntimeTypeHandleIfPossible())
                    Environment.FailFast("Did not pre-allocate instantiation type typehandle");
            }

            // We need to create a RuntimeMethodHandle for this method
            MethodEntrypointPtr entrypoint = s_methodEntrypointHash.GetOrCreateValue(new MethodEntrypointLookup(canonMethod));
            if (entrypoint.MethodCode != IntPtr.Zero)
                entryPoint = entrypoint.MethodCode;
            else
                entryPoint = entrypoint.MethodEntrypointThunk;

            return true;
        }
    }
}
