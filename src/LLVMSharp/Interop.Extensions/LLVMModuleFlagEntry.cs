// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMModuleFlagEntry
    {
        public LLVMModuleFlagEntry(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMModuleFlagEntry(LLVMOpaqueModuleFlagEntry* Comdat)
        {
            return new LLVMModuleFlagEntry((IntPtr)Comdat);
        }

        public static implicit operator LLVMOpaqueModuleFlagEntry*(LLVMModuleFlagEntry Comdat)
        {
            return (LLVMOpaqueModuleFlagEntry*)Comdat.Handle;
        }
    }
}
