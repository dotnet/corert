;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

include AsmMacros.inc

ifdef FEATURE_DYNAMIC_CODE

ifdef _DEBUG
TRASH_SAVED_ARGUMENT_REGISTERS equ 1
else
TRASH_SAVED_ARGUMENT_REGISTERS equ 0
endif

if TRASH_SAVED_ARGUMENT_REGISTERS ne 0
EXTERN RhpIntegerTrashValues    : QWORD
EXTERN RhpFpTrashValues         : QWORD
endif ;; TRASH_SAVED_ARGUMENT_REGISTERS

SIZEOF_RETADDR                  equ 8h

SIZEOF_PINVOKE_FRAME_PTR        equ 8h

SIZEOF_RETURN_BLOCK             equ 10h    ; for 16 bytes of conservatively reported space that the callee can
                                           ; use to manage the return value that the call eventually generates

SIZEOF_FP_REGS                  equ 40h    ; xmm0-3

SIZEOF_PINVOKE_FRAME            equ 60h

SIZEOF_OUT_REG_HOMES            equ 20h    ; Callee register spill

;
; From CallerSP to ChildSP, the stack frame is composed of the following six adjacent
; regions:
;
;       SIZEOF_RETADDR
;       SIZEOF_PINVOKE_FRAME_PTR
;       SIZEOF_RETURN_BLOCK
;       SIZEOF_FP_REGS
;       SIZEOF_PINVOKE_FRAME
;       SIZEOF_OUT_REG_HOMES
; 

DISTANCE_FROM_FP_REGS_TO_CALLERSP               equ SIZEOF_FP_REGS + SIZEOF_RETURN_BLOCK + SIZEOF_PINVOKE_FRAME_PTR + SIZEOF_RETADDR

DISTANCE_FROM_CHILDSP_TO_FP_REGS                equ SIZEOF_OUT_REG_HOMES + SIZEOF_PINVOKE_FRAME

DISTANCE_FROM_CHILDSP_TO_RETURN_BLOCK           equ DISTANCE_FROM_CHILDSP_TO_FP_REGS + SIZEOF_FP_REGS

DISTANCE_FROM_CHILDSP_TO_CALLERSP               equ DISTANCE_FROM_CHILDSP_TO_FP_REGS + DISTANCE_FROM_FP_REGS_TO_CALLERSP

; RBP is required to point one slot above the PInvoke frame pointer and therefore points
; to the caller return address.
DISTANCE_FROM_CHILDSP_TO_RBP                    equ DISTANCE_FROM_CHILDSP_TO_CALLERSP - SIZEOF_RETADDR

; Note that the PInvoke frame lies directly below the FP regs area.
DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_CALLERSP  equ DISTANCE_FROM_FP_REGS_TO_CALLERSP
DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_RETADDR   equ DISTANCE_FROM_FP_REGS_TO_CALLERSP - SIZEOF_RETADDR

;
; Note: The distance from the top of the PInvoke frame to the CallerSP must be a multiple
; of 16.  If not, PUSH_COOP_PINVOKE_FRAME will inject 8 bytes of padding (in order to
; ensure a 16-byte aligned ChildSP) and will therefore break the expected stack layout.
;

.errnz DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_CALLERSP mod 16

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

;
; Stack frame layout (from lower addresses to higher addresses):
;
; [callee return]                                   ChildSP-008     CallerSP-0e8
; [out rcx]                                         ChildSP+000     CallerSP-0e0
; [out rdx]                                         ChildSP+008     CallerSP-0d8
; [out r8]                                          ChildSP+010     CallerSP-0d0
; [out r9]                                          ChildSP+018     CallerSP-0c8
; [pinvoke frame, 60h]                              ChildSP+020     CallerSP-0c0
; [XMM regs (argument regs from the caller), 40h]   ChildSP+080     CallerSP-060
; [ConservativelyReportedReturnBlock 10h]           ChildSP+0c0     CallerSP-020
; [ptr to pinvoke frame 8h]                         ChildSP+0d0     CallerSP-010
; [caller return addr]                              ChildSP+0d8     CallerSP-008
; [in rcx (argument reg from the caller)]           ChildSP+0e0     CallerSP+000
; [in rdx (argument reg from the caller)]           ChildSP+0e8     CallerSP+008
; [in r8 (argument reg from the caller)]            ChildSP+0f0     CallerSP+010
; [in r9 (argument reg from the caller)]            ChildSP+0f8     CallerSP+018
; [stack-passed arguments from the caller]          ChildSP+100     CallerSP+020
;
; Note: The callee receives a pointer to the base of the conservatively reported return
; block, and the callee has knowledge of the exact layout of all pieces of the frame
; that lie at or above the pushed XMM registers.
;

NESTED_ENTRY RhpUniversalTransition, _TEXT        

        alloc_stack DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_RETADDR
        
        ; Build the frame that the stack walker will use to unwind through this function.  The
        ; <NoModeSwitch> flag indicates that this function never uses Enable/DisablePreemptiveGC,
        ; implying that frame address does not need to be recorded in the current thread object.
        ; This macro trashes rax but does not trash any other registers.

        PUSH_COOP_PINVOKE_FRAME notUsed, rax, DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_RETADDR, <NoModeSwitch>

        ; Note that rax now holds the address of the newly allocated frame.  Also note that, in
        ; addition to allocating the PInvoke frame, the macro also allocated the outgoing
        ; arguments area.

        save_reg_postrsp    rcx,   0h + DISTANCE_FROM_CHILDSP_TO_CALLERSP
        save_reg_postrsp    rdx,   8h + DISTANCE_FROM_CHILDSP_TO_CALLERSP
        save_reg_postrsp    r8,   10h + DISTANCE_FROM_CHILDSP_TO_CALLERSP
        save_reg_postrsp    r9,   18h + DISTANCE_FROM_CHILDSP_TO_CALLERSP

        save_xmm128_postrsp xmm0, DISTANCE_FROM_CHILDSP_TO_FP_REGS
        save_xmm128_postrsp xmm1, DISTANCE_FROM_CHILDSP_TO_FP_REGS + 10h
        save_xmm128_postrsp xmm2, DISTANCE_FROM_CHILDSP_TO_FP_REGS + 20h
        save_xmm128_postrsp xmm3, DISTANCE_FROM_CHILDSP_TO_FP_REGS + 30h
        
        END_PROLOGUE

        ; Set rbp to point after our PInvokeTransitionFrame pointer, then store the pointer to this frame
        ; See StackFrameIterator::HandleManagedCalloutThunk.
        lea     rbp, [rsp + DISTANCE_FROM_CHILDSP_TO_RBP]
        mov     [rbp + MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET], rax

if TRASH_SAVED_ARGUMENT_REGISTERS ne 0

        ; Before calling out, trash all of the argument registers except the ones (rcx, rdx) that
        ; hold outgoing arguments.  All of these registers have been saved to the transition
        ; frame, and the code at the call target is required to use only the transition frame
        ; copies when dispatching this call to the eventual callee.

        movsd           xmm0, mmword ptr [RhpFpTrashValues + 0h]
        movsd           xmm1, mmword ptr [RhpFpTrashValues + 8h]
        movsd           xmm2, mmword ptr [RhpFpTrashValues + 10h]
        movsd           xmm3, mmword ptr [RhpFpTrashValues + 18h]

        mov             r8, qword ptr [RhpIntegerTrashValues + 10h]
        mov             r9, qword ptr [RhpIntegerTrashValues + 18h]

endif ; TRASH_SAVED_ARGUMENT_REGISTERS

        ;
        ; Call out to the target, while storing and reporting arguments to the GC.
        ;
        mov  rdx, r11
        lea  rcx, [rsp + DISTANCE_FROM_CHILDSP_TO_RETURN_BLOCK]
        call r10
LABELED_RETURN_ADDRESS ReturnFromUniversalTransition

        ; restore fp argument registers
        movdqa          xmm0, [rsp + DISTANCE_FROM_CHILDSP_TO_FP_REGS      ]
        movdqa          xmm1, [rsp + DISTANCE_FROM_CHILDSP_TO_FP_REGS + 10h]
        movdqa          xmm2, [rsp + DISTANCE_FROM_CHILDSP_TO_FP_REGS + 20h]
        movdqa          xmm3, [rsp + DISTANCE_FROM_CHILDSP_TO_FP_REGS + 30h]

        ; restore integer argument registers
        mov             rcx, [rsp +  0h + DISTANCE_FROM_CHILDSP_TO_CALLERSP]
        mov             rdx, [rsp +  8h + DISTANCE_FROM_CHILDSP_TO_CALLERSP]
        mov             r8,  [rsp + 10h + DISTANCE_FROM_CHILDSP_TO_CALLERSP]
        mov             r9,  [rsp + 18h + DISTANCE_FROM_CHILDSP_TO_CALLERSP]
        
        ; epilog
        nop

        ; Pop the outgoing arguments area and the PInvoke frame.
        POP_COOP_PINVOKE_FRAME DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_RETADDR

        ; Pop the extra space that was allocated between the PInvoke frame and the caller return address.
        add             rsp, DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_RETADDR

        TAILJMP_RAX

NESTED_END RhpUniversalTransition, _TEXT

endif

end
