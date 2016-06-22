// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

class UnixNativeCodeManager : public ICodeManager
{
    TADDR m_moduleBase;
    PTR_PTR_VOID m_pClasslibFunctions;
    UInt32 m_nClasslibFunctions;

public:
    UnixNativeCodeManager(TADDR moduleBase,
                         PTR_PTR_VOID pClasslibFunctions, UInt32 nClasslibFunctions);

    virtual ~UnixNativeCodeManager();

    //
    // Code manager methods
    //

    bool FindMethodInfo(PTR_VOID        ControlPC, 
                        MethodInfo *    pMethodInfoOut);

    bool IsFunclet(MethodInfo * pMethodInfo);

    PTR_VOID GetFramePointer(MethodInfo *   pMethodInfo,
                             REGDISPLAY *   pRegisterSet);

    void EnumGcRefs(MethodInfo *    pMethodInfo, 
                    PTR_VOID        safePointAddress,
                    REGDISPLAY *    pRegisterSet,
                    GCEnumContext * hCallback);

    bool UnwindStackFrame(MethodInfo *    pMethodInfo,
                          REGDISPLAY *    pRegisterSet,                 // in/out
                          PTR_VOID *      ppPreviousTransitionFrame);   // out

    UIntNative GetConservativeUpperBoundForOutgoingArgs(MethodInfo *   pMethodInfo,
                                                        REGDISPLAY *   pRegisterSet);

    bool GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                    REGDISPLAY *    pRegisterSet,       // in
                                    PTR_PTR_VOID *  ppvRetAddrLocation, // out
                                    GCRefKind *     pRetValueKind);     // out

    void UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo);

    PTR_VOID RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC);

    bool EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumState);

    bool EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClause);

    void * GetClasslibFunction(ClasslibFunctionId functionId);
};
