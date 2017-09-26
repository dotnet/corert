;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

    TEXTAREA

        MACRO 
        UNIVERSAL_TRANSITION $FunctionName

    NESTED_ENTRY Rhp$FunctionName
        brk 0xf000

    EXPORT_POINTER_TO_ADDRESS PointerToReturnFrom$FunctionName
        brk 0xf000

    NESTED_END Rhp$FunctionName

        MEND

    ; To enable proper step-in behavior in the debugger, we need to have two instances
    ; of the thunk. For the first one, the debugger steps into the call in the function, 
    ; for the other, it steps over it.
    UNIVERSAL_TRANSITION UniversalTransition
    UNIVERSAL_TRANSITION UniversalTransition_DebugStepTailCall

    END
