// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMDisasmContextRef : IEquatable<LLVMDisasmContextRef>
    {
        public LLVMDisasmContextRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static explicit operator LLVMDisasmContextRef(void* value)
        {
            return new LLVMDisasmContextRef((IntPtr)value);
        }

        public static implicit operator void*(LLVMDisasmContextRef value)
        {
            return (void*)value.Handle;
        }

        public static bool operator ==(LLVMDisasmContextRef left, LLVMDisasmContextRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMDisasmContextRef left, LLVMDisasmContextRef right) => !(left == right);

        public override bool Equals(object obj) => obj is LLVMDisasmContextRef other && Equals(other);

        public bool Equals(LLVMDisasmContextRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();
    }
}
