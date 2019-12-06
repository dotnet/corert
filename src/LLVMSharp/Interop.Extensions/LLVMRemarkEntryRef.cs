// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMRemarkEntryRef
    {
        public LLVMRemarkEntryRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMRemarkEntryRef(LLVMRemarkOpaqueEntry* value)
        {
            return new LLVMRemarkEntryRef((IntPtr)value);
        }

        public static implicit operator LLVMRemarkOpaqueEntry*(LLVMRemarkEntryRef value)
        {
            return (LLVMRemarkOpaqueEntry*)value.Handle;
        }
    }
}
