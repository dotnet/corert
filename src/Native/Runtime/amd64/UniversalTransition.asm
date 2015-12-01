;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

include AsmMacros.inc

ifdef FEATURE_DYNAMIC_CODE

;;
;; Defines an assembly thunk used to make a transition from managed code to a callee,
;; then (based on the return value from the callee), either returning or jumping to
;; a new location while preserving the input arguments.  The usage of this thunk also
;; ensures arguments passed are properly reported.
;;
;; TODO: This code currently only tailcalls, and does not return.
;;
;; Inputs:
;;      rcx, rdx, r8, r9, stack space: arguments as normal
;;      r10: The location of the target code the UniversalTransition thunk will call
;;      r11: The only parameter to the target function (passed in rdx to callee)
;;

SIZEOF_OUT_REG_HOMES     equ 20h    ; Callee register spill
SIZEOF_FP_REGS           equ 40h    ; xmm0-3
SIZEOF_PINVOKE_FRAME     equ 80h    ; for default arg push
SIZEOF_SCRATCH_SPACE    equ 10h    ; for 16 bytes of conservatively reported scratch space
OFFSETOF_FP_ARG_SPILL    equ SIZEOF_PINVOKE_FRAME + 10h
OFFSETOF_SCRATCH_SPACE  equ OFFSETOF_FP_ARG_SPILL + SIZEOF_FP_REGS

ALLOC_SIZE               equ SIZEOF_FP_REGS + SIZEOF_SCRATCH_SPACE + 10h
SIZEOF_STACK_FRAME       equ SIZEOF_PINVOKE_FRAME + ALLOC_SIZE + 10h


; [callee return]
; [out rcx]
; [out rdx]
; [out r8]
; [out r9]
; [pinvoke frame, 60h]
; [XMM regs, 40h]
; [ConservativelyReportedScratchSpace 10h] (+0xc0)
; [ptr to pinvoke frame 8h] (+0xd0)
; [caller return addr] (+0xd8)
; [in rcx]
; [in rdx]
; [in r8]
; [in r9]


NESTED_ENTRY RhpUniversalTransition, _TEXT        
        mov                     [rsp+8h], r10   ; Temporarily save r10 as it's actually a parameter

        alloc_stack ALLOC_SIZE
        
        ; @TODO We are getting the thread here to avoid bifurcating PUSH_COOP_PINVOKE_FRAME, but we
        ; really don't need the frame stored in the "hack pinvoke tunnel" because this codepath
        ; doesn't use Enable/DisablePreemtiveGC

        INLINE_GETTHREAD        rax, r10        ; rax <- Thread pointer, r10 <- trashed
        PUSH_COOP_PINVOKE_FRAME rax, r10, ALLOC_SIZE

        mov                 rax, [rsp + SIZEOF_STACK_FRAME]  ; restore r10 from input into rax
        save_reg_postrsp    rcx,   0h + SIZEOF_STACK_FRAME
        save_reg_postrsp    rdx,   8h + SIZEOF_STACK_FRAME
        save_reg_postrsp    r8,   10h + SIZEOF_STACK_FRAME
        save_reg_postrsp    r9,   18h + SIZEOF_STACK_FRAME

        save_xmm128_postrsp xmm0, OFFSETOF_FP_ARG_SPILL
        save_xmm128_postrsp xmm1, OFFSETOF_FP_ARG_SPILL + 10h
        save_xmm128_postrsp xmm2, OFFSETOF_FP_ARG_SPILL + 20h
        save_xmm128_postrsp xmm3, OFFSETOF_FP_ARG_SPILL + 30h
        
        END_PROLOGUE
        
        ; Set rbp to point after our PInvokeTransitionFrame pointer, then store the pointer to this frame
        ; See StackFrameIterator::HandleManagedCalloutThunk.
        lea rbp, [rsp+SIZEOF_PINVOKE_FRAME + ALLOC_SIZE + 8h]
        mov     [rbp + MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET], r10
        
        ;
        ; Call out to the target, while storing and reporting arguments to the GC.
        ;
        mov  rdx, r11
        lea  rcx, [rsp + OFFSETOF_SCRATCH_SPACE] 
        call rax
LABELED_RETURN_ADDRESS ReturnFromUniversalTransition

        ; restore fp argument registers
        movdqa          xmm0, [rsp + OFFSETOF_FP_ARG_SPILL      ]
        movdqa          xmm1, [rsp + OFFSETOF_FP_ARG_SPILL + 10h]
        movdqa          xmm2, [rsp + OFFSETOF_FP_ARG_SPILL + 20h]
        movdqa          xmm3, [rsp + OFFSETOF_FP_ARG_SPILL + 30h]

        ; restore integer argument registers
        mov             rcx, [rsp +  0h + SIZEOF_STACK_FRAME]
        mov             rdx, [rsp +  8h + SIZEOF_STACK_FRAME]
        mov             r8,  [rsp + 10h + SIZEOF_STACK_FRAME]
        mov             r9,  [rsp + 18h + SIZEOF_STACK_FRAME]
        
        ; epilog
        nop
        
        POP_COOP_PINVOKE_FRAME ALLOC_SIZE
        add             rsp, ALLOC_SIZE

        TAILJMP_RAX

NESTED_END RhpUniversalTransition, _TEXT

endif

end
