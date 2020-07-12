// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
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

typedef void(*DebugFuncEvalAbortHelperFunctionType)(UInt64);

class DebugFuncEval
{
public:
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

    /// <summary>
    /// Retrieve the global FuncEval mode.
    /// </summary>
    /// <remarks>
    /// During debugging, if a FuncEval is requested, 
    /// the func eval infrastructure needs to know what mode to execute the FuncEval request 
    /// The C# supporting code will call this API to obtain the mode. By that time, the value 
    /// should have been set through the UpdateFuncEvalMode() method on the ISosRedhawk7 interface.
    /// </remarks>
    static UInt32 GetFuncEvalMode();

    /// <summary>
    /// Retrieve the most recent FuncEval Hijack instruction pointer
    /// </summary>
    /// <remarks>
    /// The most recent FuncEval Hijack instruction pointer is set through the debugger
    /// It is used for the stack walker to understand the hijack frame
    /// </remarks>
    static UInt64 GetMostRecentFuncEvalHijackInstructionPointer();
};

#else

class DebugFuncEval
{
public:
    /// <summary>
    /// Retrieve the most recent FuncEval Hijack instruction pointer
    /// </summary>
    /// <remarks>
    /// The most recent FuncEval Hijack instruction pointer is set through the debugger
    /// It is used for the stack walker to understand the hijack frame
    /// </remarks>
    static UInt64 GetMostRecentFuncEvalHijackInstructionPointer();
};

#endif //!DACCESS_COMPILE

#endif // __DEBUG_FUNC_EVAL_H__
