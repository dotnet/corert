;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

;;
;; Unmanaged helpers used by the managed System.GC class.
;;

    .586
    .model  flat
    option  casemap:none
    .code

include AsmMacros.inc

;; DWORD getcpuid(DWORD arg, unsigned char result[16])

FASTCALL_FUNC getcpuid, 8

        push    ebx
        push    esi
        mov     esi, edx
        mov     eax, ecx
        xor     ecx, ecx
        cpuid
        mov     [esi+ 0], eax
        mov     [esi+ 4], ebx
        mov     [esi+ 8], ecx
        mov     [esi+12], edx
        pop     esi
        pop     ebx

        ret

FASTCALL_ENDFUNC

;; The following function uses Deterministic Cache Parameter leafs to crack the cache hierarchy information on Prescott & Above platforms. 
;;  This function takes 3 arguments:
;;     Arg1 is an input to ECX. Used as index to specify which cache level to return infoformation on by CPUID.
;;     Arg2 is an input to EAX. For deterministic code enumeration, we pass in 4H in arg2.
;;     Arg3 is a pointer to the return buffer
;;   No need to check whether or not CPUID is supported because we have already called CPUID with success to come here.

;; DWORD getextcpuid(DWORD arg1, DWORD arg2, unsigned char result[16])

FASTCALL_FUNC getextcpuid, 12

        push    ebx
        push    esi
        mov     ecx, ecx
        mov     eax, edx
        cpuid
        mov     esi, [esp + 12]
        mov     [esi+ 0], eax
        mov     [esi+ 4], ebx
        mov     [esi+ 8], ecx
        mov     [esi+12], edx
        pop     esi
        pop     ebx

        ret

FASTCALL_ENDFUNC

        end
