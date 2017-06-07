// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

// TODO: Implement Arm support

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// RhpThrowHwEx
//
// INPUT:  RDI:  exception code of fault
//         RSI:  faulting RIP
//
// OUTPUT:
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
NESTED_ENTRY RhpThrowHwEx, _TEXT, NoHandler

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

  // struct PAL_LIMITED_CONTEXT
  // {
  // }

  // allocate outgoing args area and space for the ExInfo

  // init the exception object to null
  // init to the first pass
  // ExKind.HardwareFault

  // link the ExInfo into thread's ExInfo chain
  // pExInfo->m_pPrevExInfo = m_pExInfoStackHead
  // m_pExInfoStackHead = pExInfo

  // set the exception context field on the ExInfo
  // rdx <- PAL_LIMITED_CONTEXT*
  // init ExInfo.m_pExContext

  // rdi still contains the exception code
  // rsi contains the address of the ExInfo

  // call C_Func(RhThrowHwEx)

ALTERNATE_ENTRY RhpThrowHwEx2

  // no return
  // int 3

NESTED_END RhpThrowHwEx


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// RhpThrowEx
//
// INPUT:  RDI:  exception object
//
// OUTPUT:
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
NESTED_ENTRY RhpThrowEx, _TEXT, NoHandler

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

  // struct PAL_LIMITED_CONTEXT
  // {

  // }

  // allocate space for the ExInfo

  // There is runtime C# code that can tail call to RhpThrowEx using a binder intrinsic.  So the return
  // address could have been hijacked when we were in that C# code and we must remove the hijack and
	// reflect the correct return address in our exception context record.  The other throw helpers don't
  // need this because they cannot be tail-called from C#.

  // rsi <- ExInfo*

  // init the exception object to null
  // init to the first pass
  // ExKind.Throw

  // link the ExInfo into the thread's ExInfo chain
  // pExInfo->m_pPrevExInfo = m_pExInfoStackHead
  // m_pExInfoStackHead = pExInfo

  // set the exception context field on the ExInfo
	// rdx <- PAL_LIMITED_CONTEXT*
  // init ExInfo.m_pExContext

 	// rdi still contains the exception object
	// rsi contains the address of the ExInfo

  // rdi still contains the exception object
	// rsi contains the address of the ExInfo
	// call    C_FUNC(RhThrowEx)

ALTERNATE_ENTRY RhpThrowEx2
  // no return
	// int 3

NESTED_END RhpThrowEx, _TEXT


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// void FASTCALL RhpRethrow()
//
// SUMMARY:  Similar to RhpThrowEx, except that it passes along the currently active ExInfo
//
// INPUT:
//
// OUTPUT:
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
NESTED_ENTRY RhpRethrow, _TEXT, NoHandler

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

	//  struct PAL_LIMITED_CONTEXT
	//  {
  //  }

	// allocate space for the ExInfo

  // rsi <- ExInfo*
	// init the exception object to null
	// init to the first pass

  // link the ExInfo into the thread's ExInfo chain
	// rdi <- currently active ExInfo
	// pExInfo->m_pPrevExInfo = m_pExInfoStackHead
	// m_pExInfoStackHead = pExInfo

  // set the exception context field on the ExInfo
  // rdx <- PAL_LIMITED_CONTEXT*
  // init ExInfo.m_pExContext

	// rdi contains the currently active ExInfo
	// rsi contains the address of the new ExInfo
	//call    C_FUNC(RhRethrow)
ALTERNATE_ENTRY RhpRethrow2

	// no return
	// int 3

NESTED_END RhpRethrow, _TEXT

//
// Prologue of all funclet calling helpers (RhpCallXXXXFunclet)
//

//
// Epilogue of all funclet calling helpers (RhpCallXXXXFunclet)
//


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// void* FASTCALL RhpCallCatchFunclet(RtuObjectRef exceptionObj, void* pHandlerIP, REGDISPLAY* pRegDisplay,
//                                    ExInfo* pExInfo)
//
// INPUT:  RDI:  exception object
//         RSI:  handler funclet address
//         RDX:   REGDISPLAY*
//         RCX:   ExInfo*
//
// OUTPUT:
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
NESTED_ENTRY RhpCallCatchFunclet, _TEXT, NoHandler

	// save arguments for later

  // save Thread* for later

 	// Clear the DoNotTriggerGc state before calling out to our managed catch funclet.

  // rdi <- establisher frame
	// rsi <- exception object
	// call handler funclet

ALTERNATE_ENTRY RhpCallCatchFunclet2

	// rdx <- dispatch context

#ifdef _DEBUG
  // Call into some C++ code to validate the pop of the ExInfo.  We only do this in debug because we
	// have to spill all the preserved registers and then refill them after the call.

	// rdi <- Thread*
	// rsi <- current ExInfo *
	// rdx  <- resume SP value

  // rdx <- dispatch context

	// reload resume IP
#endif

	// rsi <- Thread*

	// We must unhijack the thread at this point because the section of stack where the hijack is applied
	// may go dead.  If it does, then the next time we try to unhijack the thread, it will corrupt the stack.

	// rdi <- current ExInfo *
	// rdx <- resume SP value
	// rcx <- 0

LOCAL_LABEL(ExInfoLoop):
	// rdi <- next ExInfo
	// we're done if it's null
	// keep looping if it's lower than the new SP

LOCAL_LABEL(ExInfoLoopDone):
  // store the new head on the Thread

	// reset the SP

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

NESTED_END RhpCallCatchFunclet, _TEXT

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// void FASTCALL RhpCallFinallyFunclet(void* pHandlerIP, REGDISPLAY* pRegDisplay)
//
// INPUT:  RDI:  handler funclet address
//         RSI:  REGDISPLAY*
//
// OUTPUT:
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
NESTED_ENTRY RhpCallFinallyFunclet, _TEXT, NoHandler

	// save arguments for later

  // save Thread* for later

	//
	// We want to suppress hijacking between invocations of subsequent finallys.  We do this because we
	// cannot tolerate a GC after one finally has run (and possibly side-effected the GC state of the
	// method) and then been popped off the stack, leaving behind no trace of its effect.
	//
	// So we clear the state before and set it after invocation of the handler.
	//

	// rdi <- establisher frame
	// handler funclet address

ALTERNATE_ENTRY RhpCallFinallyFunclet2

	// rsi <- regdisplay

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

NESTED_END RhpCallFinallyFunclet, _TEXT

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// void* FASTCALL RhpCallFilterFunclet(RtuObjectRef exceptionObj, void* pFilterIP, REGDISPLAY* pRegDisplay)
//
// INPUT:  RDI:  exception object
//         RSI:  filter funclet address
//         RDX:  REGDISPLAY*
//
// OUTPUT:
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
NESTED_ENTRY RhpCallFilterFunclet, _TEXT, NoHandler

	// rax <- handler funclet address
	// rsi <- exception object
	// rdi <- establisher frame

ALTERNATE_ENTRY RhpCallFilterFunclet2

 	// RAX contains the result of the filter execution

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

NESTED_END RhpCallFilterFunclet, _TEXT
