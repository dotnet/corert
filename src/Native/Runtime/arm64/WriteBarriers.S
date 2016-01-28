// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO: Implement Unix write barriers
#include <unixasmmacros.inc>

LEAF_ENTRY RhpAssignRef, _TEXT
    str x1, [x0]
    ret
LEAF_END RhpAssignRef, _TEXT

LEAF_ENTRY RhpCheckedAssignRef, _TEXT
    str x1, [x0]
    ret
LEAF_END RhpCheckedAssignRef, _TEXT

//
// RhpByRefAssignRef simulates movs instruction for object references.
//
// On entry:
//  x0: address of ref-field (assigned to)
//  x1: address of the data (source)
//  x3: be trashed
//
// On exit:
//  x0, x1 are incremented by 8, 
//  x3: trashed
//
LEAF_ENTRY RhpByRefAssignRef, _TEXT
    ldr x3, [x1], #8
    str x3, [x0], #8
    ret
LEAF_END RhpByRefAssignRef, _TEXT
