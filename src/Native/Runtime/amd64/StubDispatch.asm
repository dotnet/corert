;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

include AsmMacros.inc


ifdef FEATURE_CACHED_INTERFACE_DISPATCH


EXTERN RhpCidResolve : PROC
EXTERN RhpUniversalTransition_DebugStepTailCall : PROC
EXTERN RhpCastableObjectResolve : PROC

EXTERN  t_TLS_DispatchCell:QWORD
EXTERN  _tls_index:DWORD

LEAF_ENTRY RhpCastableObjectDispatch_CommonStub, _TEXT
        ;; There are arbitrary callers passing arguments with arbitrary signatures.
        ;; Custom calling convention:
        ;;      r10: pointer to the current thunk's data block (data contains 2 pointer values: context + target pointers)

        mov     [rsp + 8], rcx                                     ;; Save rcx in a home scratch location. Pushing the 
                                                                   ;; register on the stack will break callstack unwind
        mov     ecx, [_tls_index]
        mov     r11, gs:[_tls_array]
        mov     r11, [r11 + rcx * 8]
        mov     eax, SECTIONREL t_TLS_DispatchCell
        lea     rax, [r11 + rax]

        mov     rcx, [rsp + 8]                                     ;; Restore rcx

        ;; store dispatch cell address in thread static
        mov     r11, [r10]
        mov     [rax], r11

        ;; jump to the target
        mov     rax, [r10 + 8]
        TAILJMP_RAX
LEAF_END RhpCastableObjectDispatch_CommonStub, _TEXT

LEAF_ENTRY RhpTailCallTLSDispatchCell, _TEXT
        ;; Load the dispatch cell out of the TLS variable
        mov rax, gs:[_tls_array]
        mov r10d, _tls_index
        mov r10, [rax + r10 * 8]
        mov eax, SECTIONREL t_TLS_DispatchCell
        mov r10, [r10+rax]

        ;; Load the target of the dispatch cell into rax
        mov rax, [r10]
        ;; And tail call to it
        TAILJMP_RAX
LEAF_END RhpTailCallTLSDispatchCell, _TEXT

LEAF_ENTRY RhpCastableObjectDispatchHelper_TailCalled, _TEXT
        ;; Load the dispatch cell out of the TLS variable
        mov rax, gs:[_tls_array]
        mov r10d, _tls_index
        mov r10, [rax + r10 * 8]
        mov eax, SECTIONREL t_TLS_DispatchCell
        mov r10, [r10+rax]
        jmp RhpCastableObjectDispatchHelper
LEAF_END RhpCastableObjectDispatchHelper_TailCalled, _TEXT

LEAF_ENTRY RhpCastableObjectDispatchHelper, _TEXT
        ;; TODO! Implement fast lookup helper to avoid the universal transition each time we
        ;; hit a CastableObject

        ;; If the initial lookup fails, call into managed under the universal thunk
        ;; to run the full lookup routine

        ;; r10 contains indirection cell address, move to r11 where it will be passed by
        ;; the universal transition thunk as an argument to RhpCastableObjectResolve
        mov r11, r10
        lea r10, RhpCastableObjectResolve
        jmp RhpUniversalTransition_DebugStepTailCall
LEAF_END RhpCastableObjectDispatchHelper, _TEXT


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

;; Stub dispatch routine for dispatch to a vtable slot
LEAF_ENTRY RhpVTableOffsetDispatch, _TEXT
        ;; r10 currently contains the indirection cell address. 
        ;; load rax to point to the vtable offset (which is stored in the m_pCache field).
        mov     rax, [r10 + OFFSETOF__InterfaceDispatchCell__m_pCache]

        ;; Load the EEType from the object instance in rcx, and add it to the vtable offset
        ;; to get the address in the vtable of what we want to dereference
        add     rax, [rcx]

        ;; Load the target address of the vtable into rax
        mov     rax, [rax]

        TAILJMP_RAX
LEAF_END RhpVTableOffsetDispatch, _TEXT


;; Initial dispatch on an interface when we don't have a cache yet.
LEAF_ENTRY RhpInitialInterfaceDispatch, _TEXT
ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch

        ;; Just tail call to the cache miss helper.
        jmp RhpInterfaceDispatchSlow

LEAF_END RhpInitialInterfaceDispatch, _TEXT

;; Cache miss case, call the runtime to resolve the target and update the cache.
;; Use universal transition helper to allow an exception to flow out of resolution
LEAF_ENTRY RhpInterfaceDispatchSlow, _TEXT
        ;; r10 contains indirection cell address, move to r11 where it will be passed by
        ;; the universal transition thunk as an argument to RhpCidResolve
        mov r11, r10
        lea r10, RhpCidResolve
        jmp RhpUniversalTransition_DebugStepTailCall

LEAF_END RhpInterfaceDispatchSlow, _TEXT


endif ;; FEATURE_CACHED_INTERFACE_DISPATCH

end
