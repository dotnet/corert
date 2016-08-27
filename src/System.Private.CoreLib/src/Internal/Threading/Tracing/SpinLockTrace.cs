// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using SpinLockTraceCallbacks = Internal.Runtime.Augments.SpinLockTraceCallbacks;

namespace Internal.Threading.Tracing
{
    /// <summary>
    /// Helper class for reporting <see cref="System.Threading.SpinLock"/>-related events.
    /// Calls are forwarded to an instance of <see cref="SpinLockTraceCallbacks"/>, if one has been
    /// provided.
    /// </summary>
    public static class SpinLockTrace
    {
        private static SpinLockTraceCallbacks s_callbacks;

        public static bool Enabled
        {
            get
            {
                SpinLockTraceCallbacks callbacks = s_callbacks;
                if (callbacks == null)
                    return false;
                if (!callbacks.Enabled)
                    return false;
                return true;
            }
        }

        public static void Initialize(SpinLockTraceCallbacks callbacks)
        {
            s_callbacks = callbacks;
        }

        public static void SpinLock_FastPathFailed(int ownerID)
        {
            SpinLockTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.SpinLock_FastPathFailed(ownerID);
        }
    }
}
