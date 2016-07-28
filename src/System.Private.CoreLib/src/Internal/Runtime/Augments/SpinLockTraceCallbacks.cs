// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.Runtime.Augments
{
    /// <summary>
    /// Callbacks to allow <see cref="System.Threading.SpinLock"/> to report ETW events without
    /// having to put the relevant EventSource (and all the related types) in this assembly.
    /// Implemented in System.Private.Threading.
    /// </summary>
    public abstract class SpinLockTraceCallbacks
    {
        public abstract bool Enabled { get; }

        public abstract void SpinLock_FastPathFailed(int ownerID);
    }
}
