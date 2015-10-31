;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

;;
;; Unmanaged helpers used by the managed System.GC class.
;;

#include "AsmMacros.h"

        TEXTAREA

;; Force a collection.
;; On entry:
;;  r0 = generation to collect (-1 for all)
;;  r1 = mode (default, forced or optimized)
;;
;; This helper is special because it's not called via a p/invoke that transitions to pre-emptive mode. We do
;; this because the GC wants to be called in co-operative mode. But we are going to cause a GC, so we need to
;; make this stack crawlable. As a result we use the same trick as the allocation helpers and build an
;; explicit transition frame based on the entry state so the GC knows where to start crawling this thread's
;; stack.
        NESTED_ENTRY RhCollect

        COOP_PINVOKE_FRAME_PROLOG

        ;; Initiate the collection. (Arguments are already in the correct registers).
        bl          $REDHAWKGCINTERFACE__GARBAGECOLLECT

        nop     ; debugger bug workaround, this fixes the stack trace

        COOP_PINVOKE_FRAME_EPILOG

        NESTED_END RhCollect

;; Re-register an object of a finalizable type for finalization.
;;  rcx : object
;;
    NESTED_ENTRY RhReRegisterForFinalize

        EXTERN RhReRegisterForFinalizeHelper

        COOP_PINVOKE_FRAME_PROLOG

        ;; Call to the C++ helper that does most of the work.
        bl      RhReRegisterForFinalizeHelper

        COOP_PINVOKE_FRAME_EPILOG

    NESTED_END RhReRegisterForFinalize

;; RhGetGcTotalMemory
;;  No inputs, returns total GC memory as 64-bit value in rax.
;;
    NESTED_ENTRY RhGetGcTotalMemory

        EXTERN RhGetGcTotalMemoryHelper

        COOP_PINVOKE_FRAME_PROLOG

        ;; Call to the C++ helper that does most of the work.
        bl      RhGetGcTotalMemoryHelper

        COOP_PINVOKE_FRAME_EPILOG

    NESTED_END RhGetGcTotalMemory

        end
