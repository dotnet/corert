// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMPassRegistryRef : IEquatable<LLVMPassRegistryRef>
    {
        public LLVMPassRegistryRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMPassRegistryRef(LLVMOpaquePassRegistry* value)
        {
            return new LLVMPassRegistryRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaquePassRegistry*(LLVMPassRegistryRef value)
        {
            return (LLVMOpaquePassRegistry*)value.Handle;
        }

        public static bool operator ==(LLVMPassRegistryRef left, LLVMPassRegistryRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMPassRegistryRef left, LLVMPassRegistryRef right) => !(left == right);

        public override bool Equals(object obj) => obj is LLVMPassRegistryRef other && Equals(other);

        public bool Equals(LLVMPassRegistryRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();
    }
}
