// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.intel_syntax noprefix
#include <unixasmmacros.inc>

//
// Defines a small assembly thunk designed to be used when unmanaged code in the runtime calls out to managed
// code. In such cases the stack walker needs to be able to bridge the unmanaged gap in the stack between the
// callout and whatever managed code initially entered the runtime. This thunk makes that goal achievable by
// (a) exporting a well-known address in the thunk that will be the result of unwinding from the callout (so
// the stack frame iterator knows when its hit this case) and (b) placing a copy of a pointer to a transition
// frame saved when the previous managed caller entered the runtime into a well-known location relative to the
// thunk's frame, enabling the stack frame iterator to recover the transition frame address and use it to
// re-initialize the stack walk at the previous managed caller.
//
// If we end up with more cases of this (currently it's used only for the ICastable extension point for
// interface dispatch) then we might decide to produce a general routine which can handle an arbitrary number
// of arguments to the target method. For now we'll just implement the case we need, which takes two regular
// arguments (that's the 2 in the ManagedCallout2 name).
//
// Inputs:
//      rdi : Argument 1 to target method
//      rsi : Argument 2 to target method
//      rdx : Target method address
//      rcx : Pointer to previous managed method's transition frame into the runtime
//
NESTED_ENTRY ManagedCallout2, _TEXT, NoHandler

        // Push an rbp frame. Apart from making it easier to walk the stack the stack frame iterator locates
        // the transition frame for the previous managed caller relative to the frame pointer to keep the code
        // architecture independent.
        push_nonvol_reg rbp
        mov     rbp, rsp

        // Allocate space for transition frame pointer and stack alignment padding.
        alloc_stack 8 + 8

        END_PROLOGUE

        // Stash the previous transition frame's address immediately on top of the old rbp value. This
        // position is important; the stack frame iterator knows about this setup.
        mov     [rbp + MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET], rcx

        // Call the target method. Arguments are already in the correct registers. The
        // ReturnFromManagedCallout2 label must immediately follow the call instruction.
        call    rdx
ALTERNATE_ENTRY ReturnFromManagedCallout2

        // Pop the rbp frame and return.
        mov     rsp, rbp
        pop     rbp
        ret

NESTED_END ManagedCallout2, _TEXT
