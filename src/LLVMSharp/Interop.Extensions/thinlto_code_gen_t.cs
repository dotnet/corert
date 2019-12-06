// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct thinlto_code_gen_t
    {
        public thinlto_code_gen_t(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator thinlto_code_gen_t(LLVMOpaqueThinLTOCodeGenerator* Comdat)
        {
            return new thinlto_code_gen_t((IntPtr)Comdat);
        }

        public static implicit operator LLVMOpaqueThinLTOCodeGenerator*(thinlto_code_gen_t Comdat)
        {
            return (LLVMOpaqueThinLTOCodeGenerator*)Comdat.Handle;
        }
    }
}
