// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <unixasmmacros.inc>

.syntax unified
.thumb

// TODO: Implement Arm support

NESTED_ENTRY RhCallDescrWorker, _TEXT, NoHandler
LOCAL_LABEL(ReturnFromCallDescrThunk):

    // UNIXTODO: Implement this function
	  // int 3
#ifdef _DEBUG
    bl C_FUNC(NYI_Assert)
#endif
NESTED_END RhCallDescrWorker, _TEXT

//.text
//.align      8
//C_FUNC(PointerToReturnFromCallDescrThunk):
//.quad       LOCAL_LABEL(ReturnFromCallDescrThunk)
//.global     C_FUNC(PointerToReturnFromCallDescrThunk)

LEAF_ENTRY PointerToReturnFromCallDescrThunk, _TEXT
#ifdef _DEBUG
    bl C_FUNC(NYI_Assert)
#endif
LEAF_END PointerToReturnFromCallDescrThunk, _TEXT
