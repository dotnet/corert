;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.


#include "ksarm64.h"

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;  DATA SECTIONS  ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;; TODO __tls_array                         equ 0x2C    ;; offsetof(TEB, ThreadLocalStoragePointer)

POINTER_SIZE                        equ 0x08

;; TLS variables
    AREA    |.tls$|, DATA
ThunkParamSlot % 0x8

    TEXTAREA

    EXTERN _tls_index

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; Interop Thunks Helpers ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

    ;;
    ;; RhCommonStub
    ;;
    NESTED_ENTRY RhCommonStub
        brk 0xf000
    NESTED_END RhCommonStub

    ;;
    ;; IntPtr RhGetCommonStubAddress()
    ;;
    LEAF_ENTRY RhGetCommonStubAddress
        brk 0xf000
    LEAF_END RhGetCommonStubAddress


    ;;
    ;; IntPtr RhGetCurrentThunkContext()
    ;;
    LEAF_ENTRY RhGetCurrentThunkContext
        brk 0xf000
    LEAF_END RhGetCurrentThunkContext

    END
