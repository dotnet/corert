// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <unixasmmacros.inc>
#include "AsmOffsets.inc"

#define __tls_array 0

#ifdef FEATURE_CACHED_INTERFACE_DISPATCH

    .extern RhpCidResolve
    .extern RhpUniversalTransition_DebugStepTailCall

    .macro GET_TLS_DISPATCH_CELL
        brk 1
/*
        ldr         x9, =_tls_index
        ldr         w9, [x9]
        ldr         xip1, [xpr, #__tls_array]
        ldr         xip1, [xip1, x9, lsl #3]
        ldr         x9, =SECTIONREL_t_TLS_DispatchCell
        ldr         x9, [x9]
        ldr         xip1, [xip1, x9]
*/
    .endm

    .macro SET_TLS_DISPATCH_CELL
        // xip1 : Value to be assigned to the TLS variable
        brk 1
/*
        ldr         x9, =_tls_index
        ldr         w9, [x9]
        ldr         x10, [xpr, #__tls_array]
        ldr         x10, [x10, x9, lsl #3]
        ldr         x9, =SECTIONREL_t_TLS_DispatchCell
        ldr         x9, [x9]
        str         xip1, [x10, x9]
*/
    .endm

/* TODO
SECTIONREL_t_TLS_DispatchCell
        DCD t_TLS_DispatchCell
        RELOC 8, t_TLS_DispatchCell      ;; SECREL
        DCD 0
*/

    // Macro that generates code to check a single cache entry.
    .macro CHECK_CACHE_ENTRY entry
        // Check a single entry in the cache.
        //  x9   : Cache data structure. Also used for target address jump.
        //  x10  : Instance EEType*
        //  x11  : x11 still contains the indirection cell address. do not trash
        //  x12  : Trashed
        ldr     x12, [x9, #(OFFSETOF__InterfaceDispatchCache__m_rgEntries + (\entry * 16))]
        cmp     x10, x12
        bne     0f
        ldr     x9, [x9, #(OFFSETOF__InterfaceDispatchCache__m_rgEntries + (\entry * 16) + 8)]
        br      x9
0:
    .endm


    LEAF_ENTRY RhpCastableObjectDispatch_CommonStub, _TEXT
        // Custom calling convention:
        //      xip0 has pointer to the current thunks data block

        // store dispatch cell address in thread static
        ldr     xip1, [xip0]
        SET_TLS_DISPATCH_CELL

        // Now load the target address and jump to it.
        ldr     x9, [xip0, #8]
        br      x9
    LEAF_END RhpCastableObjectDispatch_CommonStub, _TEXT

    LEAF_ENTRY RhpTailCallTLSDispatchCell, _TEXT
        // Load the dispatch cell out of the TLS variable
        GET_TLS_DISPATCH_CELL

        // Tail call to the target of the dispatch cell, preserving the cell address in xip1
        ldr     x9, [xip1]
        br      x9
    LEAF_END RhpTailCallTLSDispatchCell, _TEXT

//
// Macro that generates a stub consuming a cache with the given number of entries.
//
    .macro DEFINE_INTERFACE_DISPATCH_STUB entries

    NESTED_ENTRY "RhpInterfaceDispatch\entries", _TEXT, NoHandler

        // x11 currently holds the indirection cell address. We need to get the cache structure instead.
        ldr     x9, [x11, #OFFSETOF__InterfaceDispatchCell__m_pCache]

        // Load the EEType from the object instance in x0.
        ldr     x10, [x0]

    .global CurrentEntry 
    .set CurrentEntry, 0

    .rept \entries
        CHECK_CACHE_ENTRY CurrentEntry
        .set CurrentEntry, CurrentEntry + 1
    .endr

        // x11 still contains the indirection cell address.
        b RhpInterfaceDispatchSlow

    NESTED_END "RhpInterfaceDispatch\entries", _TEXT

    .endm

//
// Define all the stub routines we currently need.
//
    DEFINE_INTERFACE_DISPATCH_STUB 1
    DEFINE_INTERFACE_DISPATCH_STUB 2
    DEFINE_INTERFACE_DISPATCH_STUB 4
    DEFINE_INTERFACE_DISPATCH_STUB 8
    DEFINE_INTERFACE_DISPATCH_STUB 16
    DEFINE_INTERFACE_DISPATCH_STUB 32
    DEFINE_INTERFACE_DISPATCH_STUB 64

//
// Initial dispatch on an interface when we dont have a cache yet.
//
    LEAF_ENTRY RhpInitialInterfaceDispatch, _TEXT
        // Just tail call to the cache miss helper.
        b RhpInterfaceDispatchSlow
    LEAF_END RhpInitialInterfaceDispatch, _TEXT

//
// Stub dispatch routine for dispatch to a vtable slot
//
    LEAF_ENTRY RhpVTableOffsetDispatch, _TEXT
        // xip1 has the interface dispatch cell address in it. 
        // load x12 to point to the vtable offset (which is stored in the m_pCache field).
        ldr     x12, [xip1, #OFFSETOF__InterfaceDispatchCell__m_pCache]

        // Load the EEType from the object instance in x0, and add it to the vtable offset
        // to get the address in the vtable of what we want to dereference
        ldr     x13, [x0]
        add     x12, x12, x13

        // Load the target address of the vtable into x12
        ldr     x12, [x12]

        br      x12
    LEAF_END RhpVTableOffsetDispatch, _TEXT

//
// Cache miss case, call the runtime to resolve the target and update the cache.
//
    LEAF_ENTRY RhpInterfaceDispatchSlow, _TEXT
    ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch
        // xip1 has the interface dispatch cell address in it. 
        // Calling convention of the universal thunk is:
        //  xip0: contains target address for the thunk to call
        //  xip1: contains parameter of the thunks target
        adrp xip0, RhpCidResolve
        add	xip0, xip0, :lo12:RhpCidResolve
        mov xip1, x11
        b       RhpUniversalTransition_DebugStepTailCall
    LEAF_END RhpInterfaceDispatchSlow, _TEXT

#endif // FEATURE_CACHED_INTERFACE_DISPATCH
