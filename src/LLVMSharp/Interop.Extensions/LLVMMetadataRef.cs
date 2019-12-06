// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMMetadataRef
    {
        public LLVMMetadataRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMMetadataRef(LLVMOpaqueMetadata* value)
        {
            return new LLVMMetadataRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueMetadata*(LLVMMetadataRef value)
        {
            return (LLVMOpaqueMetadata*)value.Handle;
        }
    }
}
