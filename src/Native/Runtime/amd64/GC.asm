;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

include AsmMacros.inc

;; extern "C" DWORD getcpuid(DWORD arg, unsigned char result[16]);
NESTED_ENTRY getcpuid, _TEXT

        push_nonvol_reg    rbx
        push_nonvol_reg    rsi
    END_PROLOGUE

        mov     eax, ecx                ; first arg
        mov     rsi, rdx                ; second arg (result)
        xor     ecx, ecx                ; clear ecx - needed for "Structured Extended Feature Flags"
        cpuid
        mov     [rsi+ 0], eax
        mov     [rsi+ 4], ebx
        mov     [rsi+ 8], ecx
        mov     [rsi+12], edx
        pop     rsi
        pop     rbx
        ret
NESTED_END getcpuid, _TEXT

;The following function uses Deterministic Cache Parameter leafs to crack the cache hierarchy information on Prescott & Above platforms. 
;  This function takes 3 arguments:
;     Arg1 is an input to ECX. Used as index to specify which cache level to return information on by CPUID.
;         Arg1 is already passed in ECX on call to getextcpuid, so no explicit assignment is required;  
;     Arg2 is an input to EAX. For deterministic code enumeration, we pass in 4H in arg2.
;     Arg3 is a pointer to the return dwbuffer
NESTED_ENTRY getextcpuid, _TEXT
        push_nonvol_reg    rbx
        push_nonvol_reg    rsi
    END_PROLOGUE
        
        mov     eax, edx                ; second arg (input to  EAX)
        mov     rsi, r8                 ; third arg  (pointer to return dwbuffer)       
        cpuid
        mov     [rsi+ 0], eax
        mov     [rsi+ 4], ebx
        mov     [rsi+ 8], ecx
        mov     [rsi+12], edx
        pop     rsi
        pop     rbx

        ret
NESTED_END getextcpuid, _TEXT

;; extern "C" DWORD __stdcall xmmYmmStateSupport();
LEAF_ENTRY xmmYmmStateSupport, _TEXT
        mov     ecx, 0                  ; Specify xcr0
        xgetbv                          ; result in EDX:EAX
        and eax, 06H
        cmp eax, 06H                    ; check OS has enabled both XMM and YMM state support
        jne     not_supported
        mov     eax, 1
        jmp     done
    not_supported:
        mov     eax, 0
    done:
        ret
LEAF_END xmmYmmStateSupport, _TEXT

        end
