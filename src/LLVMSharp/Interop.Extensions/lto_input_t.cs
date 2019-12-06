// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct lto_input_t
    {
        public lto_input_t(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator lto_input_t(LLVMOpaqueLTOInput* Comdat)
        {
            return new lto_input_t((IntPtr)Comdat);
        }

        public static implicit operator LLVMOpaqueLTOInput*(lto_input_t Comdat)
        {
            return (LLVMOpaqueLTOInput*)Comdat.Handle;
        }
    }
}
