// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMRemarkParserRef
    {
        public LLVMRemarkParserRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMRemarkParserRef(LLVMRemarkOpaqueParser* value)
        {
            return new LLVMRemarkParserRef((IntPtr)value);
        }

        public static implicit operator LLVMRemarkOpaqueParser*(LLVMRemarkParserRef value)
        {
            return (LLVMRemarkOpaqueParser*)value.Handle;
        }
    }
}
