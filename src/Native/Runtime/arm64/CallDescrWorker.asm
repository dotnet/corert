;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

    TEXTAREA

;;-----------------------------------------------------------------------------
;; This helper routine enregisters the appropriate arguments and makes the
;; actual call.
;;-----------------------------------------------------------------------------
;;void RhCallDescrWorker(CallDescrData * pCallDescrData);
    NESTED_ENTRY RhCallDescrWorker
        brk 0xf000

    EXPORT_POINTER_TO_ADDRESS PointerToReturnFromCallDescrThunk
        brk 0xf000

    NESTED_END RhCallDescrWorker

    END
