// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

#ifdef _DEBUG
.rodata
AssertMsg:                              .asciz "__FILE__:%s: %s is not implemented\n"
FileName:                               .asciz "CallDescrWorker.S"
RhCallDescrWorkerName:                  .asciz "RhCallDescrWorker"
PointerToReturnFromCallDescrThunkName:  .asciz "PointerToReturnFromCallDescrThunk"

.text
.macro GEN_ASSERT_FUNC func
        GEN_ASSERT AssertMsg, FileName, \func
.endm
#endif

// TODO: Implement Arm support

NESTED_ENTRY RhCallDescrWorker, _TEXT, NoHandler
LOCAL_LABEL(ReturnFromCallDescrThunk):

    // UNIXTODO: Implement this function
	  // int 3
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhCallDescrWorkerName
#endif
NESTED_END RhCallDescrWorker, _TEXT

//.text
//.align      8
//C_FUNC(PointerToReturnFromCallDescrThunk):
//.quad       LOCAL_LABEL(ReturnFromCallDescrThunk)
//.global     C_FUNC(PointerToReturnFromCallDescrThunk)

LEAF_ENTRY PointerToReturnFromCallDescrThunk, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC PointerToReturnFromCallDescrThunk
#endif
LEAF_END PointerToReturnFromCallDescrThunk, _TEXT
