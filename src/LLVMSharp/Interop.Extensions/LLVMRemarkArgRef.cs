// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMRemarkArgRef
    {
        public LLVMRemarkArgRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMRemarkArgRef(LLVMRemarkOpaqueArg* value)
        {
            return new LLVMRemarkArgRef((IntPtr)value);
        }

        public static implicit operator LLVMRemarkOpaqueArg*(LLVMRemarkArgRef value)
        {
            return (LLVMRemarkOpaqueArg*)value.Handle;
        }
    }
}
