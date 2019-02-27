// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    internal sealed class ThreadBooleanCounter
    {
        [ThreadStatic]
        private static ThreadLocalNode t_node;

        private ThreadLocalNode _nodesHead;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set()
        {
            ThreadLocalNode node = t_node;
            if (node != null)
            {
                node.Set();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => t_node?.Clear();

        public int Count
        {
            get
            {
                // Make sure up-to-date thread-local node state is visible to this thread
                Interlocked.MemoryBarrierProcessWide();

                lock (ThreadInt64PersistentCounter.LockObj)
                {
                    int count = 0;
                    for (ThreadLocalNode node = _nodesHead; node != null; node = node.Next)
                    {
                        if (node.IsSet)
                        {
                            ++count;
                            Debug.Assert(count > 0);
                        }
                    }
                    return count;
                }
            }
        }

        private sealed class ThreadLocalNode
        {
            private bool _isSet;
            private readonly ThreadBooleanCounter _counter;
            private ThreadLocalNode _previous;
            private ThreadLocalNode _next;

            public ThreadLocalNode(ThreadBooleanCounter counter)
            {
                Debug.Assert(counter != null);

                _isSet = true;
                _counter = counter;

                lock (ThreadInt64PersistentCounter.LockObj)
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
                ThreadBooleanCounter counter = _counter;
                lock (ThreadInt64PersistentCounter.LockObj)
                {
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

            public bool IsSet => _isSet;
            public ThreadLocalNode Next => _next;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set()
            {
                Debug.Assert(!_isSet);
                _isSet = true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear()
            {
                Debug.Assert(_isSet);
                _isSet = false;
            }
        }
    }
}
