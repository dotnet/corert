// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "CommonTypes.h"
#include "DebugFuncEval.h"

GVAL_IMPL_INIT(UInt32, g_FuncEvalMode, 0);
GVAL_IMPL_INIT(UInt32, g_FuncEvalParameterBufferSize, 0);
GVAL_IMPL_INIT(UInt64, g_MostRecentFuncEvalHijackInstructionPointer, 0);
GPTR_IMPL_INIT(PTR_VOID, g_HighLevelDebugFuncEvalAbortHelperAddr, 0);

#ifndef DACCESS_COMPILE

/* static */ UInt32 DebugFuncEval::GetFuncEvalParameterBufferSize()
{
    return g_FuncEvalParameterBufferSize;
}

/* static */ UInt32 DebugFuncEval::GetFuncEvalMode()
{
    return g_FuncEvalMode;
}

/* static */ UInt64 DebugFuncEval::GetMostRecentFuncEvalHijackInstructionPointer()
{
    return g_MostRecentFuncEvalHijackInstructionPointer;
}

/* static */ HighLevelDebugFuncEvalAbortHelperType DebugFuncEval::GetHighLevelDebugFuncEvalAbortHelper()
{
    return (HighLevelDebugFuncEvalAbortHelperType)g_HighLevelDebugFuncEvalAbortHelperAddr;
}

/* static */ void DebugFuncEval::SetHighLevelDebugFuncEvalAbortHelper(HighLevelDebugFuncEvalAbortHelperType highLevelDebugFuncEvalAbortHelper)
{
    g_HighLevelDebugFuncEvalAbortHelperAddr = (PTR_PTR_VOID)highLevelDebugFuncEvalAbortHelper;
}

/// <summary>
/// Retrieve the global FuncEval parameter buffer size.
/// </summary>
/// <remarks>
/// During debugging, if a FuncEval is requested, 
/// the func eval infrastructure needs to know how much buffer to allocate for the debugger to 
/// write the parameter information in. The C# supporting code will call this API to obtain the 
/// buffer size. By that time, the value should have been set through the UpdateFuncEvalParameterBufferSize() 
/// method on the ISosRedhawk7 interface.
/// </remarks>
EXTERN_C REDHAWK_API UInt32 __cdecl RhpGetFuncEvalParameterBufferSize()
{
    return DebugFuncEval::GetFuncEvalParameterBufferSize();
}

/// <summary>
/// Retrieve the global FuncEval mode.
/// </summary>
/// <remarks>
/// During debugging, if a FuncEval is requested, 
/// the func eval infrastructure needs to know what mode to execute the FuncEval request 
/// The C# supporting code will call this API to obtain the mode. By that time, the value 
/// should have been set through the UpdateFuncEvalMode() method on the ISosRedhawk7 interface.
/// </remarks>
EXTERN_C REDHAWK_API UInt32 __cdecl RhpGetFuncEvalMode()
{
    return DebugFuncEval::GetFuncEvalMode();
}

/// <summary>
/// Initiate the func eval abort
/// </summary>
/// <remarks>
/// This is the entry point of FuncEval abort
/// When the debugger decides to abort the FuncEval, it will create a remote thread calling this function.
/// This function will call back into the highLevelDebugFuncEvalAbortHelper to perform the abort.
EXTERN_C REDHAWK_API void __cdecl RhpInitiateFuncEvalAbort(void* pointerFromDebugger)
{
    HighLevelDebugFuncEvalAbortHelperType highLevelDebugFuncEvalAbortHelper = DebugFuncEval::GetHighLevelDebugFuncEvalAbortHelper();
    highLevelDebugFuncEvalAbortHelper((UInt64)pointerFromDebugger);
}

/// <summary>
/// Set the high level debug func eval abort helper
/// </summary>
/// <remarks>
/// The high level debug func eval abort helper is a function that perform the actual func eval abort 
/// It is implemented in System.Private.Debug.dll 
EXTERN_C REDHAWK_API void __cdecl RhpSetHighLevelDebugFuncEvalAbortHelper(HighLevelDebugFuncEvalAbortHelperType highLevelDebugFuncEvalAbortHelper)
{
    DebugFuncEval::SetHighLevelDebugFuncEvalAbortHelper(highLevelDebugFuncEvalAbortHelper);
}

#else

UInt64 DebugFuncEval::GetMostRecentFuncEvalHijackInstructionPointer()
{
    return g_MostRecentFuncEvalHijackInstructionPointer;
}

#endif //!DACCESS_COMPILE

EXTERN_C void * RhpDebugFuncEvalHelper;
GPTR_IMPL_INIT(PTR_VOID, g_RhpDebugFuncEvalHelperAddr, &RhpDebugFuncEvalHelper);

GPTR_IMPL_INIT(PTR_VOID, g_RhpInitiateFuncEvalAbortAddr, (void**)&RhpInitiateFuncEvalAbort);