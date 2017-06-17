// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "CommonTypes.h"
#include "DebugFuncEval.h"

GVAL_IMPL_INIT(UInt32, g_FuncEvalMode, 0);
GVAL_IMPL_INIT(UInt64, g_FuncEvalTarget, 0);
GVAL_IMPL_INIT(UInt32, g_FuncEvalParameterBufferSize, 0);

#ifndef DACCESS_COMPILE

void* DebugFuncEval::GetFuncEvalTarget()
{
    return (void*)g_FuncEvalTarget;
}

UInt32 DebugFuncEval::GetFuncEvalParameterBufferSize()
{
    return g_FuncEvalParameterBufferSize;
}

UInt32 DebugFuncEval::GetFuncEvalMode()
{
    return g_FuncEvalMode;
}

/// <summary>
/// Retrieve the global FuncEval target address.
/// </summary>
/// <remarks>
/// During debugging, if a FuncEval is requested, 
/// the func eval infrastructure needs to know which function to call, and
/// the C# supporting code will call this API to obtain the FuncEval target address.
/// By that time, the value should have been set through the UpdateFuncEvalTarget() method 
/// on the ISosRedhawk7 interface.
/// </remarks>
EXTERN_C REDHAWK_API void* __cdecl RhpGetFuncEvalTargetAddress()
{
    return DebugFuncEval::GetFuncEvalTarget();
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

#endif //!DACCESS_COMPILE
