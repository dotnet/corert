// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This file is used by AsmOffsets.h to validate that our
// assembly-code offsets always match their C++ counterparts.
//
// NOTE: the offsets MUST be in hex notation WITHOUT the 0x prefix

#ifndef UNIX_AMD64_ABI
PLAT_ASM_SIZEOF(250, ExInfo)
PLAT_ASM_OFFSET(0, ExInfo, m_pPrevExInfo)
PLAT_ASM_OFFSET(8, ExInfo, m_pExContext)
PLAT_ASM_OFFSET(10, ExInfo, m_exception)
PLAT_ASM_OFFSET(18, ExInfo, m_kind)
PLAT_ASM_OFFSET(19, ExInfo, m_passNumber)
PLAT_ASM_OFFSET(1c, ExInfo, m_idxCurClause)
PLAT_ASM_OFFSET(20, ExInfo, m_frameIter)
PLAT_ASM_OFFSET(240, ExInfo, m_notifyDebuggerSP)

PLAT_ASM_OFFSET(0, PInvokeTransitionFrame, m_RIP)
PLAT_ASM_OFFSET(8, PInvokeTransitionFrame, m_FramePointer)
PLAT_ASM_OFFSET(10, PInvokeTransitionFrame, m_pThread)
PLAT_ASM_OFFSET(18, PInvokeTransitionFrame, m_dwFlags)
PLAT_ASM_OFFSET(20, PInvokeTransitionFrame, m_PreservedRegs)

PLAT_ASM_SIZEOF(220, StackFrameIterator)
PLAT_ASM_OFFSET(10, StackFrameIterator, m_FramePointer)
PLAT_ASM_OFFSET(18, StackFrameIterator, m_ControlPC)
PLAT_ASM_OFFSET(20, StackFrameIterator, m_RegDisplay)

PLAT_ASM_SIZEOF(100, PAL_LIMITED_CONTEXT)
PLAT_ASM_OFFSET(0, PAL_LIMITED_CONTEXT, IP)

PLAT_ASM_OFFSET(8, PAL_LIMITED_CONTEXT, Rsp)
PLAT_ASM_OFFSET(10, PAL_LIMITED_CONTEXT, Rbp)
PLAT_ASM_OFFSET(18, PAL_LIMITED_CONTEXT, Rdi)
PLAT_ASM_OFFSET(20, PAL_LIMITED_CONTEXT, Rsi)
PLAT_ASM_OFFSET(28, PAL_LIMITED_CONTEXT, Rax)
PLAT_ASM_OFFSET(30, PAL_LIMITED_CONTEXT, Rbx)

PLAT_ASM_OFFSET(38, PAL_LIMITED_CONTEXT, R12)
PLAT_ASM_OFFSET(40, PAL_LIMITED_CONTEXT, R13)
PLAT_ASM_OFFSET(48, PAL_LIMITED_CONTEXT, R14)
PLAT_ASM_OFFSET(50, PAL_LIMITED_CONTEXT, R15)
PLAT_ASM_OFFSET(60, PAL_LIMITED_CONTEXT, Xmm6)
PLAT_ASM_OFFSET(70, PAL_LIMITED_CONTEXT, Xmm7)
PLAT_ASM_OFFSET(80, PAL_LIMITED_CONTEXT, Xmm8)
PLAT_ASM_OFFSET(90, PAL_LIMITED_CONTEXT, Xmm9)
PLAT_ASM_OFFSET(0a0, PAL_LIMITED_CONTEXT, Xmm10)
PLAT_ASM_OFFSET(0b0, PAL_LIMITED_CONTEXT, Xmm11)
PLAT_ASM_OFFSET(0c0, PAL_LIMITED_CONTEXT, Xmm12)
PLAT_ASM_OFFSET(0d0, PAL_LIMITED_CONTEXT, Xmm13)
PLAT_ASM_OFFSET(0e0, PAL_LIMITED_CONTEXT, Xmm14)
PLAT_ASM_OFFSET(0f0, PAL_LIMITED_CONTEXT, Xmm15)

PLAT_ASM_SIZEOF(130, REGDISPLAY)
PLAT_ASM_OFFSET(78, REGDISPLAY, SP)

PLAT_ASM_OFFSET(18, REGDISPLAY, pRbx)
PLAT_ASM_OFFSET(20, REGDISPLAY, pRbp)
PLAT_ASM_OFFSET(28, REGDISPLAY, pRsi)
PLAT_ASM_OFFSET(30, REGDISPLAY, pRdi)
PLAT_ASM_OFFSET(58, REGDISPLAY, pR12)
PLAT_ASM_OFFSET(60, REGDISPLAY, pR13)
PLAT_ASM_OFFSET(68, REGDISPLAY, pR14)
PLAT_ASM_OFFSET(70, REGDISPLAY, pR15)
PLAT_ASM_OFFSET(90, REGDISPLAY, Xmm)

#else // !UNIX_AMD64_ABI

PLAT_ASM_SIZEOF(1a0, ExInfo)
PLAT_ASM_OFFSET(0, ExInfo, m_pPrevExInfo)
PLAT_ASM_OFFSET(8, ExInfo, m_pExContext)
PLAT_ASM_OFFSET(10, ExInfo, m_exception)
PLAT_ASM_OFFSET(18, ExInfo, m_kind)
PLAT_ASM_OFFSET(19, ExInfo, m_passNumber)
PLAT_ASM_OFFSET(1c, ExInfo, m_idxCurClause)
PLAT_ASM_OFFSET(20, ExInfo, m_frameIter)
PLAT_ASM_OFFSET(198, ExInfo, m_notifyDebuggerSP)

PLAT_ASM_OFFSET(0, PInvokeTransitionFrame, m_RIP)
PLAT_ASM_OFFSET(8, PInvokeTransitionFrame, m_FramePointer)
PLAT_ASM_OFFSET(10, PInvokeTransitionFrame, m_pThread)
PLAT_ASM_OFFSET(18, PInvokeTransitionFrame, m_dwFlags)
PLAT_ASM_OFFSET(20, PInvokeTransitionFrame, m_PreservedRegs)

PLAT_ASM_SIZEOF(178, StackFrameIterator)
PLAT_ASM_OFFSET(10, StackFrameIterator, m_FramePointer)
PLAT_ASM_OFFSET(18, StackFrameIterator, m_ControlPC)
PLAT_ASM_OFFSET(20, StackFrameIterator, m_RegDisplay)

PLAT_ASM_SIZEOF(50, PAL_LIMITED_CONTEXT)
PLAT_ASM_OFFSET(0, PAL_LIMITED_CONTEXT, IP)

PLAT_ASM_OFFSET(8, PAL_LIMITED_CONTEXT, Rsp)
PLAT_ASM_OFFSET(10, PAL_LIMITED_CONTEXT, Rbp)
PLAT_ASM_OFFSET(18, PAL_LIMITED_CONTEXT, Rax)
PLAT_ASM_OFFSET(20, PAL_LIMITED_CONTEXT, Rbx)
PLAT_ASM_OFFSET(28, PAL_LIMITED_CONTEXT, Rdx)

PLAT_ASM_OFFSET(30, PAL_LIMITED_CONTEXT, R12)
PLAT_ASM_OFFSET(38, PAL_LIMITED_CONTEXT, R13)
PLAT_ASM_OFFSET(40, PAL_LIMITED_CONTEXT, R14)
PLAT_ASM_OFFSET(48, PAL_LIMITED_CONTEXT, R15)

PLAT_ASM_SIZEOF(98, REGDISPLAY)
PLAT_ASM_OFFSET(78, REGDISPLAY, SP)

PLAT_ASM_OFFSET(18, REGDISPLAY, pRbx)
PLAT_ASM_OFFSET(20, REGDISPLAY, pRbp)
PLAT_ASM_OFFSET(28, REGDISPLAY, pRsi)
PLAT_ASM_OFFSET(30, REGDISPLAY, pRdi)
PLAT_ASM_OFFSET(58, REGDISPLAY, pR12)
PLAT_ASM_OFFSET(60, REGDISPLAY, pR13)
PLAT_ASM_OFFSET(68, REGDISPLAY, pR14)
PLAT_ASM_OFFSET(70, REGDISPLAY, pR15)

#endif // !UNIX_AMD64_ABI
