// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMModuleProviderRef : IEquatable<LLVMModuleProviderRef>
    {
        public LLVMModuleProviderRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMModuleProviderRef(LLVMOpaqueModuleProvider* value)
        {
            return new LLVMModuleProviderRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueModuleProvider*(LLVMModuleProviderRef value)
        {
            return (LLVMOpaqueModuleProvider*)value.Handle;
        }

        public static bool operator ==(LLVMModuleProviderRef left, LLVMModuleProviderRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMModuleProviderRef left, LLVMModuleProviderRef right) => !(left == right);

        public LLVMPassManagerRef CreateFunctionPassManager() => LLVM.CreateFunctionPassManager(this);

        public override bool Equals(object obj) => obj is LLVMModuleProviderRef other && Equals(other);

        public bool Equals(LLVMModuleProviderRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();
    }
}
