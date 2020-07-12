// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#include "common.h"

#include <windows.h>

#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "regdisplay.h"
#include "ICodeManager.h"
#include "CoffNativeCodeManager.h"
#include "varint.h"
#include "holder.h"

#include "CommonMacros.inl"

#define GCINFODECODER_NO_EE
#include "coreclr/gcinfodecoder.cpp"

#define UBF_FUNC_KIND_MASK      0x03
#define UBF_FUNC_KIND_ROOT      0x00
#define UBF_FUNC_KIND_HANDLER   0x01
#define UBF_FUNC_KIND_FILTER    0x02

#define UBF_FUNC_HAS_EHINFO             0x04
#define UBF_FUNC_REVERSE_PINVOKE        0x08
#define UBF_FUNC_HAS_ASSOCIATED_DATA    0x10

#ifdef TARGET_X86
//
// x86 ABI does not define RUNTIME_FUNCTION. Define our own to allow unification between x86 and other platforms.
//
typedef struct _RUNTIME_FUNCTION {
    DWORD BeginAddress;
    DWORD EndAddress;
    DWORD UnwindData;
} RUNTIME_FUNCTION, *PRUNTIME_FUNCTION;

typedef struct _KNONVOLATILE_CONTEXT_POINTERS {

    // The ordering of these fields should be aligned with that
    // of corresponding fields in CONTEXT
    //
    // (See REGDISPLAY in Runtime/regdisp.h for details)
    PDWORD Edi;
    PDWORD Esi;
    PDWORD Ebx;
    PDWORD Edx;
    PDWORD Ecx;
    PDWORD Eax;

    PDWORD Ebp;

} KNONVOLATILE_CONTEXT_POINTERS, *PKNONVOLATILE_CONTEXT_POINTERS;

typedef struct _UNWIND_INFO {
    ULONG FunctionLength;
} UNWIND_INFO, *PUNWIND_INFO;

#elif defined(TARGET_AMD64)

#define UNW_FLAG_NHANDLER 0x0
#define UNW_FLAG_EHANDLER 0x1
#define UNW_FLAG_UHANDLER 0x2
#define UNW_FLAG_CHAININFO 0x4

//
// The following structures are defined in Windows x64 unwind info specification
// http://www.bing.com/search?q=msdn+Exception+Handling+x64
//
typedef union _UNWIND_CODE {
    struct {
        uint8_t CodeOffset;
        uint8_t UnwindOp : 4;
        uint8_t OpInfo : 4;
    };

    uint16_t FrameOffset;
} UNWIND_CODE, *PUNWIND_CODE;

typedef struct _UNWIND_INFO {
    uint8_t Version : 3;
    uint8_t Flags : 5;
    uint8_t SizeOfProlog;
    uint8_t CountOfUnwindCodes;
    uint8_t FrameRegister : 4;
    uint8_t FrameOffset : 4;
    UNWIND_CODE UnwindCode[1];
} UNWIND_INFO, *PUNWIND_INFO;

#endif // TARGET_X86

typedef DPTR(struct _UNWIND_INFO)      PTR_UNWIND_INFO;
typedef DPTR(union _UNWIND_CODE)       PTR_UNWIND_CODE;

static PTR_VOID GetUnwindDataBlob(TADDR moduleBase, PTR_RUNTIME_FUNCTION pRuntimeFunction, /* out */ size_t * pSize)
{
#if defined(TARGET_AMD64)
    PTR_UNWIND_INFO pUnwindInfo(dac_cast<PTR_UNWIND_INFO>(moduleBase + pRuntimeFunction->UnwindInfoAddress));

    size_t size = offsetof(UNWIND_INFO, UnwindCode) + sizeof(UNWIND_CODE) * pUnwindInfo->CountOfUnwindCodes;

    // Chained unwind info is not supported at this time
    ASSERT((pUnwindInfo->Flags & UNW_FLAG_CHAININFO) == 0);

    if (pUnwindInfo->Flags & (UNW_FLAG_EHANDLER | UNW_FLAG_UHANDLER))
    {
        // Personality routine
        size = ALIGN_UP(size, sizeof(DWORD)) + sizeof(DWORD);
    }

    *pSize = size;

    return pUnwindInfo;

#elif defined(TARGET_X86)

    PTR_UNWIND_INFO pUnwindInfo(dac_cast<PTR_UNWIND_INFO>(moduleBase + pRuntimeFunction->UnwindInfoAddress));

    *pSize = sizeof(UNWIND_INFO);

    return pUnwindInfo;

#elif defined(TARGET_ARM)

    // if this function uses packed unwind data then at least one of the two least significant bits
    // will be non-zero.  if this is the case then there will be no xdata record to enumerate.
    ASSERT((pRuntimeFunction->UnwindData & 0x3) == 0);

    // compute the size of the unwind info
    PTR_TADDR xdata = dac_cast<PTR_TADDR>(pRuntimeFunction->UnwindData + moduleBase);

    ULONG epilogScopes = 0;
    ULONG unwindWords = 0;
    ULONG size = 0;

    if ((xdata[0] >> 23) != 0)
    {
        size = 4;
        epilogScopes = (xdata[0] >> 23) & 0x1f;
        unwindWords = (xdata[0] >> 28) & 0x0f;
    }
    else
    {
        size = 8;
        epilogScopes = xdata[1] & 0xffff;
        unwindWords = (xdata[1] >> 16) & 0xff;
    }

    if (!(xdata[0] & (1 << 21)))
        size += 4 * epilogScopes;

    size += 4 * unwindWords;

    if ((xdata[0] & (1 << 20)) != 0)
    {
        // Personality routine
        size += 4;
    }

    *pSize = size;
    return xdata;
#else
    PORTABILITY_ASSERT("GetUnwindDataBlob");
    *pSize = 0;
    return NULL;
#endif
}


CoffNativeCodeManager::CoffNativeCodeManager(TADDR moduleBase, 
                                             PTR_VOID pvManagedCodeStartRange, UInt32 cbManagedCodeRange,
                                             PTR_RUNTIME_FUNCTION pRuntimeFunctionTable, UInt32 nRuntimeFunctionTable,
                                             PTR_PTR_VOID pClasslibFunctions, UInt32 nClasslibFunctions)
    : m_moduleBase(moduleBase), 
      m_pvManagedCodeStartRange(pvManagedCodeStartRange), m_cbManagedCodeRange(cbManagedCodeRange),
      m_pRuntimeFunctionTable(pRuntimeFunctionTable), m_nRuntimeFunctionTable(nRuntimeFunctionTable),
      m_pClasslibFunctions(pClasslibFunctions), m_nClasslibFunctions(nClasslibFunctions)
{
}

CoffNativeCodeManager::~CoffNativeCodeManager()
{
}

static int LookupUnwindInfoForMethod(UInt32 relativePc,
                                     PTR_RUNTIME_FUNCTION pRuntimeFunctionTable,
                                     int low,
                                     int high)
{
#ifdef TARGET_ARM
    relativePc |= THUMB_CODE;
#endif 

    // Binary search the RUNTIME_FUNCTION table
    // Use linear search once we get down to a small number of elements
    // to avoid Binary search overhead.
    while (high - low > 10) 
    {
       int middle = low + (high - low) / 2;

       PTR_RUNTIME_FUNCTION pFunctionEntry = pRuntimeFunctionTable + middle;
       if (relativePc < pFunctionEntry->BeginAddress) 
       {
           high = middle - 1;
       } 
       else 
       {
           low = middle;
       }
    }

    for (int i = low; i < high; i++)
    {
        PTR_RUNTIME_FUNCTION pNextFunctionEntry = pRuntimeFunctionTable + (i + 1);
        if (relativePc < pNextFunctionEntry->BeginAddress)
        {
            high = i;
            break;
        }
    }

    PTR_RUNTIME_FUNCTION pFunctionEntry = pRuntimeFunctionTable + high;
    if (relativePc >= pFunctionEntry->BeginAddress)
    {
        return high;
    }

    ASSERT_UNCONDITIONALLY("Invalid code address");
    return -1;
}

struct CoffNativeMethodInfo
{
    PTR_RUNTIME_FUNCTION mainRuntimeFunction;
    PTR_RUNTIME_FUNCTION runtimeFunction;
    bool executionAborted;
};

// Ensure that CoffNativeMethodInfo fits into the space reserved by MethodInfo
static_assert(sizeof(CoffNativeMethodInfo) <= sizeof(MethodInfo), "CoffNativeMethodInfo too big");

bool CoffNativeCodeManager::FindMethodInfo(PTR_VOID        ControlPC, 
                                           MethodInfo *    pMethodInfoOut)
{
    // Stackwalker may call this with ControlPC that does not belong to this code manager
    if (dac_cast<TADDR>(ControlPC) < dac_cast<TADDR>(m_pvManagedCodeStartRange) ||
        dac_cast<TADDR>(m_pvManagedCodeStartRange) + m_cbManagedCodeRange <= dac_cast<TADDR>(ControlPC))
    {
        return false;
    }

    CoffNativeMethodInfo * pMethodInfo = (CoffNativeMethodInfo *)pMethodInfoOut;

    TADDR relativePC = dac_cast<TADDR>(ControlPC) - m_moduleBase;

    int MethodIndex = LookupUnwindInfoForMethod((UInt32)relativePC, m_pRuntimeFunctionTable,
        0, m_nRuntimeFunctionTable - 1);
    if (MethodIndex < 0)
        return false;

    PTR_RUNTIME_FUNCTION pRuntimeFunction = m_pRuntimeFunctionTable + MethodIndex;

    pMethodInfo->runtimeFunction = pRuntimeFunction;

    // The runtime function could correspond to a funclet.  We need to get to the 
    // runtime function of the main method.
    for (;;)
    {
        size_t unwindDataBlobSize;
        PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pRuntimeFunction, &unwindDataBlobSize);

        uint8_t unwindBlockFlags = *(dac_cast<DPTR(uint8_t)>(pUnwindDataBlob) + unwindDataBlobSize);
        if ((unwindBlockFlags & UBF_FUNC_KIND_MASK) == UBF_FUNC_KIND_ROOT)
            break;

        pRuntimeFunction--;
    }

    pMethodInfo->mainRuntimeFunction = pRuntimeFunction;

    pMethodInfo->executionAborted = false;

    return true;
}

bool CoffNativeCodeManager::IsFunclet(MethodInfo * pMethInfo)
{
    CoffNativeMethodInfo * pMethodInfo = (CoffNativeMethodInfo *)pMethInfo;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pMethodInfo->runtimeFunction, &unwindDataBlobSize);

    uint8_t unwindBlockFlags = *(dac_cast<DPTR(uint8_t)>(pUnwindDataBlob) + unwindDataBlobSize);

    // A funclet will have an entry in funclet to main method map
    return (unwindBlockFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT;
}

bool CoffNativeCodeManager::IsFilter(MethodInfo * pMethInfo)
{
    CoffNativeMethodInfo * pMethodInfo = (CoffNativeMethodInfo *)pMethInfo;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pMethodInfo->runtimeFunction, &unwindDataBlobSize);

    uint8_t unwindBlockFlags = *(dac_cast<DPTR(uint8_t)>(pUnwindDataBlob) + unwindDataBlobSize);

    return (unwindBlockFlags & UBF_FUNC_KIND_MASK) == UBF_FUNC_KIND_FILTER;
}

PTR_VOID CoffNativeCodeManager::GetFramePointer(MethodInfo *   pMethInfo,
                                         REGDISPLAY *   pRegisterSet)
{
    CoffNativeMethodInfo * pMethodInfo = (CoffNativeMethodInfo *)pMethInfo;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pMethodInfo->runtimeFunction, &unwindDataBlobSize);

    uint8_t unwindBlockFlags = *(dac_cast<DPTR(uint8_t)>(pUnwindDataBlob) + unwindDataBlobSize);

    // Return frame pointer for methods with EH and funclets
    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) != 0 || (unwindBlockFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT)
    {
        return (PTR_VOID)pRegisterSet->GetFP();
    }

    return NULL;
}

void CoffNativeCodeManager::EnumGcRefs(MethodInfo *    pMethodInfo, 
                                       PTR_VOID        safePointAddress,
                                       REGDISPLAY *    pRegisterSet,
                                       GCEnumContext * hCallback)
{
    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pNativeMethodInfo->mainRuntimeFunction, &unwindDataBlobSize);

    PTR_UInt8 p = dac_cast<PTR_UInt8>(pUnwindDataBlob) + unwindDataBlobSize;

    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) != 0)
        p += sizeof(int32_t);

    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) != 0)
        p += sizeof(int32_t);

    TADDR methodStartAddress = m_moduleBase + pNativeMethodInfo->mainRuntimeFunction->BeginAddress;
    UInt32 codeOffset = (UInt32)(dac_cast<TADDR>(safePointAddress) - methodStartAddress);

    GcInfoDecoder decoder(
        GCInfoToken(p),
        GcInfoDecoderFlags(DECODE_GC_LIFETIMES | DECODE_SECURITY_OBJECT | DECODE_VARARG),
        codeOffset - 1 // TODO: Is this adjustment correct?
        );

    ICodeManagerFlags flags = (ICodeManagerFlags)0;
    if (pNativeMethodInfo->executionAborted)
        flags = ICodeManagerFlags::ExecutionAborted;
    if (IsFilter(pMethodInfo))
        flags = (ICodeManagerFlags)(flags | ICodeManagerFlags::NoReportUntracked);

    if (!decoder.EnumerateLiveSlots(
        pRegisterSet,
        false /* reportScratchSlots */, 
        flags,
        hCallback->pCallback,
        hCallback
        ))
    {
        assert(false);
    }
}

UIntNative CoffNativeCodeManager::GetConservativeUpperBoundForOutgoingArgs(MethodInfo * pMethodInfo, REGDISPLAY * pRegisterSet)
{
#if defined(TARGET_AMD64)

    // Return value
    UIntNative upperBound;
    CoffNativeMethodInfo* pNativeMethodInfo = (CoffNativeMethodInfo *) pMethodInfo;
    
    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pNativeMethodInfo->runtimeFunction, &unwindDataBlobSize);
    PTR_UInt8 p = dac_cast<PTR_UInt8>(pUnwindDataBlob) + unwindDataBlobSize;
    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) != 0)
        p += sizeof(int32_t);

    if ((unwindBlockFlags & UBF_FUNC_REVERSE_PINVOKE) != 0)
    {
        TADDR basePointer =  dac_cast<TADDR>(pRegisterSet->GetFP());        
        
        // Get the method's GC info
        GcInfoDecoder decoder(GCInfoToken(p), DECODE_REVERSE_PINVOKE_VAR);
        UINT32 stackBasedRegister = decoder.GetStackBaseRegister();
        
        if (stackBasedRegister == NO_STACK_BASE_REGISTER)
        {
            basePointer = dac_cast<TADDR>(pRegisterSet->GetSP());
        }
        else
        {
            basePointer = dac_cast<TADDR>(pRegisterSet->GetFP());
        }
        // Reverse PInvoke case.  The embedded reverse PInvoke frame is guaranteed to reside above
        // all outgoing arguments.
        INT32 slot = decoder.GetReversePInvokeFrameStackSlot();
        upperBound =  (UIntNative) dac_cast<TADDR>(basePointer + slot);
    }
    else
    {
        // Check for a pushed RBP value
        if (GetFramePointer(pMethodInfo, pRegisterSet) == NULL)
        {
            // Unwind the current method context to get the caller's stack pointer
            // and obtain the upper bound of the callee is the value just below the caller's return address on the stack
            SIZE_T  EstablisherFrame;
            PVOID   HandlerData;
            CONTEXT context;
            context.Rsp = pRegisterSet->GetSP();
            context.Rbp = pRegisterSet->GetFP();
            context.Rip = pRegisterSet->GetIP();
    
            RtlVirtualUnwind(NULL,
                            dac_cast<TADDR>(m_moduleBase),
                            pRegisterSet->IP,
                            (PRUNTIME_FUNCTION)pNativeMethodInfo->runtimeFunction,
                            &context,
                            &HandlerData,
                            &EstablisherFrame,
                            NULL);

            upperBound = dac_cast<TADDR>(context.Rsp - sizeof (PVOID));
        }
        else
        {
            // In amd64, it is guaranteed that if there is a pushed RBP
            // value at the top of the frame it resides above all outgoing arguments.  Unlike x86,
            // the frame pointer generally points to a location that is separated from the pushed RBP
            // value by an offset that is recorded in the info header.  Recover the address of the
            // pushed RBP value by subtracting this offset.
            upperBound = (UIntNative) dac_cast<TADDR>(pRegisterSet->GetFP() - ((PTR_UNWIND_INFO) pUnwindDataBlob)->FrameOffset);
        }
    }
    return upperBound;
#else
    assert(false);
    return false;
#endif
}

bool CoffNativeCodeManager::UnwindStackFrame(MethodInfo *    pMethodInfo,
                                      REGDISPLAY *    pRegisterSet,                 // in/out
                                      PTR_VOID *      ppPreviousTransitionFrame)    // out
{
    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pNativeMethodInfo->runtimeFunction, &unwindDataBlobSize);

    PTR_UInt8 p = dac_cast<PTR_UInt8>(pUnwindDataBlob) + unwindDataBlobSize;

    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) != 0)
        p += sizeof(int32_t);

    if ((unwindBlockFlags & UBF_FUNC_REVERSE_PINVOKE) != 0)
    {
        // Reverse PInvoke transition should be on the main function body only
        assert(pNativeMethodInfo->mainRuntimeFunction == pNativeMethodInfo->runtimeFunction);

        if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) != 0)
            p += sizeof(int32_t);

        GcInfoDecoder decoder(GCInfoToken(p), DECODE_REVERSE_PINVOKE_VAR);
        INT32 slot = decoder.GetReversePInvokeFrameStackSlot();
        assert(slot != NO_REVERSE_PINVOKE_FRAME);

        TADDR basePointer = NULL;
        UINT32 stackBasedRegister = decoder.GetStackBaseRegister();
        if (stackBasedRegister == NO_STACK_BASE_REGISTER)
        {
            basePointer = dac_cast<TADDR>(pRegisterSet->GetSP());
        }
        else
        {
            basePointer = dac_cast<TADDR>(pRegisterSet->GetFP());
        }
        *ppPreviousTransitionFrame = *(void**)(basePointer + slot);
        return true;
    }

    *ppPreviousTransitionFrame = NULL;

    CONTEXT context;
    KNONVOLATILE_CONTEXT_POINTERS contextPointers;

#ifdef _DEBUG
    memset(&context, 0xDD, sizeof(context));
    memset(&contextPointers, 0xDD, sizeof(contextPointers));
#endif

#ifdef TARGET_X86
    #define FOR_EACH_NONVOLATILE_REGISTER(F) \
        F(E, ax) F(E, cx) F(E, dx) F(E, bx) F(E, bp) F(E, si) F(E, di)
    #define WORDPTR PDWORD
#else
    #define FOR_EACH_NONVOLATILE_REGISTER(F) \
        F(R, ax) F(R, cx) F(R, dx) F(R, bx) F(R, bp) F(R, si) F(R, di) \
        F(R, 8) F(R, 9) F(R, 10) F(R, 11) F(R, 12) F(R, 13) F(R, 14) F(R, 15)
    #define WORDPTR PDWORD64
#endif

#define REGDISPLAY_TO_CONTEXT(prefix, reg) \
    contextPointers.prefix####reg = (WORDPTR) pRegisterSet->pR##reg; \
    if (pRegisterSet->pR##reg != NULL) context.prefix##reg = *(pRegisterSet->pR##reg);

#define CONTEXT_TO_REGDISPLAY(prefix, reg) \
    pRegisterSet->pR##reg = (PTR_UIntNative) contextPointers.prefix####reg;

    FOR_EACH_NONVOLATILE_REGISTER(REGDISPLAY_TO_CONTEXT);

#ifdef TARGET_X86
    PORTABILITY_ASSERT("CoffNativeCodeManager::UnwindStackFrame");
#else // TARGET_X86
    memcpy(&context.Xmm6, pRegisterSet->Xmm, sizeof(pRegisterSet->Xmm));

    context.Rsp = pRegisterSet->SP;
    context.Rip = pRegisterSet->IP;

    SIZE_T  EstablisherFrame;
    PVOID   HandlerData;

    RtlVirtualUnwind(NULL,
                    dac_cast<TADDR>(m_moduleBase),
                    pRegisterSet->IP,
                    (PRUNTIME_FUNCTION)pNativeMethodInfo->runtimeFunction,
                    &context,
                    &HandlerData,
                    &EstablisherFrame,
                    &contextPointers);

    pRegisterSet->SP = context.Rsp;
    pRegisterSet->IP = context.Rip;

    pRegisterSet->pIP = PTR_PCODE(pRegisterSet->SP - sizeof(TADDR));

    memcpy(pRegisterSet->Xmm, &context.Xmm6, sizeof(pRegisterSet->Xmm));
#endif // TARGET_X86

    FOR_EACH_NONVOLATILE_REGISTER(CONTEXT_TO_REGDISPLAY);

#undef FOR_EACH_NONVOLATILE_REGISTER
#undef REGDISPLAY_TO_CONTEXT
#undef CONTEXT_TO_REGDISPLAY

    return true;
}

// Convert the return kind that was encoded by RyuJIT to the
// value that CoreRT runtime can understand and support.
GCRefKind GetGcRefKind(ReturnKind returnKind)
{
    static_assert((GCRefKind)ReturnKind::RT_Scalar == GCRK_Scalar, "ReturnKind::RT_Scalar does not match GCRK_Scalar");
    static_assert((GCRefKind)ReturnKind::RT_Object == GCRK_Object, "ReturnKind::RT_Object does not match GCRK_Object");
    static_assert((GCRefKind)ReturnKind::RT_ByRef  == GCRK_Byref, "ReturnKind::RT_ByRef does not match GCRK_Byref");
    ASSERT((returnKind == RT_Scalar) || (returnKind == GCRK_Object) || (returnKind == GCRK_Byref));

    return (GCRefKind)returnKind;
}

bool CoffNativeCodeManager::GetReturnAddressHijackInfo(MethodInfo *    pMethodInfo,
                                                REGDISPLAY *    pRegisterSet,       // in
                                                PTR_PTR_VOID *  ppvRetAddrLocation, // out
                                                GCRefKind *     pRetValueKind)      // out
{
#if defined(TARGET_AMD64)
    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pNativeMethodInfo->runtimeFunction, &unwindDataBlobSize);

    PTR_UInt8 p = dac_cast<PTR_UInt8>(pUnwindDataBlob) + unwindDataBlobSize;

    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) != 0)
        p += sizeof(int32_t);

    // Check whether this is a funclet
    if ((unwindBlockFlags & UBF_FUNC_KIND_MASK) != UBF_FUNC_KIND_ROOT)
        return false;

    // Skip hijacking a reverse-pinvoke method - it doesn't get us much because we already synchronize
    // with the GC on the way back to native code.
    if ((unwindBlockFlags & UBF_FUNC_REVERSE_PINVOKE) != 0)
        return false;

    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) != 0)
        p += sizeof(int32_t);

    // Decode the GC info for the current method to determine its return type
    GcInfoDecoder decoder(
        GCInfoToken(p),
        GcInfoDecoderFlags(DECODE_RETURN_KIND),
        0
        );

    GCRefKind gcRefKind = GetGcRefKind(decoder.GetReturnKind());

    // Unwind the current method context to the caller's context to get its stack pointer
    // and obtain the location of the return address on the stack
    SIZE_T  EstablisherFrame;
    PVOID   HandlerData;
    CONTEXT context;
    context.Rsp = pRegisterSet->GetSP();
    context.Rbp = pRegisterSet->GetFP();
    context.Rip = pRegisterSet->GetIP();

    RtlVirtualUnwind(NULL,
                    dac_cast<TADDR>(m_moduleBase),
                    pRegisterSet->IP,
                    (PRUNTIME_FUNCTION)pNativeMethodInfo->runtimeFunction,
                    &context,
                    &HandlerData,
                    &EstablisherFrame,
                    NULL);

    *ppvRetAddrLocation = (PTR_PTR_VOID)(context.Rsp - sizeof (PVOID));
    *pRetValueKind = gcRefKind;
    return true;
#else
    return false;
#endif // defined(TARGET_AMD64)
}

void CoffNativeCodeManager::UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo)
{
    // @TODO: CORERT: UnsynchronizedHijackMethodLoops
}

PTR_VOID CoffNativeCodeManager::RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC)
{
    // GCInfo decoder needs to know whether execution of the method is aborted 
    // while querying for gc-info.  But ICodeManager::EnumGCRef() doesn't receive any
    // flags from mrt. Call to this method is used as a cue to mark the method info
    // as execution aborted. Note - if pMethodInfo was cached, this scheme would not work.
    //
    // If the method has EH, then JIT will make sure the method is fully interruptible
    // and we will have GC-info available at the faulting address as well.

    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;
    pNativeMethodInfo->executionAborted = true;

    return controlPC;
}

struct CoffEHEnumState
{
    PTR_UInt8 pMethodStartAddress;
    PTR_UInt8 pEHInfo;
    UInt32 uClause;
    UInt32 nClauses;
};

// Ensure that CoffEHEnumState fits into the space reserved by EHEnumState
static_assert(sizeof(CoffEHEnumState) <= sizeof(EHEnumState), "CoffEHEnumState too big");

bool CoffNativeCodeManager::EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddress, EHEnumState * pEHEnumStateOut)
{
    assert(pMethodInfo != NULL);
    assert(pMethodStartAddress != NULL);
    assert(pEHEnumStateOut != NULL);

    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;
    CoffEHEnumState * pEnumState = (CoffEHEnumState *)pEHEnumStateOut;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pNativeMethodInfo->mainRuntimeFunction, &unwindDataBlobSize);

    PTR_UInt8 p = dac_cast<PTR_UInt8>(pUnwindDataBlob) + unwindDataBlobSize;

    uint8_t unwindBlockFlags = *p++;

    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) != 0)
        p += sizeof(int32_t);

    // return if there is no EH info associated with this method
    if ((unwindBlockFlags & UBF_FUNC_HAS_EHINFO) == 0)
    {
        return false;
    }

    *pMethodStartAddress = dac_cast<PTR_VOID>(m_moduleBase + pNativeMethodInfo->mainRuntimeFunction->BeginAddress);

    pEnumState->pMethodStartAddress = dac_cast<PTR_UInt8>(*pMethodStartAddress);
    pEnumState->pEHInfo = dac_cast<PTR_UInt8>(m_moduleBase + *dac_cast<PTR_Int32>(p));
    pEnumState->uClause = 0;
    pEnumState->nClauses = VarInt::ReadUnsigned(pEnumState->pEHInfo);

    return true;
}

bool CoffNativeCodeManager::EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClauseOut)
{
    assert(pEHEnumState != NULL);
    assert(pEHClauseOut != NULL);

    CoffEHEnumState * pEnumState = (CoffEHEnumState *)pEHEnumState;
    if (pEnumState->uClause >= pEnumState->nClauses)
        return false;
    pEnumState->uClause++;

    pEHClauseOut->m_tryStartOffset = VarInt::ReadUnsigned(pEnumState->pEHInfo);

    UInt32 tryEndDeltaAndClauseKind = VarInt::ReadUnsigned(pEnumState->pEHInfo);
    pEHClauseOut->m_clauseKind = (EHClauseKind)(tryEndDeltaAndClauseKind & 0x3);
    pEHClauseOut->m_tryEndOffset = pEHClauseOut->m_tryStartOffset + (tryEndDeltaAndClauseKind >> 2);

    // For each clause, we have up to 4 integers:
    //      1)  try start offset
    //      2)  (try length << 2) | clauseKind
    //      3)  if (typed || fault || filter)    { handler start offset }
    //      4a) if (typed)                       { type RVA }
    //      4b) if (filter)                      { filter start offset }
    //
    // The first two integers have already been decoded

    switch (pEHClauseOut->m_clauseKind)
    {
    case EH_CLAUSE_TYPED:
        pEHClauseOut->m_handlerAddress = pEnumState->pMethodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);

        // Read target type
        {
            // @TODO: CORERT: Compress EHInfo using type table index scheme
            // https://github.com/dotnet/corert/issues/972
            UInt32 typeRVA = *((PTR_UInt32&)pEnumState->pEHInfo)++;
            pEHClauseOut->m_pTargetType = dac_cast<PTR_VOID>(m_moduleBase + typeRVA);
        }
        break;
    case EH_CLAUSE_FAULT:
        pEHClauseOut->m_handlerAddress = pEnumState->pMethodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        break;
    case EH_CLAUSE_FILTER:
        pEHClauseOut->m_handlerAddress = pEnumState->pMethodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        pEHClauseOut->m_filterAddress = pEnumState->pMethodStartAddress + VarInt::ReadUnsigned(pEnumState->pEHInfo);
        break;
    default:
        UNREACHABLE_MSG("unexpected EHClauseKind");
    }

    return true;
}

PTR_VOID CoffNativeCodeManager::GetOsModuleHandle()
{
    return dac_cast<PTR_VOID>(m_moduleBase);
}

PTR_VOID CoffNativeCodeManager::GetMethodStartAddress(MethodInfo * pMethodInfo)
{
    CoffNativeMethodInfo * pNativeMethodInfo = (CoffNativeMethodInfo *)pMethodInfo;
    return dac_cast<PTR_VOID>(m_moduleBase + pNativeMethodInfo->mainRuntimeFunction->BeginAddress);
}

void * CoffNativeCodeManager::GetClasslibFunction(ClasslibFunctionId functionId)
{
    uint32_t id = (uint32_t)functionId;

    if (id >= m_nClasslibFunctions)
        return nullptr;

    return m_pClasslibFunctions[id];
}

PTR_VOID CoffNativeCodeManager::GetAssociatedData(PTR_VOID ControlPC)
{
    if (dac_cast<TADDR>(ControlPC) < dac_cast<TADDR>(m_pvManagedCodeStartRange) || 
        dac_cast<TADDR>(m_pvManagedCodeStartRange) + m_cbManagedCodeRange <= dac_cast<TADDR>(ControlPC))
    {
        return NULL;
    }

    TADDR relativePC = dac_cast<TADDR>(ControlPC) - m_moduleBase;

    int MethodIndex = LookupUnwindInfoForMethod((UInt32)relativePC, m_pRuntimeFunctionTable, 0, m_nRuntimeFunctionTable - 1);
    if (MethodIndex < 0)
        return NULL;

    PTR_RUNTIME_FUNCTION pRuntimeFunction = m_pRuntimeFunctionTable + MethodIndex;

    size_t unwindDataBlobSize;
    PTR_VOID pUnwindDataBlob = GetUnwindDataBlob(m_moduleBase, pRuntimeFunction, &unwindDataBlobSize);

    PTR_UInt8 p = dac_cast<PTR_UInt8>(pUnwindDataBlob) + unwindDataBlobSize;

    uint8_t unwindBlockFlags = *p++;
    if ((unwindBlockFlags & UBF_FUNC_HAS_ASSOCIATED_DATA) == 0)
        return NULL;

    UInt32 dataRVA = *(UInt32*)p;
    return dac_cast<PTR_VOID>(m_moduleBase + dataRVA);
}

extern "C" bool __stdcall RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange);
extern "C" void __stdcall UnregisterCodeManager(ICodeManager * pCodeManager);
extern "C" bool __stdcall RegisterUnboxingStubs(PTR_VOID pvStartRange, UInt32 cbRange);

extern "C"
bool RhRegisterOSModule(void * pModule,
                        void * pvManagedCodeStartRange, UInt32 cbManagedCodeRange,
                        void * pvUnboxingStubsStartRange, UInt32 cbUnboxingStubsRange,
                        void ** pClasslibFunctions, UInt32 nClasslibFunctions)
{
    PIMAGE_DOS_HEADER pDosHeader = (PIMAGE_DOS_HEADER)pModule;
    PIMAGE_NT_HEADERS pNTHeaders = (PIMAGE_NT_HEADERS)((TADDR)pModule + pDosHeader->e_lfanew);

    IMAGE_DATA_DIRECTORY * pRuntimeFunctions = &(pNTHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION]);

    NewHolder<CoffNativeCodeManager> pCoffNativeCodeManager = new (nothrow) CoffNativeCodeManager((TADDR)pModule,
        pvManagedCodeStartRange, cbManagedCodeRange,
        dac_cast<PTR_RUNTIME_FUNCTION>((TADDR)pModule + pRuntimeFunctions->VirtualAddress),
        pRuntimeFunctions->Size / sizeof(RUNTIME_FUNCTION),
        pClasslibFunctions, nClasslibFunctions);

    if (pCoffNativeCodeManager == nullptr)
        return false;

    if (!RegisterCodeManager(pCoffNativeCodeManager, pvManagedCodeStartRange, cbManagedCodeRange))
        return false;

    if (!RegisterUnboxingStubs(pvUnboxingStubsStartRange, cbUnboxingStubsRange))
    {
        UnregisterCodeManager(pCoffNativeCodeManager);
        return false;
    }

    pCoffNativeCodeManager.SuppressRelease();

    return true;
}
