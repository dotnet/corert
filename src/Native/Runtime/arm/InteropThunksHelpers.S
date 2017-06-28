// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

#ifdef _DEBUG
.rodata
AssertMsg:                              .asciz "__FILE__:%s: %s is not implemented\n"
FileName:                               .asciz "InteropThunksHelpers.S"
RhCommonStubName:                       .asciz "RhCommonStub"
RhGetCommonStubAddressName:             .asciz "RhGetCommonStubAddress"
RhGetCurrentThunkContextName:           .asciz "RhGetCurrentThunkContext"

.text
.macro GEN_ASSERT_FUNC func
        GEN_ASSERT AssertMsg, FileName, \func
.endm
#endif

// TODO: Implement Arm support

//
// RhCommonStub
//
NESTED_ENTRY RhCommonStub, _TEXT, NoHandler
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhCommonStubName
#endif
NESTED_END RhCommonStub, _TEXT

//
// IntPtr RhGetCommonStubAddress()
//
LEAF_ENTRY RhGetCommonStubAddress, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhGetCommonStubAddressName
#endif
LEAF_END RhGetCommonStubAddress, _TEXT

//
// IntPtr RhGetCurrentThunkContext()
//
LEAF_ENTRY RhGetCurrentThunkContext, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhGetCurrentThunkContextName
#endif
LEAF_END RhGetCurrentThunkContext, _TEXT
