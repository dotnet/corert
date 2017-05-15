// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Runtime
{
    internal static class FinalizerInitRunner
    {
        // Here, we are subscribing to a callback from the runtime.  This callback is made from the finalizer
        // thread before any objects are finalized.  
        [RuntimeExport("InitializeFinalizerThread")]
        public static void DoInitialize()
        {
            // Make sure that the finalizer thread is RoInitialized before any objects are finalized.  If this
            // fails, it will throw an exception and that will go unhandled, triggering a FailFast.
            RuntimeThread.RoInitialize();
        }
    }
}
