// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "common.h"

#include "sal.h"
#include "gcenv.structs.h"
#include "gcenv.base.h"

#include <stdlib.h> 

#pragma warning(disable:4297)

extern "C" Object * RhNewObject(MethodTable * pMT);
extern "C" Object * RhNewArray(MethodTable * pMT, int32_t elements);
extern "C" void * RhTypeCast_IsInstanceOf(void * pObject, MethodTable * pMT);
extern "C" void * RhTypeCast_CheckCast(void * pObject, MethodTable * pMT);
extern "C" __declspec(noreturn) void RhpThrowEx(void * pEx);

#ifdef CPPCODEGEN

extern "C" Object * __allocate_object(MethodTable * pMT)
{
    return RhNewObject(pMT);
}

extern "C" Object * __allocate_array(size_t elements, MethodTable * pMT)
{
    return RhNewArray(pMT, (int32_t)elements); // TODO: type mismatch
}

extern "C" Object * __castclass(void * obj, MethodTable * pTargetMT)
{
    return (Object *)RhTypeCast_CheckCast(obj, pTargetMT);
}

extern "C" Object * __isinst(void * obj, MethodTable * pTargetMT)
{
    return (Object *)RhTypeCast_IsInstanceOf(obj, pTargetMT);
}

extern "C" void __throw_exception(void * pEx)
{
    RhpThrowEx(pEx);
}

void __range_check_fail()
{
    throw "ThrowRangeOverflowException";
}

#endif // CPPCODEGEN


extern "C" void RhpReversePInvoke2(ReversePInvokeFrame* pRevFrame);
extern "C" void RhpReversePInvokeReturn(ReversePInvokeFrame* pRevFrame);
extern "C" int32_t RhpEnableConservativeStackReporting();
extern "C" void RhpRegisterSimpleModule(SimpleModuleHeader* pModule);
extern "C" void * RhpHandleAlloc(void * pObject, int handleType);

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

extern "C" void __EEType_System_Private_CoreLib_System_String();
extern "C" void __EEType_System_Private_CoreLib_System_String__Array();

Object * __allocate_string(int32_t len)
{
#ifdef CPPCODEGEN
    return RhNewArray(System::String::__getMethodTable(), len);
#else
    return RhNewArray((MethodTable*)__EEType_System_Private_CoreLib_System_String, len);
#endif
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

void PrintStringObject(System::String *pStringToPrint)
{
    // Get the number of characters in managed string (stored as UTF16)
    int32_t length = *((int32_t*)((char*)(pStringToPrint)+sizeof(intptr_t)));

    // Get the pointer to the start of the character array
    uint16_t *pString = (uint16_t*)((char*)(pStringToPrint)+sizeof(intptr_t) + sizeof(int32_t));
    
    // Loop to display the string
    int32_t index = 0;
    while (index < length)
    {
        putwchar(*pString);
        pString++;
        index++;
    }   
}
extern "C" void __not_yet_implemented(System::String * pMethodName, System::String * pMessage)
{
    printf("ILCompiler failed generating code for this method; execution cannot continue.\n");
    printf("This is likely because of a feature that is not yet implemented in the compiler.\n");
    printf("Method: ");
    PrintStringObject(pMethodName);
    printf("\n\n");
    printf("Reason: ");
    PrintStringObject(pMessage);
    printf("\n");

    exit(-1);
}

extern "C" void __fail_fast()
{
    // TODO: FailFast
    throw "__fail_fast";
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
    // TODO: OOM handling
    return (OBJECTHANDLE)RhpHandleAlloc(pString, 2 /* Normal */);
}

Object * __get_commandline_args(int argc, char * argv[])
{
#ifdef CPPCODEGEN
	 MethodTable * pStringArrayMT = System::String__Array::__getMethodTable();
#else
	 MethodTable * pStringArrayMT = (MethodTable*)__EEType_System_Private_CoreLib_System_String__Array;
#endif
	System::Array * args = (System::Array *)RhNewArray(pStringArrayMT, argc);

	for (int i = 0; i < argc; i++)
	{
		__stelem_ref(args, i, __load_string_literal(argv[i]));
	}
	
	return (Object *)args;
}

extern "C" void RhGetCurrentThreadStackTrace()
{
    throw "RhGetCurrentThreadStackTrace";
}

extern "C" void RhpGetDispatchCellInfo()
{
    throw "RhpGetDispatchCellInfo";
}
extern "C" void RhpUpdateDispatchCellCache()
{
    throw "RhpUpdateDispatchCellCache";
}
extern "C" void RhpSearchDispatchCellCache()
{
    throw "RhpSearchDispatchCellCache";
}
extern "C" void RhCollect()
{
    throw "RhCollect";
}
extern "C" void RhpCallCatchFunclet()
{
    throw "RhpCallCatchFunclet";
}
extern "C" void RhpCallFilterFunclet()
{
    throw "RhpCallFilterFunclet";
}
extern "C" void RhpCallFinallyFunclet()
{
    throw "RhpCallFinallyFunclet";
}
extern "C" void RhpUniversalTransition()
{
    throw "RhpUniversalTransition";
}
extern "C" void RhpFailFastForPInvokeExceptionPreemp()
{
    throw "RhpFailFastForPInvokeExceptionPreemp";
}
extern "C" void RhpThrowEx(void * pEx)
{
    throw "RhpThrowEx";
}
extern "C" void RhpThrowHwEx()
{
    throw "RhpThrowHwEx";
}
extern "C" void RhpEtwExceptionThrown()
{
    throw "RhpEtwExceptionThrown";
}
extern "C" void RhReRegisterForFinalize()
{
    throw "RhReRegisterForFinalize";
}

#ifndef CPPCODEGEN
SimpleModuleHeader __module = { NULL, NULL /* &__gcStatics, &__gcStaticsDescs */ };

extern "C" int __managed__Main(Object*);

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
        Object* gcBlock = RhNewObject((MethodTable*)*currentBlock);
        // TODO: OOM handling
        *currentBlock = RhpHandleAlloc(gcBlock, 2 /* Normal */);
    }

    return 0;
}

int main(int argc, char * argv[]) {
    if (__initialize_runtime() != 0) return -1;
    __register_module(&__module);
    ReversePInvokeFrame frame; __reverse_pinvoke(&frame);
	
    if (__strings_fixup() != 0) return -1;
    if (__statics_fixup() != 0) return -1;

    int retval;
    try
    {
		// Managed apps don't see the first args argument (full path of executable) so skip it
		assert(argc > 0);
		Object* args = __get_commandline_args(argc - 1, argv + 1);
		retval = __managed__Main(args);
    }
    catch (const char* &e)
    {
        printf("Call to an unimplemented runtime method; execution cannot continue.\n");
        printf("Method: %s\n", e);
        retval = -1;
    }

    __reverse_pinvoke_return(&frame);
    __shutdown_runtime();
    return retval;
}

#endif
