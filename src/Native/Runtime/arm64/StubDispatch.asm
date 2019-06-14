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

        ldr         x9, =_tls_index
        ldr         w9, [x9]
        ldr         xip1, [xpr, #__tls_array]
        ldr         xip1, [xip1, x9 lsl #3]
        ldr         x9, =SECTIONREL_t_TLS_DispatchCell
        ldr         x9, [x9]
        ldr         xip1, [xip1, x9]
    MEND

    MACRO
        SET_TLS_DISPATCH_CELL
        ;; xip1 : Value to be assigned to the TLS variable

        ldr         x9, =_tls_index
        ldr         w9, [x9]
        ldr         x10, [xpr, #__tls_array]
        ldr         x10, [x10, x9 lsl #3]
        ldr         x9, =SECTIONREL_t_TLS_DispatchCell
        ldr         x9, [x9]
        str         xip1, [x10, x9]
    MEND

SECTIONREL_t_TLS_DispatchCell
        DCD t_TLS_DispatchCell
        RELOC 8, t_TLS_DispatchCell      ;; SECREL
        DCD 0

    ;; Macro that generates code to check a single cache entry.
    MACRO
        CHECK_CACHE_ENTRY $entry
        ;; Check a single entry in the cache.
        ;;  x9   : Cache data structure. Also used for target address jump.
        ;;  x10  : Instance EEType*
        ;;  x11  : Trashed
        ldr     x11, [x9, #(OFFSETOF__InterfaceDispatchCache__m_rgEntries + ($entry * 16))]
        cmp     x10, x11
        bne     %ft0    ;; Jump to label '0'
        ldr     x9, [x9, #(OFFSETOF__InterfaceDispatchCache__m_rgEntries + ($entry * 16) + 8)]
        br      x9
0   ;; Label '0'
    MEND


    LEAF_ENTRY RhpCastableObjectDispatch_CommonStub
        ;; Custom calling convention:
        ;;      xip0 has pointer to the current thunk's data block

        ;; store dispatch cell address in thread static
        ldr     xip1, [xip0]
        SET_TLS_DISPATCH_CELL

        ;; Now load the target address and jump to it.
        ldr     x9, [xip0, #8]
        br      x9
    LEAF_END RhpCastableObjectDispatch_CommonStub

    LEAF_ENTRY RhpTailCallTLSDispatchCell
        ;; Load the dispatch cell out of the TLS variable
        GET_TLS_DISPATCH_CELL

        ;; Tail call to the target of the dispatch cell, preserving the cell address in xip1
        ldr     x9, [xip1]
        br      x9
    LEAF_END RhpTailCallTLSDispatchCell

    LEAF_ENTRY RhpCastableObjectDispatchHelper_TailCalled
        ;; Load the dispatch cell out of the TLS variable
        GET_TLS_DISPATCH_CELL
        b       RhpCastableObjectDispatchHelper
    LEAF_END RhpCastableObjectDispatchHelper_TailCalled

    LEAF_ENTRY  RhpCastableObjectDispatchHelper
        ;; The address of the indirection cell is passed to this function in the xip1
        ;; The calling convention of the universal thunk is that the parameter
        ;; for the universal thunk target is to be placed in xip1
        ;; and the universal thunk target address is to be placed in xip0
        ldr     xip0, =RhpCastableObjectResolve

        b       RhpUniversalTransition_DebugStepTailCall
    LEAF_END RhpCastableObjectDispatchHelper


;;
;; Macro that generates a stub consuming a cache with the given number of entries.
;;
    GBLS StubName

    MACRO
        DEFINE_INTERFACE_DISPATCH_STUB $entries

StubName    SETS    "RhpInterfaceDispatch$entries"

    NESTED_ENTRY $StubName

        ;; xip1 currently holds the indirection cell address. We need to get the cache structure instead.
        ldr     x9, [xip1, #OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; Load the EEType from the object instance in x0.
        ldr     x10, [x0]

    GBLA CurrentEntry 
CurrentEntry SETA 0

    WHILE CurrentEntry < $entries
        CHECK_CACHE_ENTRY CurrentEntry
CurrentEntry SETA CurrentEntry + 1
    WEND

        ;; xip1 still contains the indirection cell address.
        b RhpInterfaceDispatchSlow

    NESTED_END $StubName

    MEND

;;
;; Define all the stub routines we currently need.
;;
    DEFINE_INTERFACE_DISPATCH_STUB 1
    DEFINE_INTERFACE_DISPATCH_STUB 2
    DEFINE_INTERFACE_DISPATCH_STUB 4
    DEFINE_INTERFACE_DISPATCH_STUB 8
    DEFINE_INTERFACE_DISPATCH_STUB 16
    DEFINE_INTERFACE_DISPATCH_STUB 32
    DEFINE_INTERFACE_DISPATCH_STUB 64


;;
;; Initial dispatch on an interface when we don't have a cache yet.
;;
    LEAF_ENTRY RhpInitialInterfaceDispatch
        ;; Just tail call to the cache miss helper.
        b RhpInterfaceDispatchSlow
    LEAF_END RhpInitialInterfaceDispatch

;;
;; Stub dispatch routine for dispatch to a vtable slot
;;
    LEAF_ENTRY RhpVTableOffsetDispatch
        ;; xip1 has the interface dispatch cell address in it. 
        ;; load x12 to point to the vtable offset (which is stored in the m_pCache field).
        ldr     x12, [xip1, #OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; Load the EEType from the object instance in x0, and add it to the vtable offset
        ;; to get the address in the vtable of what we want to dereference
        ldr     x13, [x0]
        add     x12, x12, x13

        ;; Load the target address of the vtable into x12
        ldr     x12, [x12]

        br      x12
    LEAF_END RhpVTableOffsetDispatch

;;
;; Cache miss case, call the runtime to resolve the target and update the cache.
;;
    LEAF_ENTRY RhpInterfaceDispatchSlow
    ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch
        ;; xip1 has the interface dispatch cell address in it. 
        ;; Calling convention of the universal thunk is:
        ;;  xip0: contains target address for the thunk to call
        ;;  xip1: contains parameter of the thunk's target
        ldr     xip0, =RhpCidResolve
        b       RhpUniversalTransition_DebugStepTailCall
    LEAF_END RhpInterfaceDispatchSlow

#endif // FEATURE_CACHED_INTERFACE_DISPATCH

    END
