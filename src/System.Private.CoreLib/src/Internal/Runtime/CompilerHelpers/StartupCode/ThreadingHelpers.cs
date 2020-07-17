// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Internal.Runtime.CompilerHelpers
{
    public static class ThreadingHelpers
    {
        /// <summary>
        /// This enumeration encodes the state machine used by CallOnce. We don't use a real
        /// enum type as the interlocked / volatile functions take and return integers and
        /// we would basically require a typecast everywhere these are used.
        /// </summary>
        private static class CallOnceState
        {
            /// <summary>
            /// The CallOnce method has not yet been run
            /// </summary>
            public const int NotRun = 0;

            /// <summary>
            /// The CallOnce method is running right now [maybe on a different thread]
            /// </summary>
            public const int Running = 1;

            /// <summary>
            /// The CallOnce method has already finished
            /// </summary>
            public const int HasRun = 2;
        }

        /// <summary>
        /// This method implements a low-level idiom of calling a [typically initialization] method
        /// only once in a multithreaded environment; all other threads must wait
        /// for the initialization method to finish (much like what happens in STL for call_once).
        ///
        /// It was initially put here in order to be used in the ILC-generated method
        /// Mcg.StartupCodeTrigger.Initialize which uses it to make sure that InternalInitialize
        /// gets called only once and all concurrent threads wait until the initialization finishes.
        ///
        /// Please keep that in mind - by the time StartupCodeTrigger.Initialize calls CallOnce,
        /// most BCL hasn't yet been initialized as this is what StartupCodeTrigger.InternalInitialize
        /// does among other tasks.
        ///
        /// Please refer to EagerStaticConstructorOrder enumeration to see what early
        /// framework initialization occurs in StartupCodeTrigger.Initialize for the shared library.
        /// </summary>
        /// <param name="callOnceAction">Delegate to call once</param>
        /// <param name="callOnceGuard">Helper variable used to synchronize the initialization among threads</param>
        public static void CallOnce(Action callOnceAction, ref int callOnceGuard)
        {
            if (callOnceGuard != CallOnceState.HasRun)
            {
                if (Interlocked.CompareExchange(ref callOnceGuard, CallOnceState.Running, CallOnceState.NotRun) == CallOnceState.NotRun)
                {
                    // We have won the race for calling the callOnceAction
                    callOnceAction();
                    Volatile.Write(ref callOnceGuard, CallOnceState.HasRun);
                }
                else
                {
                    // We have lost the race so just wait until another thread finishes the initialization
                    while (Volatile.Read(ref callOnceGuard) != CallOnceState.HasRun)
                    {
                        Thread.Yield();
                    }
                }
            }
        }
    }
}
