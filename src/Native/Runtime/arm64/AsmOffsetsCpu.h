// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This file is used by AsmOffsets.h to validate that our
// assembly-code offsets always match their C++ counterparts.
//
// NOTE: the offsets MUST be in hex notation WITHOUT the 0x prefix

PLAT_ASM_SIZEOF(280, ExInfo)
PLAT_ASM_OFFSET(0, ExInfo, m_pPrevExInfo)
PLAT_ASM_OFFSET(8, ExInfo, m_pExContext)
PLAT_ASM_OFFSET(10, ExInfo, m_exception)
PLAT_ASM_OFFSET(18, ExInfo, m_kind)
PLAT_ASM_OFFSET(19, ExInfo, m_passNumber)
PLAT_ASM_OFFSET(1c, ExInfo, m_idxCurClause)
PLAT_ASM_OFFSET(20, ExInfo, m_frameIter)
PLAT_ASM_OFFSET(278, ExInfo, m_notifyDebuggerSP)

PLAT_ASM_OFFSET(0, PInvokeTransitionFrame, m_RIP)
PLAT_ASM_OFFSET(8, PInvokeTransitionFrame, m_FramePointer)
PLAT_ASM_OFFSET(10, PInvokeTransitionFrame, m_pThread)
PLAT_ASM_OFFSET(18, PInvokeTransitionFrame, m_dwFlags)
PLAT_ASM_OFFSET(20, PInvokeTransitionFrame, m_PreservedRegs)

PLAT_ASM_SIZEOF(258, StackFrameIterator)
PLAT_ASM_OFFSET(10, StackFrameIterator, m_FramePointer)
PLAT_ASM_OFFSET(18, StackFrameIterator, m_ControlPC)
PLAT_ASM_OFFSET(20, StackFrameIterator, m_RegDisplay)

PLAT_ASM_SIZEOF(148, PAL_LIMITED_CONTEXT)
PLAT_ASM_OFFSET(100, PAL_LIMITED_CONTEXT, IP)

// @TODO: Add ARM64 entries for PAL_LIMITED_CONTEXT

PLAT_ASM_SIZEOF(150, REGDISPLAY)
PLAT_ASM_OFFSET(f8, REGDISPLAY, SP)

// @TODO: Add ARM64 entries for REGDISPLAY
