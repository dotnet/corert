// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
//
// Implements the single finalizer thread for a Redhawk instance. Essentially waits for an event to fire
// indicating finalization is necessary then drains the queue of pending finalizable objects, calling the
// finalize method for each one.
// 

namespace System.Runtime
{
    // We choose this name to avoid clashing with any future public class with the name Finalizer. 
    public static class __Finalizer
    {
        private static bool s_fHaveNewClasslibs /* = false */;

        [NativeCallable(EntryPoint = "ProcessFinalizers", CallingConvention = CallingConvention.Cdecl)]
        public static void ProcessFinalizers()
        {
            while (true)
            {
                // Wait until there's some work to be done. If true is returned we should finalize objects,
                // otherwise memory is low and we should initiate a collection. 
                if (InternalCalls.RhpWaitForFinalizerRequest() != 0)
                {
                    if (s_fHaveNewClasslibs)
                    {
                        s_fHaveNewClasslibs = false;
                        MakeFinalizerInitCallbacks();
                    }

                    // Drain the queue of finalizable objects.
                    Object target = InternalCalls.RhpGetNextFinalizableObject();
                    while (target != null)
                    {
                        // Call the finalizer on the current target object. If the finalizer throws we'll fail
                        // fast via normal Redhawk exception semantics (since we don't attempt to catch
                        // anything).
                        unsafe
                        {
                            CalliIntrinsics.CallVoid(target.EEType->FinalizerCode, target);
                        }

                        target = InternalCalls.RhpGetNextFinalizableObject();
                    }

                    // Tell anybody that's interested that the finalization pass is complete (there is a race condition here
                    // where we might immediately signal a new request as complete, but this is acceptable).
                    InternalCalls.RhpSignalFinalizationComplete();
                }
                else
                {
                    // RhpWaitForFinalizerRequest() returned false and indicated that memory is low. We help
                    // out by initiating a garbage collection and then go back to waiting for another request.
                    InternalCalls.RhCollect(-1, InternalGCCollectionMode.Blocking);
                }
            }
        }

        // Each class library can sign up for a callback to run code on the finalizer thread before any 
        // objects derived from that class library's System.Object are finalized.  This is where we make those
        // callbacks.  When a class library is loaded, we set the s_fHasNewClasslibs flag and then the next
        // time the finalizer runs, we call this function to make any outstanding callbacks.
        private unsafe static void MakeFinalizerInitCallbacks()
        {
            while (true)
            {
                IntPtr pfnFinalizerInitCallback = InternalCalls.RhpGetNextFinalizerInitCallback();
                if (pfnFinalizerInitCallback == IntPtr.Zero)
                    break;

                CalliIntrinsics.CallVoid(pfnFinalizerInitCallback);
            }
        }

        [RuntimeExport("RhpSetHaveNewClasslibs")]
        public static void RhpSetHaveNewClasslibs()
        {
            s_fHaveNewClasslibs = true;
        }
    }
}
