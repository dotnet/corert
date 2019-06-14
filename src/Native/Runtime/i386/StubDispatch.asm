;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

        .586
        .model  flat
        option  casemap:none
        .code


include AsmMacros.inc


ifdef FEATURE_CACHED_INTERFACE_DISPATCH

EXTERN RhpCidResolve : PROC
EXTERN _RhpUniversalTransition_DebugStepTailCall@0 : PROC
EXTERN RhpCastableObjectResolve : PROC

EXTERN  _t_TLS_DispatchCell:DWORD
EXTERN  __tls_index:DWORD

GET_TLS_DISPATCH_CELL macro
        ASSUME fs : NOTHING
        mov     eax, [__tls_index]
        add     eax, eax
        add     eax, eax
        add     eax, fs:[__tls_array]
        mov     eax, [eax]
        mov     eax, [eax + SECTIONREL _t_TLS_DispatchCell]
endm

SET_TLS_DISPATCH_CELL macro
        ;; ecx: value to assign to the TLS variable
        ASSUME fs : NOTHING
        push    eax
        mov     eax, [__tls_index]
        add     eax, eax
        add     eax, eax
        add     eax, fs:[__tls_array]
        mov     eax, [eax]
        lea     eax, [eax + SECTIONREL _t_TLS_DispatchCell]
        mov     [eax],ecx
        pop     eax
endm

_RhpCastableObjectDispatch_CommonStub proc public
        ;; There are arbitrary callers passing arguments with arbitrary signatures.
        ;; Custom calling convention:
        ;;      eax: pointer to the current thunk's data block (data contains 2 pointer values: context + target pointers)

        ;; make some scratch regs
        push    ecx

        ;; store dispatch cell address into the TLS variable
        mov     ecx, [eax]
        SET_TLS_DISPATCH_CELL
        
        ;; restore the regs we used
        pop     ecx

        ;; jump to the target
        mov     eax, [eax + 4]                                  ;;   eax <- target slot data
        jmp     eax
_RhpCastableObjectDispatch_CommonStub endp

_RhpTailCallTLSDispatchCell proc public
        ;; Load the dispatch cell out of the TLS variable
        GET_TLS_DISPATCH_CELL

        ;; Tail call to the target of the dispatch cell
        jmp     dword ptr [eax]
_RhpTailCallTLSDispatchCell endp

_RhpCastableObjectDispatchHelper_TailCalled proc public
        push    ebp
        mov     ebp, esp
        ;; Load the dispatch cell out of the TLS variable
        GET_TLS_DISPATCH_CELL
        jmp     _RhpCastableObjectDispatchHelper2
_RhpCastableObjectDispatchHelper_TailCalled endp

_RhpCastableObjectDispatchHelper proc public
        push    ebp
        mov     ebp, esp
        ;; TODO! Implement fast lookup helper to avoid the universal transition each time we
        ;; hit a CastableObject

        ;; If the initial lookup fails, call into managed under the universal thunk
        ;; to run the full lookup routine

        ;; EAX currently contains the cache block. We need to point it to the 
        ;; indirection cell using the back pointer in the cache block
        mov     eax, [eax + OFFSETOF__InterfaceDispatchCache__m_pCell]

ALTERNATE_ENTRY RhpCastableObjectDispatchHelper2       
        ;; indirection cell address is in EAX, it will be passed by
        ;; the universal transition thunk as an argument to RhpCastableObjectResolve
        push    eax
        lea     eax, RhpCastableObjectResolve
        push    eax
        jmp     _RhpUniversalTransition_DebugStepTailCall@0
_RhpCastableObjectDispatchHelper endp


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
        pop     ebx
        jmp     RhpInterfaceDispatchSlow

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
RhpInterfaceDispatchSlow proc
;; eax points at InterfaceDispatchCell

        ;; Setup call to Universal Transition thunk
        push        ebp
        mov         ebp, esp
        push        eax   ; First argument (Interface Dispatch Cell)
        lea         eax, [RhpCidResolve]
        push        eax ; Second argument (RhpCidResolve)

        ;; Jump to Universal Transition
        jmp         _RhpUniversalTransition_DebugStepTailCall@0
RhpInterfaceDispatchSlow endp

;; Out of line helper used when we try to interface dispatch on a null pointer. Sets up the stack so the
;; debugger gives a reasonable stack trace.
RhpInterfaceDispatchNullReference proc public
        push    ebp
        mov     ebp, esp
        mov     ebx, [ecx]  ;; This should A/V
        int     3
RhpInterfaceDispatchNullReference endp

;; Stub dispatch routine for dispatch to a vtable slot
_RhpVTableOffsetDispatch proc public
        ;; eax currently contains the indirection cell address. We need to update it to point to the vtable offset (which is in the m_pCache field)
        mov     eax, [eax + OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; add the vtable offset to the EEType pointer 
        add     eax, [ecx]

        ;; Load the target address of the vtable into eax
        mov     eax, [eax]

        ;; tail-jump to the target
        jmp     eax
_RhpVTableOffsetDispatch endp


;; Initial dispatch on an interface when we don't have a cache yet.
_RhpInitialInterfaceDispatch proc public
    ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch

        jmp RhpInterfaceDispatchSlow

_RhpInitialInterfaceDispatch endp


endif ;; FEATURE_CACHED_INTERFACE_DISPATCH

end
