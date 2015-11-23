//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 
//

// TODO: Implement Unix write barriers
#include <unixasmmacros.inc>

LEAF_ENTRY RhpBulkWriteBarrier, _TEXT
	bx	lr
LEAF_END RhpBulkWriteBarrier, _TEXT

LEAF_ENTRY RhpAssignRef, _TEXT
	str	r1, [r0]
	bx	lr
LEAF_END RhpAssignRef, _TEXT

LEAF_ENTRY RhpCheckedAssignRef, _TEXT
	str	r1, [r0]
	bx	lr
LEAF_END RhpCheckedAssignRef, _TEXT

//
// RhpByRefAssignRef simulates movs instruction for object references.
//
// On entry:
//      r0: address of ref-field (assigned to)
//      r1: address of the data (source)
//      r3: be trashed
//
// On exit:
//      r0, r1 are incremented by 4, 
//      r3: trashed
//
LEAF_ENTRY RhpByRefAssignRef, _TEXT
	ldr	r3, [r1], #4
	str	r3, [r0], #4
	bx	lr
LEAF_END RhpByRefAssignRef, _TEXT
