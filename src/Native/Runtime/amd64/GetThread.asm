;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

include asmmacros.inc

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpGetThread
;;
;;
;; INPUT: 
;;
;; OUTPUT: RAX: Thread pointer
;;
;; TRASHES: R10
;;
;; MUST PRESERVE ARGUMENT REGISTERS
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
LEAF_ENTRY RhpGetThread, _TEXT
        ;; rax = GetThread(), TRASHES r10
        INLINE_GETTHREAD rax, r10
        ret
LEAF_END RhpGetThread, _TEXT


        end