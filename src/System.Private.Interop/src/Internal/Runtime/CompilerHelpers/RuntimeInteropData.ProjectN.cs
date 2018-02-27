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
    internal partial class RuntimeInteropData : InteropCallbacks
    {
        public override IntPtr GetForwardDelegateCreationStub(RuntimeTypeHandle delegateTypeHandle)
        {
            McgModuleManager.GetPInvokeDelegateData(delegateTypeHandle, out McgPInvokeDelegateData data);
            IntPtr pStub = data.ForwardDelegateCreationStub;
            if (pStub == IntPtr.Zero)
                throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(delegateTypeHandle));
            return pStub;
        }

        public override IntPtr GetDelegateMarshallingStub(RuntimeTypeHandle delegateTypeHandle, bool openStaticDelegate)
        {
            McgModuleManager.GetPInvokeDelegateData(delegateTypeHandle, out McgPInvokeDelegateData pinvokeDelegateData);
            IntPtr pStub = openStaticDelegate ? pinvokeDelegateData.ReverseOpenStaticDelegateStub : pinvokeDelegateData.ReverseStub;
            if (pStub == IntPtr.Zero)
                throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(delegateTypeHandle));
            return pStub;
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
            if (McgModuleManager.TryGetStructUnsafeStructType(structureTypeHandle, out unsafeStructType))
            {
                size = unsafeStructType.GetValueTypeSize();
                return true;
            }
            return false;
        }
        #endregion
    }
}
