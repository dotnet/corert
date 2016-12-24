// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Buffers
{
    internal sealed class ArrayPoolEventSource
    {
        internal readonly static ArrayPoolEventSource Log = new ArrayPoolEventSource();

        // TODO: EventSource instrumentation https://github.com/dotnet/corert/issues/2414

        internal bool IsEnabled()
        {
            return false;
        }

        internal enum BufferAllocatedReason : int
        {
            Pooled,
            OverMaximumSize,
            PoolExhausted
        }

        internal void BufferRented(int bufferId, int bufferSize, int poolId, int bucketId)
        {
        }

        internal void BufferAllocated(int bufferId, int bufferSize, int poolId, int bucketId, BufferAllocatedReason reason)
        {
        }

        internal void BufferReturned(int bufferId, int bufferSize, int poolId)
        {
        }
    }
}
