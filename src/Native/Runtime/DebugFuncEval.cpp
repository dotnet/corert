// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "CommonTypes.h"
#include "DebugFuncEval.h"

GVAL_IMPL_INIT(UInt64, g_FuncEvalTarget, 0);

#ifndef DACCESS_COMPILE

void* DebugFuncEval::GetFuncEvalTarget()
{
    return (void*)g_FuncEvalTarget;
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

#endif //!DACCESS_COMPILE
