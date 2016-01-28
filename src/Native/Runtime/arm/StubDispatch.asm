;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

        TEXTAREA

#ifdef FEATURE_VSD

        SETALIAS RESOLVE_WORKER_STATIC, ?ResolveWorkerStatic@VirtualCallStubManager@@CAPAEPAEPAVEEType@@PAPBE@Z
        SETALIAS BACKPATCH_WORKER_STATIC, ?BackPatchWorkerStatic@VirtualCallStubManager@@CAXPBEPAPBE@Z

        EXTERN $RESOLVE_WORKER_STATIC
        EXTERN $BACKPATCH_WORKER_STATIC

;; This is the initial and failure entrypoint for VSD. All interface call sites
;; start off pointing here, and all failed mono- and poly-morphic target lookups
;; end up here. The purpose is to save the right registers and pass control to
;; VirtualCallStubManager to find the correct target, change the indirection cell
;; (if necessary) and populate the poly-morphic cache (if necessary).
        NESTED_ENTRY VSDResolveWorkerAsmStub

        ;; Save argument registers (including floating point) and the return address.
        PROLOG_PUSH {r0-r4,lr}  ; Save r4 just for alignment purposes
        PROLOG_VPUSH {d0-d7}

        ldr     r3, [r0]    ; Extract EEType* from object in r0

        ;; Setup arguments to VirtualCallStubManager::ResolveWorkerStatic (r2, indirCellAddrForRegisterIndirect
        ;; is never used on ARM)
        mov     r0, lr      ; Return address
        mov     r1, r3      ; Object EEType*

        bl      $RESOLVE_WORKER_STATIC
        orr     r12, r0, #1 ; make sure to set the thumb2 bit

        EPILOG_VPOP {d0-d7}
        EPILOG_POP {r0-r4,lr}

        EPILOG_BRANCH_REG r12

        NESTED_END VSDResolveWorkerAsmStub

;; Call the callsite back patcher.  The fail stub piece of the resolver is being
;; call too often, i.e. dispatch stubs are failing the expect MT test too often.
;; In this stub wraps the call to the BackPatchWorker to take care of any stack magic
;; needed.
        NESTED_ENTRY VSDBackPatchWorkerAsmStub

        ;; Save argument registers (including floating point) and the return address.
        PROLOG_PUSH {r0-r4,lr}  ; Save r4 just for alignment purposes
        PROLOG_VPUSH {d0-d7}

        ;; Setup arguments to VirtualCallStubManager::BackPatchWorkerStatic (r1, indirCellAddrForRegisterIndirect
        ;; is never used on ARM)
        ldr     r0, [sp, #(8*8 + 6*4 + 4)]     ; Return address

        bl      $BACKPATCH_WORKER_STATIC

        EPILOG_VPOP {d0-d7}
        EPILOG_POP {r0-r4,lr}

        EPILOG_RETURN

        NESTED_END VSDBackPatchWorkerAsmStub

#endif // FEATURE_VSD

#ifdef FEATURE_CACHED_INTERFACE_DISPATCH

    EXTERN RhpResolveInterfaceMethodCacheMiss

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
    NESTED_ENTRY RhpInterfaceDispatchSlow
    ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch

        ;; Save argument registers (including floating point) and the return address. Note that we depend on
        ;; these registers (which may contain GC references) being spilled before we build the
        ;; PInvokeTransitionFrame below due to the way we build a stack range to report to the GC
        ;; conservatively during a collection.
        PROLOG_PUSH {r0-r3,lr}
        PROLOG_VPUSH {d0-d7}

        ;; Build PInvokeTransitionFrame. This is only required if we end up resolving the interface method via
        ;; a callout to a managed ICastable method. In that instance we need to be able to cope with garbage
        ;; collections which in turn need to be able to walk the stack from the ICastable method, skip the
        ;; unmanaged runtime portions and resume walking at our caller. This frame provides both the means to
        ;; unwind to that caller and a place to spill callee saved registers in case they contain GC
        ;; references from the caller.

        PROLOG_STACK_ALLOC 8        ; Align the stack and save space for caller's SP
        PROLOG_PUSH {r4-r6,r8-r10}  ; Save preserved registers
        PROLOG_STACK_ALLOC 8        ; Save space for flags and Thread*
        PROLOG_PUSH {r7}            ; Save caller's FP
        PROLOG_PUSH {r11,lr}        ; Save caller's frame-chain pointer and PC

        ;; Compute SP value at entry to this method and save it in the last slot of the frame (slot #11).
        add         r1, sp, #((12 * 4) + 4 + (8 * 8) + (5 * 4))
        str         r1, [sp, #(11 * 4)]

        ;; Record the bitmask of saved registers in the frame (slot #4).
        mov         r1, #DEFAULT_FRAME_SAVE_FLAGS
        str         r1, [sp, #(4 * 4)]

        ;; First argument is the instance we're dispatching on which is already in r0.

        ;; Second argument is the dispatch data cell. 
        ;; We still have this in r12
        mov     r1, r12

        ;; The third argument is the address of the transition frame we build above. Currently it's at the top
        ;; of the stack so sp points to it.
        mov     r2, sp

        bl    RhpResolveInterfaceMethodCacheMiss

        ;; Move the result (the target address) to r12 so it doesn't get overridden when we restore the
        ;; argument registers. Additionally make sure the thumb2 bit is set.
        orr     r12, r0, #1

        ;; Pop the PInvokeTransitionFrame
        EPILOG_POP  {r11,lr}        ; Restore caller's frame-chain pointer and PC (return address)
        EPILOG_POP  {r7}            ; Restore caller's FP
        EPILOG_STACK_FREE 8         ; Discard flags and Thread*
        EPILOG_POP  {r4-r6,r8-r10}  ; Restore preserved registers
        EPILOG_STACK_FREE 8         ; Discard caller's SP and stack alignment padding

        ;; Restore the argument registers.
        EPILOG_VPOP {d0-d7}
        EPILOG_POP {r0-r3,lr}

        EPILOG_BRANCH_REG r12

    NESTED_END RhpInterfaceDispatchSlow


#endif // FEATURE_CACHED_INTERFACE_DISPATCH

        end
