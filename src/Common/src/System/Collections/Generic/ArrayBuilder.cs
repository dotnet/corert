// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Debug = System.Diagnostics.Debug;

namespace System.Collections.Generic
{
    //
    // Helper class for building lists that avoids unnecessary allocation
    //
    internal struct ArrayBuilder<T>
    {
        T[] _items;
        int _count;

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
            var oldCount = _count;
            ZeroExtend(newItems.Length);
            Array.Copy(newItems, 0, _items, oldCount, newItems.Length);
        }

        public void ZeroExtend(int numItems)
        {
            Debug.Assert(numItems >= 0);

            if (_items == null || (_count + numItems) >= _items.Length)
            {
                int newCount = 2 * _count + 1;
                while ((_count + numItems) >= newCount)
                {
                    newCount = 2 * newCount + 1;
                }
                Array.Resize(ref _items, newCount);
            }
            _count += numItems;
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
