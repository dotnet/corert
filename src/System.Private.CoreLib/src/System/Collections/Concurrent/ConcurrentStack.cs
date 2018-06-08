// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Collections.Generic
{
    // Reduced copy of System.Collections.Concurrent.ConcurrentStack<T>
    internal class ConcurrentStack<T>
    {
        private class Node
        {
            internal readonly T _value; // Value of the node.
            internal Node _next; // Next pointer.

            internal Node(T value)
            {
                _value = value;
                _next = null;
            }
        }

        private volatile Node _head; // The stack is a singly linked list, and only remembers the head.

        private const int BACKOFF_MAX_YIELDS = 8; // Arbitrary number to cap backoff.

        public int Count
        {
            get
            {
                int count = 0;

                for (Node curr = _head; curr != null; curr = curr._next)
                {
                    count++; //we don't handle overflow, to be consistent with existing generic collection types in CLR
                }

                return count;
            }
        }

        public void Push(T item)
        {
            Node newNode = new Node(item);
            newNode._next = _head;
            if (Interlocked.CompareExchange(ref _head, newNode, newNode._next) == newNode._next)
            {
                return;
            }

            // If we failed, go to the slow path and loop around until we succeed.
            PushCore(newNode, newNode);
        }

        private void PushCore(Node head, Node tail)
        {
            SpinWait spin = new SpinWait();

            // Keep trying to CAS the existing head with the new node until we succeed.
            do
            {
                spin.SpinOnce();
                // Reread the head and link our new node.
                tail._next = _head;
            }
            while (Interlocked.CompareExchange(
                ref _head, head, tail._next) != tail._next);
        }

        public bool TryPop(out T result)
        {
            Node head = _head;
            //stack is empty
            if (head == null)
            {
                result = default(T);
                return false;
            }
            if (Interlocked.CompareExchange(ref _head, head._next, head) == head)
            {
                result = head._value;
                return true;
            }

            // Fall through to the slow path.
            return TryPopCore(out result);
        }

        private bool TryPopCore(out T result)
        {
            Node poppedNode;

            if (TryPopCore(1, out poppedNode) == 1)
            {
                result = poppedNode._value;
                return true;
            }

            result = default(T);
            return false;
        }

        private int TryPopCore(int count, out Node poppedHead)
        {
            SpinWait spin = new SpinWait();

            // Try to CAS the head with its current next.  We stop when we succeed or
            // when we notice that the stack is empty, whichever comes first.
            Node head;
            Node next;
            int backoff = 1;
            Random r = null;
            while (true)
            {
                head = _head;
                // Is the stack empty?
                if (head == null)
                {
                    poppedHead = null;
                    return 0;
                }
                next = head;
                int nodesCount = 1;
                for (; nodesCount < count && next._next != null; nodesCount++)
                {
                    next = next._next;
                }

                // Try to swap the new head.  If we succeed, break out of the loop.
                if (Interlocked.CompareExchange(ref _head, next._next, head) == head)
                {
                    // Return the popped Node.
                    poppedHead = head;
                    return nodesCount;
                }

                // We failed to CAS the new head.  Spin briefly and retry.
                for (int i = 0; i < backoff; i++)
                {
                    spin.SpinOnce();
                }

                if (spin.NextSpinWillYield)
                {
                    if (r == null)
                    {
                        r = new Random();
                    }
                    backoff = r.Next(1, BACKOFF_MAX_YIELDS);
                }
                else
                {
                    backoff *= 2;
                }
            }
        }
    }
}