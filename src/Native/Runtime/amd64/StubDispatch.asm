;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

include AsmMacros.inc

ifdef FEATURE_VSD

RESOLVE_WORKER_STATIC   equ ?ResolveWorkerStatic@VirtualCallStubManager@@CAPEAEPEAEPEAVEEType@@PEAPEBE@Z
BACKPATCH_WORKER_STATIC equ ?BackPatchWorkerStatic@VirtualCallStubManager@@CAXPEBEPEAPEBE@Z

;; VirtualStubDispatch
EXTERN RESOLVE_WORKER_STATIC : PROC
EXTERN BACKPATCH_WORKER_STATIC : PROC

;; This is the initial and failure entrypoint for VSD. All interface call sites
;; start off pointing here, and all failed mono- and poly-morphic target lookups
;; end up here. The purpose is to save the right registers and pass control to
;; VirtualCallStubManager to find the correct target, change the indirection cell
;; (if necessary) and populate the poly-morphic cache (if necessary).

VSDRWAS_ReservedStack equ 20h + 8h + 40h    ;; Scratch space, padding, and room for the FP args

NESTED_ENTRY VSDResolveWorkerAsmStub, _TEXT
        ;; Figure out the return address
        mov                 r10, [rsp]

        alloc_stack         VSDRWAS_ReservedStack

        ;; preserve the argument registers in the scratch space across the helper call.
        save_reg_postrsp    rcx, (VSDRWAS_ReservedStack + 8*1)
        save_reg_postrsp    rdx, (VSDRWAS_ReservedStack + 8*2)
        save_reg_postrsp    r8,  (VSDRWAS_ReservedStack + 8*3)
        save_reg_postrsp    r9,  (VSDRWAS_ReservedStack + 8*4)
        save_xmm128_postrsp xmm0, (20h + 16*0)
        save_xmm128_postrsp xmm1, (20h + 16*1)
        save_xmm128_postrsp xmm2, (20h + 16*2)
        save_xmm128_postrsp xmm3, (20h + 16*3)
        END_PROLOGUE
        
        ;; what was in rax (could be address of indirection in shared generic instantiation case)
        ;; gets passed in r8
        mov     r8, r11

        ;; load the eetype from the object instance in rcx
        mov     rdx, [rcx]

        ;; return address is first argument
        mov     rcx, r10

        call    RESOLVE_WORKER_STATIC

        ;; Restore the argument registers.
        movdqa  xmm0, [rsp + 20h + 16*0]
        movdqa  xmm1, [rsp + 20h + 16*1]
        movdqa  xmm2, [rsp + 20h + 16*2]
        movdqa  xmm3, [rsp + 20h + 16*3]
        mov     r9,  [rsp + VSDRWAS_ReservedStack + 8*4]
        mov     r8,  [rsp + VSDRWAS_ReservedStack + 8*3]
        mov     rdx, [rsp + VSDRWAS_ReservedStack + 8*2]
        mov     rcx, [rsp + VSDRWAS_ReservedStack + 8*1]

        add     rsp, VSDRWAS_ReservedStack
        TAILJMP_RAX
NESTED_END VSDResolveWorkerAsmStub, _TEXT


;; Call the callsite back patcher.  The fail stub piece of the resolver is being
;; call too often, i.e. dispatch stubs are failing the expect EEType test too often.

VSDBPWAS_ReservedStack equ 20h + 40h + 10h   ;; Scratch space, room for the FP args, and space to save R11

NESTED_ENTRY VSDBackPatchWorkerAsmStub, _TEXT
        ;; Account for the return address in our unwind info. We've been called from
        ;; the ResolveStub, and it does not have any unwind info, nor has it reserved
        ;; any scratch space. We make it okay to be called from a leaf function by
        ;; accounting for the stack spaced it used making the call to us in our frame.
        ;; i.e., this fools the unwinder into using the return address of the previous
        ;; frame for the return address of this frame, thereby skipping the leaf frame.
        .allocstack 8
        
        ;; Figure out the return address of the method that has called the ResolveStub.
        ;; It is at +8 becuase the entry at the top of the stack is the return address
        ;; to the ResolveStub.
        mov     r10, [rsp + 8]
        
        alloc_stack     VSDBPWAS_ReservedStack

        ;; Preserve the argument registers across our helper call. We use the scratch space
        ;; allocated by the caller of the ResolveStub, thus the extra 8 bytes in the offsets.
        save_reg_postrsp    rcx, (VSDBPWAS_ReservedStack + 8 + 8*1)
        save_reg_postrsp    rdx, (VSDBPWAS_ReservedStack + 8 + 8*2)
        save_reg_postrsp    r8,  (VSDBPWAS_ReservedStack + 8 + 8*3)
        save_reg_postrsp    r9,  (VSDBPWAS_ReservedStack + 8 + 8*4)

        ;; save xmm regs just after the scratch space for our callees
        save_xmm128_postrsp xmm0, (20h + 10h*0)
        save_xmm128_postrsp xmm1, (20h + 10h*1)
        save_xmm128_postrsp xmm2, (20h + 10h*2)
        save_xmm128_postrsp xmm3, (20h + 10h*3)
        
        ;; save the indirection cell address for shared generic instantiation case after the xmm regs
        save_reg_postrsp    r11,  (20h + 10h*4)
        
        END_PROLOGUE

        ;; whatever was in r11 is second argument - this is the
        ;; address of the indirection cell in the shared generic instantiation case
        mov     rdx, r11
 
        ;; return address is first argument
        mov     rcx, r10

        call BACKPATCH_WORKER_STATIC

        ;; restore shared generic instantiation indirection cell
        mov     r11,  [rsp + 20h + 10h*4]

        ;; Restore the argument registers.
        movdqa  xmm0, [rsp + 20h + 10h*0]
        movdqa  xmm1, [rsp + 20h + 10h*1]
        movdqa  xmm2, [rsp + 20h + 10h*2]
        movdqa  xmm3, [rsp + 20h + 10h*3]
        mov     r9,  [rsp + VSDBPWAS_ReservedStack + 8 + 8*4]
        mov     r8,  [rsp + VSDBPWAS_ReservedStack + 8 + 8*3]
        mov     rdx, [rsp + VSDBPWAS_ReservedStack + 8 + 8*2]
        mov     rcx, [rsp + VSDBPWAS_ReservedStack + 8 + 8*1]

        add     rsp, VSDBPWAS_ReservedStack
        ret
NESTED_END VSDBackPatchWorkerAsmStub, _TEXT

endif ;; FEATURE_VSD

ifdef FEATURE_CACHED_INTERFACE_DISPATCH

EXTERN RhpResolveInterfaceMethodCacheMiss : PROC


;; Macro that generates code to check a single cache entry.
CHECK_CACHE_ENTRY macro entry
NextLabel textequ @CatStr( Attempt, %entry+1 )
        cmp     rax, [r11 + OFFSETOF__InterfaceDispatchCache__m_rgEntries + (entry * 16)]
        jne     NextLabel
        jmp     qword ptr [r11 + OFFSETOF__InterfaceDispatchCache__m_rgEntries + (entry * 16) + 8]
NextLabel:
endm


;; Macro that generates a stub consuming a cache with the given number of entries.
DEFINE_INTERFACE_DISPATCH_STUB macro entries

StubName textequ @CatStr( RhpInterfaceDispatch, entries )

LEAF_ENTRY StubName, _TEXT

;EXTERN CID_g_cInterfaceDispatches : DWORD
        ;inc     [CID_g_cInterfaceDispatches]

        ;; r10 currently contains the indirection cell address. 
        ;; load r11 to point to the cache block.
        mov     r11, [r10 + OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; Load the EEType from the object instance in rcx.
        mov     rax, [rcx]

CurrentEntry = 0
    while CurrentEntry lt entries
        CHECK_CACHE_ENTRY %CurrentEntry
CurrentEntry = CurrentEntry + 1
    endm

        ;; r10 still contains the the indirection cell address.

        jmp RhpInterfaceDispatchSlow

LEAF_END StubName, _TEXT

    endm ;; DEFINE_INTERFACE_DISPATCH_STUB


;; Define all the stub routines we currently need.
;;
;; The mrt100dbi requires these be exported to identify mrt100 code that dispatches back into managed.
;; If you change or add any new dispatch stubs, please also change slr.def and dbi\process.cpp CordbProcess::GetExportStepInfo
;;
DEFINE_INTERFACE_DISPATCH_STUB 1
DEFINE_INTERFACE_DISPATCH_STUB 2
DEFINE_INTERFACE_DISPATCH_STUB 4
DEFINE_INTERFACE_DISPATCH_STUB 8
DEFINE_INTERFACE_DISPATCH_STUB 16
DEFINE_INTERFACE_DISPATCH_STUB 32
DEFINE_INTERFACE_DISPATCH_STUB 64


;; Initial dispatch on an interface when we don't have a cache yet.
LEAF_ENTRY RhpInitialInterfaceDispatch, _TEXT
ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch

        ;; Just tail call to the cache miss helper.
        jmp RhpInterfaceDispatchSlow

LEAF_END RhpInitialInterfaceDispatch, _TEXT

;; Cache miss case, call the runtime to resolve the target and update the cache.
NESTED_ENTRY RhpInterfaceDispatchSlow, _TEXT

RIDS_ReservedStack equ 20h + 60h + 40h + 8h     ;; Scratch space, transition frame, xmm registers and padding

        alloc_stack         RIDS_ReservedStack

        ;; Preserve the argument registers in the scratch space across the helper call. Note that we depend on these
        ;; registers (which may contain GC references) being spilled before we build the PInvokeTransitionFrame below
        ;; due to the way we build a stack range to report to the GC conservatively during a collection.
        save_reg_postrsp    rcx, (RIDS_ReservedStack + 8*1)
        save_reg_postrsp    rdx, (RIDS_ReservedStack + 8*2)
        save_reg_postrsp    r8,  (RIDS_ReservedStack + 8*3)
        save_reg_postrsp    r9,  (RIDS_ReservedStack + 8*4)
        save_xmm128_postrsp xmm0, (20h + 60h + 16*0)
        save_xmm128_postrsp xmm1, (20h + 60h + 16*1)
        save_xmm128_postrsp xmm2, (20h + 60h + 16*2)
        save_xmm128_postrsp xmm3, (20h + 60h + 16*3)
        END_PROLOGUE
        
        ;; Build PInvokeTransitionFrame. This is only required if we end up resolving the interface method via
        ;; a callout to a managed ICastable method. In that instance we need to be able to cope with garbage
        ;; collections which in turn need to be able to walk the stack from the ICastable method, skip the
        ;; unmanaged runtime portions and resume walking at our caller. This frame provides both the means to
        ;; unwind to that caller and a place to spill callee saved registers in case they contain GC
        ;; references from the caller.

        ;; Save caller's rip.
        mov     rax, [rsp + RIDS_ReservedStack]
        mov     [rsp + 20h + 8*0], rax

        ;; Save caller's rbp.
        mov     [rsp + 20h + 8*1], rbp

        ;; Zero out the Thread*, it's not used by the stackwalker.
        xor     rax, rax
        mov     [rsp + 20h + 8*2], rax

        ;; Set the flags.
        mov     dword ptr [rsp + 20h + 8*3], PTFF_SAVE_ALL_PRESERVED + PTFF_SAVE_RSP

        ;; Save callee saved registers.
        mov     [rsp + 20h + 8*4], rbx
        mov     [rsp + 20h + 8*5], rsi
        mov     [rsp + 20h + 8*6], rdi
        mov     [rsp + 20h + 8*7], r12
        mov     [rsp + 20h + 8*8], r13
        mov     [rsp + 20h + 8*9], r14
        mov     [rsp + 20h + 8*10], r15

        ;; Calculate and store the caller's rsp.
        lea     rax, [rsp + RIDS_ReservedStack + 8]
        mov     [rsp + 20h + 8*11], rax

        ;; First argument is the instance we're dispatching on which is already in rcx.

        ;; Second argument is the dispatch data cell. We still have this in r10
        mov     rdx, r10

        ;; The third argument is the address of the transition frame we build above.
        lea     r8, [rsp + 20h]

        call    RhpResolveInterfaceMethodCacheMiss

        ;; Recover callee-saved values from the transition frame in case a GC updated them.
        mov     rbx, [rsp + 20h + 8*4]
        mov     rsi, [rsp + 20h + 8*5]
        mov     rdi, [rsp + 20h + 8*6]
        mov     r12, [rsp + 20h + 8*7]
        mov     r13, [rsp + 20h + 8*8]
        mov     r14, [rsp + 20h + 8*9]
        mov     r15, [rsp + 20h + 8*10]

        ;; Restore the argument registers.
        movdqa  xmm0, [rsp + 20h + 60h + 16*0]
        movdqa  xmm1, [rsp + 20h + 60h + 16*1]
        movdqa  xmm2, [rsp + 20h + 60h + 16*2]
        movdqa  xmm3, [rsp + 20h + 60h + 16*3]
        mov     r9,  [rsp + RIDS_ReservedStack + 8*4]
        mov     r8,  [rsp + RIDS_ReservedStack + 8*3]
        mov     rdx, [rsp + RIDS_ReservedStack + 8*2]
        mov     rcx, [rsp + RIDS_ReservedStack + 8*1]

        add     rsp, RIDS_ReservedStack
        TAILJMP_RAX
NESTED_END RhpInterfaceDispatchSlow, _TEXT




endif ;; FEATURE_CACHED_INTERFACE_DISPATCH

end
