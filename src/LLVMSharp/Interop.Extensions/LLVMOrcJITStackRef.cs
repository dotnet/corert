// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMOrcJITStackRef
    {
        public LLVMOrcJITStackRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMOrcJITStackRef(LLVMOrcOpaqueJITStack* value)
        {
            return new LLVMOrcJITStackRef((IntPtr)value);
        }

        public static implicit operator LLVMOrcOpaqueJITStack*(LLVMOrcJITStackRef value)
        {
            return (LLVMOrcOpaqueJITStack*)value.Handle;
        }
    }
}
