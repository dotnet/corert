//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// This file is used by AsmOffsets.cpp to validate that our 
// assembly-code offsets always match their C++ counterparts.
//
// You must #define PLAT_ASM_OFFSET and PLAT_ASM_SIZEOF before you #include this file
//

#if defined(_X86_)
#define ASM_OFFSET(x86_offset, arm_offset, amd64_offset, cls, member) PLAT_ASM_OFFSET(x86_offset, cls, member)
#define ASM_SIZEOF(x86_offset, arm_offset, amd64_offset, cls        ) PLAT_ASM_SIZEOF(x86_offset, cls)
#define ASM_CONST(x86_const, arm_const, amd64_const, expr)            PLAT_ASM_CONST(x86_const, expr)
#elif defined(_AMD64_)
#define ASM_OFFSET(x86_offset, arm_offset, amd64_offset, cls, member) PLAT_ASM_OFFSET(amd64_offset, cls, member)
#define ASM_SIZEOF(x86_offset, arm_offset, amd64_offset, cls        ) PLAT_ASM_SIZEOF(amd64_offset, cls)
#define ASM_CONST(x86_const, arm_const, amd64_const, expr)            PLAT_ASM_CONST(amd64_const, expr)
#elif defined(_ARM_)
#define ASM_OFFSET(x86_offset, arm_offset, amd64_offset, cls, member) PLAT_ASM_OFFSET(arm_offset, cls, member)
#define ASM_SIZEOF(x86_offset, arm_offset, amd64_offset, cls        ) PLAT_ASM_SIZEOF(arm_offset, cls)
#define ASM_CONST(x86_const, arm_const, amd64_const, expr)            PLAT_ASM_CONST(arm_const, expr)
#else
#error unknown architecture
#endif

//
// NOTE: the offsets MUST be in hex notation WITHOUT the 0x prefix
// 
//          x86,  arm,amd64, constant symbol
ASM_CONST(14c08,14c08,14c08, RH_LARGE_OBJECT_SIZE)
ASM_CONST(  400,  400,  800, CLUMP_SIZE)
ASM_CONST(    a,    a,    b, LOG2_CLUMP_SIZE)

//          x86,  arm,amd64, class,   member

ASM_OFFSET(   0,    0,    0, Object,  m_pEEType)

ASM_OFFSET(   4,    4,    8, Array,   m_Length)

ASM_OFFSET(   0,    0,    0, EEType,  m_usComponentSize)
ASM_OFFSET(   2,    2,    2, EEType,  m_usFlags)
ASM_OFFSET(   4,    4,    4, EEType,  m_uBaseSize)
ASM_OFFSET(  14,   14,   18, EEType,  m_VTable)

ASM_OFFSET(   0,    0,    0, Thread,  m_rgbAllocContextBuffer)
ASM_OFFSET(  1c,   1c,   28, Thread,  m_ThreadStateFlags)
ASM_OFFSET(  20,   20,   30, Thread,  m_pTransitionFrame)
ASM_OFFSET(  24,   24,   38, Thread,  m_pHackPInvokeTunnel)
ASM_OFFSET(  34,   34,   58, Thread,  m_ppvHijackedReturnAddressLocation)
ASM_OFFSET(  38,   38,   60, Thread,  m_pvHijackedReturnAddress)
ASM_OFFSET(  3c,   3c,   68, Thread,  m_pExInfoStackHead)

ASM_SIZEOF(  14,   14,   20, EHEnum) 

ASM_SIZEOF(  b0,  128,  250, ExInfo) 
ASM_OFFSET(   0,    0,    0, ExInfo,  m_pPrevExInfo)
ASM_OFFSET(   4,    4,    8, ExInfo,  m_pExContext)
ASM_OFFSET(   8,    8,   10, ExInfo,  m_exception)
ASM_OFFSET(  0c,   0c,   18, ExInfo,  m_kind)
ASM_OFFSET(  0d,   0d,   19, ExInfo,  m_passNumber)
ASM_OFFSET(  10,   10,   1c, ExInfo,  m_idxCurClause)
ASM_OFFSET(  14,   18,   20, ExInfo,  m_frameIter)
ASM_OFFSET(  ac,  120,  240, ExInfo,  m_notifyDebuggerSP)

ASM_OFFSET(   0,    0,    0, alloc_context, alloc_ptr)
ASM_OFFSET(   4,    4,    8, alloc_context, alloc_limit)


ASM_OFFSET(   4,    4,    8, RuntimeInstance, m_pThreadStore)

ASM_OFFSET(   0,    4,    0, PInvokeTransitionFrame, m_RIP)
ASM_OFFSET(   4,    8,    8, PInvokeTransitionFrame, m_FramePointer)
ASM_OFFSET(   8,   0C,   10, PInvokeTransitionFrame, m_pThread)
ASM_OFFSET(  0C,   10,   18, PInvokeTransitionFrame, m_dwFlags)
ASM_OFFSET(  10,   14,   20, PInvokeTransitionFrame, m_PreservedRegs)

ASM_SIZEOF(  98,  108,  220, StackFrameIterator)
ASM_OFFSET(  08,   08,   10, StackFrameIterator, m_FramePointer)
ASM_OFFSET(  0C,   0C,   18, StackFrameIterator, m_ControlPC)
ASM_OFFSET(  10,   10,   20, StackFrameIterator, m_RegDisplay)

ASM_SIZEOF(  1c,   70,  100, PAL_LIMITED_CONTEXT)
ASM_OFFSET(   0,   24,    0, PAL_LIMITED_CONTEXT, IP)
#ifdef _ARM_
ASM_OFFSET(   0,    0,    0, PAL_LIMITED_CONTEXT, R0)
ASM_OFFSET(   0,    4,    0, PAL_LIMITED_CONTEXT, R4)
ASM_OFFSET(   0,    8,    0, PAL_LIMITED_CONTEXT, R5)
ASM_OFFSET(   0,   0c,    0, PAL_LIMITED_CONTEXT, R6)
ASM_OFFSET(   0,   10,    0, PAL_LIMITED_CONTEXT, R7)
ASM_OFFSET(   0,   14,    0, PAL_LIMITED_CONTEXT, R8)
ASM_OFFSET(   0,   18,    0, PAL_LIMITED_CONTEXT, R9)
ASM_OFFSET(   0,   1c,    0, PAL_LIMITED_CONTEXT, R10)
ASM_OFFSET(   0,   20,    0, PAL_LIMITED_CONTEXT, R11)
ASM_OFFSET(   0,   28,    0, PAL_LIMITED_CONTEXT, SP)
ASM_OFFSET(   0,   2c,    0, PAL_LIMITED_CONTEXT, LR)
#else // _ARM_
ASM_OFFSET(   4,    0,    8, PAL_LIMITED_CONTEXT, Rsp)
ASM_OFFSET(   8,    0,   10, PAL_LIMITED_CONTEXT, Rbp)
ASM_OFFSET(  0c,    0,   18, PAL_LIMITED_CONTEXT, Rdi)
ASM_OFFSET(  10,    0,   20, PAL_LIMITED_CONTEXT, Rsi)
ASM_OFFSET(  14,    0,   28, PAL_LIMITED_CONTEXT, Rax)
ASM_OFFSET(  18,    0,   30, PAL_LIMITED_CONTEXT, Rbx)
#ifdef _AMD64_
ASM_OFFSET(   0,    0,   38, PAL_LIMITED_CONTEXT, R12)
ASM_OFFSET(   0,    0,   40, PAL_LIMITED_CONTEXT, R13)
ASM_OFFSET(   0,    0,   48, PAL_LIMITED_CONTEXT, R14)
ASM_OFFSET(   0,    0,   50, PAL_LIMITED_CONTEXT, R15)
ASM_OFFSET(   0,    0,   60, PAL_LIMITED_CONTEXT, Xmm6)
ASM_OFFSET(   0,    0,   70, PAL_LIMITED_CONTEXT, Xmm7)
ASM_OFFSET(   0,    0,   80, PAL_LIMITED_CONTEXT, Xmm8)
ASM_OFFSET(   0,    0,   90, PAL_LIMITED_CONTEXT, Xmm9)
ASM_OFFSET(   0,    0,  0a0, PAL_LIMITED_CONTEXT, Xmm10)
ASM_OFFSET(   0,    0,  0b0, PAL_LIMITED_CONTEXT, Xmm11)
ASM_OFFSET(   0,    0,  0c0, PAL_LIMITED_CONTEXT, Xmm12)
ASM_OFFSET(   0,    0,  0d0, PAL_LIMITED_CONTEXT, Xmm13)
ASM_OFFSET(   0,    0,  0e0, PAL_LIMITED_CONTEXT, Xmm14)
ASM_OFFSET(   0,    0,  0f0, PAL_LIMITED_CONTEXT, Xmm15)
#endif // _AMD64_
#endif // _ARM_

ASM_SIZEOF(  28,   88,  130, REGDISPLAY)
ASM_OFFSET(  1c,   38,   78, REGDISPLAY, SP)
#ifdef _ARM_
ASM_OFFSET(   0,   10,    0, REGDISPLAY, pR4)
ASM_OFFSET(   0,   14,    0, REGDISPLAY, pR5)
ASM_OFFSET(   0,   18,    0, REGDISPLAY, pR6)
ASM_OFFSET(   0,   1c,    0, REGDISPLAY, pR7)
ASM_OFFSET(   0,   20,    0, REGDISPLAY, pR8)
ASM_OFFSET(   0,   24,    0, REGDISPLAY, pR9)
ASM_OFFSET(   0,   28,    0, REGDISPLAY, pR10)
ASM_OFFSET(   0,   2c,    0, REGDISPLAY, pR11)
ASM_OFFSET(   0,   48,    0, REGDISPLAY, D)
#else // _ARM_
ASM_OFFSET(  0c,    0,   18, REGDISPLAY, pRbx)
ASM_OFFSET(  10,    0,   20, REGDISPLAY, pRbp)
ASM_OFFSET(  14,    0,   28, REGDISPLAY, pRsi)
ASM_OFFSET(  18,    0,   30, REGDISPLAY, pRdi)
#ifdef _AMD64_
ASM_OFFSET(   0,    0,   58, REGDISPLAY, pR12)
ASM_OFFSET(   0,    0,   60, REGDISPLAY, pR13)
ASM_OFFSET(   0,    0,   68, REGDISPLAY, pR14)
ASM_OFFSET(   0,    0,   70, REGDISPLAY, pR15)
ASM_OFFSET(   0,    0,   90, REGDISPLAY, Xmm)
#endif // _AMD64_
#endif // _ARM_

#ifdef FEATURE_CACHED_INTERFACE_DISPATCH
ASM_OFFSET(   4,    4,   8, InterfaceDispatchCell, m_pCache)
#ifndef _AMD64_
ASM_OFFSET(   8,    8,   10, InterfaceDispatchCache, m_pCell)
#endif
ASM_OFFSET(  10,   10,   20, InterfaceDispatchCache, m_rgEntries)
#endif

ASM_OFFSET(   4,    4,    8, StaticClassConstructionContext, m_initialized)

#ifdef FEATURE_DYNAMIC_CODE
ASM_OFFSET(   0,    0,    0, CallDescrData, pSrc)
ASM_OFFSET(   4,    4,    8, CallDescrData, numStackSlots)
ASM_OFFSET(   8,    8,    C, CallDescrData, fpReturnSize)
ASM_OFFSET(   C,    C,   10, CallDescrData, pArgumentRegisters)
ASM_OFFSET(  10,   10,   18, CallDescrData, pFloatArgumentRegisters)
ASM_OFFSET(  14,   14,   20, CallDescrData, pTarget)
ASM_OFFSET(  18,   18,   28, CallDescrData, pReturnBuffer)
#endif
