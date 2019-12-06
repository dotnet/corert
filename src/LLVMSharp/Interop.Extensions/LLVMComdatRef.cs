// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMComdatRef
    {
        public LLVMComdatRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMComdatRef(LLVMComdat* Comdat)
        {
            return new LLVMComdatRef((IntPtr)Comdat);
        }

        public static implicit operator LLVMComdat*(LLVMComdatRef Comdat)
        {
            return (LLVMComdat*)Comdat.Handle;
        }
    }
}
