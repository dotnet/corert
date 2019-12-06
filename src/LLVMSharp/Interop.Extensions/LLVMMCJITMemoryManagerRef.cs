// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMMCJITMemoryManagerRef : IEquatable<LLVMMCJITMemoryManagerRef>
    {
        public LLVMMCJITMemoryManagerRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMMCJITMemoryManagerRef(LLVMOpaqueMCJITMemoryManager* value)
        {
            return new LLVMMCJITMemoryManagerRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueMCJITMemoryManager*(LLVMMCJITMemoryManagerRef value)
        {
            return (LLVMOpaqueMCJITMemoryManager*)value.Handle;
        }

        public static bool operator ==(LLVMMCJITMemoryManagerRef left, LLVMMCJITMemoryManagerRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMMCJITMemoryManagerRef left, LLVMMCJITMemoryManagerRef right) => !(left == right);

        public override bool Equals(object obj) => obj is LLVMMCJITMemoryManagerRef other && Equals(other);

        public bool Equals(LLVMMCJITMemoryManagerRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();
    }
}
