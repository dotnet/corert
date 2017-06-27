// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>
#include <AsmOffsets.inc>

.syntax unified
.thumb

.macro INLINE_GET_TLS_VAR Var, trashReg
	        ldr	         r0, =\Var(tlsgd)
	        bl	         __tls_get_addr@PLT
	        ldr	         \trashReg, =\Var(gottpoff)
	        add	         r0, \trashReg
.endmacro

.macro INLINE_GETTHREAD trashReg
	        // Inlined version of call C_FUNC(RhpGetThread)
	        INLINE_GET_TLS_VAR tls_CurrentThread, \trashReg
.endmacro

//
// RhpPInvoke
//
// IN:  R0: address of pinvoke frame
//
// This helper assumes that its callsite is as good to start the stackwalk as the actual PInvoke callsite.
// The codegenerator must treat the callsite of this helper as GC triggering and generate the GC info for it.
// Also, the codegenerator must ensure that there are no live GC references in callee saved registers.
//
NESTED_ENTRY RhpPInvoke, _TEXT, NoHandler
	        PROLOG_PUSH "{r4-r7,lr}"

	        str         lr, [r0, #OFFSETOF__PInvokeTransitionFrame__m_RIP]
	        str         r7, [r0, #OFFSETOF__PInvokeTransitionFrame__m_FramePointer]
	        str         sp, [r0, #OFFSETOF__PInvokeTransitionFrame__m_PreservedRegs]

	        mov	        r5, r0
	        // R0 = GetThread(), R6 - Trash Register
	        INLINE_GETTHREAD r6
	        str         r0, [r5, #OFFSETOF__PInvokeTransitionFrame__m_pThread]
	        str         r5, [r0, #OFFSETOF__Thread__m_pTransitionFrame]

	        ldr         r6, =C_FUNC(RhpTrapThreads)
	        ldr         r6,	[r6]
	        cbnz        r6, LOCAL_LABEL(InvokeRareTrapThread)

	        EPILOG_POP  "{r4-r7,pc}"

LOCAL_LABEL(InvokeRareTrapThread):
	        b           C_FUNC(RhpWaitForSuspend2)
NESTED_END RhpPInvoke, _TEXT

//
// RhpPInvokeReturn
//
// IN:  R0: address of pinvoke frame
//
LEAF_ENTRY RhpPInvokeReturn, _TEXT
	        ldr         r3, [r0, #OFFSETOF__PInvokeTransitionFrame__m_pThread]

	        mov         r2, #0
	        str         r2, [r3, #OFFSETOF__Thread__m_pTransitionFrame]

	        ldr         r3, =C_FUNC(RhpTrapThreads)
	        ldr         r3,	[r3]
	        cbnz        r3, LOCAL_LABEL(ReturnRareTrapThread)

	        bx          lr

LOCAL_LABEL(ReturnRareTrapThread):
	        // passing transition frame pointer in r0
	        b           C_FUNC(RhpWaitForGC2)
LEAF_END RhpPInvokeReturn, _TEXT
