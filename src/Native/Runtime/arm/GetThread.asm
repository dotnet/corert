;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

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

        ;; r0 = GetThread(), TRASHES r12
        INLINE_GETTHREAD r0, r12
        bx lr

        LEAF_END
FASTCALL_ENDFUNC

        end
