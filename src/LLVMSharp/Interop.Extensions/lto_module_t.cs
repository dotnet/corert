// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct lto_module_t
    {
        public lto_module_t(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator lto_module_t(LLVMOpaqueLTOModule* Comdat)
        {
            return new lto_module_t((IntPtr)Comdat);
        }

        public static implicit operator LLVMOpaqueLTOModule*(lto_module_t Comdat)
        {
            return (LLVMOpaqueLTOModule*)Comdat.Handle;
        }
    }
}
