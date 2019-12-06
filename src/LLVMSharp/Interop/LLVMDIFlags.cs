// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

// Ported from https://github.com/llvm/llvm-project/tree/llvmorg-9.0.0/llvm/include/llvm-c
// Original source is Copyright (c) the LLVM Project and Contributors. Licensed under the Apache License v2.0 with LLVM Exceptions. See NOTICE.txt in the project root for license information.

namespace LLVMSharp.Interop
{
    public enum LLVMDIFlags
    {
        LLVMDIFlagZero = 0,
        LLVMDIFlagPrivate = 1,
        LLVMDIFlagProtected = 2,
        LLVMDIFlagPublic = 3,
        LLVMDIFlagFwdDecl = 1 << 2,
        LLVMDIFlagAppleBlock = 1 << 3,
        LLVMDIFlagBlockByrefStruct = 1 << 4,
        LLVMDIFlagVirtual = 1 << 5,
        LLVMDIFlagArtificial = 1 << 6,
        LLVMDIFlagExplicit = 1 << 7,
        LLVMDIFlagPrototyped = 1 << 8,
        LLVMDIFlagObjcClassComplete = 1 << 9,
        LLVMDIFlagObjectPointer = 1 << 10,
        LLVMDIFlagVector = 1 << 11,
        LLVMDIFlagStaticMember = 1 << 12,
        LLVMDIFlagLValueReference = 1 << 13,
        LLVMDIFlagRValueReference = 1 << 14,
        LLVMDIFlagReserved = 1 << 15,
        LLVMDIFlagSingleInheritance = 1 << 16,
        LLVMDIFlagMultipleInheritance = 2 << 16,
        LLVMDIFlagVirtualInheritance = 3 << 16,
        LLVMDIFlagIntroducedVirtual = 1 << 18,
        LLVMDIFlagBitField = 1 << 19,
        LLVMDIFlagNoReturn = 1 << 20,
        LLVMDIFlagTypePassByValue = 1 << 22,
        LLVMDIFlagTypePassByReference = 1 << 23,
        LLVMDIFlagEnumClass = 1 << 24,
        LLVMDIFlagFixedEnum = LLVMDIFlagEnumClass,
        LLVMDIFlagThunk = 1 << 25,
        LLVMDIFlagNonTrivial = 1 << 26,
        LLVMDIFlagBigEndian = 1 << 27,
        LLVMDIFlagLittleEndian = 1 << 28,
        LLVMDIFlagIndirectVirtualBase = (1 << 2) | (1 << 5),
        LLVMDIFlagAccessibility = LLVMDIFlagPrivate | LLVMDIFlagProtected | LLVMDIFlagPublic,
        LLVMDIFlagPtrToMemberRep = LLVMDIFlagSingleInheritance | LLVMDIFlagMultipleInheritance | LLVMDIFlagVirtualInheritance,
    }
}
