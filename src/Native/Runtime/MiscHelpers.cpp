// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Miscellaneous unmanaged helpers called by managed code.
//

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
#include "slist.h"
#include "holder.h"
#include "Crst.h"
#include "rhbinder.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "regdisplay.h"
#include "gcrhinterface.h"
#include "varint.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "event.h"
#include "threadstore.h"
#include "threadstore.inl"
#include "thread.inl"
#include "gcrhinterface.h"
#include "shash.h"
#include "TypeManager.h"
#include "eetype.h"
#include "ObjectLayout.h"
#include "slist.inl"
#include "eetype.inl"
#include "CommonMacros.inl"
#include "volatile.h"
#include "GCMemoryHelpers.h"
#include "GCMemoryHelpers.inl"
#include "yieldprocessornormalized.h"

COOP_PINVOKE_HELPER(void, RhDebugBreak, ())
{
    PalDebugBreak();
}

// Busy spin for the given number of iterations.
COOP_PINVOKE_HELPER(void, RhSpinWait, (Int32 iterations))
{
    YieldProcessorNormalizationInfo normalizationInfo;
    YieldProcessorNormalizedForPreSkylakeCount(normalizationInfo, iterations);
}

// Yield the cpu to another thread ready to process, if one is available.
EXTERN_C REDHAWK_API UInt32_BOOL __cdecl RhYield()
{
    // This must be called via p/invoke -- it's a wait operation and we don't want to block thread suspension on this.
    ASSERT_MSG(!ThreadStore::GetCurrentThread()->IsCurrentThreadInCooperativeMode(),
        "You must p/invoke to RhYield");

    return PalSwitchToThread();
}

EXTERN_C REDHAWK_API void __cdecl RhFlushProcessWriteBuffers()
{
    // This must be called via p/invoke -- it's a wait operation and we don't want to block thread suspension on this.
    ASSERT_MSG(!ThreadStore::GetCurrentThread()->IsCurrentThreadInCooperativeMode(),
        "You must p/invoke to RhFlushProcessWriteBuffers");

    PalFlushProcessWriteBuffers();
}

// Get the list of currently loaded Redhawk modules (as OS HMODULE handles). The caller provides a reference
// to an array of pointer-sized elements and we return the total number of modules currently loaded (whether
// that is less than, equal to or greater than the number of elements in the array). If there are more modules
// loaded than the array will hold then the array is filled to capacity and the caller can tell further
// modules are available based on the return count. It is also possible to call this method without an array,
// in which case just the module count is returned (note that it's still possible for the module count to
// increase between calls to this method).
COOP_PINVOKE_HELPER(UInt32, RhGetLoadedOSModules, (Array * pResultArray))
{
    // Note that we depend on the fact that this is a COOP helper to make writing into an unpinned array safe.

    // If a result array is passed then it should be an array type with pointer-sized components that are not
    // GC-references.
    ASSERT(!pResultArray || pResultArray->get_EEType()->IsArray());
    ASSERT(!pResultArray || !pResultArray->get_EEType()->HasReferenceFields());
    ASSERT(!pResultArray || pResultArray->get_EEType()->get_ComponentSize() == sizeof(void*));

    UInt32 cResultArrayElements = pResultArray ? pResultArray->GetArrayLength() : 0;
    HANDLE * pResultElements = pResultArray ? (HANDLE*)(pResultArray + 1) : NULL;

    UInt32 cModules = 0;

    ReaderWriterLock::ReadHolder read(&GetRuntimeInstance()->GetTypeManagerLock());

    RuntimeInstance::OsModuleList *osModules = GetRuntimeInstance()->GetOsModuleList();
    
    for (RuntimeInstance::OsModuleList::Iterator iter = osModules->Begin(); iter != osModules->End(); iter++)
    {
        if (pResultArray && (cModules < cResultArrayElements))
            pResultElements[cModules] = iter->m_osModule;
        cModules++;
    }

    return cModules;
}

COOP_PINVOKE_HELPER(HANDLE, RhGetOSModuleFromPointer, (PTR_VOID pPointerVal))
{
    ICodeManager * pCodeManager = GetRuntimeInstance()->FindCodeManagerByAddress(pPointerVal);

    if (pCodeManager != NULL)
        return (HANDLE)pCodeManager->GetOsModuleHandle();

    return NULL;
}

COOP_PINVOKE_HELPER(HANDLE, RhGetOSModuleFromEEType, (EEType * pEEType))
{
    return pEEType->GetTypeManagerPtr()->AsTypeManager()->GetOsModuleHandle();
}

COOP_PINVOKE_HELPER(TypeManagerHandle, RhGetModuleFromEEType, (EEType * pEEType))
{
    return *pEEType->GetTypeManagerPtr();
}

COOP_PINVOKE_HELPER(Boolean, RhFindBlob, (TypeManagerHandle *pTypeManagerHandle, UInt32 blobId, UInt8 ** ppbBlob, UInt32 * pcbBlob))
{
    TypeManagerHandle typeManagerHandle = *pTypeManagerHandle;

    ReadyToRunSectionType section =
        (ReadyToRunSectionType)((UInt32)ReadyToRunSectionType::ReadonlyBlobRegionStart + blobId);
    ASSERT(section <= ReadyToRunSectionType::ReadonlyBlobRegionEnd);

    TypeManager* pModule = typeManagerHandle.AsTypeManager();

    int length;
    void* pBlob;
    pBlob = pModule->GetModuleSection(section, &length);

    *ppbBlob = (UInt8*)pBlob;
    *pcbBlob = (UInt32)length;

    return pBlob != NULL;
}

// This helper is not called directly but is used by the implementation of RhpCheckCctor to locate the
// CheckStaticClassConstruction classlib callback. It must not trigger a GC. The return address passed points
// to code in the caller's module and can be used in the lookup.
COOP_PINVOKE_HELPER(void *, GetClasslibCCtorCheck, (void * pReturnAddress))
{
    // Locate the calling module from the context structure address (which is in writable memory in the
    // module image).
    ICodeManager * pCodeManager = GetRuntimeInstance()->FindCodeManagerByAddress(pReturnAddress);
    ASSERT(pCodeManager);

    // Lookup the callback registered by the classlib.
    void * pCallback = pCodeManager->GetClasslibFunction(ClasslibFunctionId::CheckStaticClassConstruction);

    // We have no fallback path if we got here but the classlib doesn't implement the callback.
    if (pCallback == NULL)
        RhFailFast();

    return pCallback;
}

COOP_PINVOKE_HELPER(void *, RhGetTargetOfUnboxingAndInstantiatingStub, (void * pUnboxStub))
{
    return GetRuntimeInstance()->GetTargetOfUnboxingAndInstantiatingStub(pUnboxStub);
}

#if TARGET_ARM
//*****************************************************************************
//  Extract the 16-bit immediate from ARM Thumb2 Instruction (format T2_N)
//*****************************************************************************
static FORCEINLINE UInt16 GetThumb2Imm16(UInt16 * p)
{
    return ((p[0] << 12) & 0xf000) |
        ((p[0] << 1) & 0x0800) |
        ((p[1] >> 4) & 0x0700) |
        ((p[1] >> 0) & 0x00ff);
}

//*****************************************************************************
//  Extract the 32-bit immediate from movw/movt sequence
//*****************************************************************************
inline UInt32 GetThumb2Mov32(UInt16 * p)
{
    // Make sure we are decoding movw/movt sequence
    ASSERT((*(p + 0) & 0xFBF0) == 0xF240);
    ASSERT((*(p + 2) & 0xFBF0) == 0xF2C0);

    return (UInt32)GetThumb2Imm16(p) + ((UInt32)GetThumb2Imm16(p + 2) << 16);
}

//*****************************************************************************
//  Extract the 24-bit distance from a B/BL instruction
//*****************************************************************************
inline Int32 GetThumb2BlRel24(UInt16 * p)
{
    UInt16 Opcode0 = p[0];
    UInt16 Opcode1 = p[1];

    UInt32 S = Opcode0 >> 10;
    UInt32 J2 = Opcode1 >> 11;
    UInt32 J1 = Opcode1 >> 13;

    Int32 ret =
        ((S << 24) & 0x1000000) |
        (((J1 ^ S ^ 1) << 23) & 0x0800000) |
        (((J2 ^ S ^ 1) << 22) & 0x0400000) |
        ((Opcode0 << 12) & 0x03FF000) |
        ((Opcode1 << 1) & 0x0000FFE);

    // Sign-extend and return
    return (ret << 7) >> 7;
}
#endif // TARGET_ARM

// Given a pointer to code, find out if this points to an import stub
// or unboxing stub, and if so, return the address that stub jumps to
COOP_PINVOKE_HELPER(UInt8 *, RhGetCodeTarget, (UInt8 * pCodeOrg))
{
    bool unboxingStub = false;

    // First, check the unboxing stubs regions known by the runtime (if any exist)
    if (!GetRuntimeInstance()->IsUnboxingStub(pCodeOrg))
    {
        return pCodeOrg;
    }

#ifdef TARGET_AMD64
    UInt8 * pCode = pCodeOrg;

    // is this "add rcx/rdi,8"?
    if (pCode[0] == 0x48 &&
        pCode[1] == 0x83 &&
#ifdef UNIX_AMD64_ABI
        pCode[2] == 0xc7 &&
#else
        pCode[2] == 0xc1 &&
#endif
        pCode[3] == 0x08)
    {
        // unboxing sequence
        unboxingStub = true;
        pCode += 4;
    }
    // is this an indirect jump?
    if (pCode[0] == 0xff && pCode[1] == 0x25)
    {
        // normal import stub - dist to IAT cell is relative to the point *after* the instruction
        Int32 distToIatCell = *(Int32 *)&pCode[2];
        UInt8 ** pIatCell = (UInt8 **)(pCode + 6 + distToIatCell);
        return *pIatCell;
    }
    // is this an unboxing stub followed by a relative jump?
    else if (unboxingStub && pCode[0] == 0xe9)
    {
        // relative jump - dist is relative to the point *after* the instruction
        Int32 distToTarget = *(Int32 *)&pCode[1];
        UInt8 * target = pCode + 5 + distToTarget;
        return target;
    }

#elif TARGET_X86
    UInt8 * pCode = pCodeOrg;

    // is this "add ecx,4"?
    if (pCode[0] == 0x83 && pCode[1] == 0xc1 && pCode[2] == 0x04)
    {
        // unboxing sequence
        unboxingStub = true;
        pCode += 3;
    }
    // is this an indirect jump?
    if (pCode[0] == 0xff && pCode[1] == 0x25)
    {
        // normal import stub - address of IAT follows
        UInt8 **pIatCell = *(UInt8 ***)&pCode[2];
        return *pIatCell;
    }
    // is this an unboxing stub followed by a relative jump?
    else if (unboxingStub && pCode[0] == 0xe9)
    {
        // relative jump - dist is relative to the point *after* the instruction
        Int32 distToTarget = *(Int32 *)&pCode[1];
        UInt8 * pTarget = pCode + 5 + distToTarget;
        return pTarget;
    }

#elif TARGET_ARM
    UInt16 * pCode = (UInt16 *)((size_t)pCodeOrg & ~THUMB_CODE);
    // is this "adds r0,4"?
    if (pCode[0] == 0x3004)
    {
        // unboxing sequence
        unboxingStub = true;
        pCode += 1;
    }
    // is this movw r12,#imm16; movt r12,#imm16; ldr pc,[r12]
    // or movw r12,#imm16; movt r12,#imm16; bx r12
    if  ((pCode[0] & 0xfbf0) == 0xf240 && (pCode[1] & 0x0f00) == 0x0c00
        && (pCode[2] & 0xfbf0) == 0xf2c0 && (pCode[3] & 0x0f00) == 0x0c00
        && ((pCode[4] == 0xf8dc && pCode[5] == 0xf000) || pCode[4] == 0x4760))
    {
        if (pCode[4] == 0xf8dc && pCode[5] == 0xf000)
        {
            // ldr pc,[r12]
            UInt8 **pIatCell = (UInt8 **)GetThumb2Mov32(pCode);
            return *pIatCell;
        }
        else if (pCode[4] == 0x4760)
        {
            // bx r12
            return (UInt8 *)GetThumb2Mov32(pCode);
        }
    }
    // is this an unboxing stub followed by a relative jump?
    else if (unboxingStub && (pCode[0] & 0xf800) == 0xf000 && (pCode[1] & 0xd000) == 0x9000)
    {
        Int32 distToTarget = GetThumb2BlRel24(pCode);
        UInt8 * pTarget = (UInt8 *)(pCode + 2) + distToTarget + THUMB_CODE;
        return (UInt8 *)pTarget;
    }

#elif TARGET_ARM64
    UInt32 * pCode = (UInt32 *)pCodeOrg;
    // is this "add x0,x0,#8"?
    if (pCode[0] == 0x91002000)
    {
        // unboxing sequence
        unboxingStub = true;
        pCode++;
    }
    // is this an indirect jump?
    // adrp xip0,#imm21; ldr xip0,[xip0,#imm12]; br xip0
    if ((pCode[0] & 0x9f00001f) == 0x90000010 &&
        (pCode[1] & 0xffc003ff) == 0xf9400210 &&
        pCode[2] == 0xd61f0200)
    {
        // normal import stub - dist to IAT cell is relative to (PC & ~0xfff)
        // adrp: imm = SignExtend(immhi:immlo:Zeros(12), 64);
        Int64 distToIatCell = (((((Int64)pCode[0] & ~0x1f) << 40) >> 31) | ((pCode[0] >> 17) & 0x3000));
        // ldr: offset = LSL(ZeroExtend(imm12, 64), 3);
        distToIatCell += (pCode[1] >> 7) & 0x7ff8;
        UInt8 ** pIatCell = (UInt8 **)(((Int64)pCode & ~0xfff) + distToIatCell);
        return *pIatCell;
    }
    // is this an unboxing stub followed by a relative jump?
    else if (unboxingStub && (pCode[0] >> 26) == 0x5)
    {
        // relative jump - dist is relative to the instruction
        // offset = SignExtend(imm26:'00', 64);
        Int64 distToTarget = ((Int64)pCode[0] << 38) >> 36;
        return (UInt8 *)pCode + distToTarget;
    }
#else
    UNREFERENCED_PARAMETER(unboxingStub);
    PORTABILITY_ASSERT("RhGetCodeTarget");
#endif

    return pCodeOrg;
}

//
// Return true if the array slice is valid
//
FORCEINLINE bool CheckArraySlice(Array * pArray, Int32 index, Int32 length)
{
    Int32 arrayLength = pArray->GetArrayLength();

    return (0 <= index) && (index <= arrayLength) &&
           (0 <= length) && (length <= arrayLength) &&
           (length <= arrayLength - index);
}

//
// This function handles all cases of Array.Copy that do not require conversions or casting. It returns false if the copy cannot be performed, leaving
// the handling of the complex cases or throwing appropriate exception to the higher level framework.
//
COOP_PINVOKE_HELPER(Boolean, RhpArrayCopy, (Array * pSourceArray, Int32 sourceIndex, Array * pDestinationArray, Int32 destinationIndex, Int32 length))
{
    if (pSourceArray == NULL || pDestinationArray == NULL)
        return false;

    EEType* pArrayType = pSourceArray->get_EEType();
    EEType* pDestinationArrayType = pDestinationArray->get_EEType();
    if (pArrayType != pDestinationArrayType)
    {
        if (!pArrayType->IsEquivalentTo(pDestinationArrayType))
           return false;
    }

    size_t componentSize = pArrayType->get_ComponentSize();
    if (componentSize == 0) // Not an array
        return false;

    if (!CheckArraySlice(pSourceArray, sourceIndex, length))
        return false;

    if (!CheckArraySlice(pDestinationArray, destinationIndex, length))
        return false;

    if (length == 0)
        return true;

    UInt8 * pSourceData = (UInt8 *)pSourceArray->GetArrayData() + sourceIndex * componentSize;
    UInt8 * pDestinationData = (UInt8 *)pDestinationArray->GetArrayData() + destinationIndex * componentSize;
    size_t size = length * componentSize;

    if (pArrayType->HasReferenceFields())
    {
        if (pDestinationData <= pSourceData || pSourceData + size <= pDestinationData)
            InlineForwardGCSafeCopy(pDestinationData, pSourceData, size);
        else
            InlineBackwardGCSafeCopy(pDestinationData, pSourceData, size);

        InlinedBulkWriteBarrier(pDestinationData, size);
    }
    else
    {
        memmove(pDestinationData, pSourceData, size);
    }

    return true;
}

//
// This function handles all cases of Array.Clear that do not require conversions. It returns false if the operation cannot be performed, leaving
// the handling of the complex cases or throwing appropriate exception to the higher level framework. It is only allowed to return false for illegal 
// calls as the BCL side has fallback for "complex cases" only.
//
COOP_PINVOKE_HELPER(Boolean, RhpArrayClear, (Array * pArray, Int32 index, Int32 length))
{
    if (pArray == NULL)
        return false;

    EEType* pArrayType = pArray->get_EEType();

    size_t componentSize = pArrayType->get_ComponentSize();
    if (componentSize == 0) // Not an array
        return false;

    if (!CheckArraySlice(pArray, index, length))
        return false;

    if (length == 0)
        return true;

    InlineGCSafeFillMemory((UInt8 *)pArray->GetArrayData() + index * componentSize, length * componentSize, 0);

    return true;
}

// Get the universal transition thunk. If the universal transition stub is called through
// the normal PE static linkage model, a jump stub would be used which may interfere with
// the custom calling convention of the universal transition thunk. So instead, a special
// api just for getting the thunk address is needed.
// TODO: On ARM this may still result in a jump stub that trashes R12. Determine if anything
//       needs to be done about that when we implement the stub for ARM.
extern "C" void RhpUniversalTransition();
COOP_PINVOKE_HELPER(void*, RhGetUniversalTransitionThunk, ())
{
    return (void*)RhpUniversalTransition;
}

extern CrstStatic g_CastCacheLock;

EXTERN_C REDHAWK_API void __cdecl RhpAcquireCastCacheLock()
{
    g_CastCacheLock.Enter();
}

EXTERN_C REDHAWK_API void __cdecl RhpReleaseCastCacheLock()
{
    g_CastCacheLock.Leave();
}

extern CrstStatic g_ThunkPoolLock;

EXTERN_C REDHAWK_API void __cdecl RhpAcquireThunkPoolLock()
{
    g_ThunkPoolLock.Enter();
}

EXTERN_C REDHAWK_API void __cdecl RhpReleaseThunkPoolLock()
{
    g_ThunkPoolLock.Leave();
}

EXTERN_C Int32 __cdecl RhpCalculateStackTraceWorker(void* pOutputBuffer, UInt32 outputBufferLength);

EXTERN_C REDHAWK_API Int32 __cdecl RhpGetCurrentThreadStackTrace(void* pOutputBuffer, UInt32 outputBufferLength)
{
    // This must be called via p/invoke rather than RuntimeImport to make the stack crawlable.

    ThreadStore::GetCurrentThread()->SetupHackPInvokeTunnel();

    return RhpCalculateStackTraceWorker(pOutputBuffer, outputBufferLength);
}

COOP_PINVOKE_HELPER(void*, RhpRegisterFrozenSegment, (void* pSegmentStart, size_t length))
{
    return RedhawkGCInterface::RegisterFrozenSegment(pSegmentStart, length);
}

COOP_PINVOKE_HELPER(void, RhpUnregisterFrozenSegment, (void* pSegmentHandle))
{
    RedhawkGCInterface::UnregisterFrozenSegment((GcSegmentHandle)pSegmentHandle);
}

COOP_PINVOKE_HELPER(void*, RhpGetModuleSection, (TypeManagerHandle *pModule, Int32 headerId, Int32* length))
{
    return pModule->AsTypeManager()->GetModuleSection((ReadyToRunSectionType)headerId, length);
}

COOP_PINVOKE_HELPER(void, RhGetCurrentThreadStackBounds, (PTR_VOID * ppStackLow, PTR_VOID * ppStackHigh))
{
    ThreadStore::GetCurrentThread()->GetStackBounds(ppStackLow, ppStackHigh);
}

#ifdef TARGET_UNIX

// Function to call when a thread is detached from the runtime
ThreadExitCallback g_threadExitCallback;

COOP_PINVOKE_HELPER(void, RhSetThreadExitCallback, (void * pCallback))
{
    g_threadExitCallback = (ThreadExitCallback)pCallback;
}

#endif // TARGET_UNIX

COOP_PINVOKE_HELPER(Int32, RhGetProcessCpuCount, ())
{
    return PalGetProcessCpuCount();
}
