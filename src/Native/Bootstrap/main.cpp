// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
extern "C" void RhpStelemRef(void * pArray, int index, void * pObj);
extern "C" void * RhpLdelemaRef(void * pArray, int index, MethodTable * pMT);
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

extern "C" void __stelem_ref(void * pArray, unsigned idx, void * obj)
{
    RhpStelemRef(pArray, idx, obj);
}

extern "C" void* __ldelema_ref(void * pArray, unsigned idx, MethodTable * type)
{
    return RhpLdelemaRef(pArray, idx, type);
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

REDHAWK_PALIMPORT bool REDHAWK_PALAPI PalInit();

int __initialize_runtime()
{
    if (!PalInit())
    {
        return 1;
    }

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

extern "C" void* __EEType_System_Private_CoreLib_System_String;

Object * __allocate_string(int32_t len)
{
#ifdef CPPCODEGEN
    return RhNewArray(System::String::__getMethodTable(), len);
#else
    return RhNewArray((MethodTable*)__EEType_System_Private_CoreLib_System_String, len);
#endif
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

extern "C" void RhGetCurrentThreadStackTrace()
{
    throw "RhGetCurrentThreadStackTrace";
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

extern "C" void * g_pDispatchMapTemporaryWorkaround;
void * g_pDispatchMapTemporaryWorkaround;

extern "C" void* __StringTableStart;
extern "C" void* __StringTableEnd;
extern "C" void* GetModuleSection(int id, int* length)
{
    struct ModuleSectionSymbol
    {
        void* symbolId;
        size_t length;
    };

    // TODO: emit this table from the compiler per module.
    // The order should be kept in sync with ModuleSectionIds in StartupCodeHelpers.cs in CoreLib.
    static ModuleSectionSymbol symbols[] = {
#ifdef CPPCODEGEN
        { System::String::__getMethodTable(), sizeof(void*) },
        { nullptr, 0 },
#else
        { &__EEType_System_Private_CoreLib_System_String, sizeof(void*) },
        { &__StringTableStart, (size_t)((uint8_t*)&__StringTableEnd - (uint8_t*)&__StringTableStart) },
#endif
    };

    *length = (int) symbols[id].length;
    return symbols[id].symbolId;
}

#ifndef CPPCODEGEN
SimpleModuleHeader __module = { NULL, NULL /* &__gcStatics, &__gcStaticsDescs */ };

extern "C" void* __InterfaceDispatchMapTable;
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

#if defined(_WIN32)
extern "C" int __managed__Main(int argc, wchar_t* argv[]);
int wmain(int argc, wchar_t* argv[])
#else
extern "C" int __managed__Main(int argc, char* argv[]);
int main(int argc, char* argv[])
#endif
{
    if (__initialize_runtime() != 0) return -1;
    __register_module(&__module);
    g_pDispatchMapTemporaryWorkaround = (void*)&__InterfaceDispatchMapTable;
    ReversePInvokeFrame frame; __reverse_pinvoke(&frame);

    if (__statics_fixup() != 0) return -1;

    int retval;
    try
    {
		// Managed apps don't see the first args argument (full path of executable) so skip it
		assert(argc > 0);
		retval = __managed__Main(argc, argv);
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

#endif // !CPPCODEGEN
