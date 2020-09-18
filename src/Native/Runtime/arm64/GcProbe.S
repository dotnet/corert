// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <unixasmmacros.inc>
#include "AsmOffsets.inc"


//
// Set the Thread state and wait for a GC to complete.
//
// Register state on entry:
//  x4: thread pointer
//  
// Register state on exit:
//  x4: thread pointer
//  All other registers trashed
//

    .global RhpWaitForGCNoAbort

    .macro WaitForGCCompletion

        ldr         w2, [x4, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         w2, #TSF_SuppressGcStress__OR__TSF_DoNotTriggerGC
        bne         0f

        ldr         x9, [x4, #OFFSETOF__Thread__m_pHackPInvokeTunnel]
        bl          RhpWaitForGCNoAbort
0:
    .endm

    LEAF_ENTRY RhpGcPoll
        PREPARE_EXTERNAL_VAR_INDIRECT_W RhpTrapThreads, 0
        tbnz        x0, #TrapThreadsFlags_TrapThreads_Bit, RhpGcPollRare
        ret
    LEAF_END RhpGcPoll

    NESTED_ENTRY RhpGcPollRare, _TEXT, NoHandler
        PUSH_COOP_PINVOKE_FRAME x0

#if FEATURE_EMULATED_TLS
        bl RhpGetThread
        mov x4, x0
#else
        INLINE_GETTHREAD x4
#endif

        str         x4, [sp, #OFFSETOF__PInvokeTransitionFrame__m_pThread]

        // Unhijack this thread, if necessary.
        INLINE_THREAD_UNHIJACK x4, x0, x1       // trashes x0, x1

        WaitForGCCompletion

        POP_COOP_PINVOKE_FRAME
        ret
    NESTED_END RhpGcPollRare
