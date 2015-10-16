// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "common.h"

#define USE_MRT 0

#include "gcenv.h"

#if !USE_MRT
#include "gc.h"
#include "objecthandle.h"
#include "gcdesc.h"
#else
extern "C" Object * RhNewObject(MethodTable * pMT);
extern "C" Object * RhNewArray(MethodTable * pMT, int32_t elements);
extern "C" void RhpReversePInvoke2(ReversePInvokeFrame* pRevFrame);
extern "C" void RhpReversePInvokeReturn(ReversePInvokeFrame* pRevFrame);
extern "C" int32_t RhpEnableConservativeStackReporting();
extern "C" void RhpRegisterSimpleModule(SimpleModuleHeader* pModule);
#endif // USE_MRT

#include "platform.h"

int __initialize_runtime()
{
#if USE_MRT
    RhpEnableConservativeStackReporting();
#else
    //
    // Initialize system info
    //
    InitializeSystemInfo();

    // 
    // Initialize free object methodtable. The GC uses a special array-like methodtable as placeholder
    // for collected free space.
    //
    static MethodTable freeObjectMT;
    freeObjectMT.InitializeFreeObject();
    g_pFreeObjectMethodTable = &freeObjectMT;

    //
    // Initialize handle table
    //
    if (!Ref_Initialize())
        return -1;

    //
    // Initialize GC heap
    //
    GCHeap *pGCHeap = GCHeap::CreateGCHeap();
    if (!pGCHeap)
        return -1;

    if (FAILED(pGCHeap->Initialize()))
        return -1;

    //
    // Initialize current thread
    //
    ThreadStore::AttachCurrentThread(false);
#endif

    return 0;
}

void __shutdown_runtime()
{
}

void __reverse_pinvoke(ReversePInvokeFrame* pRevFrame)
{
#if USE_MRT
    RhpReversePInvoke2(pRevFrame);
#endif // USE_MRT
}

void __reverse_pinvoke_return(ReversePInvokeFrame* pRevFrame)
{
#if USE_MRT
    RhpReversePInvokeReturn(pRevFrame);
#endif // USE_MRT
}

extern "C" void* __GCStaticRegionStart;
extern "C" void* __GCStaticRegionEnd;

void __register_module(SimpleModuleHeader* pModule)
{
#if USE_MRT
    RhpRegisterSimpleModule(pModule);
#endif // USE_MRT

#ifndef CPPCODEGEN
    // Initialize GC statics in the module
    // TODO: emit a ModuleHeader and use it here

    for (void** currentBlock = &__GCStaticRegionStart; currentBlock < &__GCStaticRegionEnd; currentBlock++)
    {
        Object* gcBlock = __allocate_object((MethodTable*)*currentBlock);
        *currentBlock = CreateGlobalHandle(ObjectToOBJECTREF(gcBlock));
    }
#endif
}

namespace mscorlib { namespace System { 

    class Object {
    public:
        EEType * get_EEType() { return *(EEType **)this; }
    };

    class Array : public Object {
    public:
        int32_t GetArrayLength() {
            return *(int32_t *)((void **)this + 1);
        }
        void * GetArrayData() {
            return (void **)this + 2;
        }
    };

    class String : public Object { public:
        static MethodTable * __getMethodTable();
    };

    class String__Array : public Object { public:
        static MethodTable * __getMethodTable();
    };

    class EETypePtr { public:
        intptr_t m_value;
    };

}; };

using namespace mscorlib;

//
// The fast paths for object allocation and write barriers is performance critical. They are often
// hand written in assembly code, etc.
//
extern "C" Object * __allocate_object(MethodTable * pMT)
{
#if !USE_MRT
    alloc_context * acontext = GetThread()->GetAllocContext();
    Object * pObject;

    size_t size = pMT->GetBaseSize();

    BYTE* result = acontext->alloc_ptr;
    BYTE* advance = result + size;
    if (advance <= acontext->alloc_limit)
    {
        acontext->alloc_ptr = advance;
        pObject = (Object *)result;
    }
    else
    {
        pObject = GCHeap::GetGCHeap()->Alloc(acontext, size, 0);
        if (pObject == NULL)
            return NULL; // TODO: Throw OOM
    }

    pObject->SetMethodTable(pMT);

    return pObject;
#else
    return RhNewObject(pMT);
#endif
}

extern "C" void __EEType_mscorlib_System_String();

Object * __allocate_string(int32_t len)
{
#if !USE_MRT
    alloc_context * acontext = GetThread()->GetAllocContext();
    Object * pObject;

    // TODO: Overflow checks
    size_t size = 2 * sizeof(intptr_t) + sizeof(int32_t) + 2 * len;
    // Align up
    size = (size + (sizeof(intptr_t) - 1)) & ~(sizeof(intptr_t) - 1);

    BYTE* result = acontext->alloc_ptr;
    BYTE* advance = result + size;
    if (advance <= acontext->alloc_limit)
    {
        acontext->alloc_ptr = advance;
        pObject = (Object *)result;
    }
    else
    {
        pObject = GCHeap::GetGCHeap()->Alloc(acontext, size, 0);
        if (pObject == NULL)
            return NULL; // TODO: Throw OOM
    }
#ifdef CPPCODEGEN
    pObject->SetMethodTable(System::String::__getMethodTable());
#else
    pObject->SetMethodTable((MethodTable*)__EEType_mscorlib_System_String);
#endif
	*(int32_t *)(((intptr_t *)pObject) + 1) = len;
	return pObject;
#else
    return RhNewArray(System::String::__getMethodTable(), len);
#endif
}

extern "C" Object * __allocate_array(size_t elements, MethodTable * pMT)
{
#if !USE_MRT
    alloc_context * acontext = GetThread()->GetAllocContext();
    Object * pObject;

    // TODO: Overflow checks
    size_t size = 3 * sizeof(intptr_t) + (elements * pMT->RawGetComponentSize());
    // Align up
    size = (size + (sizeof(intptr_t) - 1)) & ~(sizeof(intptr_t) - 1);

    BYTE* result = acontext->alloc_ptr;
    BYTE* advance = result + size;
    if (advance <= acontext->alloc_limit)
    {
        acontext->alloc_ptr = advance;
        pObject = (Object *)result;
    }
    else
    {
        pObject = GCHeap::GetGCHeap()->Alloc(acontext, size, 0);
        if (pObject == NULL)
            return NULL; // TODO: Throw OOM
    }

    pObject->SetMethodTable(pMT);

    *(int32_t *)(((intptr_t *)pObject)+1) = (int32_t)elements;

    return pObject;
#else
    return RhNewArray(pMT, (int32_t)elements); // TODO: type mismatch
#endif 
}

#if defined(_WIN64)
// Card byte shift is different on 64bit.
#define card_byte_shift     11
#else
#define card_byte_shift     10
#endif

#define card_byte(addr) (((size_t)(addr)) >> card_byte_shift)

inline void ErectWriteBarrier(Object ** dst, Object * ref)
{
    // if the dst is outside of the heap (unboxed value classes) then we
    //      simply exit
    if (((BYTE*)dst < g_lowest_address) || ((BYTE*)dst >= g_highest_address))
        return;

    if ((BYTE*)ref >= g_ephemeral_low && (BYTE*)ref < g_ephemeral_high)
    {
        // volatile is used here to prevent fetch of g_card_table from being reordered 
        // with g_lowest/highest_address check above. See comment in code:gc_heap::grow_brick_card_tables.
        BYTE* pCardByte = (BYTE *)*(volatile BYTE **)(&g_card_table) + card_byte((BYTE *)dst);
        if (*pCardByte != 0xFF)
            *pCardByte = 0xFF;
    }
}

extern "C" void WriteBarrier(Object ** dst, Object * ref)
{
    *dst = ref;
    ErectWriteBarrier(dst, ref);
}

void __throw_exception(void * pEx)
{
    // TODO: Exception throwing
    throw pEx;
}

Object * __load_string_literal(const char * string)
{
    // TODO: Cache/intern string literals
    // TODO: Unicode string literals

    size_t len = strlen(string);

    Object * pString = __allocate_string((int32_t)len);

    uint16_t * p = (uint16_t *)((char*)pString + sizeof(intptr_t) + sizeof(int32_t));
    for (size_t i = 0; i < len; i++)
        p[i] = string[i];
    return pString;
}

OBJECTHANDLE __load_static_string_literal(const uint8_t* utf8, int32_t utf8Len, int32_t strLen)
{
    Object * pString = __allocate_string(strLen);
    uint16_t * buffer = (uint16_t *)((char*)pString + sizeof(intptr_t) + sizeof(int32_t));
    if (strLen > 0)
        UTF8ToWideChar((char*)utf8, utf8Len, buffer, strLen);
    return CreateGlobalHandle(ObjectToOBJECTREF(pString));
}

// TODO: Rewrite in C#

extern "C" Object * __castclass_class(void * p, MethodTable * pTargetMT)
{
    Object * o = (Object *)p;

    if (o == NULL)
        return o;

    MethodTable * pMT = o->RawGetMethodTable();

    do {
        if (pMT == pTargetMT)
            return o;

        pMT = pMT->GetParent();
    } while (pMT);

    // TODO: Handle corner cases, throw proper exception
    throw 1;
}

extern "C" Object * __isinst_class(void * p, MethodTable * pTargetMT)
{
    Object * o = (Object *)p;

    if (o == NULL)
        return o;

    MethodTable * pMT = o->RawGetMethodTable();

    do {
        if (pMT == pTargetMT)
            return o;

        pMT = pMT->GetParent();
    } while (pMT);

    // TODO: Handle corner cases
    return NULL;
}

__declspec(noreturn)
__declspec(noinline)
void ThrowRangeOverflowException()
{
    throw 0;
}

void __range_check_fail()
{
    ThrowRangeOverflowException();
}

void __range_check(void * a, size_t elem)
{
    if (elem >= *((size_t*)a + 1))
        ThrowRangeOverflowException();
}

#ifdef CPPCODEGEN
Object * __get_commandline_args(int argc, char * argv[])
{
    System::Array * p = (System::Array *)__allocate_array(argc, System::String__Array::__getMethodTable());

    for (int i = 0; i < argc; i++)
    {
        // TODO: Write barrier
        ((Object **)(p->GetArrayData()))[i] = __load_string_literal(argv[i]);
    }    

    return (Object *)p;
}
#endif

// FCalls

#ifdef _MSC_VER
#pragma warning(disable:4297)
#endif

FORCEINLINE bool CheckArraySlice(System::Array * pArray, int32_t index, int32_t length)
{
    int32_t arrayLength = pArray->GetArrayLength();

    return (0 <= index) && (index <= arrayLength) &&
        (0 <= length) && (length <= arrayLength) &&
        (length <= arrayLength - index);
}

extern "C" uint8_t RhpArrayCopy(System::Array* pSourceArray, int32_t sourceIndex, System::Array* pDestinationArray, int32_t destinationIndex, int32_t length)
{
    if (pSourceArray == NULL || pDestinationArray == NULL)
        return false;

    EEType* pArrayType = pSourceArray->get_EEType();
    EEType* pDestinationArrayType = pDestinationArray->get_EEType();

    if (pArrayType != pDestinationArrayType)
    {
        // TODO: Type safety check
        return false;
#if false
        if (!pArrayType->IsEquivalentTo(pDestinationArrayType))
            return false;
#endif
    }

    if (!pArrayType->HasComponentSize())
        return false; // an array

    size_t componentSize = pArrayType->RawGetComponentSize();

    if (!CheckArraySlice(pSourceArray, sourceIndex, length))
        return false;

    if (!CheckArraySlice(pDestinationArray, destinationIndex, length))
        return false;

    if (length == 0)
        return true;

    uint8_t * pSourceData = (uint8_t *)pSourceArray->GetArrayData() + sourceIndex * componentSize;
    uint8_t * pDestinationData = (uint8_t *)pDestinationArray->GetArrayData() + destinationIndex * componentSize;
    size_t size = length * componentSize;

    // TODO: Write barrier
#if false
    if (pArrayType->HasReferenceFields())
    {
        if (pDestinationData <= pSourceData || pSourceData + size <= pDestinationData)
            ForwardGCSafeCopy(pDestinationData, pSourceData, size);
        else
            BackwardGCSafeCopy(pDestinationData, pSourceData, size);

        RhpBulkWriteBarrier(pDestinationData, (UInt32)size);
    }
    else
#endif
    {
        memmove(pDestinationData, pSourceData, size);
    }

    return true;
}

void GCSafeFillMemory(void * mem, size_t size, size_t pv)
{
    uint8_t * memBytes = (uint8_t *)mem;
    uint8_t * endBytes = &memBytes[size];

    // handle unaligned bytes at the beginning 
    while (!IS_ALIGNED(memBytes, sizeof(void *)) && (memBytes < endBytes))
        *memBytes++ = (uint8_t)pv;

    // now write pointer sized pieces 
    size_t nPtrs = (endBytes - memBytes) / sizeof(void *);
    UIntNative* memPtr = (UIntNative*)memBytes;
    for (size_t i = 0; i < nPtrs; i++)
        *memPtr++ = pv;

    // handle remaining bytes at the end 
    memBytes = (uint8_t*)memPtr;
    while (memBytes < endBytes)
        *memBytes++ = (uint8_t)pv;
}

extern "C" uint8_t RhpArrayClear(System::Array *pArray, int32_t index, int32_t length)
{
    if (pArray == NULL)
        return false;

    EEType* pArrayType = pArray->get_EEType();

    size_t componentSize = pArrayType->RawGetComponentSize();
    if (componentSize == 0) // Not an array
        return false;

    if (!CheckArraySlice(pArray, index, length))
        return false;

    if (length == 0)
        return true;

    GCSafeFillMemory((uint8_t *)pArray->GetArrayData() + index * componentSize, length * componentSize, 0);

    return true;
}

extern "C" uint8_t RhTypeCast_AreTypesEquivalent(System::EETypePtr pType1, System::EETypePtr pType2)
{
    if (pType1.m_value == pType2.m_value)
    {
        return 1;
    }

    throw 42;
}

extern "C" System::String* RhNewArray(System::EETypePtr, int32_t len)
{
    return (System::String*)__allocate_string(len);
}

extern "C" intptr_t RhFindMethodStartAddress(intptr_t)
{
    throw 42;
}

extern "C" intptr_t RhGetModuleFromPointer(intptr_t)
{
    throw 42;
}

extern "C" System::Object* RhMemberwiseClone(System::Object*)
{
    throw 42;
}

extern "C" int32_t RhGetModuleFileName(intptr_t, uint16_t**)
{
    throw 42;
}

extern "C" void RhSuppressFinalize(System::Object*)
{
}

extern "C" uint8_t RhGetCorElementType(System::EETypePtr)
{
    throw 42;
}

extern "C" System::EETypePtr RhGetRelatedParameterType(System::EETypePtr)
{
    throw 42;
}

extern "C" uint16_t RhGetComponentSize(System::EETypePtr)
{
    throw 42;
}

extern "C" uint8_t RhHasReferenceFields(System::EETypePtr)
{
    throw 42;
}

extern "C" uint8_t RhIsValueType(System::EETypePtr)
{
    throw 42;
}

extern "C" uint8_t RhIsArray(System::EETypePtr)
{
    throw 42;
}

extern "C" intptr_t RhHandleAlloc(System::Object *pObject, int type)
{
    return (intptr_t)HndCreateHandle(g_HandleTableMap.pBuckets[0]->pTable[GetCurrentThreadHomeHeapNumber()], type, (OBJECTREF)pObject);
}

extern "C" void RhHandleFree(OBJECTHANDLE handle)
{
    DestroyTypedHandle(handle);
}

extern "C" intptr_t RhHandleAllocDependent(System::Object*, System::Object*)
{
    throw 42;
}

extern "C" System::Object* RhHandleGetDependent(intptr_t)
{
    throw 42;
}

extern "C" System::Object* RhHandleGet(OBJECTHANDLE handle)
{
    return (System::Object*)ObjectFromHandle(handle);
}

extern "C" intptr_t RhSetErrorInfoBuffer(intptr_t)
{
    throw 42;
}

extern "C" uint32_t RhGetLoadedModules(System::Object*)
{
    throw 42;
}

extern "C" uint8_t RhGetExceptionsForCurrentThread(System::Object*, int *)
{
    throw 42;
}

extern "C" intptr_t RhGetModuleFromEEType(System::EETypePtr)
{
    throw 42;
}

#ifndef CPPCODEGEN
SimpleModuleHeader __module = { NULL, NULL /* &__gcStatics, &__gcStaticsDescs */ };

extern "C" int repro_Program__Main();

extern "C" void __str_fixup();
extern "C" void __str_fixup_end();
int __reloc_string_fixup()
{
    for (unsigned** ptr = (unsigned**)__str_fixup;
         ptr < (unsigned**)__str_fixup_end; ptr++)
    {
        int utf8Len;
        uint8_t* bytes = (uint8_t*) *ptr;
        if (AsmDataFormat::DecodeUnsigned(&bytes, bytes + 5, (unsigned*)&utf8Len) != 0)
            return -1;

        assert(bytes <= ((uint8_t*)*ptr) + 5);
        assert(utf8Len >= 0);

        int strLen = 0;
        if (utf8Len != 0)
        {
            strLen = UTF8ToWideCharLen((char*)bytes, utf8Len);
            if (strLen <= 0) return -1;
        }

        OBJECTHANDLE handle;
        *((OBJECTHANDLE*)ptr) = __load_static_string_literal(bytes, utf8Len, strLen);
        // TODO: This "handle" will leak, deallocate with __unload
    }
    return 0;
}

int main(int argc, char * argv[]) {
    if (__initialize_runtime() != 0) return -1;
    __register_module(&__module);
    ReversePInvokeFrame frame; __reverse_pinvoke(&frame);
	
    if (__reloc_string_fixup() != 0) return -1;

    repro_Program__Main();

    __reverse_pinvoke_return(&frame);
    __shutdown_runtime();
    return 0;
}
#endif
