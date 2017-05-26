// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    [CLSCompliant(false)]
    public abstract class TypeLoaderCallbacks
    {
        public abstract bool TryGetConstructedGenericTypeForComponents(RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles, out RuntimeTypeHandle runtimeTypeHandle);
        public abstract int GetThreadStaticsSizeForDynamicType(int index, out int numTlsCells);
        public abstract IntPtr GenericLookupFromContextAndSignature(IntPtr context, IntPtr signature, out IntPtr auxResult);
        public abstract bool GetRuntimeMethodHandleComponents(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodArgs);
        public abstract bool CompareMethodSignatures(RuntimeSignature signature1, RuntimeSignature signature2);
        public abstract IntPtr GetDelegateThunk(Delegate delegateObject, int thunkKind);
        public abstract IntPtr TryGetDefaultConstructorForType(RuntimeTypeHandle runtimeTypeHandle);
        public abstract bool TryGetGenericVirtualTargetForTypeAndSlot(RuntimeTypeHandle targetHandle, ref RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, ref string methodName, ref RuntimeSignature methodSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer, out bool slotUpdated);
        public abstract bool GetRuntimeFieldHandleComponents(RuntimeFieldHandle runtimeFieldHandle, out RuntimeTypeHandle declaringTypeHandle, out string fieldName);

        /// <summary>
        /// Register a new runtime-allocated code thunk in the diagnostic stream.
        /// </summary>
        /// <param name="thunkAddress">Address of thunk to register</param>
        public abstract void RegisterThunk(IntPtr thunkAddress);

        /// <summary>
        /// Convert an unboxing function pointer to a non-unboxing function pointer
        /// </summary>
        public abstract IntPtr ConvertUnboxingFunctionPointerToUnderlyingNonUnboxingPointer(IntPtr unboxingFunctionPointer, RuntimeTypeHandle declaringType);
    }
}
