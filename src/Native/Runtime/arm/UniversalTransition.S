// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

// TODO: Implement Arm support

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// RhpUniversalTransition
//
// At input to this function, r0-3, d0-7 and the stack may contain any number of arguments.
//
// In addition, there are 2 extra arguments passed in the RED ZONE (8 byte negative space
// off of sp).
// sp-4 will contain the managed function that is to be called by this transition function
// sp-8 will contain the pointer sized extra argument to the managed function
//
// When invoking the callee:
//
//  r0 shall contain a pointer to the TransitionBlock
//  r1 shall contain the value that was in sp-8 at entry to this function
//
// Frame layout is:
//
//  {StackPassedArgs}                           ChildSP+078     CallerSP+000
//  {IntArgRegs (r0-r3) (0x10 bytes)}           ChildSP+068     CallerSP-010
//  {ReturnBlock (0x20 bytes)}                  ChildSP+048     CallerSP-030
//   -- The base address of the Return block is the TransitionBlock pointer, the floating point args are
//      in the neg space of the TransitionBlock pointer.  Note that the callee has knowledge of the exact
//      layout of all pieces of the frame that lie at or above the pushed floating point registers.
//  {FpArgRegs (d0-d7) (0x40 bytes)}            ChildSP+008     CallerSP-070
//  {PushedLR}                                  ChildSP+004     CallerSP-074
//  {PushedR11}                                 ChildSP+000     CallerSP-078
//
// NOTE: If the frame layout ever changes, the C++ UniversalTransitionStackFrame structure
// must be updated as well.
//
// NOTE: The callee receives a pointer to the base of the ReturnBlock, and the callee has
// knowledge of the exact layout of all pieces of the frame that lie at or above the pushed
// FpArgRegs.
//
// NOTE: The stack walker guarantees that conservative GC reporting will be applied to
// everything between the base of the ReturnBlock and the top of the StackPassedArgs.
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////

NESTED_ENTRY RhpUniversalTransition, _TEXT, NoHandler

LOCAL_LABEL(ReturnFromUniversalTransition):

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

NESTED_END RhpUniversalTransition

NESTED_ENTRY PointerToReturnFromUniversalTransition, _TEXT, NoHandler

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

NESTED_END PointerToReturnFromUniversalTransition


NESTED_ENTRY RhpUniversalTransition_DebugStepTailCall, _TEXT, NoHandler

LOCAL_LABEL(ReturnFromUniversalTransition_DebugStepTailCall):

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

NESTED_END RhpUniversalTransition_DebugStepTailCall

NESTED_ENTRY PointerToReturnFromUniversalTransition_DebugStepTailCall, _TEXT, NoHandler

#ifdef _DEBUG
	bl C_FUNC(NYI_Assert)
#endif

NESTED_END PointerToReturnFromUniversalTransition_DebugStepTailCall
