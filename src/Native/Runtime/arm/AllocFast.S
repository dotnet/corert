// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

#ifdef _DEBUG
.rodata
AssertMsg:                    .asciz "__FILE__:%s: %s is not implemented\n"
FileName:                     .asciz "AllocFast.S"
RhpNewFastName:               .asciz "RhpNewFast"
RhpNewFinalizableName:        .asciz "RhpNewFinalizable"
RhpNewObjectName:             .asciz "RhpNewObject"
RhpNewArrayName:              .asciz "RhpNewArray"
RhpNewArrayRareName:          .asciz "RhpNewArrayRare"
RhpNewFastAlign8Name:         .asciz "RhpNewFastAlign8"
RhpNewFinalizableAlign8Name:  .asciz "RhpNewFinalizableAlign8"
RhpNewFastMisalignName:       .asciz "RhpNewFastMisalign"
RhpNewArrayAlign8Name:        .asciz "RhpNewArrayAlign8"

.text
.macro GEN_ASSERT_FUNC func
        GEN_ASSERT AssertMsg, FileName, \func
.endm
#endif

// TODO: Implement Arm support

// Allocate non-array, non-finalizable object. If the allocation doesn't fit into the current thread's
// allocation context then automatically fallback to the slow allocation path.
//  r0 == EEType
    LEAF_ENTRY RhpNewFast, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewFastName
#endif
    LEAF_END RhpNewFast, _TEXT

// Allocate non-array object with finalizer.
//  r0 == EEType
    LEAF_ENTRY RhpNewFinalizable, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewFinalizableName
#endif
    LEAF_END RhpNewFinalizable, _TEXT

// Allocate non-array object.
//  r0 == EEType
//  r1 == alloc flags
    NESTED_ENTRY RhpNewObject, _TEXT, NoHandler
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewObjectName
#endif
    NESTED_END RhpNewObject, _TEXT

// Allocate one dimensional, zero based array (SZARRAY).
//  r0 == EEType
//  r1 == element count
    LEAF_ENTRY RhpNewArray, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewArrayName
#endif
    LEAF_END RhpNewArray, _TEXT

// Allocate one dimensional, zero based array (SZARRAY) using the slow path that calls a runtime helper.
//  r0 == EEType
//  r1 == element count
//  r2 == array size + Thread::m_alloc_context::alloc_ptr
//  r3 == Thread
    NESTED_ENTRY RhpNewArrayRare, _TEXT, NoHandler
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewArrayRareName
#endif
    NESTED_END RhpNewArrayRare, _TEXT

// Allocate simple object (not finalizable, array or value type) on an 8 byte boundary.
//  r0 == EEType
    LEAF_ENTRY RhpNewFastAlign8, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewFastAlign8Name
#endif
    LEAF_END RhpNewFastAlign8, _TEXT

// Allocate a finalizable object (by definition not an array or value type) on an 8 byte boundary.
//  r0 == EEType
    LEAF_ENTRY RhpNewFinalizableAlign8, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewFinalizableAlign8Name
#endif
    LEAF_END RhpNewFinalizableAlign8, _TEXT

// Allocate a value type object (i.e. box it) on an 8 byte boundary + 4 (so that the value type payload
// itself is 8 byte aligned).
//  r0 == EEType
    LEAF_ENTRY RhpNewFastMisalign, _TEXT
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewFastMisalignName
#endif
    LEAF_END RhpNewFastMisalign, _TEXT

// Allocate an array on an 8 byte boundary.
//  r0 == EEType
//  r1 == element count
    NESTED_ENTRY RhpNewArrayAlign8, _TEXT, NoHandler
#ifdef _DEBUG
        GEN_ASSERT_FUNC RhpNewArrayAlign8Name
#endif
    NESTED_END RhpNewArrayAlign8, _TEXT
