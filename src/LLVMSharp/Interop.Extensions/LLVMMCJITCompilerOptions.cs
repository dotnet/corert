// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMMCJITCompilerOptions
    {
        public static LLVMMCJITCompilerOptions Create()
        {
            LLVMMCJITCompilerOptions Options;
            LLVM.InitializeMCJITCompilerOptions(&Options, (UIntPtr)Marshal.SizeOf<LLVMMCJITCompilerOptions>());
            return Options;
        }
    }
}
