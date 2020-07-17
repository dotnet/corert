// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

.syntax unified
.thumb

#include <../../Runtime/unix/unixasmmacros.inc>

LEAF_ENTRY ConstrainedCallSupport_GetStubs, _TEXT
    // TODO: implement
    bx     lr
LEAF_END ConstrainedCallSupport_GetStubs, _TEXT
