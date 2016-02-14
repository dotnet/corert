// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace System.Collections.Generic
{
    /// <summary>
    /// Helper class for building lists that avoids unnecessary allocation
    /// </summary>
    internal struct ArrayBuilder<T>
    {
        private T[] _items;
        private int _count;

        public T[] ToArray()
        {
            if (_items == null)
                return Array.Empty<T>();
            if (_count != _items.Length)
                Array.Resize(ref _items, _count);
            return _items;
        }

        public void Add(T item)
        {
            if (_items == null || _count == _items.Length)
                Array.Resize(ref _items, 2 * _count + 1);
            _items[_count++] = item;
        }

        public void Append(T[] newItems)
        {
            if (newItems.Length == 0)
                return;
            EnsureCapacity(_count + newItems.Length);
            Array.Copy(newItems, 0, _items, _count, newItems.Length);
            _count += newItems.Length;
        }

        public void ZeroExtend(int numItems)
        {
            Debug.Assert(numItems >= 0);
            EnsureCapacity(_count + numItems);
            _count += numItems;
        }

        public void EnsureCapacity(int requestedCapacity)
        {
            if (requestedCapacity > ((_items != null) ? _items.Length : 0))
            {
                int newCount = Math.Max(2 * _count + 1, requestedCapacity);
                Array.Resize(ref _items, newCount);
            }
        }

        public int Count
        {
            get
            {
                return _count;
            }
        }

        public T this[int index]
        {
            get
            {
                return _items[index];
            }
            set
            {
                _items[index] = value;
            }
        }

        public bool Contains(T t)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_items[i].Equals(t))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
