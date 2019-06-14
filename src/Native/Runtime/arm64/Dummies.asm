;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

        TEXTAREA

        LEAF_ENTRY RhpLMod
        DCW     0xdefe
        bx      lr
        LEAF_END RhpLMod

        LEAF_ENTRY RhpLMul
        DCW     0xdefe
        bx      lr
        LEAF_END RhpLMul

        END
