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

public:
    CoffNativeCodeManager(TADDR moduleBase, PTR_RUNTIME_FUNCTION pRuntimeFunctionTable, UInt32 nRuntimeFunctionTable);
    ~CoffNativeCodeManager();

    //
    // Code manager methods
    //

    bool FindMethodInfo(PTR_VOID        ControlPC, 
                        MethodInfo *    pMethodInfoOut,
                        UInt32 *        pCodeOffset);

    bool IsFunclet(MethodInfo * pMethodInfo);

    PTR_VOID GetFramePointer(MethodInfo *   pMethodInfo,
                             REGDISPLAY *   pRegisterSet);

    void EnumGcRefs(MethodInfo *    pMethodInfo, 
                    UInt32          codeOffset,
                    REGDISPLAY *    pRegisterSet,
                    GCEnumContext * hCallback);

    bool UnwindStackFrame(MethodInfo *    pMethodInfo,
                          UInt32          codeOffset,
                          REGDISPLAY *    pRegisterSet,                 // in/out
                          PTR_VOID *      ppPreviousTransitionFrame);   // out

    UIntNative GetConservativeUpperBoundForOutgoingArgs(MethodInfo *   pMethodInfo,
                                                        REGDISPLAY *   pRegisterSet);

    bool GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                    UInt32          codeOffset,
                                    REGDISPLAY *    pRegisterSet,       // in
                                    PTR_PTR_VOID *  ppvRetAddrLocation, // out
                                    GCRefKind *     pRetValueKind);     // out

    void UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo);

    void RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, UInt32 * pCodeOffset);

    bool EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumState);

    bool EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClause);
};
