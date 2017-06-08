// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

// TODO: Implement Arm support

LEAF_ENTRY RhpCastableObjectDispatch_CommonStub, _TEXT
	// UNIXTODO: Implement this function
	//int 3
LEAF_END RhpCastableObjectDispatch_CommonStub, _TEXT

LEAF_ENTRY RhpTailCallTLSDispatchCell, _TEXT
	// UNIXTODO: Implement this function
	// int 3
LEAF_END RhpTailCallTLSDispatchCell, _TEXT

LEAF_ENTRY RhpCastableObjectDispatchHelper_TailCalled, _TEXT
	// UNIXTODO: Implement this function
	// int 3
LEAF_END RhpCastableObjectDispatchHelper_TailCalled, _TEXT

LEAF_ENTRY RhpCastableObjectDispatchHelper, _TEXT
	// UNIXTODO: Implement this function
	// int 3
LEAF_END RhpCastableObjectDispatchHelper, _TEXT


// Stub dispatch routine for dispatch to a vtable slot
LEAF_ENTRY RhpVTableOffsetDispatch, _TEXT
  // UNIXTODO: Implement this function
  // int 3
LEAF_END RhpVTableOffsetDispatch, _TEXT

// Initial dispatch on an interface when we don't have a cache yet.
LEAF_ENTRY RhpInitialInterfaceDispatch, _TEXT
ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch

  // Just tail call to the cache miss helper.
  // jmp     C_FUNC(RhpInterfaceDispatchSlow)

LEAF_END RhpInitialInterfaceDispatch, _TEXT

// Cache miss case, call the runtime to resolve the target and update the cache.
// Use universal transition helper to allow an exception to flow out of resolution
LEAF_ENTRY RhpInterfaceDispatchSlow, _TEXT
  // jmp C_FUNC(RhpUniversalTransition_DebugStepTailCall)

LEAF_END RhpInterfaceDispatchSlow, _TEXT


LEAF_ENTRY RhpInterfaceDispatch1, _TEXT

LEAF_END RhpInterfaceDispatch1, _TEXT

LEAF_ENTRY RhpInterfaceDispatch2, _TEXT

LEAF_END RhpInterfaceDispatch2, _TEXT

LEAF_ENTRY RhpInterfaceDispatch4, _TEXT

LEAF_END RhpInterfaceDispatch4, _TEXT

LEAF_ENTRY RhpInterfaceDispatch8, _TEXT

LEAF_END RhpInterfaceDispatch8, _TEXT

LEAF_ENTRY RhpInterfaceDispatch16, _TEXT

LEAF_END RhpInterfaceDispatch16, _TEXT

LEAF_ENTRY RhpInterfaceDispatch32, _TEXT

LEAF_END RhpInterfaceDispatch32, _TEXT

LEAF_ENTRY RhpInterfaceDispatch64, _TEXT

LEAF_END RhpInterfaceDispatch64, _TEXT
