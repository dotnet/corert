// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

// TODO: Implement Arm support

// Allocate non-array, non-finalizable object. If the allocation doesn't fit into the current thread's
// allocation context then automatically fallback to the slow allocation path.
//  r0 == EEType
    LEAF_ENTRY RhpNewFast, _TEXT
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    LEAF_END RhpNewFast, _TEXT

// Allocate non-array object with finalizer.
//  r0 == EEType
    LEAF_ENTRY RhpNewFinalizable, _TEXT
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    LEAF_END RhpNewFinalizable, _TEXT

// Allocate non-array object.
//  r0 == EEType
//  r1 == alloc flags
    NESTED_ENTRY RhpNewObject, _TEXT, NoHandler
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    NESTED_END RhpNewObject, _TEXT

// Allocate one dimensional, zero based array (SZARRAY).
//  r0 == EEType
//  r1 == element count
    LEAF_ENTRY RhpNewArray, _TEXT
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    LEAF_END RhpNewArray, _TEXT

// Allocate one dimensional, zero based array (SZARRAY) using the slow path that calls a runtime helper.
//  r0 == EEType
//  r1 == element count
//  r2 == array size + Thread::m_alloc_context::alloc_ptr
//  r3 == Thread
    NESTED_ENTRY RhpNewArrayRare, _TEXT, NoHandler
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    NESTED_END RhpNewArrayRare, _TEXT

// Allocate simple object (not finalizable, array or value type) on an 8 byte boundary.
//  r0 == EEType
    LEAF_ENTRY RhpNewFastAlign8, _TEXT
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    LEAF_END RhpNewFastAlign8, _TEXT

// Allocate a finalizable object (by definition not an array or value type) on an 8 byte boundary.
//  r0 == EEType
    LEAF_ENTRY RhpNewFinalizableAlign8, _TEXT
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    LEAF_END RhpNewFinalizableAlign8, _TEXT

// Allocate a value type object (i.e. box it) on an 8 byte boundary + 4 (so that the value type payload
// itself is 8 byte aligned).
//  r0 == EEType
    LEAF_ENTRY RhpNewFastMisalign, _TEXT
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    LEAF_END RhpNewFastMisalign, _TEXT

// Allocate an array on an 8 byte boundary.
//  r0 == EEType
//  r1 == element count
    NESTED_ENTRY RhpNewArrayAlign8, _TEXT, NoHandler
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
    NESTED_END RhpNewArrayAlign8, _TEXT
