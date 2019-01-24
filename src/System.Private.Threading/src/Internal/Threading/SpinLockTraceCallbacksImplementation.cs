// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::Internal.Runtime.Augments;
using global::System.Threading;

namespace Internal.Threading
{
    internal sealed class SpinLockTraceCallbacksImplementation : SpinLockTraceCallbacks
    {
        public sealed override bool Enabled
        {
            get
            {
                return CdsSyncEtwBCLProvider.Log.IsEnabled();
            }
        }

        public sealed override void SpinLock_FastPathFailed(int ownerID)
        {
            CdsSyncEtwBCLProvider.Log.SpinLock_FastPathFailed(ownerID);
        }
    }
}
