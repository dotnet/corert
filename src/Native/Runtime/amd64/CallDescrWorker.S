// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.intel_syntax noprefix
#include <unixasmmacros.inc>

NESTED_ENTRY RhCallDescrWorker, _TEXT, NoHandler
ALTERNATE_ENTRY ReturnFromCallDescrThunk
    // UNIXTODO: Implement this function
    int 3
NESTED_END RhCallDescrWorker, _TEXT
