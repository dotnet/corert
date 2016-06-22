// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifndef __UNIX_CONTEXT_H__
#define __UNIX_CONTEXT_H__

// Convert Unix native context to PAL_LIMITED_CONTEXT
void NativeContextToPalContext(const void* context, PAL_LIMITED_CONTEXT* palContext);
// Redirect Unix native context to the PAL_LIMITED_CONTEXT and also set the first two argument registers
void RedirectNativeContext(void* context, const PAL_LIMITED_CONTEXT* palContext, UIntNative arg0Reg, UIntNative arg1Reg);

// Find LSDA and start address for a function at address controlPC
bool FindProcInfo(UIntNative controlPC, UIntNative* startAddress, UIntNative* lsda);
// Virtually unwind stack to the caller of the context specified by the REGDISPLAY
bool VirtualUnwind(REGDISPLAY* pRegisterSet);

#ifdef _AMD64_
// Get value of a register from the native context. The index is the processor specific
// register index stored in machine instructions.
uint64_t GetRegisterValueByIndex(void* context, uint32_t index);
// Get value of the program counter from the native context
uint64_t GetPC(void* context);
#endif // _AMD64_

#endif // __UNIX_CONTEXT_H__
