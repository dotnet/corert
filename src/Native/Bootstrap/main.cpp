// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "common.h"

#include "gcenv.h"

#include "gc.h"
#include "objecthandle.h"

#pragma warning(disable:4297)

extern "C" Object * RhNewObject(MethodTable * pMT);
extern "C" Object * RhNewArray(MethodTable * pMT, int32_t elements);
extern "C" void RhpReversePInvoke2(ReversePInvokeFrame* pRevFrame);
extern "C" void RhpReversePInvokeReturn(ReversePInvokeFrame* pRevFrame);
extern "C" int32_t RhpEnableConservativeStackReporting();
extern "C" void RhpRegisterSimpleModule(SimpleModuleHeader* pModule);

#define DLL_PROCESS_ATTACH      1
extern "C" BOOL WINAPI RtuDllMain(HANDLE hPalInstance, DWORD dwReason, void* pvReserved);

#include "platform.h"

int __initialize_runtime()
{
    RtuDllMain(NULL, DLL_PROCESS_ATTACH, NULL);

    RhpEnableConservativeStackReporting();

    return 0;
}

void __shutdown_runtime()
{
}

void __reverse_pinvoke(ReversePInvokeFrame* pRevFrame)
{
    RhpReversePInvoke2(pRevFrame);
}

void __reverse_pinvoke_return(ReversePInvokeFrame* pRevFrame)
{
    RhpReversePInvokeReturn(pRevFrame);
}

void __register_module(SimpleModuleHeader* pModule)
{
    RhpRegisterSimpleModule(pModule);
}

namespace System_Private_CoreLib { namespace System { 

    class Object {
    public:
        MethodTable * get_EEType() { return *(MethodTable **)this; }
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

using namespace System_Private_CoreLib;

//
// The fast paths for object allocation and write barriers is performance critical. They are often
// hand written in assembly code, etc.
//
extern "C" Object * __allocate_object(MethodTable * pMT)
{
    return RhNewObject(pMT);
}

extern "C" void __EEType_System_Private_CoreLib_System_String();

Object * __allocate_string(int32_t len)
{
#ifdef CPPCODEGEN
    return RhNewArray(System::String::__getMethodTable(), len);
#else
    return RhNewArray((MethodTable*)__EEType_System_Private_CoreLib_System_String, len);
#endif
}

extern "C" Object * __allocate_array(size_t elements, MethodTable * pMT)
{
    return RhNewArray(pMT, (int32_t)elements); // TODO: type mismatch
}

extern "C" void __stelem_ref(System::Array * pArray, unsigned idx, Object * val)
{
    // TODO: Range checks, writer barrier, etc.
    ((Object **)(pArray->GetArrayData()))[idx] = val;
}

extern "C" void* __ldelema_ref(System::Array * pArray, unsigned idx, MethodTable * type)
{
    // TODO: Range checks, etc.
    return &(((Object **)(pArray->GetArrayData()))[idx]);
}

extern "C" void __throw_exception(void * pEx)
{
    // TODO: Exception throwing
    throw pEx;
}

extern "C" void __fail_fast()
{
    // TODO: FailFast
    throw 42;
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

extern "C" void Buffer_BlockCopy(class System::Array * src, int srcOfs, class System::Array * dst, int dstOfs, int count)
{
    // TODO: Argument validation
    memmove((uint8_t*)dst + 2 * sizeof(void*) + dstOfs, (uint8_t*)src + 2 * sizeof(void*) + srcOfs, count);
}

extern "C" Object* RhMemberwiseClone(Object*)
{
    throw 42;
}

extern "C" uint8_t RhGetCorElementType(MethodTable*)
{
    throw 42;
}

extern "C" MethodTable* RhGetRelatedParameterType(MethodTable*)
{
    throw 42;
}

extern "C" uint16_t RhGetComponentSize(MethodTable*)
{
    throw 42;
}

extern "C" uint8_t RhHasReferenceFields(MethodTable*)
{
    throw 42;
}

extern "C" uint8_t RhIsValueType(MethodTable*)
{
    throw 42;
}

extern "C" uint8_t RhIsArray(MethodTable*)
{
    throw 42;
}

extern "C" int32_t RhGetEETypeHash(MethodTable*)
{
    throw 42;
}

extern "C" uint8_t RhTypeCast_AreTypesEquivalent(MethodTable*, MethodTable*)
{
    throw 42;
}

extern "C" uint8_t RhTypeCast_AreTypesAssignable(MethodTable*, MethodTable*)
{
    throw 42;
}

extern "C" void RhGetCurrentThreadStackTrace()
{
    throw 42;
}

extern "C" intptr_t RhHandleAlloc(Object * pObject, int type)
{
    return (intptr_t)CreateTypedHandle(g_HandleTableMap.pBuckets[0]->pTable[GetCurrentThreadHomeHeapNumber()], pObject, type);
}

extern "C" intptr_t RhHandleAllocDependent(Object* pPrimary, Object* pSecondary)
{
    return (intptr_t)CreateDependentHandle(g_HandleTableMap.pBuckets[0]->pTable[GetCurrentThreadHomeHeapNumber()], pPrimary, pSecondary);
}

extern "C" void RhGetNonArrayBaseType()
{
    throw 42;
}

extern "C" void RhGetEETypeClassification()
{
    throw 42;
}

extern "C" void RhpUniversalTransition()
{
    throw 42;
}
extern "C" void RhpFailFastForPInvokeExceptionPreemp()
{
    throw 42;
}
extern "C" void RhpFailFastForPInvokeExceptionCoop()
{
    throw 42;
}
extern "C" void RhpThrowHwEx()
{
    throw 42;
}

extern "C" void RhExceptionHandling_FailedAllocation()
{
    throw 42;
}
extern "C" void RhpCalculateStackTraceWorker()
{
    throw 42;
}
extern "C" void RhThrowHwEx()
{
    throw 42;
}
extern "C" void RhThrowEx()
{
    throw 42;
}
extern "C" void RhRethrow()
{
    throw 42;
}

#ifdef CPPCODEGEN
extern "C" void RhpBulkWriteBarrier()
{
    throw 42;
}
#endif

#ifndef CPPCODEGEN
SimpleModuleHeader __module = { NULL, NULL /* &__gcStatics, &__gcStaticsDescs */ };

extern "C" int __managed__Main();

namespace AsmDataFormat
{
    typedef uint8_t byte;
    typedef uint32_t UInt32;

    static UInt32 ReadUInt32(byte **ppStream)
    {
        UInt32 result = *(UInt32*)(*ppStream); // Assumes little endian and unaligned access
        *ppStream += 4;
        return result;
    }
    static int DecodeUnsigned(byte** ppStream, byte* pStreamEnd, UInt32 *pValue)
    {
        if (*ppStream >= pStreamEnd)
            return -1;

        UInt32 value = 0;
        UInt32 val = **ppStream;
        if ((val & 1) == 0)
        {
            value = (val >> 1);
            *ppStream += 1;
        }
        else if ((val & 2) == 0)
        {
            if (*ppStream + 1 >= pStreamEnd)
                return -1;

            value = (val >> 2) |
                (((UInt32)*(*ppStream + 1)) << 6);
            *ppStream += 2;
        }
        else if ((val & 4) == 0)
        {
            if (*ppStream + 2 >= pStreamEnd)
                return -1;

            value = (val >> 3) |
                (((UInt32)*(*ppStream + 1)) << 5) |
                (((UInt32)*(*ppStream + 2)) << 13);
            *ppStream += 3;
        }
        else if ((val & 8) == 0)
        {
            if (*ppStream + 3 >= pStreamEnd)
                return -1;

            value = (val >> 4) |
                (((UInt32)*(*ppStream + 1)) << 4) |
                (((UInt32)*(*ppStream + 2)) << 12) |
                (((UInt32)*(*ppStream + 3)) << 20);
            *ppStream += 4;
        }
        else if ((val & 16) == 0)
        {
            if (*ppStream + 4 >= pStreamEnd)
                return -1;
            *ppStream += 1;
            value = ReadUInt32(ppStream);
        }
        else
        {
            return -1;
        }

        *pValue = value;
        return 0;
    }
}

extern "C" void __str_fixup();
extern "C" void __str_fixup_end();
int __strings_fixup()
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

        *((OBJECTHANDLE*)ptr) = __load_static_string_literal(bytes, utf8Len, strLen);
        // TODO: This "handle" will leak, deallocate with __unload
    }
    return 0;
}

extern "C" void* __GCStaticRegionStart;
extern "C" void* __GCStaticRegionEnd;
int __statics_fixup()
{
    for (void** currentBlock = &__GCStaticRegionStart; currentBlock < &__GCStaticRegionEnd; currentBlock++)
    {
        Object* gcBlock = __allocate_object((MethodTable*)*currentBlock);
        *currentBlock = CreateGlobalHandle(ObjectToOBJECTREF(gcBlock));
    }

    return 0;
}

int main(int argc, char * argv[]) {
    if (__initialize_runtime() != 0) return -1;
    __register_module(&__module);
    ReversePInvokeFrame frame; __reverse_pinvoke(&frame);
	
    if (__strings_fixup() != 0) return -1;
    if (__statics_fixup() != 0) return -1;

    int retval = __managed__Main();

    __reverse_pinvoke_return(&frame);
    __shutdown_runtime();
    return retval;
}

#endif
