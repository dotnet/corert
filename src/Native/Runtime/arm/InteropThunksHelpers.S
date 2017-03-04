// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

// TODO: Implement Arm support

//
// InteropNative_CommonStub
//
NESTED_ENTRY InteropNative_CommonStub, _TEXT, NoHandler
#ifdef _DEBUG
    bl C_FUNC(NYI_Assert)
#endif
NESTED_END InteropNative_CommonStub, _TEXT

//
// IntPtr InteropNative_GetCommonStubAddress()
//
LEAF_ENTRY InteropNative_GetCommonStubAddress, _TEXT
#ifdef _DEBUG
    bl C_FUNC(NYI_Assert)
#endif
LEAF_END InteropNative_GetCommonStubAddress, _TEXT

//
// IntPtr InteropNative_GetCurrentThunkContext()
//
LEAF_ENTRY InteropNative_GetCurrentThunkContext, _TEXT
#ifdef _DEBUG
    bl C_FUNC(NYI_Assert)
#endif
LEAF_END 	InteropNative_GetCurrentThunkContext, _TEXT
