// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMTargetDataRef : IEquatable<LLVMTargetDataRef>
    {
        public LLVMTargetDataRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMTargetDataRef(LLVMOpaqueTargetData* TargetData)
        {
            return new LLVMTargetDataRef((IntPtr)TargetData);
        }

        public static implicit operator LLVMOpaqueTargetData*(LLVMTargetDataRef TargetData)
        {
            return (LLVMOpaqueTargetData*)TargetData.Handle;
        }

        public static bool operator ==(LLVMTargetDataRef left, LLVMTargetDataRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMTargetDataRef left, LLVMTargetDataRef right) => !(left == right);

        public override bool Equals(object obj) => obj is LLVMTargetDataRef other && Equals(other);

        public bool Equals(LLVMTargetDataRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();
    }
}
