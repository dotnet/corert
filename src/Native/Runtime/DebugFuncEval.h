// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// -----------------------------------------------------------------------------------------------------------
// Support for evaluating expression in the debuggee during debugging
// -----------------------------------------------------------------------------------------------------------

#ifndef __DEBUG_FUNC_EVAL_H__
#define __DEBUG_FUNC_EVAL_H__

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"

#ifndef DACCESS_COMPILE

class DebugFuncEval
{
public:
    /// <summary>
    /// Retrieve the global FuncEval target address
    /// </summary>
    /// <remarks>
    /// During debugging, if a FuncEval is requested, 
    /// The func eval infrastructure needs to know which function to call, and
    /// It will call this API to obtain the target address.
    /// By the time, the value should have been set through the UpdateFuncEvalTarget() method 
    /// on the ISosRedhawk7 interface.
    /// </remarks>
    static void* GetFuncEvalTarget();


    /// <summary>
    /// Retrieve the global FuncEval parameter buffer size.
    /// </summary>
    /// <remarks>
    /// During debugging, if a FuncEval is requested, 
    /// the func eval infrastructure needs to know how much buffer to allocate for the debugger to 
    /// write the parameter information in. The C# supporting code will call this API to obtain the 
    /// buffer size. By that time, the value should have been set through the UpdateFuncEvalParameterSize() 
    /// method on the ISosRedhawk7 interface.
    /// </remarks>
    static UInt32 GetFuncEvalParameterBufferSize();
};

#endif //!DACCESS_COMPILE

#endif // __DEBUG_FUNC_EVAL_H__
