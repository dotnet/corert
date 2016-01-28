;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

        .586
        .model  flat
        option  casemap:none
        .code


include AsmMacros.inc

ifdef FEATURE_VSD

RESOLVE_WORKER_STATIC   equ ?ResolveWorkerStatic@VirtualCallStubManager@@CGPAEPAEPAVEEType@@PAPBE@Z
BACKPATCH_WORKER_STATIC equ ?BackPatchWorkerStatic@VirtualCallStubManager@@CGXPBEPAPBE@Z

;; VirtualStubDispatch
EXTERN RESOLVE_WORKER_STATIC : PROC
EXTERN BACKPATCH_WORKER_STATIC : PROC

;; This is the initial and failure entrypoint for VSD. All interface call sites
;; start off pointing here, and all failed mono- and poly-morphic target lookups
;; end up here. The purpose is to save the right registers and pass control to
;; VirtualCallStubManager to find the correct target, change the indirection cell
;; (if necessary) and populate the poly-morphic cache (if necessary).
FASTCALL_FUNC VSDResolveWorkerAsmStub, 0
        ;; ebp frame
        push    ebp
        mov     ebp,esp

        ;; save registers (VirtualCallStubManager::ResolveWorkerStatic is __stdcall)
        push ecx
        push edx

        ;; ARG 3
        ;; eax containing the site addr (in the case of an indirect call
        ;; to the VSD stub, which is used for shared generic code
        push eax

        ;; ARG 2
        ;; load the eetype from the object instance in ecx
        push    [ecx]

        ;; ARG 1
        ;; store return address as first argument
        ;; figure out the return address
        push    [ebp+4]

        call   RESOLVE_WORKER_STATIC

        ;; restore registers
        pop     edx
        pop     ecx

        ;; ebp frame
        mov     esp,ebp
        pop     ebp

        jmp     eax
FASTCALL_ENDFUNC

;; Call the callsite back patcher.  The fail stub piece of the resolver is being
;; call too often, i.e. dispatch stubs are failing the expect MT test too often.
;; In this stub wraps the call to the BackPatchWorker to take care of any stack magic
;; needed.
FASTCALL_FUNC VSDBackPatchWorkerAsmStub, 0
        ;; ebp frame
        push ebp
        mov  ebp,esp

        ;; save registers (VirtualCallStubManager::BackPatchWorkerStatic is __stdcall)
        push ecx
        push edx

        ;; ARG 2
        ;; eax containing the site addr (in the case of an indirect call)
        ;; to the VSD stub, which is used for shared generic code
        push eax

        ;; ARG 1
        ;; store return address as first argument
        ;; figure out the return address. It is at +8 because the
        ;; entry at the top of the stack is the return to the resolve stub, and
        ;; the entry after that is the return address of the caller.
        push    [ebp+8]

        call BACKPATCH_WORKER_STATIC

        ;; restore registers
        pop  edx
        pop  ecx

        ;; restore ebp frame
        mov  esp,ebp
        pop  ebp

        ret
FASTCALL_ENDFUNC

endif ;; FEATURE_VSD

ifdef FEATURE_CACHED_INTERFACE_DISPATCH

EXTERN @RhpResolveInterfaceMethodCacheMiss@12 : PROC


;; Macro that generates code to check a single cache entry.
CHECK_CACHE_ENTRY macro entry
NextLabel textequ @CatStr( Attempt, %entry+1 )
        cmp     ebx, [eax + (OFFSETOF__InterfaceDispatchCache__m_rgEntries + (entry * 8))]
        jne     @F
        pop     ebx
        jmp     dword ptr [eax + (OFFSETOF__InterfaceDispatchCache__m_rgEntries + (entry * 8) + 4)]
@@:
endm


;; Macro that generates a stub consuming a cache with the given number of entries.
DEFINE_INTERFACE_DISPATCH_STUB macro entries

StubName textequ @CatStr( _RhpInterfaceDispatch, entries )

    StubName proc public

        ;; Check the instance here to catch null references. We're going to touch it again below (to cache
        ;; the EEType pointer), but that's after we've pushed ebx below, and taking an A/V there will
        ;; mess up the stack trace for debugging. We also don't have a spare scratch register (eax holds
        ;; the cache pointer and the push of ebx below is precisely so we can access a second register
        ;; to hold the EEType pointer).
        test    ecx, ecx
        je      RhpInterfaceDispatchNullReference

        ;; eax currently contains the indirection cell address. We need to update it to point to the cache
        ;; block instead.
        mov     eax, [eax + OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; Cache pointer is already loaded in the only scratch register we have so far, eax. We need
        ;; another scratch register to hold the instance type so save the value of ebx and use that.
        push    ebx

        ;; Load the EEType from the object instance in ebx.
        mov     ebx, [ecx]

CurrentEntry = 0
    while CurrentEntry lt entries
        CHECK_CACHE_ENTRY %CurrentEntry
CurrentEntry = CurrentEntry + 1
    endm

        ;; eax currently contains the cache block. We need to point it back to the 
        ;; indirection cell using the back pointer in the cache block
        mov     eax, [eax + OFFSETOF__InterfaceDispatchCache__m_pCell]
        jmp     InterfaceDispatchCacheMiss

    StubName endp

    endm ;; DEFINE_INTERFACE_DISPATCH_STUB


;; Define all the stub routines we currently need.
DEFINE_INTERFACE_DISPATCH_STUB 1
DEFINE_INTERFACE_DISPATCH_STUB 2
DEFINE_INTERFACE_DISPATCH_STUB 4
DEFINE_INTERFACE_DISPATCH_STUB 8
DEFINE_INTERFACE_DISPATCH_STUB 16
DEFINE_INTERFACE_DISPATCH_STUB 32
DEFINE_INTERFACE_DISPATCH_STUB 64

;; Shared out of line helper used on cache misses.
    InterfaceDispatchCacheMiss proc

        ;; Push an ebp frame since it makes some of our later calculations easier.
        push    ebp
        mov     ebp, esp

        ;; Save argument registers while we call out to the C++ helper. Note that we depend on these registers
        ;; (which may contain GC references) being spilled before we build the PInvokeTransitionFrame below
        ;; due to the way we build a stack range to report to the GC conservatively during a collection.
        push    ecx
        push    edx

        ;; Build PInvokeTransitionFrame. This is only required if we end up resolving the interface method via
        ;; a callout to a managed ICastable method. In that instance we need to be able to cope with garbage
        ;; collections which in turn need to be able to walk the stack from the ICastable method, skip the
        ;; unmanaged runtime portions and resume walking at our caller. This frame provides both the means to
        ;; unwind to that caller and a place to spill callee saved registers in case they contain GC
        ;; references from the caller.

        ;; Calculate caller's esp: relative to ebp's current value we've pushed the old ebp, ebx and a return
        ;; address.
        lea     edx, [ebp + (3 * 4)]
        push    edx

        ;; Push callee saved registers. Note we've already pushed ebx but we need to do it here again so that
        ;; it is reported to the GC correctly if necessary. As such it's necessary to pushed the saved version
        ;; of ebx and make sure when we restore it we use this copy and discard the version that was initially
        ;; pushed (since its value may now be stale).
        push    edi
        push    esi
        mov     edx, [ebp + 04h]    ; Old RBX value
        push    edx

        ;; Push flags.
        push    PTFF_SAVE_ALL_PRESERVED + PTFF_SAVE_RSP

        ;; Leave space for the Thread* (stackwalker does not use this).
        push    0

        ;; The caller's ebp.
        push    [ebp]

        ;; The caller's eip.
        push    [ebp + 08h]

        ;; First argument is the instance we're dispatching on which is already in ecx.

        ;; Second argument is the dispatch data cell. 
        ;; We still have this in eax
        mov     edx, eax

        ;; The third argument is the address of the transition frame we build above. Currently it's at the top
        ;; of the stack so esp points to it.
        push    esp

        call    @RhpResolveInterfaceMethodCacheMiss@12

        ;; Recover callee-saved values from the transition frame in case a GC updated them.
        mov     ebx, [esp + 010h]
        mov     esi, [esp + 014h]
        mov     edi, [esp + 018h]

        ;; Restore real argument registers.
        mov     edx, [ebp - 08h]
        mov     ecx, [ebp - 04h]

        ;; Remove the transition and ebp frames from the stack.
        mov     esp, ebp
        pop     ebp

        ;; Discard the space where ebx was pushed on entry, its value is now potentially stale.
        add     esp, 4

        ;; Final target address is in eax.
        jmp     eax

    InterfaceDispatchCacheMiss endp

;; Out of line helper used when we try to interface dispatch on a null pointer. Sets up the stack so the
;; debugger gives a reasonable stack trace.
RhpInterfaceDispatchNullReference proc public
        push    ebp
        mov     ebp, esp
        mov     ebx, [ecx]  ;; This should A/V
        int     3
RhpInterfaceDispatchNullReference endp


;; Initial dispatch on an interface when we don't have a cache yet.
    RhpInitialInterfaceDispatch proc public
    ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch

        ;; Mainly we just tail call to the cache miss helper. But this helper expects that ebx has been pushed
        ;; on the stack.
        push    ebx

        jmp InterfaceDispatchCacheMiss

    RhpInitialInterfaceDispatch endp


endif ;; FEATURE_CACHED_INTERFACE_DISPATCH

end
