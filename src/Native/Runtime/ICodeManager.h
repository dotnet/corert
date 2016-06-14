// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
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
    EH_CLAUSE_FILTER = 2,
    EH_CLAUSE_UNUSED = 3,
};

struct EHClause
{
    EHClauseKind m_clauseKind;
    UInt32 m_tryStartOffset;
    UInt32 m_tryEndOffset;
    UInt8* m_filterAddress;
    UInt8* m_handlerAddress;
    void* m_pTargetType;
};

// Constants used with RhpGetClasslibFunction, to indicate which classlib function
// we are interested in. 
// Note: make sure you change the def in System\Runtime\exceptionhandling.cs if you change this!
enum class ClasslibFunctionId
{
    GetRuntimeException = 0,
    FailFast = 1,
    UnhandledExceptionHandler = 2,
    AppendExceptionStackFrame = 3,
};

class ICodeManager
{
public:
    virtual bool FindMethodInfo(PTR_VOID        ControlPC, 
                                MethodInfo *    pMethodInfoOut) = 0;

    virtual bool IsFunclet(MethodInfo * pMethodInfo) = 0;

    virtual PTR_VOID GetFramePointer(MethodInfo *   pMethodInfo,
                                     REGDISPLAY *   pRegisterSet) = 0;

    virtual void EnumGcRefs(MethodInfo *    pMethodInfo, 
                            PTR_VOID        safePointAddress,
                            REGDISPLAY *    pRegisterSet,
                            GCEnumContext * hCallback) = 0;

    virtual bool UnwindStackFrame(MethodInfo *    pMethodInfo,
                                  REGDISPLAY *    pRegisterSet,                     // in/out
                                  PTR_VOID *      ppPreviousTransitionFrame) = 0;   // out

    virtual UIntNative GetConservativeUpperBoundForOutgoingArgs(MethodInfo *   pMethodInfo,
                                                                REGDISPLAY *   pRegisterSet) = 0;

    virtual bool GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                            REGDISPLAY *    pRegisterSet,           // in
                                            PTR_PTR_VOID *  ppvRetAddrLocation,     // out
                                            GCRefKind *     pRetValueKind) = 0;     // out

    virtual void UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo) = 0;

    virtual PTR_VOID RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC) = 0;

    virtual bool EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumState) = 0;

    virtual bool EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClause) = 0;

    virtual void * GetClasslibFunction(ClasslibFunctionId functionId) = 0;
};
