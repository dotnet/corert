// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using Internal.Runtime.Augments;
using Internal.NativeFormat;
using Internal.Runtime.TypeLoader;
using Internal.Reflection.Execution;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    internal class RuntimeInteropData : InteropCallbacks
    {
        public override bool TryGetMarshallerDataForDelegate(RuntimeTypeHandle delegateTypeHandle, out McgPInvokeDelegateData data)
        {
            return McgModuleManager.GetPInvokeDelegateData(delegateTypeHandle, out data);
        }

        #region "Struct Data"
        public override bool TryGetStructUnmarshalStub(RuntimeTypeHandle structureTypeHandle, out IntPtr unmarshalStub)
        {
            return McgModuleManager.TryGetStructUnmarshalStub(structureTypeHandle, out unmarshalStub);
        }

        public override bool TryGetStructMarshalStub(RuntimeTypeHandle structureTypeHandle, out IntPtr marshalStub)
        {
            return McgModuleManager.TryGetStructMarshalStub(structureTypeHandle, out marshalStub);
        }

        public override bool TryGetDestroyStructureStub(RuntimeTypeHandle structureTypeHandle, out IntPtr destroyStructureStub, out bool hasInvalidLayout)
        {
            return McgModuleManager.TryGetDestroyStructureStub(structureTypeHandle, out destroyStructureStub, out hasInvalidLayout);
        }

        public override bool TryGetStructFieldOffset(RuntimeTypeHandle structureTypeHandle, string fieldName, out bool structExists, out uint offset)
        {
            return McgModuleManager.TryGetStructFieldOffset(structureTypeHandle, fieldName,  out structExists, out offset);
        }

        public override bool TryGetStructUnsafeStructSize(RuntimeTypeHandle structureTypeHandle, out int size)
        {
            RuntimeTypeHandle unsafeStructType;
            size = 0;
            if (McgModuleManager.TryGetStructUnsafeStructType(typeHandle, out unsafeStructType))
            {
                size = unsafeStructType.GetValueTypeSize();
                return true;
            }
            return false;
        }
        #endregion
    }
}
