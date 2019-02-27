// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    internal sealed class ThreadInt64PersistentCounter
    {
        internal static readonly object LockObj = new object();

        [ThreadStatic]
        private static ThreadLocalNode t_node;

        private ThreadLocalNode _nodesHead;
        private long _overflowCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Increment()
        {
            ThreadLocalNode node = t_node;
            if (node != null)
            {
                node.Increment();
                return;
            }

            TryCreateNode();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TryCreateNode()
        {
            Debug.Assert(t_node == null);

            ThreadLocalNode node;
            try
            {
                node = new ThreadLocalNode(this);
            }
            catch (OutOfMemoryException)
            {
                return;
            }

            t_node = node;
        }

        public long Count
        {
            get
            {
                // Make sure up-to-date thread-local node state is visible to this thread
                Interlocked.MemoryBarrierProcessWide();

                lock (LockObj)
                {
                    long count = _overflowCount;
                    for (ThreadLocalNode node = _nodesHead; node != null; node = node.Next)
                    {
                        count += node.Count;
                    }
                    return count;
                }
            }
        }

        private sealed class ThreadLocalNode
        {
            private uint _count;
            private readonly ThreadInt64PersistentCounter _counter;
            private ThreadLocalNode _previous;
            private ThreadLocalNode _next;

            public ThreadLocalNode(ThreadInt64PersistentCounter counter)
            {
                Debug.Assert(counter != null);

                _count = 1;
                _counter = counter;

                lock (LockObj)
                {
                    ThreadLocalNode head = counter._nodesHead;
                    if (head != null)
                    {
                        _next = head;
                        head._previous = this;
                    }
                    counter._nodesHead = this;
                }
            }

            ~ThreadLocalNode()
            {
                ThreadInt64PersistentCounter counter = _counter;
                lock (LockObj)
                {
                    counter._overflowCount += _count;

                    ThreadLocalNode previous = _previous;
                    ThreadLocalNode next = _next;

                    if (previous != null)
                    {
                        previous._next = next;
                    }
                    else
                    {
                        Debug.Assert(counter._nodesHead == this);
                        counter._nodesHead = next;
                    }

                    if (next != null)
                    {
                        next._previous = previous;
                    }
                }
            }

            public uint Count => _count;
            public ThreadLocalNode Next => _next;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Increment()
            {
                uint newCount = _count + 1;
                if (newCount != 0)
                {
                    _count = newCount;
                    return;
                }

                OnIncrementOverflow();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void OnIncrementOverflow()
            {
                // Accumulate the count for this increment into the overflow count and reset the thread-local count

                // The lock, in coordination with other places that read these values, ensures that both changes below become
                // visible together
                ThreadInt64PersistentCounter counter = _counter;
                lock (LockObj)
                {
                    _count = 0;
                    counter._overflowCount += (long)uint.MaxValue + 1;
                }
            }
        }
    }
}
