// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

// Ported from https://github.com/llvm/llvm-project/tree/llvmorg-9.0.0/llvm/include/llvm-c
// Original source is Copyright (c) the LLVM Project and Contributors. Licensed under the Apache License v2.0 with LLVM Exceptions. See NOTICE.txt in the project root for license information.

namespace LLVMSharp.Interop
{
    public enum llvm_lto_status
    {
        LLVM_LTO_UNKNOWN,
        LLVM_LTO_OPT_SUCCESS,
        LLVM_LTO_READ_SUCCESS,
        LLVM_LTO_READ_FAILURE,
        LLVM_LTO_WRITE_FAILURE,
        LLVM_LTO_NO_TARGET,
        LLVM_LTO_NO_WORK,
        LLVM_LTO_MODULE_MERGE_FAILURE,
        LLVM_LTO_ASM_FAILURE,
        LLVM_LTO_NULL_OBJECT,
    }
}
