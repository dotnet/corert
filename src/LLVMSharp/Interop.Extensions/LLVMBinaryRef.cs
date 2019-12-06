// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMBinaryRef
    {
        public LLVMBinaryRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMBinaryRef(LLVMOpaqueBinary* Comdat)
        {
            return new LLVMBinaryRef((IntPtr)Comdat);
        }

        public static implicit operator LLVMOpaqueBinary*(LLVMBinaryRef Comdat)
        {
            return (LLVMOpaqueBinary*)Comdat.Handle;
        }
    }
}
