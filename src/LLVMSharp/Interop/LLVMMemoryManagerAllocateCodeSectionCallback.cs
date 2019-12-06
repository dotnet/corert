// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

// Ported from https://github.com/llvm/llvm-project/tree/llvmorg-9.0.0/llvm/include/llvm-c
// Original source is Copyright (c) the LLVM Project and Contributors. Licensed under the Apache License v2.0 with LLVM Exceptions. See NOTICE.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace LLVMSharp.Interop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("uint8_t *")]
    public unsafe delegate byte* LLVMMemoryManagerAllocateCodeSectionCallback([NativeTypeName("void *")] void* Opaque, [NativeTypeName("uintptr_t")] UIntPtr Size, [NativeTypeName("unsigned int")] uint Alignment, [NativeTypeName("unsigned int")] uint SectionID, [NativeTypeName("const char *")] sbyte* SectionName);
}
