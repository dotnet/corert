// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Internal.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    [CLSCompliant(false)]
    public abstract class TypeLoaderCallbacks
    {
        public abstract int GetThreadStaticsSizeForDynamicType(int index, out int numTlsCells);
        public abstract IntPtr GenericLookupFromContextAndSignature(IntPtr context, IntPtr signature, out IntPtr auxResult);
        public abstract bool GetRuntimeMethodHandleComponents(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodArgs);
        public abstract bool CompareMethodSignatures(IntPtr signature1, IntPtr signature2);
        public abstract IntPtr GetDelegateThunk(Delegate delegateObject, int thunkKind);
    }
}
