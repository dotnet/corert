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
        public abstract bool CompareMethodSignatures(RuntimeMethodSignature signature1, RuntimeMethodSignature signature2);
        public abstract IntPtr GetDelegateThunk(Delegate delegateObject, int thunkKind);
        public abstract IntPtr TryGetDefaultConstructorForType(RuntimeTypeHandle runtimeTypeHandle);
        public abstract bool TryGetGenericVirtualTargetForTypeAndSlot(RuntimeTypeHandle targetHandle, ref RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, ref string methodName, ref RuntimeMethodSignature methodSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer, out bool slotUpdated);
    }
}
