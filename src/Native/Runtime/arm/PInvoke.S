// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

// TODO: Implement Arm support

//
// RhpPInvoke
//
NESTED_ENTRY RhpPInvoke, _TEXT, NoHandler
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
NESTED_END RhpPInvoke, _TEXT

//
// RhpPInvokeReturn
//
LEAF_ENTRY RhpPInvokeReturn, _TEXT
#ifdef _DEBUG
        bl C_FUNC(NYI_Assert)
#endif
LEAF_END RhpPInvokeReturn, _TEXT
