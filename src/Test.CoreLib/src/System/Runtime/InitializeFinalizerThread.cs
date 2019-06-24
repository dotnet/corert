// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace System.Runtime
{
    internal static class FinalizerInitRunner
    {
        // Here, we are subscribing to a callback from the runtime.  This callback is made from the finalizer
        // thread before any objects are finalized.  
        public static void DoInitialize()
        {
        }
    }
}
