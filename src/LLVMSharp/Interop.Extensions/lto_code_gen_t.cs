// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct lto_code_gen_t
    {
        public lto_code_gen_t(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator lto_code_gen_t(LLVMOpaqueLTOCodeGenerator* Comdat)
        {
            return new lto_code_gen_t((IntPtr)Comdat);
        }

        public static implicit operator LLVMOpaqueLTOCodeGenerator*(lto_code_gen_t Comdat)
        {
            return (LLVMOpaqueLTOCodeGenerator*)Comdat.Handle;
        }
    }
}
