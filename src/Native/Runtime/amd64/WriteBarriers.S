//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 
//

// TODO: Implement Unix write barriers
.intel_syntax noprefix
#include <unixasmmacros.inc>

LEAF_ENTRY RhpBulkWriteBarrier, _TEXT
	ret
LEAF_END RhpBulkWriteBarrier, _TEXT

LEAF_ENTRY RhpAssignRef, _TEXT
	mov [rdi], rsi
	ret
LEAF_END RhpAssignRef, _TEXT

LEAF_ENTRY RhpCheckedAssignRef, _TEXT
	mov [rdi], rsi
	ret
LEAF_END RhpCheckedAssignRef, _TEXT

//
// RhpByRefAssignRef simulates movs instruction for object references.
//
// On entry:
//      rdi: address of ref-field (assigned to)
//      rsi: address of the data (source)
//      rcx: be trashed
//
// On exit:
//      rdi, rsi are incremented by 8, 
//      rcx: trashed
//
LEAF_ENTRY RhpByRefAssignRef, _TEXT
    mov     rcx, [rsi]
    mov     [rdi], rcx

    // Increment the pointers before leaving
    add     rdi, 8h
    add     rsi, 8h
    ret
LEAF_END RhpByRefAssignRef, _TEXT
