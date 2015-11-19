// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            Interop.WinRT.RoInitialize(Interop.WinRT.RO_INIT_TYPE.RO_INIT_MULTITHREADED);
        }
    }
}
