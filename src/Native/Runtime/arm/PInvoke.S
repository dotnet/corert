// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

#ifdef _DEBUG
.rodata
AssertMsg:                              .asciz "__FILE__:%s: %s is not implemented\n"
FileName:                               .asciz "PInvoke.S"
RhpPInvokeName:                         .asciz "RhpPInvoke"
RhpPInvokeReturnName:                   .asciz "RhpPInvokeReturn"

.text
.macro GEN_ASSERT_FUNC func
        GEN_ASSERT AssertMsg, FileName, \func
.endm
#endif

// TODO: Implement Arm support

//
// RhpPInvoke
//
NESTED_ENTRY RhpPInvoke, _TEXT, NoHandler
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpPInvokeName
#endif
NESTED_END RhpPInvoke, _TEXT

//
// RhpPInvokeReturn
//
LEAF_ENTRY RhpPInvokeReturn, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpPInvokeReturnName
#endif
LEAF_END RhpPInvokeReturn, _TEXT
