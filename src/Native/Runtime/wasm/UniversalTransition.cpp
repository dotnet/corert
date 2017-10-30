// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO: Implement these. See UniversalTransition.s under other architectures for how
// each function should work. This file should ideally be pure WebAssembly for better speed.

#include <AsmOffsets.inc>         // generated by the build from AsmOffsets.cpp
#include <cassert>

void* PointerToReturnFromUniversalTransition;
extern "C" void UniversalTransition()
{
    assert(false);
}

void* PointerToReturnFromUniversalTransition_DebugStepTailCall;
extern "C" void UniversalTransition_DebugStepTailCall()
{
    assert(false);
}
