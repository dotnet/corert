// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#if defined(_TARGET_AMD64_)
struct T_RUNTIME_FUNCTION {
    uint32_t BeginAddress;
    uint32_t EndAddress;
    uint32_t UnwindInfoAddress;
};
#elif defined(_TARGET_ARM_)
struct T_RUNTIME_FUNCTION {
    uint32_t BeginAddress;
    uint32_t UnwindData;
};
#elif defined(_TARGET_ARM64_)
struct T_RUNTIME_FUNCTION {
    uint32_t BeginAddress;
    union {
        uint32_t UnwindData;
        struct {
            uint32_t Flag : 2;
            uint32_t FunctionLength : 11;
            uint32_t RegF : 3;
            uint32_t RegI : 4;
            uint32_t H : 1;
            uint32_t CR : 2;
            uint32_t FrameSize : 9;
        } PackedUnwindData;
    };
};
#else
#error unexpected target architecture
#endif

typedef DPTR(T_RUNTIME_FUNCTION) PTR_RUNTIME_FUNCTION;

class CoffNativeCodeManager : public ICodeManager
{
    TADDR m_moduleBase;
    PTR_RUNTIME_FUNCTION m_pRuntimeFunctionTable;
    UInt32 m_nRuntimeFunctionTable;

    PTR_PTR_VOID m_pClasslibFunctions;
    UInt32 m_nClasslibFunctions;

public:
    CoffNativeCodeManager(TADDR moduleBase, 
                          PTR_RUNTIME_FUNCTION pRuntimeFunctionTable, UInt32 nRuntimeFunctionTable,
                          PTR_PTR_VOID pClasslibFunctions, UInt32 nClasslibFunctions);
    ~CoffNativeCodeManager();

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
