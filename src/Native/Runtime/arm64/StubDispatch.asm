;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

        TEXTAREA


#ifdef FEATURE_CACHED_INTERFACE_DISPATCH


    ;;EXTERN t_TLS_DispatchCell

SECTIONREL_t_TLS_DispatchCell
        ;;DCD     t_TLS_DispatchCell
        ;;RELOC   15 ;; SECREL

    LEAF_ENTRY RhpCastableObjectDispatch_CommonStub
        brk 0xf000
    LEAF_END RhpCastableObjectDispatch_CommonStub

    LEAF_ENTRY RhpTailCallTLSDispatchCell
        brk 0xf000
    LEAF_END RhpTailCallTLSDispatchCell

    LEAF_ENTRY RhpCastableObjectDispatchHelper_TailCalled
        brk 0xf000
    LEAF_END RhpCastableObjectDispatchHelper_TailCalled

    LEAF_ENTRY  RhpCastableObjectDispatchHelper
        brk 0xf000
    ALTERNATE_ENTRY RhpCastableObjectDispatchHelper2
        brk 0xf000
    LEAF_END RhpCastableObjectDispatchHelper


;; Macro that generates a stub consuming a cache with the given number of entries.
    GBLS StubName

    MACRO
        DEFINE_INTERFACE_DISPATCH_STUB $entries

StubName    SETS    "RhpInterfaceDispatch$entries"

    NESTED_ENTRY $StubName
        brk 0xf000
    NESTED_END $StubName

    MEND

;; Define all the stub routines we currently need.
        DEFINE_INTERFACE_DISPATCH_STUB 1
        DEFINE_INTERFACE_DISPATCH_STUB 2
        DEFINE_INTERFACE_DISPATCH_STUB 4
        DEFINE_INTERFACE_DISPATCH_STUB 8
        DEFINE_INTERFACE_DISPATCH_STUB 16
        DEFINE_INTERFACE_DISPATCH_STUB 32
        DEFINE_INTERFACE_DISPATCH_STUB 64


;; Initial dispatch on an interface when we don't have a cache yet.
    LEAF_ENTRY RhpInitialInterfaceDispatch
        brk 0xf000
    LEAF_END RhpInitialInterfaceDispatch

    LEAF_ENTRY RhpVTableOffsetDispatch
        brk 0xf000
    LEAF_END RhpVTableOffsetDispatch

;; Cache miss case, call the runtime to resolve the target and update the cache.
    LEAF_ENTRY RhpInterfaceDispatchSlow
    ALTERNATE_ENTRY RhpInitialDynamicInterfaceDispatch
        brk 0xf000
    LEAF_END RhpInterfaceDispatchSlow


#endif // FEATURE_CACHED_INTERFACE_DISPATCH

        end
