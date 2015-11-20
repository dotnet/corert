//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Miscellaneous unmanaged helpers called by managed code.
//

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
#include "static_check.h"
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
#include "gcrhinterface.h"
#include "module.h"
#include "eetype.h"
#include "ObjectLayout.h"
#include "GenericInstance.h"
#include "slist.inl"
#include "eetype.inl"
#include "CommonMacros.inl"

// Busy spin for the given number of iterations.
COOP_PINVOKE_HELPER(void, RhSpinWait, (Int32 iterations))
{
    for(int i = 0; i < iterations; i++)
        PalYieldProcessor();
}

// Yield the cpu to another thread ready to process, if one is available.
COOP_PINVOKE_HELPER(UInt32_BOOL, RhYield, ())
{
    return PalSwitchToThread();
}

// Get the rarely used (optional) flags of an EEType. If they're not present 0 will be returned.
COOP_PINVOKE_HELPER(UInt32, RhpGetEETypeRareFlags, (EEType * pEEType))
{
    return pEEType->get_RareFlags();
}

// For an ICastable type return a pointer to code that implements ICastable.IsInstanceOfInterface.
COOP_PINVOKE_HELPER(UIntNative, RhpGetICastableIsInstanceOfInterfaceMethod, (EEType * pEEType))
{
    ASSERT(pEEType->IsICastable());
    return (UIntNative)pEEType->get_ICastableIsInstanceOfInterfaceMethod();
}

// For an ICastable type return a pointer to code that implements ICastable.ICastableGetImplType.
COOP_PINVOKE_HELPER(UIntNative, RhpGetICastableGetImplTypeMethod, (EEType * pEEType))
{
    ASSERT(pEEType->IsICastable());
    return (UIntNative)pEEType->get_ICastableGetImplTypeMethod();
}

// Return the unboxed size of a value type.
COOP_PINVOKE_HELPER(UInt32, RhGetValueTypeSize, (EEType * pEEType))
{
    ASSERT(pEEType->get_IsValueType());

    // get_BaseSize returns the GC size including space for the sync block index field, the EEType* and
    // padding for GC heap alignment. Must subtract all of these to get the size used for locals, array
    // elements or fields of another type.
    return pEEType->get_BaseSize() - (sizeof(ObjHeader) + sizeof(EEType*) + pEEType->get_ValueTypeFieldPadding());
}

// Return the DispatchMap pointer of a type
COOP_PINVOKE_HELPER(DispatchMap*, RhGetDispatchMapForType, (EEType * pEEType))
{
    return pEEType->GetDispatchMap();
}

// Get the list of currently loaded Redhawk modules (as OS HMODULE handles). The caller provides a reference
// to an array of pointer-sized elements and we return the total number of modules currently loaded (whether
// that is less than, equal to or greater than the number of elements in the array). If there are more modules
// loaded than the array will hold then the array is filled to capacity and the caller can tell further
// modules are available based on the return count. It is also possible to call this method without an array,
// in which case just the module count is returned (note that it's still possible for the module count to
// increase between calls to this method).
COOP_PINVOKE_HELPER(UInt32, RhGetLoadedModules, (Array * pResultArray))
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

    FOREACH_MODULE(pModule)
    {
        if (pResultArray && (cModules < cResultArrayElements))
            pResultElements[cModules] = pModule->GetOsModuleHandle();

        cModules++;
    }
    END_FOREACH_MODULE

    return cModules;
}

COOP_PINVOKE_HELPER(HANDLE, RhGetModuleFromPointer, (PTR_VOID pPointerVal))
{
    Module * pModule = GetRuntimeInstance()->FindModuleByAddress(pPointerVal);

    if (pModule != NULL)
        return pModule->GetOsModuleHandle();

    return NULL;
}

COOP_PINVOKE_HELPER(HANDLE, RhGetModuleFromEEType, (EEType * pEEType))
{
    // Runtime allocated EETypes have no associated module, but class libraries shouldn't be able to get to
    // any of these since they're currently only used for the canonical version of a generic EEType and we
    // provide no means to go from the cloned version to the canonical version.
    ASSERT(!pEEType->IsRuntimeAllocated());

    // For dynamically created types, return the module handle that contains the template type
    if (pEEType->IsDynamicType())
        pEEType = pEEType->get_DynamicTemplateType();

    FOREACH_MODULE(pModule)
    {
        if (pModule->ContainsReadOnlyDataAddress(pEEType) || pModule->ContainsDataAddress(pEEType))
            return pModule->GetOsModuleHandle();
    }
    END_FOREACH_MODULE

    // We should never get here (an EEType not located in any module) so fail fast to indicate the bug.
    RhFailFast();
    return NULL;
}

COOP_PINVOKE_HELPER(Boolean, RhFindBlob, (HANDLE hOsModule, UInt32 blobId, UInt8 ** ppbBlob, UInt32 * pcbBlob))
{
    // Search for the Redhawk module contained by the OS module.
    FOREACH_MODULE(pModule)
    {
        if (pModule->GetOsModuleHandle() == hOsModule)
        {
            // Found a module match. Look through the blobs for one with a matching ID.
            UInt32 cbBlobs;
            BlobHeader * pBlob = pModule->GetReadOnlyBlobs(&cbBlobs);

            while (cbBlobs)
            {
                UInt32 cbTotalBlob = sizeof(BlobHeader) + pBlob->m_size;
                ASSERT(cbBlobs >= cbTotalBlob);

                if (pBlob->m_id == blobId)
                {
                    // Found the matching blob, return it.
                    *ppbBlob = (UInt8*)(pBlob + 1);
                    *pcbBlob = pBlob->m_size;
                    return TRUE;
                }

                cbBlobs -= cbTotalBlob;
                pBlob = (BlobHeader*)((UInt8*)pBlob + cbTotalBlob);
            }

            // If we get here then we found a module match but didn't find a blob with a matching ID. That's a
            // non-catastrophic error.
            *ppbBlob = NULL;
            *pcbBlob = 0;
            return FALSE;
        }
    }
    END_FOREACH_MODULE

    // If we get here we were passed a bad module handle and should fail fast since this indicates a nasty bug
    // (which could lead to the wrong blob being returned in some cases).
    RhFailFast();

    return FALSE;
}

// This helper is not called directly but is used by the implementation of RhpCheckCctor to locate the
// CheckStaticClassConstruction classlib callback. It must not trigger a GC. The return address passed points
// to code in the caller's module and can be used in the lookup.
COOP_PINVOKE_HELPER(void *, GetClasslibCCtorCheck, (void * pReturnAddress))
{
    // Locate the calling module from the context structure address (which is in writeable memory in the
    // module image).
    Module * pModule = GetRuntimeInstance()->FindModuleByCodeAddress(pReturnAddress);
    ASSERT(pModule);

    // Locate the classlib module from the calling module.
    Module * pClasslibModule = pModule->GetClasslibModule();
    ASSERT(pClasslibModule);

    // Lookup the callback registered by the classlib.
    void * pCallback = pClasslibModule->GetClasslibCheckStaticClassConstruction();

    // We have no fallback path if we got here but the classlib doesn't implement the callback.
    if (pCallback == NULL)
        RhFailFast();

    return pCallback;
}

COOP_PINVOKE_HELPER(UInt8, RhpGetNullableEETypeValueOffset, (EEType * pEEType))
{
    return pEEType->GetNullableValueOffset();
}

COOP_PINVOKE_HELPER(EEType *, RhpGetNullableEEType, (EEType * pEEType))
{
    return pEEType->GetNullableType();
}

COOP_PINVOKE_HELPER(Boolean, RhpHasDispatchMap, (EEType * pEEType))
{
    return pEEType->HasDispatchMap();
}

COOP_PINVOKE_HELPER(DispatchMap *, RhpGetDispatchMap, (EEType * pEEType))
{
    return pEEType->GetDispatchMap();
}

COOP_PINVOKE_HELPER(EEType *, RhpGetArrayBaseType, (EEType * pEEType))
{
    return pEEType->GetArrayBaseType();
}

COOP_PINVOKE_HELPER(PTR_Code, RhpGetSealedVirtualSlot, (EEType * pEEType, UInt16 slot))
{
    return pEEType->get_SealedVirtualSlot(slot);
}

// Obtain the address of a thread static field for the current thread given the enclosing type and a field cookie
// obtained from a fixed up binder blob field record.
COOP_PINVOKE_HELPER(UInt8 *, RhGetThreadStaticFieldAddress, (EEType * pEEType, ThreadStaticFieldOffsets* pFieldCookie))
{
    RuntimeInstance * pRuntimeInstance = GetRuntimeInstance();

    // We need two pieces of information to locate a thread static field for the current thread: a TLS index
    // (one assigned per module) and an offset into the block of data allocated for each thread for that TLS
    // index.
    UInt32 uiTlsIndex;
    UInt32 uiFieldOffset;

    if (pEEType->IsDynamicType())
    {
        // Special case for thread static fields on dynamic types: the TLS storage is managed by the runtime
        // for each dynamically created type with thread statics. The TLS storage size allocated for each type
        // is the size of all the thread statics on that type. We use the field offset to get the thread static
        // data for that field on the current thread.
        GenericInstanceDesc * pGID = pRuntimeInstance->LookupGenericInstance(pEEType);
        ASSERT(pGID != NULL);
        UInt8* pTlsStorage = ThreadStore::GetCurrentThread()->GetThreadLocalStorageForDynamicType(pGID->GetThreadStaticFieldStartOffset());
        ASSERT(pTlsStorage != NULL);
        return (pFieldCookie != NULL ? pTlsStorage + pFieldCookie->FieldOffset : pTlsStorage);
    }
    else if (!pRuntimeInstance->IsInStandaloneExeMode() && pEEType->IsGeneric())
    {
        // The tricky case is a thread static field on a generic type when we're not in standalone mode. In that
        // case we need to lookup the GenericInstanceDesc for the type to locate its TLS static base
        // offset (which unlike the case below has already been fixed up to account for COFF mode linking). The
        // cookie then contains an offset from that base.
        GenericInstanceDesc * pGID = pRuntimeInstance->LookupGenericInstance(pEEType);
        uiFieldOffset = pGID->GetThreadStaticFieldStartOffset() + pFieldCookie->FieldOffset;

        // The TLS index in the GenericInstanceDesc will always be 0 (unless we perform GID unification, which
        // we don't today), so we'll need to get the TLS index from the header of the type's containing module.
        Module * pModule = pRuntimeInstance->FindModuleByReadOnlyDataAddress(pEEType);
        ASSERT(pModule != NULL);
        uiTlsIndex = *pModule->GetModuleHeader()->PointerToTlsIndex;
    }
    else
    {
        // In all other cases the field cookie contains an offset from the base of all Redhawk thread statics
        // to the field. The TLS index and offset adjustment (in cases where the module was linked with native
        // code using .tls) is that from the exe module.
        Module * pModule = pRuntimeInstance->FindModuleByReadOnlyDataAddress(pEEType);
        if (pModule == NULL)
            pModule = pRuntimeInstance->FindModuleByDataAddress(pEEType);
        ASSERT(pModule != NULL);
        ModuleHeader * pExeModuleHeader = pModule->GetModuleHeader();

        uiTlsIndex = *pExeModuleHeader->PointerToTlsIndex;
        uiFieldOffset = pExeModuleHeader->TlsStartOffset + pFieldCookie->StartingOffsetInTlsBlock + pFieldCookie->FieldOffset;
    }

    // Now look at the current thread and retrieve the address of the field.
    return ThreadStore::GetCurrentThread()->GetThreadLocalStorage(uiTlsIndex, uiFieldOffset);
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
    // Search for the module containing the code
    FOREACH_MODULE(pModule)
    {
        // If the code pointer doesn't point to a module's stub range,
        // it can't be pointing to a stub
        if (!pModule->ContainsStubAddress(pCodeOrg))
            continue;

        bool unboxingStub = false;

#ifdef TARGET_AMD64
        UInt8 * pCode = pCodeOrg;

        // is this "add rcx,8"?
        if (pCode[0] == 0x48 && pCode[1] == 0x83 && pCode[2] == 0xc1 && pCode[3] == 0x08)
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
            ASSERT(pModule->ContainsDataAddress(pIatCell));
            return *pIatCell;
        }
        // is this an unboxing stub followed by a relative jump?
        else if (unboxingStub && pCode[0] == 0xe9)
        {
            // relatie jump - dist is relative to the point *after* the instruction
            Int32 distToTarget = *(Int32 *)&pCode[1];
            UInt8 * target = pCode + 5 + distToTarget;
            return target;
        }
        return pCodeOrg;

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
            ASSERT(pModule->ContainsDataAddress(pIatCell));
            return *pIatCell;
        }
        // is this an unboxing stub followed by a relative jump?
        else if (unboxingStub && pCode[0] == 0xe9)
        {
            // relatie jump - dist is relative to the point *after* the instruction
            Int32 distToTarget = *(Int32 *)&pCode[1];
            UInt8 * pTarget = pCode + 5 + distToTarget;
            return pTarget;
        }
        return pCodeOrg;

#elif TARGET_ARM
        const UInt16 THUMB_BIT = 1;
        UInt16 * pCode = (UInt16 *)((size_t)pCodeOrg & ~THUMB_BIT);
        // is this "adds r0,4"?
        if (pCode[0] == 0x3004)
        {
            // unboxing sequence
            unboxingStub = true;
            pCode += 1;
        }
        // is this movw r12,#imm16; movt r12,#imm16; ldr pc,[r12]?
        if  ((pCode[0] & 0xfbf0) == 0xf240 && (pCode[1] & 0x0f00) == 0x0c00
          && (pCode[2] & 0xfbf0) == 0xf2c0 && (pCode[3] & 0x0f00) == 0x0c00
          && pCode[4] == 0xf8dc && pCode[5] == 0xf000)
        {
            UInt8 **pIatCell = (UInt8 **)GetThumb2Mov32(pCode);
            return *pIatCell;
        }
        // is this an unboxing stub followed by a relative jump?
        else if (unboxingStub && (pCode[0] & 0xf800) == 0xf000 && (pCode[1] & 0xd000) == 0x9000)
        {
            Int32 distToTarget = GetThumb2BlRel24(pCode);
            UInt8 * pTarget = (UInt8 *)(pCode + 2) + distToTarget + THUMB_BIT;
            return (UInt8 *)pTarget;
        }
#else
#error 'Unsupported Architecture'
#endif
    }
    END_FOREACH_MODULE;

    return pCodeOrg;
}

FORCEINLINE void ForwardGCSafeCopy(void * dest, const void *src, size_t len)
{
    // All parameters must be pointer-size-aligned
    ASSERT(IS_ALIGNED(dest, sizeof(size_t)));
    ASSERT(IS_ALIGNED(src, sizeof(size_t)));
    ASSERT(IS_ALIGNED(len, sizeof(size_t)));

    size_t size = len;
    UInt8 * dmem = (UInt8 *)dest;
    UInt8 * smem = (UInt8 *)src;

    // regions must be non-overlapping
    ASSERT(dmem <= smem || smem + size <= dmem);

    // copy 4 pointers at a time 
    while (size >= 4 * sizeof(size_t))
    {
        size -= 4 * sizeof(size_t);
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
        ((size_t *)dmem)[1] = ((size_t *)smem)[1];
        ((size_t *)dmem)[2] = ((size_t *)smem)[2];
        ((size_t *)dmem)[3] = ((size_t *)smem)[3];
        smem += 4 * sizeof(size_t);
        dmem += 4 * sizeof(size_t);
    }

    // copy 2 trailing pointers, if needed
    if ((size & (2 * sizeof(size_t))) != 0)
    {
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
        ((size_t *)dmem)[1] = ((size_t *)smem)[1];
        smem += 2 * sizeof(size_t);
        dmem += 2 * sizeof(size_t);
    }

    // finish with one pointer, if needed
    if ((size & sizeof(size_t)) != 0)
    {
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
    }
}

FORCEINLINE void BackwardGCSafeCopy(void * dest, const void *src, size_t len)
{
    // All parameters must be pointer-size-aligned
    ASSERT(IS_ALIGNED(dest, sizeof(size_t)));
    ASSERT(IS_ALIGNED(src, sizeof(size_t)));
    ASSERT(IS_ALIGNED(len, sizeof(size_t)));

    size_t size = len;
    UInt8 * dmem = (UInt8 *)dest + len;
    UInt8 * smem = (UInt8 *)src + len;

    // regions must be non-overlapping
    ASSERT(smem <= dmem || dmem + size <= smem);

    // copy 4 pointers at a time 
    while (size >= 4 * sizeof(size_t))
    {
        size -= 4 * sizeof(size_t);
        smem -= 4 * sizeof(size_t);
        dmem -= 4 * sizeof(size_t);
        ((size_t *)dmem)[3] = ((size_t *)smem)[3];
        ((size_t *)dmem)[2] = ((size_t *)smem)[2];
        ((size_t *)dmem)[1] = ((size_t *)smem)[1];
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
    }

    // copy 2 trailing pointers, if needed
    if ((size & (2 * sizeof(size_t))) != 0)
    {
        smem -= 2 * sizeof(size_t);
        dmem -= 2 * sizeof(size_t);
        ((size_t *)dmem)[1] = ((size_t *)smem)[1];
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
    }

    // finish with one pointer, if needed
    if ((size & sizeof(size_t)) != 0)
    {
        smem -= sizeof(size_t);
        dmem -= sizeof(size_t);
        ((size_t *)dmem)[0] = ((size_t *)smem)[0];
    }
}

// This function fills a piece of memory in a GC safe way.  It makes the guarantee
// that it will fill memory in at least pointer sized chunks whenever possible.
// Unaligned memory at the beginning and remaining bytes at the end are written bytewise.
// We must make this guarantee whenever we clear memory in the GC heap that could contain 
// object references.  The GC or other user threads can read object references at any time, 
// clearing them bytewise can result in a read on another thread getting incorrect data.  
FORCEINLINE void GCSafeFillMemory(void * mem, size_t size, size_t pv)
{
    UInt8 * memBytes = (UInt8 *)mem;
    UInt8 * endBytes = &memBytes[size];

    // handle unaligned bytes at the beginning 
    while (!IS_ALIGNED(memBytes, sizeof(void *)) && (memBytes < endBytes))
        *memBytes++ = (UInt8)pv;

    // now write pointer sized pieces 
    size_t nPtrs = (endBytes - memBytes) / sizeof(void *);
    UIntNative* memPtr = (UIntNative*)memBytes;
    for (size_t i = 0; i < nPtrs; i++)
        *memPtr++ = pv;

    // handle remaining bytes at the end 
    memBytes = (UInt8*)memPtr;
    while (memBytes < endBytes)
        *memBytes++ = (UInt8)pv;
}

// This is a GC-safe variant of memcpy.  It guarantees that the object references in the GC heap are updated atomically.
// This is required for type safety and proper operation of the background GC.
//
// USAGE:   1) The caller is responsible for performing the appropriate bulk write barrier.
//          2) The caller is responsible for hoisting any null reference exceptions to a place where the hardware 
//             exception can be properly translated to a managed exception.  This is handled by RhpCopyMultibyte.
//          3) The caller must ensure that all three parameters are pointer-size-aligned.  This should be the case for
//             value types which contain GC refs anyway, so if you want to copy structs without GC refs which might be
//             unaligned, then you must use RhpCopyMultibyteNoGCRefs.
COOP_PINVOKE_CDECL_HELPER(void *, memcpyGCRefs, (void * dest, const void *src, size_t len))
{ 
    // null pointers are not allowed (they are checked by RhpCopyMultibyte)
    ASSERT(dest != nullptr);
    ASSERT(src != nullptr);

    ForwardGCSafeCopy(dest, src, len);

    // memcpy returns the destination buffer
    return dest;
}

// This function clears a piece of memory in a GC safe way.  It makes the guarantee that it will clear memory in at 
// least pointer sized chunks whenever possible.  Unaligned memory at the beginning and remaining bytes at the end are 
// written bytewise. We must make this guarantee whenever we clear memory in the GC heap that could contain object 
// references.  The GC or other user threads can read object references at any time, clearing them bytewise can result 
// in a read on another thread getting incorrect data.
//
// USAGE:  The caller is responsible for hoisting any null reference exceptions to a place where the hardware exception
//         can be properly translated to a managed exception.
COOP_PINVOKE_CDECL_HELPER(void *, RhpInitMultibyte, (void * mem, int c, size_t size))
{ 
    // The caller must do the null-check because we cannot take an AV in the runtime and translate it to managed.
    ASSERT(mem != nullptr); 

    UIntNative  bv = (UInt8)c;
    UIntNative  pv = 0;

    if (bv != 0)
    {
        pv = 
#if (POINTER_SIZE == 8)
            bv << 7*8 | bv << 6*8 | bv << 5*8 | bv << 4*8 |
#endif
            bv << 3*8 | bv << 2*8 | bv << 1*8 | bv;
    }

    GCSafeFillMemory(mem, size, pv);

    // memset returns the destination buffer
    return mem;
} 

EXTERN_C void * __cdecl memmove(void *, const void *, size_t);
EXTERN_C void REDHAWK_CALLCONV RhpBulkWriteBarrier(void* pMemStart, UInt32 cbMemSize);

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
            ForwardGCSafeCopy(pDestinationData, pSourceData, size);
        else
            BackwardGCSafeCopy(pDestinationData, pSourceData, size);

        RhpBulkWriteBarrier(pDestinationData, (UInt32)size);
    }
    else
    {
        memmove(pDestinationData, pSourceData, size);
    }

    return true;
}

//
// This function handles all cases of Array.Clear that do not require conversions. It returns false if the operation cannot be performed, leaving
// the handling of the complex cases or throwing apppropriate exception to the higher level framework. It is only allowed to return false for illegal 
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

    GCSafeFillMemory((UInt8 *)pArray->GetArrayData() + index * componentSize, length * componentSize, 0);

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
