// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMRemarkDebugLocRef
    {
        public LLVMRemarkDebugLocRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMRemarkDebugLocRef(LLVMRemarkOpaqueDebugLoc* value)
        {
            return new LLVMRemarkDebugLocRef((IntPtr)value);
        }

        public static implicit operator LLVMRemarkOpaqueDebugLoc*(LLVMRemarkDebugLocRef value)
        {
            return (LLVMRemarkOpaqueDebugLoc*)value.Handle;
        }
    }
}
