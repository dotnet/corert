;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

        TEXTAREA

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpGetThread
;;
;;
;; INPUT: none
;;
;; OUTPUT: r0: Thread pointer
;;
;; MUST PRESERVE ARGUMENT REGISTERS
;; @todo check the actual requirements here, r0 is both return and argument register
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    LEAF_ENTRY RhpGetThread
        brk 0xf000
    LEAF_END
FASTCALL_ENDFUNC

    end
