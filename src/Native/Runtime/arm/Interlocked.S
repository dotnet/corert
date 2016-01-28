// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO: Implement InterLocked routines
#include <unixasmmacros.inc>

// WARNING: Code in EHHelpers.cpp makes assumptions about this helper, in particular:
// - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpLockCmpXchg32AVLocation
// - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
// r0 = destination address
// r1 = value
// r2 = comparand
LEAF_ENTRY RhpLockCmpXchg32, _TEXT
ALTERNATE_ENTRY RhpLockCmpXchg32AVLocation
    bx      lr
LEAF_END RhpLockCmpXchg32, _TEXT


// WARNING: Code in EHHelpers.cpp makes assumptions about this helper, in particular:
// - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpLockCmpXchg64AVLocation
// - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
// r0 = destination address
// r1 = value
// r2 = comparand
LEAF_ENTRY RhpLockCmpXchg64, _TEXT
ALTERNATE_ENTRY RhpLockCmpXchg64AVLocation
	bx	lr
LEAF_END RhpLockCmpXchg64, _TEXT
