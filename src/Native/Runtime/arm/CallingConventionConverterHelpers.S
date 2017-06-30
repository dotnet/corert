// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

// TODO: Implement Arm support
.syntax unified
.thumb

#ifdef _DEBUG
.rodata
AssertMsg:                                                      .asciz "__FILE__:%s: %s is not implemented\n"
FileName:                                                       .asciz "CallingConventionConverterHelpers.S"
CallingConventionConverter_ReturnThunkName:                     .asciz "CallingConventionConverter_ReturnThunk"
__jmpstub__CallingConventionConverter_CommonCallingStubName:    .asciz "__jmpstub__CallingConventionConverter_CommonCallingStub"
CallingConventionConverter_SpecifyCommonStubDataName:           .asciz "CallingConventionConverter_SpecifyCommonStubData"
CallingConventionConverter_GetStubsName:                        .asciz "CallingConventionConverter_GetStubs"

.text
.macro GEN_ASSERT_FUNC func
        GEN_ASSERT AssertMsg, FileName, \func
.endm
#endif

//
// void CallingConventionConverter_ReturnThunk()
//
LEAF_ENTRY CallingConventionConverter_ReturnThunk
#ifdef _DEBUG
        GEN_ASSERT_FUNC CallingConventionConverter_ReturnThunkName
#endif
LEAF_END CallingConventionConverter_ReturnThunk


//
// __jmpstub__CallingConventionConverter_CommonCallingStub
//
// struct CallingConventionConverter_CommonCallingStub_PointerData
// {
//     void *ManagedCallConverterThunk;
//     void *UniversalThunk;
// }
//
// struct CommonCallingStubInputData
// {
//     ULONG_PTR CallingConventionId;
//     CallingConventionConverter_CommonCallingStub_PointerData *commonData; // Only the ManagedCallConverterThunk field is used
//                                                                           // However, it is specified just like other platforms, so the behavior of the common
//                                                                           // calling stub is easier to debug
// }
//
// sp-4 - Points at CommonCallingStubInputData
//
//
LEAF_ENTRY __jmpstub__CallingConventionConverter_CommonCallingStub
#ifdef _DEBUG
        GEN_ASSERT_FUNC __jmpstub__CallingConventionConverter_CommonCallingStubName
#endif
LEAF_END __jmpstub__CallingConventionConverter_CommonCallingStub

//
// void CallingConventionConverter_SpecifyCommonStubData(CallingConventionConverter_CommonCallingStub_PointerData *commonData);
//
LEAF_ENTRY CallingConventionConverter_SpecifyCommonStubData
#ifdef _DEBUG
        GEN_ASSERT_FUNC CallingConventionConverter_SpecifyCommonStubDataName
#endif
LEAF_END CallingConventionConverter_SpecifyCommonStubData

//
// void CallingConventionConverter_GetStubs(IntPtr *returnVoidStub, IntPtr *returnIntegerStub, IntPtr *commonCallingStub)
//
LEAF_ENTRY CallingConventionConverter_GetStubs, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC CallingConventionConverter_GetStubsName
#endif
LEAF_END CallingConventionConverter_GetStubs, _TEXT
