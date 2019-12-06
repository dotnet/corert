// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMMemoryBufferRef : IEquatable<LLVMMemoryBufferRef>
    {
        public LLVMMemoryBufferRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMMemoryBufferRef(LLVMOpaqueMemoryBuffer* MemoryBuffer)
        {
            return new LLVMMemoryBufferRef((IntPtr)MemoryBuffer);
        }

        public static implicit operator LLVMOpaqueMemoryBuffer*(LLVMMemoryBufferRef MemoryBuffer)
        {
            return (LLVMOpaqueMemoryBuffer*)MemoryBuffer.Handle;
        }

        public static bool operator ==(LLVMMemoryBufferRef left, LLVMMemoryBufferRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMMemoryBufferRef left, LLVMMemoryBufferRef right) => !(left == right);

        public override bool Equals(object obj) => obj is LLVMMemoryBufferRef other && Equals(other);

        public bool Equals(LLVMMemoryBufferRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();
    }
}
