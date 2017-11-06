// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO: Implement these. See PInvoke.s under other architectures for how
// each function should work. This file should ideally be pure WebAssembly for better speed.

#include <AsmOffsets.inc>

//
// RhpPInvoke
//
// This helper assumes that its callsite is as good to start the stackwalk as the actual PInvoke callsite.
// The codegenerator must treat the callsite of this helper as GC triggering and generate the GC info for it.
// Also, the codegenerator must ensure that there are no live GC references in callee saved registers.
//
extern "C" void RhPInvoke(void* pInvokeFrameAddress)
{

}

//
// RhpPInvokeReturn
//
// IN:  R0: address of pinvoke frame
//
extern "C" void RhpPInvokeReturn(void* pInvokeFrameAddress)
{

}
