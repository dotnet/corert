// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"

#include "sal.h"
#include "gcenv.structs.h"
#include "gcenv.base.h"

#include <stdlib.h> 

#ifndef CPPCODEGEN

//
// This is the mechanism whereby multiple linked modules contribute their global data for initialization at
// startup of the application.
//
// ILC creates sections in the output obj file to mark the beginning and end of merged global data.
// It defines sentinel symbols that are used to get the addresses of the start and end of global data 
// at runtime. The section names are platform-specific to match platform-specific linker conventions.
//
#if defined(_MSC_VER)

#pragma section(".modules$A", read)
#pragma section(".modules$Z", read)
extern "C" __declspec(allocate(".modules$A")) void * __modules_a[];
extern "C" __declspec(allocate(".modules$Z")) void * __modules_z[];

__declspec(allocate(".modules$A")) void * __modules_a[] = { nullptr };
__declspec(allocate(".modules$Z")) void * __modules_z[] = { nullptr };

//
// Each obj file compiled from managed code has a .modules$I section containing a pointer to its ReadyToRun
// data (which points at eager class constructors, frozen strings, etc).
//
// The #pragma ... /merge directive folds the book-end sections and all .modules$I sections from all input
// obj files into .rdata in alphabetical order.
//
#pragma comment(linker, "/merge:.modules=.rdata")

extern "C" void __managedcode_a();
extern "C" void __managedcode_z();

#else // _MSC_VER

#if defined(__APPLE__)

extern void * __modules_a[] __asm("section$start$__DATA$__modules");
extern void * __modules_z[] __asm("section$end$__DATA$__modules");
extern char __managedcode_a __asm("section$start$__TEXT$__managedcode");
extern char __managedcode_z __asm("section$end$__TEXT$__managedcode");

#else // __APPLE__

extern "C" void * __start___modules[];
extern "C" void * __stop___modules[];
static void * (&__modules_a)[] = __start___modules;
static void * (&__modules_z)[] = __stop___modules;

extern "C" char __start___managedcode;
extern "C" char __stop___managedcode;
static char& __managedcode_a = __start___managedcode;
static char& __managedcode_z = __stop___managedcode;

#endif // __APPLE__

#endif // _MSC_VER

#endif // !CPPCODEGEN


#ifdef CPPCODEGEN

#pragma warning(disable:4297)

extern "C" Object * RhNewObject(MethodTable * pMT);
extern "C" Object * RhNewArray(MethodTable * pMT, int32_t elements);
extern "C" void * RhTypeCast_IsInstanceOf(void * pObject, MethodTable * pMT);
extern "C" void * RhTypeCast_CheckCast(void * pObject, MethodTable * pMT);
extern "C" void RhpStelemRef(void * pArray, int index, void * pObj);
extern "C" void * RhpLdelemaRef(void * pArray, int index, MethodTable * pMT);
extern "C" __NORETURN void RhpThrowEx(void * pEx);

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

extern "C" void RhpReversePInvoke2(ReversePInvokeFrame* pRevFrame);
extern "C" void RhpReversePInvokeReturn2(ReversePInvokeFrame* pRevFrame);

void __reverse_pinvoke(ReversePInvokeFrame* pRevFrame)
{
    RhpReversePInvoke2(pRevFrame);
}

void __reverse_pinvoke_return(ReversePInvokeFrame* pRevFrame)
{
    RhpReversePInvokeReturn2(pRevFrame);
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

Object * __load_string_literal(const char * string)
{
    // TODO: Cache/intern string literals
    // TODO: Unicode string literals

    size_t len = strlen(string);

    Object * pString = RhNewArray(System_Private_CoreLib::System::String::__getMethodTable(), (int32_t)len);

    uint16_t * p = (uint16_t *)((char*)pString + sizeof(intptr_t) + sizeof(int32_t));
    for (size_t i = 0; i < len; i++)
        p[i] = string[i];
    return pString;
}

extern "C" void RhpThrowEx(void * pEx)
{
    throw "RhpThrowEx";
}
extern "C" void RhpThrowHwEx()
{
    throw "RhpThrowHwEx";
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
extern "C" void RhpUniversalTransition_DebugStepTailCall()
{
    throw "RhpUniversalTransition_DebugStepTailCall";
}

void* RtRHeaderWrapper();

#endif // CPPCODEGEN

extern "C" void __fail_fast()
{
    // TODO: FailFast
    throw "__fail_fast";
}

extern "C" void RhpEtwExceptionThrown()
{
    throw "RhpEtwExceptionThrown";
}

extern "C" bool REDHAWK_PALAPI PalInit();

#define DLL_PROCESS_ATTACH      1
extern "C" BOOL WINAPI RtuDllMain(HANDLE hPalInstance, DWORD dwReason, void* pvReserved);

extern "C" int32_t RhpEnableConservativeStackReporting();

extern "C" void RhpShutdown();

#ifndef CPPCODEGEN

extern "C" bool RhpRegisterCoffModule(void * pModule,
    void * pvStartRange, uint32_t cbRange,
    void ** pClasslibFunctions, uint32_t nClasslibFunctions);

extern "C" bool RhpRegisterUnixModule(void * pModule,
    void * pvStartRange, uint32_t cbRange,
    void ** pClasslibFunctions, uint32_t nClasslibFunctions);

#ifdef _WIN32
extern "C" void* WINAPI GetModuleHandleW(const wchar_t *);
#else
extern "C" void* WINAPI PalGetModuleHandleFromPointer(void* pointer);
#endif

extern "C" void GetRuntimeException();
extern "C" void FailFast();
extern "C" void AppendExceptionStackFrame();

typedef void(*pfn)();

static const pfn c_classlibFunctions[] = {
    &GetRuntimeException,
    &FailFast,
    nullptr, // &UnhandledExceptionHandler,
    &AppendExceptionStackFrame,
};

#endif // !CPPCODEGEN

extern "C" void InitializeModules(void ** modules, int count);

#if defined(_WIN32)
extern "C" int __managed__Main(int argc, wchar_t* argv[]);
int wmain(int argc, wchar_t* argv[])
#else
extern "C" int __managed__Main(int argc, char* argv[]);
int main(int argc, char* argv[])
#endif
{
    if (!PalInit())
        return -1;

    if (!RtuDllMain(NULL, DLL_PROCESS_ATTACH, NULL))
        return -1;

    if (!RhpEnableConservativeStackReporting())
        return -1;

#ifndef CPPCODEGEN
#if defined(_WIN32)
    if (!RhpRegisterCoffModule(GetModuleHandleW(NULL),
#else // _WIN32
    if (!RhpRegisterUnixModule(PalGetModuleHandleFromPointer((void*)&main),
#endif // _WIN32
        (void*)&__managedcode_a, (uint32_t)((char *)&__managedcode_z - (char*)&__managedcode_a),
        (void **)&c_classlibFunctions, _countof(c_classlibFunctions)))
    {
        return -1;
    }
#endif // !CPPCODEGEN

#ifdef CPPCODEGEN
    ReversePInvokeFrame frame;
    __reverse_pinvoke(&frame);
#endif

#ifndef CPPCODEGEN
    InitializeModules(__modules_a, (int)((__modules_z - __modules_a)));
#else // !CPPCODEGEN
    InitializeModules((void**)RtRHeaderWrapper(), 2);
#endif // !CPPCODEGEN

    int retval;
    try
    {
        retval = __managed__Main(argc, argv);
    }
    catch (const char* &e)
    {
        printf("Call to an unimplemented runtime method; execution cannot continue.\n");
        printf("Method: %s\n", e);
        retval = -1;
    }

#ifdef CPPCODEGEN
    __reverse_pinvoke_return(&frame);
#endif

    RhpShutdown();

    return retval;
}
