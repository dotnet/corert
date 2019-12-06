// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMDiagnosticInfoRef : IEquatable<LLVMDiagnosticInfoRef>
    {
        public LLVMDiagnosticInfoRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMDiagnosticInfoRef(LLVMOpaqueDiagnosticInfo* value)
        {
            return new LLVMDiagnosticInfoRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueDiagnosticInfo*(LLVMDiagnosticInfoRef value)
        {
            return (LLVMOpaqueDiagnosticInfo*)value.Handle;
        }

        public static bool operator ==(LLVMDiagnosticInfoRef left, LLVMDiagnosticInfoRef right) => left.Equals(right);

        public static bool operator !=(LLVMDiagnosticInfoRef left, LLVMDiagnosticInfoRef right) => !(left == right);

        public override bool Equals(object obj) => obj is LLVMDiagnosticInfoRef other && Equals(other);

        public bool Equals(LLVMDiagnosticInfoRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();
    }
}
