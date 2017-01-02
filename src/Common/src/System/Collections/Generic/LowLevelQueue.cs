// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
** Private version of Queue<T> for internal System.Private.CoreLib use. This
** permits sharing more source between BCL and System.Private.CoreLib (as well as the
** fact that Queue<T> is just a useful class in general.)
**
** This does not strive to implement the full api surface area
** (but any portion it does implement should match the real Queue<T>'s
** behavior.)
**
** This file is a subset of System.Collections\System\Collections\Generics\Queue.cs
** in CoreFX and should be kept in sync with that file.
**
=============================================================================*/

using System.Diagnostics;

namespace System.Collections.Generic
{
    // A simple Queue of generic objects.  Internally it is implemented as a 
    // circular buffer, so Enqueue can be O(n).  Dequeue is O(1).
    //
    /// <summary>
    /// LowLevelQueue with no interface implementation to minimize both code and data size.
    /// Data size is smaller because there will be minimal virtual function table.
    /// Code size is smaller because only functions called will be in the binary.
    /// </summary>
    [DebuggerDisplay("Count = {Count}")]
#if TYPE_LOADER_IMPLEMENTATION
    [System.Runtime.CompilerServices.ForceDictionaryLookups]
#endif
    public class LowLevelQueue<T>
    {
        private T[] _array;
        private int _head;       // The index from which to dequeue if the queue isn't empty.
        private int _tail;       // The index at which to enqueue if the queue isn't full.
        private int _size;       // Number of elements.

        private const int MinimumGrow = 4;
        private const int GrowFactor = 200;  // double each time

        // Creates a queue with room for capacity objects. The default initial
        // capacity and grow factor are used.
        public LowLevelQueue()
        {
            _array = Array.Empty<T>();
        }

        // Creates a queue with room for capacity objects. The default grow factor
        // is used.
        public LowLevelQueue(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, SR.ArgumentOutOfRange_NeedNonNegNum);
            _array = new T[capacity];
        }

        public int Count
        {
            get { return _size; }
        }

        // Removes all Objects from the queue.
        public void Clear()
        {
            if (_size != 0)
            {
                if (_head < _tail)
                    Array.Clear(_array, _head, _size);
                else
                {
                    Array.Clear(_array, _head, _array.Length - _head);
                    Array.Clear(_array, 0, _tail);
                }

                _size = 0;
            }

            _head = 0;
            _tail = 0;
        }

        // CopyTo copies a collection into an Array, starting at a particular
        // index into the array.
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, SR.ArgumentOutOfRange_Index);
            }

            int arrayLen = array.Length;
            if (arrayLen - arrayIndex < _size)
            {
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            }

            int numToCopy = _size;
            if (numToCopy == 0)
                return;

            int firstPart = Math.Min(_array.Length - _head, numToCopy);
            Array.Copy(_array, _head, array, arrayIndex, firstPart);
            numToCopy -= firstPart;
            if (numToCopy > 0)
            {
                Array.Copy(_array, 0, array, arrayIndex + _array.Length - _head, numToCopy);
            }
        }

        // Adds item to the tail of the queue.
        public void Enqueue(T item)
        {
            if (_size == _array.Length)
            {
                int newcapacity = (int)((long)_array.Length * (long)GrowFactor / 100);
                if (newcapacity < _array.Length + MinimumGrow)
                {
                    newcapacity = _array.Length + MinimumGrow;
                }
                SetCapacity(newcapacity);
            }

            _array[_tail] = item;
            MoveNext(ref _tail);
            _size++;
        }

        // Removes the object at the head of the queue and returns it. If the queue
        // is empty, this method throws an 
        // InvalidOperationException.
        public T Dequeue()
        {
            if (_size == 0)
            {
                ThrowForEmptyQueue();
            }

            T removed = _array[_head];
            _array[_head] = default(T);
            MoveNext(ref _head);
            _size--;
            return removed;
        }

        public bool TryDequeue(out T result)
        {
            if (_size == 0)
            {
                result = default(T);
                return false;
            }

            result = _array[_head];
            _array[_head] = default(T);
            MoveNext(ref _head);
            _size--;
            return true;
        }

        // Returns the object at the head of the queue. The object remains in the
        // queue. If the queue is empty, this method throws an 
        // InvalidOperationException.
        public T Peek()
        {
            if (_size == 0)
            {
                ThrowForEmptyQueue();
            }

            return _array[_head];
        }

        public bool TryPeek(out T result)
        {
            if (_size == 0)
            {
                result = default(T);
                return false;
            }

            result = _array[_head];
            return true;
        }

        // Returns true if the queue contains at least one object equal to item.
        // Equality is determined using EqualityComparer<T>.Default.Equals().
        public bool Contains(T item)
        {
            if (_size == 0)
            {
                return false;
            }

            if (_head < _tail)
            {
                return Array.IndexOf(_array, item, _head, _size) >= 0;
            }

            // We've wrapped around. Check both partitions, the least recently enqueued first.
            return
                Array.IndexOf(_array, item, _head, _array.Length - _head) >= 0 ||
                Array.IndexOf(_array, item, 0, _tail) >= 0;
        }

        // Iterates over the objects in the queue, returning an array of the
        // objects in the Queue, or an empty array if the queue is empty.
        // The order of elements in the array is first in to last in, the same
        // order produced by successive calls to Dequeue.
        public T[] ToArray()
        {
            if (_size == 0)
            {
                return Array.Empty<T>();
            }

            T[] arr = new T[_size];

            if (_head < _tail)
            {
                Array.Copy(_array, _head, arr, 0, _size);
            }
            else
            {
                Array.Copy(_array, _head, arr, 0, _array.Length - _head);
                Array.Copy(_array, 0, arr, _array.Length - _head, _tail);
            }

            return arr;
        }

        // PRIVATE Grows or shrinks the buffer to hold capacity objects. Capacity
        // must be >= _size.
        private void SetCapacity(int capacity)
        {
            T[] newarray = new T[capacity];
            if (_size > 0)
            {
                if (_head < _tail)
                {
                    Array.Copy(_array, _head, newarray, 0, _size);
                }
                else
                {
                    Array.Copy(_array, _head, newarray, 0, _array.Length - _head);
                    Array.Copy(_array, 0, newarray, _array.Length - _head, _tail);
                }
            }

            _array = newarray;
            _head = 0;
            _tail = (_size == capacity) ? 0 : _size;
        }

        // Increments the index wrapping it if necessary.
        private void MoveNext(ref int index)
        {
            // It is tempting to use the remainder operator here but it is actually much slower 
            // than a simple comparison and a rarely taken branch.   
            int tmp = index + 1;
            index = (tmp == _array.Length) ? 0 : tmp;
        }

        private void ThrowForEmptyQueue()
        {
            Debug.Assert(_size == 0);
            throw new InvalidOperationException(SR.InvalidOperation_EmptyQueue);
        }

        public void TrimExcess()
        {
            int threshold = (int)(((double)_array.Length) * 0.9);
            if (_size < threshold)
            {
                SetCapacity(_size);
            }
        }
    }
}
