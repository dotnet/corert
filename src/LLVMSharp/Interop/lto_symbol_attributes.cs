// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

// Ported from https://github.com/llvm/llvm-project/tree/llvmorg-9.0.0/llvm/include/llvm-c
// Original source is Copyright (c) the LLVM Project and Contributors. Licensed under the Apache License v2.0 with LLVM Exceptions. See NOTICE.txt in the project root for license information.

namespace LLVMSharp.Interop
{
    public enum lto_symbol_attributes
    {
        LTO_SYMBOL_ALIGNMENT_MASK = 0x0000001F,
        LTO_SYMBOL_PERMISSIONS_MASK = 0x000000E0,
        LTO_SYMBOL_PERMISSIONS_CODE = 0x000000A0,
        LTO_SYMBOL_PERMISSIONS_DATA = 0x000000C0,
        LTO_SYMBOL_PERMISSIONS_RODATA = 0x00000080,
        LTO_SYMBOL_DEFINITION_MASK = 0x00000700,
        LTO_SYMBOL_DEFINITION_REGULAR = 0x00000100,
        LTO_SYMBOL_DEFINITION_TENTATIVE = 0x00000200,
        LTO_SYMBOL_DEFINITION_WEAK = 0x00000300,
        LTO_SYMBOL_DEFINITION_UNDEFINED = 0x00000400,
        LTO_SYMBOL_DEFINITION_WEAKUNDEF = 0x00000500,
        LTO_SYMBOL_SCOPE_MASK = 0x00003800,
        LTO_SYMBOL_SCOPE_INTERNAL = 0x00000800,
        LTO_SYMBOL_SCOPE_HIDDEN = 0x00001000,
        LTO_SYMBOL_SCOPE_PROTECTED = 0x00002000,
        LTO_SYMBOL_SCOPE_DEFAULT = 0x00001800,
        LTO_SYMBOL_SCOPE_DEFAULT_CAN_BE_HIDDEN = 0x00002800,
        LTO_SYMBOL_COMDAT = 0x00004000,
        LTO_SYMBOL_ALIAS = 0x00008000,
    }
}
