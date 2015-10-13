// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

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
            if (_items == null || (_count + newItems.Length) >= _items.Length)
            {
                int newCount = 2 * _count + 1;
                while ((_count + newItems.Length) >= newCount)
                {
                    newCount = 2 * newCount + 1;
                }
                Array.Resize(ref _items, newCount);
            }
            Array.Copy(newItems, 0, _items, _count, newItems.Length);
            _count += newItems.Length;
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
    }
}
