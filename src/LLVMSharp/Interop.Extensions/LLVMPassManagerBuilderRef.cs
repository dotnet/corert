// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMPassManagerBuilderRef : IEquatable<LLVMPassManagerBuilderRef>, IDisposable
    {
        public LLVMPassManagerBuilderRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMPassManagerBuilderRef(LLVMOpaquePassManagerBuilder* value)
        {
            return new LLVMPassManagerBuilderRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaquePassManagerBuilder*(LLVMPassManagerBuilderRef value)
        {
            return (LLVMOpaquePassManagerBuilder*)value.Handle;
        }

        public static bool operator ==(LLVMPassManagerBuilderRef left, LLVMPassManagerBuilderRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMPassManagerBuilderRef left, LLVMPassManagerBuilderRef right) => !(left == right);

        public override bool Equals(object obj) => obj is LLVMPassManagerBuilderRef other && Equals(other);

        public bool Equals(LLVMPassManagerBuilderRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                LLVM.PassManagerBuilderDispose(this);
                Handle = IntPtr.Zero;
            }
        }

        public void PopulateFunctionPassManager(LLVMPassManagerRef PM) => LLVM.PassManagerBuilderPopulateFunctionPassManager(this, PM);

        public void PopulateModulePassManager(LLVMPassManagerRef PM) => LLVM.PassManagerBuilderPopulateModulePassManager(this, PM);

        public void PopulateLTOPassManager(LLVMPassManagerRef PM, int Internalize, int RunInliner)
        {
            LLVM.PassManagerBuilderPopulateLTOPassManager(this, PM, Internalize, RunInliner);
        }

        public void SetOptLevel(uint OptLevel) => LLVM.PassManagerBuilderSetOptLevel(this, OptLevel);

        public void SetSizeLevel(uint SizeLevel) => LLVM.PassManagerBuilderSetSizeLevel(this, SizeLevel);

        public void SetDisableUnitAtATime(int Value) => LLVM.PassManagerBuilderSetDisableUnitAtATime(this, Value);

        public void SetDisableUnrollLoops(int Value) => LLVM.PassManagerBuilderSetDisableUnrollLoops(this, Value);

        public void SetDisableSimplifyLibCalls(int Value) => LLVM.PassManagerBuilderSetDisableSimplifyLibCalls(this, Value);

        public void UseInlinerWithThreshold(uint Threshold) => LLVM.PassManagerBuilderUseInlinerWithThreshold(this, Threshold);
    }
}
