// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO: Implement Unix write barriers
#include <unixasmmacros.inc>

.syntax unified
.thumb

LEAF_ENTRY RhpAssignRef, _TEXT
ALTERNATE_ENTRY RhpAssignRefAVLocation
	str	r1, [r0]
	bx	lr
LEAF_END RhpAssignRef, _TEXT

// Define a helper with a name of the form RhpCheckedAssignRefEAX etc. (along with suitable calling standard
// decoration). The location to be updated is always in RDI. The object reference that will be assigned into
// that location is in one of the other general registers determined by the value of REFREG.

// WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
// - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen on the first instruction
// - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
LEAF_ENTRY RhpCheckedAssignRef, _TEXT

// ALTERNATE_ENTRY RhpCheckedAssignRef
ALTERNATE_ENTRY RhpCheckedAssignRefAVLocation

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


// WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
// - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpCheckedLockCmpXchgAVLocation
// - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
LEAF_ENTRY RhpCheckedLockCmpXchg, _TEXT
ALTERNATE_ENTRY RhpCheckedLockCmpXchgAVLocation
LEAF_END RhpCheckedLockCmpXchg, _TEXT


// WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
// - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpCheckedXchgAVLocation
// - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
LEAF_ENTRY RhpCheckedXchg, _TEXT

// Setup rax with the new object for the exchange, that way it will automatically hold the correct result
// afterwards and we can leave rdx unaltered ready for the GC write barrier below.
ALTERNATE_ENTRY RhpCheckedXchgAVLocation
LEAF_END RhpCheckedXchg, _TEXT
