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

// 
// Unboxing stubs need to be merged, folded and sorted. They are delimited by two special sections (.unbox$A
// and .unbox$Z). All unboxing stubs are in .unbox$M sections.
//
#pragma comment(linker, "/merge:.unbox=.text")

extern "C" void __managedcode_a();
extern "C" void __managedcode_z();

extern "C" void __unbox_a();
extern "C" void __unbox_z();

#else // _MSC_VER

#if defined(__APPLE__)

extern void * __modules_a[] __asm("section$start$__DATA$__modules");
extern void * __modules_z[] __asm("section$end$__DATA$__modules");
extern char __managedcode_a __asm("section$start$__TEXT$__managedcode");
extern char __managedcode_z __asm("section$end$__TEXT$__managedcode");
extern char __unbox_a __asm("section$start$__TEXT$__unbox");
extern char __unbox_z __asm("section$end$__TEXT$__unbox");

#else // __APPLE__

extern "C" void * __start___modules[];
extern "C" void * __stop___modules[];
static void * (&__modules_a)[] = __start___modules;
static void * (&__modules_z)[] = __stop___modules;

extern "C" char __start___managedcode;
extern "C" char __stop___managedcode;
static char& __managedcode_a = __start___managedcode;
static char& __managedcode_z = __stop___managedcode;

extern "C" char __start___unbox;
extern "C" char __stop___unbox;
static char& __unbox_a = __start___unbox;
static char& __unbox_z = __stop___unbox;

#endif // __APPLE__

#endif // _MSC_VER

#endif // !CPPCODEGEN

// Do not warn that extern C methods throw exceptions. This is temporary
// as long as we have unimplemented/throwing APIs in this file.
#pragma warning(disable:4297)

#ifdef CPPCODEGEN

extern "C" Object * RhNewObject(MethodTable * pMT);
extern "C" Object * RhNewArray(MethodTable * pMT, int32_t elements);
extern "C" void * RhTypeCast_IsInstanceOf(void * pObject, MethodTable * pMT);
extern "C" void * RhTypeCast_CheckCast(void * pObject, MethodTable * pMT);
extern "C" void RhpStelemRef(void * pArray, int index, void * pObj);
extern "C" void * RhpLdelemaRef(void * pArray, int index, MethodTable * pMT);
extern "C" __NORETURN void RhpThrowEx(void * pEx);
extern "C" void RhDebugBreak();

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

extern "C" void __debug_break()
{
    RhDebugBreak();
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

extern "C" void RhpPInvoke2(PInvokeTransitionFrame* pFrame);
extern "C" void RhpPInvokeReturn2(PInvokeTransitionFrame* pFrame);

void __pinvoke(PInvokeTransitionFrame* pFrame)
{
    RhpPInvoke2(pFrame);
}

void __pinvoke_return(PInvokeTransitionFrame* pFrame)
{
    RhpPInvokeReturn2(pFrame);
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

// This works around System.Private.Interop's references to Interop.Native.
// This won't be needed once we stop dragging in S.P.Interop for basic p/invoke support.
extern "C" void CCWAddRef()
{
    throw "CCWAddRef";
}

extern "C" void __fail_fast()
{
    // TODO: FailFast
    printf("Call to an unimplemented runtime method; execution cannot continue.\n");
    printf("Method: __fail_fast\n");
    exit(-1);
}

extern "C" bool RhInitialize();
extern "C" void RhpEnableConservativeStackReporting();
extern "C" void RhpShutdown();
extern "C" void RhSetRuntimeInitializationCallback(int (*fPtr)());

#ifndef CPPCODEGEN

extern "C" bool RhRegisterOSModule(void * pModule,
    void * pvManagedCodeStartRange, uint32_t cbManagedCodeRange,
    void * pvUnboxingStubsStartRange, uint32_t cbUnboxingStubsRange,
    void ** pClasslibFunctions, uint32_t nClasslibFunctions);

extern "C" void* PalGetModuleHandleFromPointer(void* pointer);

extern "C" void GetRuntimeException();
extern "C" void FailFast();
extern "C" void AppendExceptionStackFrame();
extern "C" void GetSystemArrayEEType();
extern "C" void OnFirstChanceException();

typedef void(*pfn)();

static const pfn c_classlibFunctions[] = {
    &GetRuntimeException,
    &FailFast,
    nullptr, // &UnhandledExceptionHandler,
    &AppendExceptionStackFrame,
    nullptr, // &CheckStaticClassConstruction,
    &GetSystemArrayEEType,
    &OnFirstChanceException,
    nullptr, // &DebugFuncEvalHelper,
    nullptr, // &DebugFuncEvalAbortHelper,
};

#endif // !CPPCODEGEN

extern "C" void InitializeModules(void* osModule, void ** modules, int count, void ** pClasslibFunctions, int nClasslibFunctions);

#ifndef CORERT_DLL
#define CORERT_ENTRYPOINT __managed__Main
#if defined(_WIN32)
extern "C" int __managed__Main(int argc, wchar_t* argv[]);
#else
extern "C" int __managed__Main(int argc, char* argv[]);
#endif
#else
#define CORERT_ENTRYPOINT __managed__Startup
extern "C" void __managed__Startup();
#endif // !CORERT_DLL

static int InitializeRuntime()
{
    if (!RhInitialize())
        return -1;

#if defined(CPPCODEGEN)
    RhpEnableConservativeStackReporting();
#endif // CPPCODEGEN

#ifndef CPPCODEGEN
    void * osModule = PalGetModuleHandleFromPointer((void*)&CORERT_ENTRYPOINT);

    // TODO: pass struct with parameters instead of the large signature of RhRegisterOSModule
    if (!RhRegisterOSModule(
        osModule,
        (void*)&__managedcode_a, (uint32_t)((char *)&__managedcode_z - (char*)&__managedcode_a),
        (void*)&__unbox_a, (uint32_t)((char *)&__unbox_z - (char*)&__unbox_a),
        (void **)&c_classlibFunctions, _countof(c_classlibFunctions)))
    {
        return -1;
    }
#endif // !CPPCODEGEN

#ifndef CPPCODEGEN
    InitializeModules(osModule, __modules_a, (int)((__modules_z - __modules_a)), (void **)&c_classlibFunctions, _countof(c_classlibFunctions));
#elif defined _WASM_
    // WASMTODO: Figure out what to do here. This is a NativeCallable method in the runtime
    // and we also would have to figure out what to pass for pModuleHeaders
#else // !CPPCODEGEN
    InitializeModules(nullptr, (void**)RtRHeaderWrapper(), 2, nullptr, 0);
#endif // !CPPCODEGEN

#ifdef CORERT_DLL
    // Run startup method immediately for a native library
    __managed__Startup();
#endif // CORERT_DLL

    return 0;
}

#ifndef CORERT_DLL
#if defined(_WIN32)
int __cdecl wmain(int argc, wchar_t* argv[])
#else
int main(int argc, char* argv[])
#endif
{
    int initval = InitializeRuntime();
    if (initval != 0)
        return initval;

    int retval;
#ifdef CPPCODEGEN
    try
#endif
    {
        retval = __managed__Main(argc, argv);
    }
#ifdef CPPCODEGEN
    catch (const char* &e)
    {
        printf("Call to an unimplemented runtime method; execution cannot continue.\n");
        printf("Method: %s\n", e);
        retval = -1;
    }
#endif
    RhpShutdown();

    return retval;
}
#endif // !CORERT_DLL

#ifdef CORERT_DLL
static struct InitializeRuntimePointerHelper
{
    InitializeRuntimePointerHelper()
    {
        RhSetRuntimeInitializationCallback(&InitializeRuntime);
    }
} initializeRuntimePointerHelper;
#endif // CORERT_DLL
