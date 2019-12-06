// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

// Ported from https://github.com/llvm/llvm-project/tree/llvmorg-9.0.0/llvm/include/llvm-c
// Original source is Copyright (c) the LLVM Project and Contributors. Licensed under the Apache License v2.0 with LLVM Exceptions. See NOTICE.txt in the project root for license information.

namespace LLVMSharp.Interop
{
    public enum lto_codegen_diagnostic_severity_t
    {
        LTO_DS_ERROR = 0,
        LTO_DS_WARNING = 1,
        LTO_DS_REMARK = 3,
        LTO_DS_NOTE = 2,
    }
}
