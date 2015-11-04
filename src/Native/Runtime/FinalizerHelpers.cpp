//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Unmanaged helpers called by the managed finalizer thread.
//
#include "common.h"
#include "gcenv.h"
#include "gc.h"

#include "slist.h"
#include "gcrhinterface.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "module.h"


// Block the current thread until at least one object needs to be finalized (returns true) or memory is low
// (returns false and the finalizer thread should initiate a garbage collection).
EXTERN_C REDHAWK_API UInt32_BOOL __cdecl RhpWaitForFinalizerRequest()
{
    // We can wait for two events; finalization queue has been populated and low memory resource notification.
    // But if the latter is signalled we shouldn't wait on it again immediately -- if the garbage collection
    // the finalizer thread initiates as a result is not sufficient to remove the low memory condition the
    // event will still be signalled and we'll end up looping doing cpu intensive collections, which won't
    // help the situation at all and could make it worse. So we remember whether the last event we reported
    // was low memory and if so we'll wait at least two seconds (the CLR value) on just a finalization
    // request.
    static bool fLastEventWasLowMemory = false;

    GCHeap * pHeap = GCHeap::GetGCHeap();

    // Wait in a loop because we may have to retry if we decide to only wait for finalization events but the
    // two second timeout expires.
    do
    {
        HANDLE  lowMemEvent = NULL;
#if 0 // TODO: hook up low memory notification
        lowMemEvent = pHeap->GetLowMemoryNotificationEvent();
#endif // 0
        HANDLE  rgWaitHandles[] = { FinalizerThread::GetFinalizerEvent(), lowMemEvent };
        UInt32  cWaitHandles = (fLastEventWasLowMemory || (lowMemEvent == NULL)) ? 1 : 2;
        UInt32  uTimeout = fLastEventWasLowMemory ? 2000 : INFINITE;

        UInt32 uResult = PalWaitForMultipleObjectsEx(cWaitHandles, rgWaitHandles, FALSE, uTimeout, FALSE);
        switch (uResult)
        {
        case WAIT_OBJECT_0:
            // At least one object is ready for finalization.
            return TRUE;

        case WAIT_OBJECT_0 + 1:
            // Memory is low, tell the finalizer thread to garbage collect.
            ASSERT(!fLastEventWasLowMemory);
            fLastEventWasLowMemory = true;
            return FALSE;

        case WAIT_TIMEOUT:
            // We were waiting only for finalization events but didn't get one within the timeout period. Go
            // back to waiting for any event.
            ASSERT(fLastEventWasLowMemory);
            fLastEventWasLowMemory = false;
            break;

        default:
            ASSERT(!"Unexpected PalWaitForMultipleObjectsEx() result");
            return FALSE;
        }
    } while (true);
}

// Indicate that the current round of finalizations is complete.
EXTERN_C REDHAWK_API void __cdecl RhpSignalFinalizationComplete()
{
    FinalizerThread::SignalFinalizationDone(TRUE);
}

#ifdef FEATURE_PREMORTEM_FINALIZATION
// Enable a last pass of the finalizer during (clean) runtime shutdown. Specify the number of milliseconds
// we'll wait before giving up a proceeding with the shutdown (INFINITE is an allowable value).
COOP_PINVOKE_HELPER(void, RhEnableShutdownFinalization, (UInt32 uiTimeout))
{
    g_fPerformShutdownFinalization = true;
    g_uiShutdownFinalizationTimeout = uiTimeout;
}

// Returns true when shutdown has started and it is no longer safe to access other objects from finalizers.
COOP_PINVOKE_HELPER(UInt8, RhHasShutdownStarted, ())
{
    return g_fShutdownHasStarted ? 1 : 0;
}
#endif // FEATURE_PREMORTEM_FINALIZATION

//
// The following helpers are special in that they interact with internal GC state or directly manipulate
// managed references so they're called with a special co-operative p/invoke.
//

// Fetch next object which needs finalization or return null if we've reached the end of the list.
COOP_PINVOKE_HELPER(OBJECTREF, RhpGetNextFinalizableObject, ())
{
    while (true)
    {
        // Get the next finalizable object. If we get back NULL we've reached the end of the list.
        OBJECTREF refNext = GCHeap::GetGCHeap()->GetNextFinalizable();
        if (refNext == NULL)
            return NULL;

        // The queue may contain objects which have been marked as finalized already (via GC.SuppressFinalize()
        // for instance). Skip finalization for these but reset the flag so that the object can be put back on
        // the list with RegisterForFinalization().
        if (refNext->GetHeader()->GetBits() & BIT_SBLK_FINALIZER_RUN)
        {
            refNext->GetHeader()->ClrBit(BIT_SBLK_FINALIZER_RUN);
            continue;
        }

        // We've found the first finalizable object, return it to the caller.
        return refNext;
    }
}

// This function walks the list of modules looking for any module that is a class library and has not yet 
// had its finalizer init callback invoked.  It gets invoked in a loop, so it's technically O(n*m), but the
// number of classlibs subscribing to this callback is almost certainly going to be 1.
COOP_PINVOKE_HELPER(void *, RhpGetNextFinalizerInitCallback, ())
{
    FOREACH_MODULE(pModule)
    {
        if (pModule->IsClasslibModule()
            && !pModule->IsFinalizerInitComplete())
        {
            pModule->SetFinalizerInitComplete();
            void * retval = pModule->GetClasslibInitializeFinalizerThread();

            // The caller loops until we return null, so we should only be returning null if we've walked all
            // the modules and found no callbacks yet to be made.
            if (retval != NULL)
            {
                return retval;
            }
        }
    }
    END_FOREACH_MODULE;

    return NULL;
}
