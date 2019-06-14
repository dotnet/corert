;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

        TEXTAREA


#ifdef FEATURE_CACHED_INTERFACE_DISPATCH

    EXTERN RhpCastableObjectResolve
    EXTERN RhpCidResolve
    EXTERN RhpUniversalTransition_DebugStepTailCall

    EXTERN t_TLS_DispatchCell

    MACRO
        GET_TLS_DISPATCH_CELL
        EXTERN      _tls_index
        push        {r0}
        ldr         r12, =_tls_index
        ldr         r12, [r12]
        mrc         p15, 0, r0, c13, c0, 2
        ldr         r0, [r0, #__tls_array]
        ldr         r12, [r0, r12, lsl #2]
        ldr         r0, SECTIONREL_t_TLS_DispatchCell
        ldr         r12, [r0, r12]
        pop         {r0}
    MEND

    MACRO
        SET_TLS_DISPATCH_CELL
        EXTERN      _tls_index
        ;; r12 : Value to be assigned to the TLS variable
        push        {r0-r1}
        ldr         r0, =_tls_index
        ldr         r0, [r0]
        mrc         p15, 0, r1, c13, c0, 2
        ldr         r1, [r1, #__tls_array]
        ldr         r0, [r1, r0, lsl #2]    ;; r0 <- our TLS base
        ldr         r1, SECTIONREL_t_TLS_DispatchCell
        str         r12, [r1, r0]
        pop         {r0-r1}
    MEND

    ;; Macro that generates code to check a single cache entry.
    MACRO
        CHECK_CACHE_ENTRY $entry
        ;; Check a single entry in the cache.
        ;;  R1 : Instance EEType*
        ;;  R2: Cache data structure
        ;;  R12 : Trashed. On succesful check, set to the target address to jump to.

        ldr     r12, [r2, #(OFFSETOF__InterfaceDispatchCache__m_rgEntries + ($entry * 8))]
        cmp     r1, r12
        bne     %ft0
        ldr     r12, [r2, #(OFFSETOF__InterfaceDispatchCache__m_rgEntries + ($entry * 8) + 4)]
        b       %fa99
0
    MEND

SECTIONREL_t_TLS_DispatchCell
        DCD     t_TLS_DispatchCell
        RELOC   15 ;; SECREL

    LEAF_ENTRY RhpCastableObjectDispatch_CommonStub
        ;; Custom calling convention:
        ;;      Red zone (i.e. [sp, #-4]) has pointer to the current thunk's data block
        
        ;; store dispatch cell address in thread static
        ldr         r12, [sp, #-4]
        push        {r12}
        ldr         r12, [r12]
        SET_TLS_DISPATCH_CELL
        
        ;; Now load the target address and jump to it.
        pop         {r12}
        ldr         r12, [r12, #4]
        bx          r12
    LEAF_END RhpCastableObjectDispatch_CommonStub

    LEAF_ENTRY RhpTailCallTLSDispatchCell
        ;; Load the dispatch cell out of the TLS variable
        GET_TLS_DISPATCH_CELL

        ;; Tail call to the target of the dispatch cell, preserving the cell address in r12
        ldr     pc, [r12]
    LEAF_END RhpTailCallTLSDispatchCell

    LEAF_ENTRY RhpCastableObjectDispatchHelper_TailCalled
        ;; Load the dispatch cell out of the TLS variable
        GET_TLS_DISPATCH_CELL
        b       RhpCastableObjectDispatchHelper2
    LEAF_END RhpCastableObjectDispatchHelper_TailCalled

    LEAF_ENTRY  RhpCastableObjectDispatchHelper
        ;; The address of the cache block is passed to this function in the red zone
        ldr     r12, [sp, #-8] 
        ldr     r12, [r12, #OFFSETOF__InterfaceDispatchCache__m_pCell]
    ALTERNATE_ENTRY RhpCastableObjectDispatchHelper2
        ;; The calling convention of the universal thunk is that the parameter
        ;; for the universal thunk target is to be placed in sp-8
        ;; and the universal thunk target address is to be placed in sp-4
        str     r12, [sp, #-8]
        ldr     r12, =RhpCastableObjectResolve
        str     r12, [sp, #-4]

        b       RhpUniversalTransition_DebugStepTailCall
    LEAF_END RhpCastableObjectDispatchHelper


;; Macro that generates a stub consuming a cache with the given number of entries.
    GBLS StubName

    MACRO
        DEFINE_INTERFACE_DISPATCH_STUB $entries

StubName    SETS    "RhpInterfaceDispatch$entries"

    NESTED_ENTRY $StubName
        ;; On input we have the indirection cell data structure in r12. But we need more scratch registers and
        ;; we may A/V on a null this. Both of these suggest we need a real prolog and epilog.
        PROLOG_PUSH {r1-r2}

        ;; r12 currently holds the indirection cell address. We need to get the cache structure instead.
        ldr     r2, [r12, #OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; Load the EEType from the object instance in r0.
        ldr     r1, [r0]

        GBLA CurrentEntry 
CurrentEntry SETA 0
    WHILE CurrentEntry < $entries
        CHECK_CACHE_ENTRY CurrentEntry
CurrentEntry SETA CurrentEntry + 1
    WEND

        ;; Point r12 to the indirection cell using the back pointer in the cache block
        ldr     r12, [r2, #OFFSETOF__InterfaceDispatchCache__m_pCell]

        EPILOG_POP {r1-r2}
        EPILOG_BRANCH RhpInterfaceDispatchSlow

        ;; Common epilog for cache hits. Have to out of line it here due to limitation on the number of
        ;; epilogs imposed by the unwind code macros.
99
        ;; R2 contains address of the cache block. We store it in the red zone in case the target we jump
        ;; to needs it. Currently the RhpCastableObjectDispatchHelper is the only such target.
        ;; R12 contains the target address to jump to
        EPILOG_POP r1
        ;; The red zone is only 8 bytes long, so we have to store r2 into it between the pops.
        EPILOG_NOP str     r2, [sp, #-4]
        EPILOG_POP r2
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
        pop     { r12 }

        ;; Simply tail call the slow dispatch helper.
        b       RhpInterfaceDispatchSlow

    LEAF_END RhpInitialInterfaceDispatch

    LEAF_ENTRY RhpVTableOffsetDispatch
        ;; On input we have the indirection cell data structure in r12. But we need more scratch registers and
        ;; we may A/V on a null this. Both of these suggest we need a real prolog and epilog.
        PROLOG_PUSH {r1}

        ;; r12 currently holds the indirection cell address. We need to update it to point to the vtable
        ;; offset instead.
        ldr     r12, [r12, #OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; Load the EEType from the object instance in r0.
        ldr     r1, [r0]

        ;; add the vtable offset to the EEType pointer 
        add     r12, r1, r12

        ;; Load the target address of the vtable into r12
        ldr     r12, [r12]

        EPILOG_POP {r1}
        EPILOG_BRANCH_REG r12
    LEAF_END RhpVTableOffsetDispatch

;; Cache miss case, call the runtime to resolve the target and update the cache.
    LEAF_ENTRY RhpInterfaceDispatchSlow
    ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch
        ;; r12 has the interface dispatch cell address in it. 
        ;; The calling convention of the universal thunk is that the parameter
        ;; for the universal thunk target is to be placed in sp-8
        ;; and the universal thunk target address is to be placed in sp-4
        str     r12, [sp, #-8]
        ldr     r12, =RhpCidResolve
        str     r12, [sp, #-4]
        
        ;; jump to universal transition thunk
        b       RhpUniversalTransition_DebugStepTailCall
    LEAF_END RhpInterfaceDispatchSlow


#endif // FEATURE_CACHED_INTERFACE_DISPATCH

        end
