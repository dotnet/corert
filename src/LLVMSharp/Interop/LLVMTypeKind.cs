// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

// Ported from https://github.com/llvm/llvm-project/tree/llvmorg-9.0.0/llvm/include/llvm-c
// Original source is Copyright (c) the LLVM Project and Contributors. Licensed under the Apache License v2.0 with LLVM Exceptions. See NOTICE.txt in the project root for license information.

namespace LLVMSharp.Interop
{
    public enum LLVMTypeKind
    {
        LLVMVoidTypeKind,
        LLVMHalfTypeKind,
        LLVMFloatTypeKind,
        LLVMDoubleTypeKind,
        LLVMX86_FP80TypeKind,
        LLVMFP128TypeKind,
        LLVMPPC_FP128TypeKind,
        LLVMLabelTypeKind,
        LLVMIntegerTypeKind,
        LLVMFunctionTypeKind,
        LLVMStructTypeKind,
        LLVMArrayTypeKind,
        LLVMPointerTypeKind,
        LLVMVectorTypeKind,
        LLVMMetadataTypeKind,
        LLVMX86_MMXTypeKind,
        LLVMTokenTypeKind,
    }
}
