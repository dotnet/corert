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

ALTERNATE_ENTRY RhpThrowHwEx2

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

        EXPORT_POINTER_TO_ADDRESS PointerToRhpThrowHwEx2

        // no return
        bkpt

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

ALTERNATE_ENTRY RhpThrowEx2

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

        EXPORT_POINTER_TO_ADDRESS PointerToRhpThrowEx2

        // no return
        bkpt

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

ALTERNATE_ENTRY RhpRethrow2

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

        EXPORT_POINTER_TO_ADDRESS PointerToRhpRethrow2

        // no return
        bkpt

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

ALTERNATE_ENTRY RhpCallCatchFunclet2


#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

        EXPORT_POINTER_TO_ADDRESS PointerToRhpCallCatchFunclet2

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

ALTERNATE_ENTRY RhpCallFinallyFunclet2

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

        EXPORT_POINTER_TO_ADDRESS PointerToRhpCallFinallyFunclet2

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

ALTERNATE_ENTRY RhpCallFilterFunclet2

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

        EXPORT_POINTER_TO_ADDRESS PointerToRhpCallFilterFunclet2

NESTED_END RhpCallFilterFunclet, _TEXT
