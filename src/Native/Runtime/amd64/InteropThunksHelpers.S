// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.intel_syntax noprefix
#include <unixasmmacros.inc>

//TODO: Implement these stub method for non-windows

LEAF_ENTRY RhpCommonStub, _TEXT
    int 3
LEAF_END RhpCommonStub, _TEXT


LEAF_ENTRY RhpGetCommonStubAddress, _TEXT
    int 3
LEAF_END RhpGetCommonStubAddress, _TEXT


LEAF_ENTRY RhpGetCurrentThunkContext, _TEXT
    int 3
LEAF_END RhpGetCurrentThunkContext, _TEXT