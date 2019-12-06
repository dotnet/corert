// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMJITEventListenerRef
    {
        public LLVMJITEventListenerRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMJITEventListenerRef(LLVMOpaqueJITEventListener* value)
        {
            return new LLVMJITEventListenerRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueJITEventListener*(LLVMJITEventListenerRef value)
        {
            return (LLVMOpaqueJITEventListener*)value.Handle;
        }
    }
}
