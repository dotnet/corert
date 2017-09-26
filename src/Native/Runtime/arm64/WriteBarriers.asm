;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

;;
;; Define the helpers used to implement the write barrier required when writing an object reference into a
;; location residing on the GC heap. Such write barriers allow the GC to optimize which objects in
;; non-ephemeral generations need to be scanned for references to ephemeral objects during an ephemeral
;; collection.
;;

#include "AsmMacros.h"

    TEXTAREA

    LEAF_ENTRY RhpCheckedAssignRefXXX
        brk 0xf000
    ALTERNATE_ENTRY RhpCheckedAssignRef
        brk 0xf000
    ALTERNATE_ENTRY RhpCheckedAssignRefAvLocation
        brk 0xf000
    ALTERNATE_ENTRY RhpCheckedAssignRefAVLocation
        brk 0xf000
    LEAF_END RhpCheckedAssignRefXXX

    LEAF_ENTRY RhpAssignRefXXX
        brk 0xf000
    ALTERNATE_ENTRY RhpAssignRef
        brk 0xf000
    ALTERNATE_ENTRY RhpAssignRefAvLocationXXX
        brk 0xf000
    ALTERNATE_ENTRY RhpAssignRefAVLocation
        brk 0xf000
    LEAF_END RhpAssignRefXXX

;; Interlocked operation helpers where the location is an objectref, thus requiring a GC write barrier upon
;; successful updates. 

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpCheckedLockCmpXchgAVLocation
;; - Function "UnwindWriteBarrierToCaller" assumes no registers where pushed and LR contains the return address

    ;; Interlocked compare exchange on objectref.
    ;;
    ;; On entry:
    ;;  r0: pointer to objectref
    ;;  r1: exchange value
    ;;  r2: comparand
    ;;
    ;; On exit:
    ;;  r0: original value of objectref
    ;;  r1,r2,r3,r12: trashed
    ;;
    LEAF_ENTRY RhpCheckedLockCmpXchg
        brk 0xf000
    ALTERNATE_ENTRY  RhpCheckedLockCmpXchgAVLocation
        brk 0xf000
    LEAF_END RhpCheckedLockCmpXchg

    ;; Interlocked exchange on objectref.
    ;;
    ;; On entry:
    ;;  r0: pointer to objectref
    ;;  r1: exchange value
    ;;
    ;; On exit:
    ;;  r0: original value of objectref
    ;;  r1,r2,r3,r12: trashed
    ;;
    
    ;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
    ;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen within at RhpCheckedXchgAVLocation
    ;; - Function "UnwindWriteBarrierToCaller" assumes no registers where pushed and LR contains the return address
    
    LEAF_ENTRY RhpCheckedXchg
        brk 0xf000
    ALTERNATE_ENTRY  RhpCheckedXchgAVLocation
        brk 0xf000
    LEAF_END RhpCheckedXchg

    end
