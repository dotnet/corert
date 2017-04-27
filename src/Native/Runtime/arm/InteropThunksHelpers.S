// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

// TODO: Implement Arm support

//
// RhCommonStub
//
NESTED_ENTRY RhCommonStub, _TEXT, NoHandler
#ifdef _DEBUG
    bl C_FUNC(NYI_Assert)
#endif
NESTED_END RhCommonStub, _TEXT

//
// IntPtr RhGetCommonStubAddress()
//
LEAF_ENTRY RhGetCommonStubAddress, _TEXT
#ifdef _DEBUG
    bl C_FUNC(NYI_Assert)
#endif
LEAF_END RhGetCommonStubAddress, _TEXT

//
// IntPtr RhGetCurrentThunkContext()
//
LEAF_ENTRY RhGetCurrentThunkContext, _TEXT
#ifdef _DEBUG
    bl C_FUNC(NYI_Assert)
#endif
LEAF_END RhGetCurrentThunkContext, _TEXT
