;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

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
