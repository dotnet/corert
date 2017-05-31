// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Internal.Runtime.Augments
{
    [CLSCompliant(false)]
    public abstract class InteropCallbacks
    {
        public abstract bool TryGetMarshallerDataForDelegate(RuntimeTypeHandle delegateTypeHandle, out McgPInvokeDelegateData  delegateData);

        public abstract bool TryGetStructUnsafeStructType(RuntimeTypeHandle structureTypeHandle, out RuntimeTypeHandle unsafeStructType);

        public abstract bool TryGetStructUnmarshalStub(RuntimeTypeHandle structureTypeHandle, out IntPtr unmarshalStub);

        public abstract bool TryGetStructMarshalStub(RuntimeTypeHandle structureTypeHandle, out IntPtr marshalStub);

        public abstract bool TryGetDestroyStructureStub(RuntimeTypeHandle structureTypeHandle, out IntPtr destroyStructureStub, out bool hasInvalidLayout);

        public abstract bool TryGetStructFieldOffset(RuntimeTypeHandle structureTypeHandle, string fieldName, out bool structExists, out uint offset);
    }
}
