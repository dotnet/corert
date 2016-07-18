;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

        .586
        .xmm
        .model  flat
        option  casemap:none
        .code


include AsmMacros.inc

DEFAULT_PROBE_SAVE_FLAGS        equ PTFF_SAVE_ALL_PRESERVED + PTFF_SAVE_RSP
PROBE_SAVE_FLAGS_EVERYTHING     equ DEFAULT_PROBE_SAVE_FLAGS + PTFF_SAVE_ALL_SCRATCH
PROBE_SAVE_FLAGS_RAX_IS_GCREF   equ DEFAULT_PROBE_SAVE_FLAGS + PTFF_SAVE_RAX + PTFF_RAX_IS_GCREF
;;
;; Macro to clear the hijack state. This is safe to do because the suspension code will not Unhijack this 
;; thread if it finds it at an IP that isn't managed code.
;;
;; Register state on entry:
;;  EDX: thread pointer
;;  
;; Register state on exit:
;;  No changes
;;
ClearHijackState macro
        mov         dword ptr [edx + OFFSETOF__Thread__m_ppvHijackedReturnAddressLocation], 0
        mov         dword ptr [edx + OFFSETOF__Thread__m_pvHijackedReturnAddress], 0
endm

;;
;; The prolog for all GC suspension hijackes (normal and stress). Sets up an EBP frame,
;; fixes up the hijacked return address, and clears the hijack state.
;;
;; Register state on entry:
;;  All registers correct for return to the original return address.
;;  
;; Register state on exit:
;;  EAX: not trashed or saved
;;  EBP: new EBP frame with correct return address
;;  ESP: points to saved scratch registers (ECX & EDX)
;;  ECX: trashed
;;  EDX: thread pointer
;;
HijackFixupProlog macro
        push        eax         ; save a slot for the repaired return address
        push        ebp
        mov         ebp, esp
        push        ecx         ; save scratch registers
        push        edx         ; save scratch registers

        ;; edx <- GetThread(), TRASHES ecx
        INLINE_GETTHREAD edx, ecx

        ;;
        ;; Fix the stack by pushing the original return address
        ;;
        mov         ecx, [edx + OFFSETOF__Thread__m_pvHijackedReturnAddress]
        mov         [ebp + 4], ecx

        ClearHijackState
endm

;;
;; Epilog for the normal and GC stress hijack functions. Restores scratch registers
;; and returns to the original return address.
;;
;; Register state on entry:
;;  ESP: points to saved scratch registers
;;  EBP: ebp frame
;;  ECX, EDX: trashed
;;  All other registers correct for return to the original return address.
;;  
;; Register state on exit:
;;  All registers restored as they were when the hijack was first reached.
;;
HijackFixupEpilog macro
        pop         edx
        pop         ecx
        pop         ebp
        ret
endm

;;
;; Sets up a PInvokeTranstionFrame with room for all registers.
;;
;; Register state on entry:
;;  EDX: thread pointer
;;  BITMASK_REG_OR_VALUE: register bitmask, PTTR_SAVE_ALL_PRESERVED at a minimum
;;  EBP: ebp frame setup with correct return address
;;  ESP: points to saved scratch registers
;;  
;; Register state on exit:
;;  ESP: pointer to a PInvokeTransitionFrame on the stack
;;  EBX: thread pointer
;;  EAX: trashed
;;  ESI, EDI, EBX, EAX all saved in the frame
;;
;;  ECX is NOT trashed if BITMASK_REG_OR_VALUE is a literal value and not a register
;;
PushProbeFrame macro BITMASK_REG_OR_VALUE
        push        eax                     ; EAX
        lea         eax, [ebp + 8]                      ; get caller ESP
        push        eax                     ; ESP
        push        edi                     ; EDI
        push        esi                     ; ESI
        push        ebx                     ; EBX
        push        BITMASK_REG_OR_VALUE    ; register bitmask
ifdef _DEBUG
        mov         eax, BITMASK_REG_OR_VALUE
        and         eax, DEFAULT_PROBE_SAVE_FLAGS
        cmp         eax, DEFAULT_PROBE_SAVE_FLAGS ; make sure we have at least the flags to match what the macro pushes
        je          @F
        call        RhDebugBreak
@@:
endif ;; _DEBUG
        push        edx                     ; Thread *
        mov         eax, [ebp + 0]                      ; find previous EBP value
        push        eax                     ; m_FramePointer
        mov         eax, [ebp + 4]                      ; get return address
        push        eax                     ; m_RIP

        mov         ebx, edx                            ; save Thread pointer for later
endm

;;
;; Pops off the PInvokeTransitionFrame setup in PushProbeFrame above, restoring all registers.
;;
;; Register state on entry:
;;  ESP: pointer to a PInvokeTransitionFrame on the stack
;;  
;; Register state on exit:
;;  ESP: points to saved scratch registers, PInvokeTransitionFrame removed
;;  EBX: restored
;;  ESI: restored
;;  EDI: restored
;;  EAX: restored
;;
PopProbeFrame macro
        add         esp, 4*4h
        pop         ebx
        pop         esi
        pop         edi
        pop         eax     ; discard ESP
        pop         eax
endm

;;
;; Set the Thread state and wait for a GC to complete.
;;
;; Register state on entry:
;;  ESP: pointer to a PInvokeTransitionFrame on the stack
;;  EBX: thread pointer
;;  EBP: EBP frame
;;  
;; Register state on exit:
;;  ESP: pointer to a PInvokeTransitionFrame on the stack
;;  EBX: thread pointer
;;  EBP: EBP frame
;;  All other registers trashed
;;

EXTERN _RhpWaitForGC : PROC

WaitForGCCompletion macro
        test        dword ptr [ebx + OFFSETOF__Thread__m_ThreadStateFlags], TSF_SuppressGcStress + TSF_DoNotTriggerGc
        jnz         @F

        mov         ecx, esp
        call        _RhpWaitForGC
@@:

endm

;;
;; Main worker for our GC probes.  Do not call directly!! This assumes that HijackFixupProlog has been done.
;; Instead, go through RhpGcProbeHijack* or RhpGcStressHijack*. This waits for the
;; GC to complete then returns to the original return address.
;;
;; Register state on entry:
;;  ECX: register bitmask
;;  EDX: thread pointer
;;  EBP: EBP frame
;;  ESP: scratch registers pushed (ECX & EDX)
;;  
;; Register state on exit:
;;  All registers restored as they were when the hijack was first reached.
;;
RhpGcProbe  proc
        cmp         [RhpTrapThreads], 0
        jne         SynchronousRendezVous

        HijackFixupEpilog

SynchronousRendezVous:
        PushProbeFrame ecx      ; bitmask in ECX

        WaitForGCCompletion

        ;;
        ;; Restore preserved registers -- they may have been updated by GC 
        ;;
        PopProbeFrame

        HijackFixupEpilog

RhpGcProbe  endp

ifdef FEATURE_GC_STRESS
;;
;; Set the Thread state and invoke RedhawkGCInterface::StressGC().
;;
;; Assumes EBX is the Thread pointer.
;;
;; Register state on entry:
;;  EBX: thread pointer
;;  EBP: EBP frame
;;  ESP: pointer to a PInvokeTransitionFrame on the stack
;;  
;; Register state on exit:
;;  ESP: pointer to a PInvokeTransitionFrame on the stack
;;  EBP: EBP frame
;;  All other registers trashed
;;
StressGC macro
        mov         [ebx + OFFSETOF__Thread__m_pHackPInvokeTunnel], esp
        call        REDHAWKGCINTERFACE__STRESSGC
endm

;;
;; Worker for our GC stress probes.  Do not call directly!!  
;; Instead, go through RhpGcStressHijack. This performs the GC Stress
;; work and returns to the original return address.
;;
;; Register state on entry:
;;  EDX: thread pointer
;;  ECX: register bitmask
;;  EBP: EBP frame
;;  ESP: scratch registers pushed (ECX and EDX)
;;  
;; Register state on exit:
;;  All registers restored as they were when the hijack was first reached.
;;
RhpGcStressProbe  proc
        PushProbeFrame ecx      ; bitmask in ECX

        StressGC

        ;;
        ;; Restore preserved registers -- they may have been updated by GC 
        ;;
        PopProbeFrame

        HijackFixupEpilog

RhpGcStressProbe  endp

endif ;; FEATURE_GC_STRESS

FASTCALL_FUNC RhpGcPoll, 0
        ;
        ; loop hijacking is used instead
        ;
        int 3

FASTCALL_ENDFUNC

FASTCALL_FUNC RhpGcPollStress, 0
        ;
        ; loop hijacking is used instead
        ;
        int 3

FASTCALL_ENDFUNC

FASTCALL_FUNC RhpGcProbeHijackScalar, 0

        HijackFixupProlog
        mov         ecx, DEFAULT_PROBE_SAVE_FLAGS
        jmp         RhpGcProbe

FASTCALL_ENDFUNC

FASTCALL_FUNC RhpGcProbeHijackObject, 0

        HijackFixupProlog
        mov         ecx, DEFAULT_PROBE_SAVE_FLAGS + PTFF_SAVE_RAX + PTFF_RAX_IS_GCREF
        jmp         RhpGcProbe

FASTCALL_ENDFUNC

FASTCALL_FUNC RhpGcProbeHijackByref, 0

        HijackFixupProlog
        mov         ecx, DEFAULT_PROBE_SAVE_FLAGS + PTFF_SAVE_RAX + PTFF_RAX_IS_BYREF
        jmp         RhpGcProbe

FASTCALL_ENDFUNC

ifdef FEATURE_GC_STRESS
FASTCALL_FUNC RhpGcStressHijackScalar, 0

        HijackFixupProlog
        mov         ecx, DEFAULT_PROBE_SAVE_FLAGS
        jmp         RhpGcStressProbe

FASTCALL_ENDFUNC

FASTCALL_FUNC RhpGcStressHijackObject, 0

        HijackFixupProlog
        mov         ecx, DEFAULT_PROBE_SAVE_FLAGS + PTFF_SAVE_RAX + PTFF_RAX_IS_GCREF
        jmp         RhpGcStressProbe

FASTCALL_ENDFUNC

FASTCALL_FUNC RhpGcStressHijackByref, 0

        HijackFixupProlog
        mov         ecx, DEFAULT_PROBE_SAVE_FLAGS + PTFF_SAVE_RAX + PTFF_RAX_IS_BYREF
        jmp         RhpGcStressProbe

FASTCALL_ENDFUNC

FASTCALL_FUNC RhpHijackForGcStress, 0
        push        ebp
        mov         ebp, esp
        
        ;;
        ;; Setup a PAL_LIMITED_CONTEXT that looks like what you'd get if you had suspended this thread at the
        ;; IP after the call to this helper.
        ;;
        
        push        edx
        push        ecx
        push        ebx
        push        eax
        push        esi
        push        edi

        mov         eax, [ebp]
        push        eax             ;; (caller) Ebp
        lea         eax, [ebp + 8]
        push        eax             ;; Esp
        mov         eax, [ebp + 4]
        push        eax             ;; Eip

        push        esp        ;; address of PAL_LIMITED_CONTEXT
        call        THREAD__HIJACKFORGCSTRESS

        ;; Note: we only restore the scratch registers here. No GC has occured, so restoring
        ;; the callee saved ones is unnecessary.
        add         esp, 14h
        pop         eax
        pop         ebx
        pop         ecx
        pop         edx
        pop         ebp
        ret
FASTCALL_ENDFUNC
endif ;; FEATURE_GC_STRESS

;;
;; The following functions are _jumped_ to when we need to transfer control from one method to another for EH 
;; dispatch. These are needed to properly coordinate with the GC hijacking logic. We are essentially replacing
;; the return from the throwing method with a jump to the handler in the caller, but we need to be aware of 
;; any return address hijack that may be in place for GC suspension. These routines use a quick test of the 
;; return address against a specific GC hijack routine, and then fixup the stack pointer to what it would be 
;; after a real return from the throwing method. Then, if we are not hijacked we can simply jump to the 
;; handler in the caller.
;; 
;; If we are hijacked, then we jump to a routine that will unhijack appropriatley and wait for the GC to 
;; complete. There are also variants for GC stress.
;;
;; Note that at this point we are eiher hijacked or we are not, and this will not change until we return to 
;; managed code. It is an invariant of the system that a thread will only attempt to hijack or unhijack 
;; another thread while the target thread is suspended in managed code, and this is _not_ managed code.
;;
;; Register state on entry:
;;  EAX: handler address we want to jump to.
;;  ECX: reference to the exception object.
;;  EDX: what ESP should be after the return address and arg space are removed.
;;  EBX, ESI, EDI, and EBP are all already correct for return to the caller.
;;  The stack still contains the return address and the arguments to the call.
;;  
;; Register state on exit:
;;  ESP: what it would be after a complete return to the caller.
;;
RTU_EH_JUMP_HELPER macro funcName, hijackFuncName, isStress, stressFuncName
FASTCALL_FUNC funcName, 0
        cmp         [esp], hijackFuncName
        je          RhpGCProbeForEHJump

IF isStress EQ 1
        cmp         [esp], stressFuncName
        je          RhpGCStressProbeForEHJump
ENDIF

        ;; We are not hijacked, so we can return to the handler.
        ;; We return to keep the call/return prediction balanced.
        mov         esp, edx        ; The stack is now as if we have returned from the call.
        push eax                    ; Push the handler as the return address. 
        ret

FASTCALL_ENDFUNC
endm


;; We need an instance of the helper for each possible hijack function. The binder has enough
;; information to determine which one we need to use for any function.
RTU_EH_JUMP_HELPER RhpEHJumpScalar,         @RhpGcProbeHijackScalar@0,  0, 0
RTU_EH_JUMP_HELPER RhpEHJumpObject,         @RhpGcProbeHijackObject@0,  0, 0
RTU_EH_JUMP_HELPER RhpEHJumpByref,          @RhpGcProbeHijackByref@0,   0, 0
ifdef FEATURE_GC_STRESS
RTU_EH_JUMP_HELPER RhpEHJumpScalarGCStress, @RhpGcProbeHijackScalar@0,  1, @RhpGcStressHijackScalar@0
RTU_EH_JUMP_HELPER RhpEHJumpObjectGCStress, @RhpGcProbeHijackObject@0,  1, @RhpGcStressHijackObject@0
RTU_EH_JUMP_HELPER RhpEHJumpByrefGCStress,  @RhpGcProbeHijackByref@0,   1, @RhpGcStressHijackByref@0
endif

;;
;; Macro to setup our EBP frame and adjust the location of the EH object reference for EH jump probe funcs.
;;
;; Register state on entry:
;;  EAX: handler address we want to jump to.
;;  ECX: reference to the exception object.
;;  EDX: scratch
;;  EBX, ESI, EDI, and EBP are all already correct for return to the caller.
;;  The stack is as if we have returned from the call
;;  
;; Register state on exit:
;;  ESP: ebp frame
;;  EBP: ebp frame setup with space reserved for the repaired return address
;;  EAX: reference to the exception object
;;  ECX: scratch
;;
EHJumpProbeProlog macro
        push        eax         ; save a slot for the repaired return address
        push        ebp         ; setup an ebp frame to keep the stack nicely crawlable
        mov         ebp, esp
        push        eax         ; save the handler address so we can jump to it later
        mov         eax, ecx    ; move the ex object reference into eax so we can report it
endm

;;
;; Macro to re-adjust the location of the EH object reference, cleanup the EBP frame, and make the 
;; final jump to the handler for EH jump probe funcs.
;;
;; Register state on entry:
;;  EAX: reference to the exception object
;;  ESP: ebp frame
;;  EBP: ebp frame setup with the correct return (handler) address
;;  ECX: scratch
;;  EDX: scratch
;;  
;; Register state on exit:
;;  ESP: correct for return to the caller
;;  EBP: previous ebp frame
;;  ECX: reference to the exception object
;;  EDX: trashed
;;
EHJumpProbeEpilog macro
        mov         ecx, eax    ; Put the EX obj ref back into ecx for the handler.
        pop         eax         ; Recover the handler address.
        pop         ebp         ; Pop the ebp frame we setup.
        pop         edx         ; Pop the original return address, which we do not need.
        push eax                ; Push the handler as the return address. 
        ret
endm

;;
;; We are hijacked for a normal GC (not GC stress), so we need to unhijcak and wait for the GC to complete.
;;
;; Register state on entry:
;;  EAX: handler address we want to jump to.
;;  ECX: reference to the exception object.
;;  EDX: what ESP should be after the return address and arg space are removed.
;;  EBX, ESI, EDI, and EBP are all already correct for return to the caller.
;;  The stack is as if we have returned from the call
;;        
;; Register state on exit:
;;  ESP: correct for return to the caller
;;  EBP: previous ebp frame
;;  ECX: reference to the exception object
;;
RhpGCProbeForEHJump proc
        mov         esp, edx        ; The stack is now as if we have returned from the call.
        EHJumpProbeProlog
        
        ;; edx <- GetThread(), TRASHES ecx
        INLINE_GETTHREAD edx, ecx

        ;; Fix the stack by pushing the original return address
        mov         ecx, [edx + OFFSETOF__Thread__m_pvHijackedReturnAddress]
        mov         [ebp + 4], ecx

        ClearHijackState

ifdef _DEBUG
        ;;
        ;; If we get here, then we have been hijacked for a real GC, and our SyncState must
        ;; reflect that we've been requested to synchronize.

        cmp         [RhpTrapThreads], 0
        jne         @F

        call        RhDebugBreak
@@:
endif ;; _DEBUG


        PushProbeFrame  PROBE_SAVE_FLAGS_RAX_IS_GCREF
        WaitForGCCompletion
        PopProbeFrame

        EHJumpProbeEpilog

RhpGCProbeForEHJump endp

ifdef FEATURE_GC_STRESS
;;
;; We are hijacked for GC Stress (not a normal GC) so we need to invoke the GC stress helper.
;;
;; Register state on entry:
;;  EAX: handler address we want to jump to.
;;  ECX: reference to the exception object.
;;  EDX: what ESP should be after the return address and arg space are removed.
;;  EBX, ESI, EDI, and EBP are all already correct for return to the caller.
;;  The stack is as if we have returned from the call
;;        
;; Register state on exit:
;;  ESP: correct for return to the caller
;;  EBP: previous ebp frame
;;  ECX: reference to the exception object
;;
RhpGCStressProbeForEHJump proc
        mov         esp, edx        ; The stack is now as if we have returned from the call.
        EHJumpProbeProlog
        
        ;; edx <- GetThread(), TRASHES ecx
        INLINE_GETTHREAD edx, ecx

        ;; Fix the stack by pushing the original return address
        mov         ecx, [edx + OFFSETOF__Thread__m_pvHijackedReturnAddress]
        mov         [ebp + 4], ecx

        ClearHijackState

        PushProbeFrame  PROBE_SAVE_FLAGS_RAX_IS_GCREF
        StressGC
        PopProbeFrame

        EHJumpProbeEpilog

RhpGCStressProbeForEHJump endp

g_pTheRuntimeInstance equ ?g_pTheRuntimeInstance@@3PAVRuntimeInstance@@A
EXTERN g_pTheRuntimeInstance : DWORD
RuntimeInstance__ShouldHijackLoopForGcStress equ ?ShouldHijackLoopForGcStress@RuntimeInstance@@QAE_NI@Z
EXTERN RuntimeInstance__ShouldHijackLoopForGcStress : PROC

endif ;; FEATURE_GC_STRESS

RecoverLoopHijackTarget equ @RecoverLoopHijackTarget@8
EXTERN RecoverLoopHijackTarget : PROC
EXTERN _g_fGcStressStarted : DWORD
EXTERN RhpCall : PROC

FXSAVE_SIZE             equ 512

FASTCALL_FUNC RhpLoopHijack, 8  ;; ecx, edx are ignored, 12 bytes are on the stack, but we have to pretend
                                ;; that we don't have any stack arguments so that debuggers don't mess up our
                                ;; stack traces

        ;; On the stack: 
        ;;      [esp +  0h] -> ModuleHeader * 
        ;;      [esp +  4h] -> chunk starting index
        ;;      [esp +  8h] -> chunk sub-index (0-256)  BEWARE: this has been sign-extended, but it is unsigned
        ;;

        pushfd          ; eflags
        push        ecx
        push        edx

        ;;
        ;; NOTE: Do not trash EAX or any preserved register until the PushProbeFrame
        ;;

        ;; On the stack: 
        ;;      [esp +  0h] -> edx save
        ;;      [esp +  4h] -> ecx save
        ;;      [esp +  8h] -> eflags save
        ;;      [esp + 0ch] -> ModuleHeader * 
        ;;      [esp + 10h] -> chunk starting index
        ;;      [esp + 14h] -> chunk sub-index (0-256)  BEWARE: this has been sign-extended, but it is unsigned
        ;;

        ;; Combine the two indexes
        xor         ecx, ecx            ; ecx <- 0
        mov         cl,  [esp + 14h]    ; ecx <- chunk sub-index
        add         ecx, [esp + 10h]    ; ecx <- (chunk sub-index) + (chunk starting index) = (indirection cell index)
        push        ecx

        ;; On the stack: 
        ;;      [esp +  0h] -> indirection cell index
        ;;      [esp +  4h] -> edx save
        ;;      [esp +  8h] -> ecx save
        ;;      [esp + 0ch] -> eflags save
        ;;      [esp + 10h] -> ModuleHeader * 
        ;;      [esp + 14h] -> scratch          -> will be ebp save
        ;;      [esp + 18h] -> scratch          -> will be return address
        ;;

        ;; Setup EBP frame
        mov     [esp + 14h], ebp
        lea     ebp, [esp + 14h]

        ;; On the stack: 
        ;;      [ebp - 14h] -> indirection cell index
        ;;      [ebp - 10h] -> edx save
        ;;      [ebp - 0ch] -> ecx save
        ;;      [ebp -  8h] -> eflags save
        ;;      [ebp -  4h] -> ModuleHeader * 
        ;;  ebp:[ebp +  0h] -> ebp save
        ;;      [ebp +  4h] -> scratch          -> will be return address
        ;;

        ;; make (aligned) space for the XMM spills
        sub         esp, FXSAVE_SIZE
        and         esp, NOT 0Fh

        ;; @TODO: save AVX state (currently our code generator doesn't use AVX)
        fxsave      [esp]

        ;; PushProbeFrame wants the Thread* in edx
        INLINE_GETTHREAD edx, ecx       ;; edx <- GetThread(), TRASHES ecx

        ;; Push edx and ecx as part of the PInvokeTransitionFrame
        push    [ebp - 10h]             ;; push edx
        push    [ebp - 0ch]             ;; push ecx
        PushProbeFrame  PROBE_SAVE_FLAGS_EVERYTHING     ;; pushes 9 dwords

ifdef _DEBUG
        ;; trash the old save locations for ecx / edx to make sure we don't use them -- they must be restored from the
        ;; PInvokeTransitionFrame
        xor     ecx, ecx
        mov     [ebp - 10h], ecx
        mov     [ebp - 0ch], ecx
endif

        ;; On the stack:
        ;;  esp:[esp +  0h] ->  PInvokeTransitionFrame (pushed by PushProbeFrame)
        ;;      [esp + 24h] ->  ECX save (part of PInvokeTransitionFrame)
        ;;      [esp + 28h] ->  EDX save (part of PInvokeTransitionFrame)
        ;;      [esp + 2ch] ->  FXSAVE area
        ;; --------------------------------------------------
        ;;      [ebp - 14h] -> indirection cell index
        ;;      [ebp - 10h] -> edx save
        ;;      [ebp -  ch] -> ecx save
        ;;      [ebp -  8h] -> eflags save
        ;;      [ebp -  4h] -> ModuleHeader * 
        ;;  ebp:[ebp +  0h] -> ebp save
        ;;      [ebp +  4h] -> scratch          -> will be return address

        mov         ecx, [ebp - 14h]    ; ecx <- indirection cell index
        mov         edx, [ebp - 4h]     ; edx <- ModuleHeader *
        call        RecoverLoopHijackTarget
        mov         [ebp + 4h], eax     ; store original loop target as return address
        mov         [esp + OFFSETOF__PInvokeTransitionFrame__m_RIP], eax        ; patch EIP in the Frame

        mov         edx, [esp + OFFSETOF__PInvokeTransitionFrame__m_pThread]    ; recover Thread * from Frame

        ; Early out if GC stress is currently suppressed. Do this after we have computed the real address to
        ; return to but before we link the transition frame onto m_pHackPInvokeTunnel (because hitting this
        ; condition implies we're running restricted callouts during a GC itself and we could end up
        ; overwriting a co-op frame set by the code that caused the GC in the first place, e.g. a GC.Collect
        ; call).
        test        dword ptr [edx + OFFSETOF__Thread__m_ThreadStateFlags], TSF_SuppressGcStress + TSF_DoNotTriggerGc
        jnz         DoneWaitingForGc

        ;;
        ;; Unhijack this thread, if necessary.
        ;;
        INLINE_THREAD_UNHIJACK  edx, eax, ecx       ;; trashes EAX, ECX

        ;; The following two calls are indirected through a specially generated thunk, RhpCall. This routine
        ;; simply calls the target in EAX and returns but it is also associated with a special debug record
        ;; that tells debuggers how to unwind back from the call to the code we hijacked. Without this unwind
        ;; info the debugger gets confused when it unwinds from this routine (via the ebp chain) but doesn't
        ;; see a call instruction in the caller. Ideally the debug record would be associated directly with
        ;; this routine but MASM only supports emitting the older and insufficient FPO record.

ifdef FEATURE_GC_STRESS
        xor         eax, eax
        cmp         [_g_fGcStressStarted], eax
        jz          @F

        mov         eax, [ebp + 4]
        push        eax
        mov         ecx, [g_pTheRuntimeInstance]
        call        RuntimeInstance__ShouldHijackLoopForGcStress
        cmp         al, 0
        je          @F

        mov         edx, [esp + OFFSETOF__PInvokeTransitionFrame__m_pThread]    ; recover Thread * from Frame
        mov         [edx + OFFSETOF__Thread__m_pHackPInvokeTunnel], esp         ; esp is address of PInvokeTransitionFrame
        mov         eax, REDHAWKGCINTERFACE__STRESSGC
        call        RhpCall
@@:
endif ;; FEATURE_GC_STRESS

        mov         ecx, esp            ; esp is address of PInvokeTransitionFrame
        mov         eax, _RhpWaitForGC
        call        RhpCall

DoneWaitingForGc:

        ;; Shuffle the eflags next to ebp so that we don't have to do any funny business in the epilog that might trash 
        ;; the flags.
        mov         ecx, [ebp - 8h]
        mov         [ebp - 4h], ecx

        ; Restore our integer register state from the PInvokeTransitionFrame
        PopProbeFrame
        pop         ecx
        pop         edx

        ; Restore our FP state from the FXSAVE area
        fxrstor     [esp]

        ; Pop the rest of our frame
        lea         esp, [ebp - 4]
        popfd
        pop         ebp
        ret

FASTCALL_ENDFUNC

        end
