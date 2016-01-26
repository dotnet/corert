//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#pragma once

// TODO: Debugger/DAC support (look for TODO: JIT)

struct REGDISPLAY;

#define GC_CALL_INTERIOR            0x1
#define GC_CALL_PINNED              0x2
#define GC_CALL_CHECK_APP_DOMAIN    0x4
#define GC_CALL_STATIC              0x8

typedef void (*GCEnumCallback)(
    void *              hCallback,      // callback data
    PTR_PTR_VOID        pObject,        // address of object-reference we are reporting
    UInt32              flags           // is this a pinned and/or interior pointer
);

struct GCEnumContext
{
    GCEnumCallback pCallback;
};

enum GCRefKind : unsigned char
{ 
    GCRK_Scalar     = 0x00,
    GCRK_Object     = 0x01,
    GCRK_Byref      = 0x02,
    GCRK_Unknown    = 0xFF,
};

//
// MethodInfo is placeholder type used to allocate space for MethodInfo. Maximum size 
// of the actual method should be less or equal to the placeholder size.
// It avoids memory allocation during stackwalk.
//
class MethodInfo
{
    TADDR dummyPtrs[5];
    Int32 dummyInts[8];
};

class EHEnumState
{
    TADDR dummyPtrs[2];
    Int32 dummyInts[2];
};

enum EHClauseKind
{
    EH_CLAUSE_TYPED = 0,
    EH_CLAUSE_FAULT = 1,

    // Local Exceptions
    EH_CLAUSE_METHOD_BOUNDARY = 2,
    EH_CLAUSE_FAIL_FAST = 3,

    // CLR Exceptions
    EH_CLAUSE_FILTER = 2,
    EH_CLAUSE_UNUSED = 3,
};

struct EHClause
{
    EHClauseKind m_clauseKind;
    UInt32 m_tryStartOffset;
    UInt32 m_tryEndOffset;
    UInt32 m_filterOffset;
    UInt32 m_handlerOffset;
    void* m_pTargetType;
};

class ICodeManager
{
public:
    virtual bool FindMethodInfo(PTR_VOID        ControlPC, 
                                MethodInfo *    pMethodInfoOut,
                                UInt32 *        pCodeOffset) = 0;

    virtual bool IsFunclet(MethodInfo * pMethodInfo) = 0;

    virtual PTR_VOID GetFramePointer(MethodInfo *   pMethodInfo,
                                     REGDISPLAY *   pRegisterSet) = 0;

    virtual void EnumGcRefs(MethodInfo *    pMethodInfo, 
                            UInt32          codeOffset,
                            REGDISPLAY *    pRegisterSet,
                            GCEnumContext * hCallback) = 0;

    virtual bool UnwindStackFrame(MethodInfo *    pMethodInfo,
                                  UInt32          codeOffset,
                                  REGDISPLAY *    pRegisterSet,                     // in/out
                                  PTR_VOID *      ppPreviousTransitionFrame) = 0;   // out

    virtual UIntNative GetConservativeUpperBoundForOutgoingArgs(MethodInfo *   pMethodInfo,
                                                                REGDISPLAY *   pRegisterSet) = 0;

    virtual bool GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                            UInt32          codeOffset,
                                            REGDISPLAY *    pRegisterSet,           // in
                                            PTR_PTR_VOID *  ppvRetAddrLocation,     // out
                                            GCRefKind *     pRetValueKind) = 0;     // out

    virtual void UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo) = 0;

    virtual void RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, UInt32 * pCodeOffset) = 0;

    virtual bool EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumState) = 0;

    virtual bool EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClause) = 0;
};
