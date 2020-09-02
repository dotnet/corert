// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <unixasmmacros.inc>
#include "AsmOffsets.inc"

#ifdef FEATURE_CACHED_INTERFACE_DISPATCH

    .extern RhpCidResolve
    .extern RhpUniversalTransition_DebugStepTailCall

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
        PREPARE_EXTERNAL_VAR RhpCidResolve, xip0
        mov xip1, x11
        b       RhpUniversalTransition_DebugStepTailCall
    LEAF_END RhpInterfaceDispatchSlow, _TEXT

#endif // FEATURE_CACHED_INTERFACE_DISPATCH
