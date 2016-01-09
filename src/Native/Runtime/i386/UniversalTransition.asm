;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

        .586
        .model  flat
        option  casemap:none
        .code

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
;;      ecx, edx, stack space three pops down: arguments as normal
;;       first register sized fields on the stack is the location of the target code 
;;       the UniversalTransitionThunk will call
;;       second register sized field on the stack is the parameter to the target function
;;       followed by the return address of the whole method. (This method cannot be called
;;       via a call instruction, it must be jumped to.) The fake entrypoint is in place to 
;;       convince the stack walker this is a normal framed function.
;;
;;  NOTE! FOR CORRECTNESS THIS FUNCTION REQUIRES THAT ALL NON-LEAF MANAGED FUNCTIONS HAVE
;;        FRAME POINTERS, OR THE STACK WALKER CAN'T STACKWALK OUT OF HERE
;;

;
; Stack frame layout (from lower addresses to higher addresses):
;
; [callee return]
; [pinvoke frame, 20h]
; [in edx (argument register from the caller)]
; [in ecx (argument register from the caller)]
; [ConservativelyReportedScratchSpace 8h]
;   -- On input (i.e., when control jumps to RhpUniversalTransition), the low 4 bytes of
;      this area contain the address of the callee and the high 4 bytes of this area contain
;      the extra argument to pass to the callee.
; [ptr to pinvoke frame 4h]
; [saved ebp register]
; [caller return addr]
; [stack-passed arguments from the caller]
;
; Note: The callee receives a pointer to the pushed edx value, and the callee has
; knowledge of the exact layout of all pieces of the frame that lie at or above the pushed
; edx value.
;

FASTCALL_FUNC RhpUniversalTransition_FAKE_ENTRY, 0        
        ; Set up an ebp frame
        push        ebp
        mov         ebp, esp
        push eax
        push eax
        push eax
ALTERNATE_ENTRY RhpUniversalTransition@0
        push ecx
        push edx

        ; Build the frame that the stack walker will use to unwind through this function.  The
        ; <NoModeSwitch> flag indicates that this function never uses Enable/DisablePreemptiveGC,
        ; implying that frame address does not need to be recorded in the current thread object.
        PUSH_COOP_PINVOKE_FRAME notUsed, <NoModeSwitch>

        ;; Stash the pinvoke frame's address immediately on top of the old ebp value. This
        ;; position is important; the stack frame iterator knows about this setup.
.erre MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET eq -4
        mov [ebp-4], esp

        ;
        ; Call out to the target, while storing and reporting arguments to the GC.
        ;
        mov  eax, [ebp-0Ch]  ; Get the address of the callee 
        mov  edx, [ebp-8]    ; Get the extra argument to pass to the callee
        lea  ecx, [ebp-14h]  ; Get pointer to edx value pushed above
        call eax
LABELED_RETURN_ADDRESS ReturnFromUniversalTransition

        POP_COOP_PINVOKE_FRAME
        pop edx
        pop ecx
        add esp, 12
        pop ebp
        jmp eax

FASTCALL_ENDFUNC

endif

end
