;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

        TEXTAREA


#ifdef FEATURE_CACHED_INTERFACE_DISPATCH

    EXTERN RhpCidResolve
    EXTERN RhpUniversalTransition_DebugStepTailCall

    ;; Macro that generates code to check a single cache entry.
    MACRO
        CHECK_CACHE_ENTRY $entry
        ;; Check a single entry in the cache.
        ;;  R1 : Instance EEType*
        ;;  R12: Cache data structure
        ;;  R2 : Trashed

        ldr     r2, [r12, #(OFFSETOF__InterfaceDispatchCache__m_rgEntries + ($entry * 8))]
        cmp     r1, r2
        bne     %ft0
        ldr     r12, [r12, #(OFFSETOF__InterfaceDispatchCache__m_rgEntries + ($entry * 8) + 4)]
        b       %fa99
0
    MEND


;; Macro that generates a stub consuming a cache with the given number of entries.
    GBLS StubName

    MACRO
        DEFINE_INTERFACE_DISPATCH_STUB $entries

StubName    SETS    "RhpInterfaceDispatch$entries"

    NESTED_ENTRY $StubName
        ;; On input we have the indirection cell data structure in r12. But we need more scratch registers and
        ;; we may A/V on a null this. Both of these suggest we need a real prolog and epilog.
        PROLOG_PUSH {r1-r2}

        ;; r12 currently holds the indirection cell address. We need to update it to point to the cache
        ;; structure instead.
        ldr     r12, [r12, #OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; Load the EEType from the object instance in r0.
        ldr     r1, [r0]

        GBLA CurrentEntry 
CurrentEntry SETA 0
    WHILE CurrentEntry < $entries
        CHECK_CACHE_ENTRY CurrentEntry
CurrentEntry SETA CurrentEntry + 1
    WEND

        ;; r12 currently contains the cache block. We need to point it back to the 
        ;; indirection cell using the back pointer in the cache block
        ldr     r12, [r12, #OFFSETOF__InterfaceDispatchCache__m_pCell]

        EPILOG_POP {r1-r2}
        EPILOG_BRANCH RhpInterfaceDispatchSlow

        ;; Common epilog for cache hits. Have to out of line it here due to limitation on the number of
        ;; epilogs imposed by the unwind code macros.
99
        EPILOG_POP {r1-r2}
        EPILOG_BRANCH_REG r12

    NESTED_END $StubName

    MEND

;; Define all the stub routines we currently need.
        DEFINE_INTERFACE_DISPATCH_STUB 1
        DEFINE_INTERFACE_DISPATCH_STUB 2
        DEFINE_INTERFACE_DISPATCH_STUB 4
        DEFINE_INTERFACE_DISPATCH_STUB 8
        DEFINE_INTERFACE_DISPATCH_STUB 16
        DEFINE_INTERFACE_DISPATCH_STUB 32
        DEFINE_INTERFACE_DISPATCH_STUB 64


;; Initial dispatch on an interface when we don't have a cache yet.
    LEAF_ENTRY RhpInitialInterfaceDispatch

        ;; The stub that jumped here pushed r12, which contains the interface dispatch cell
        ;; we need to pop it here
        pop         { r12 }

        ;; Simply tail call the slow dispatch helper.
        b RhpInterfaceDispatchSlow

    LEAF_END RhpInitialInterfaceDispatch


;; Cache miss case, call the runtime to resolve the target and update the cache.
    LEAF_ENTRY RhpInterfaceDispatchSlow
    ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch
        ;; r12 has the interface dispatch cell address in it. 
        ;; The calling convention of the universal thunk is that the parameter
        ;; for the universal thunk target is to be placed in sp-8
        ;; and the universal thunk target address is to be placed in sp-4
        str    r12, [sp, #-8]
        ldr     r12, =RhpCidResolve
        str    r12, [sp, #-4]
        
        ;; jump to universal transition thunk
        b RhpUniversalTransition_DebugStepTailCall
    LEAF_END RhpInterfaceDispatchSlow


#endif // FEATURE_CACHED_INTERFACE_DISPATCH

        end
