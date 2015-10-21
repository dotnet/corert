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

namespace mscorlib { namespace System { 

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

using namespace mscorlib;

//
// The fast paths for object allocation and write barriers is performance critical. They are often
// hand written in assembly code, etc.
//
extern "C" Object * __allocate_object(MethodTable * pMT)
{
    return RhNewObject(pMT);
}

extern "C" void __EEType_mscorlib_System_String();

Object * __allocate_string(int32_t len)
{
#ifdef CPPCODEGEN
    return RhNewArray(System::String::__getMethodTable(), len);
#else
    return RhNewArray((MethodTable*)__EEType_mscorlib_System_String, len);
#endif
}

extern "C" Object * __allocate_array(size_t elements, MethodTable * pMT)
{
    return RhNewArray(pMT, (int32_t)elements); // TODO: type mismatch
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

extern "C" intptr_t RhHandleAllocDependent(Object*, Object*)
{
    throw 42;
}

extern "C" void RhpUniversalTransition()
{
    throw 42;
}
extern "C" void RhpAssignRefEDX()
{
    throw 42;
}
extern "C" void RhpCheckedAssignRefEDX()
{
    throw 42;
}
extern "C" void RhpCheckedLockCmpXchgAVLocation()
{
    throw 42;
}
extern "C" void RhpCheckedXchgAVLocation()
{
    throw 42;
}
extern "C" void RhpCopyMultibyteDestAVLocation()
{
    throw 42;
}
extern "C" void RhpCopyMultibyteSrcAVLocation()
{
    throw 42;
}
extern "C" void RhpCopyMultibyteNoGCRefsDestAVLocation()
{
    throw 42;
}
extern "C" void RhpCopyMultibyteNoGCRefsSrcAVLocation()
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

#ifndef CPPCODEGEN
SimpleModuleHeader __module = { NULL, NULL /* &__gcStatics, &__gcStaticsDescs */ };

extern "C" int repro_Program__Main();

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

    repro_Program__Main();

    __reverse_pinvoke_return(&frame);
    __shutdown_runtime();
    return 0;
}

extern "C" void mscorlib_System_Runtime_RuntimeImports__RhNewArrayAsString()
{
    throw 42;
}
extern "C" void System_Console_Interop_mincore__GetConsoleOutputCP()
{
    throw 42;
}
extern "C" void System_Console_Interop_mincore__GetStdHandle()
{
    throw 42;
}
extern "C" void System_Console_Interop_mincore__WriteFile()
{
    throw 42;
}
extern "C" void mscorlib_System_String__get_Chars()
{
    throw 42;
}
extern "C" void mscorlib_System_Runtime_RuntimeImports__memmove()
{
    throw 42;
}
extern "C" void mscorlib_Interop_mincore__PInvoke_CompareStringOrdinal()
{
    throw 42;
}
extern "C" void System_Console_Interop_mincore__GetFileType()
{
    throw 42;
}
#endif

extern "C" Object * __allocate_mdarray(MethodTable * pMT, int32_t rank, ...)
{
    va_list argp;
    va_start(argp, rank);

    size_t elements = va_arg(argp, int);

    for (int32_t i = 1; i < rank; i++)
    {
        // TODO: Overflow checks
        elements *= va_arg(argp, int);
    }

    alloc_context * acontext = GetThread()->GetAllocContext();
    Object * pObject;

    // TODO: Overflow checks
    size_t size = 2 * sizeof(intptr_t) + 2 * rank * sizeof(int32_t) + (elements * pMT->RawGetComponentSize());
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

    *(int32_t *)(((intptr_t *)pObject) + 1) = (int32_t)elements;
    int32_t* pSizes = (int32_t*)(((intptr_t *)pObject) + 2);

    va_start(argp, rank);
    for (int32_t i = 0; i < rank; i++)
    {
        *(pSizes + i) = va_arg(argp, int);
    }

    return pObject;
}

