// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*============================================================
**
**
** Private version of Stack<T> for internal System.Private.CoreLib use. This
** permits sharing more source between BCL and System.Private.CoreLib (as well as the
** fact that Stack<T> is just a useful class in general.)
**
** This does not strive to implement the full api surface area
** (but any portion it does implement should match the real Stack<T>'s
** behavior.)
** 
===========================================================*/

namespace System.Collections.Generic
{
    // Implements a variable-size Stack that uses an array of objects to store the
    // elements. A Stack has a capacity, which is the allocated length
    // of the internal array. As elements are added to a Stack, the capacity
    // of the Stack is automatically increased as required by reallocating the
    // internal array.
    // 
    /// <summary>
    /// LowLevelStack with no interface implementation to minimize both code and data size
    /// Data size is smaller because there will be minimal virtual function table.
    /// Code size is smaller because only functions called will be in the binary.
    /// </summary>
    internal class LowLevelStack<T>
    {
        protected T[] _items;
        protected int _size;
        protected int _version;

        private static readonly T[] s_emptyArray = new T[0];

        public LowLevelStack()
        {
            _items = s_emptyArray;
        }

        public LowLevelStack(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            if (capacity == 0)
                _items = s_emptyArray;
            else
            {
                _size = capacity;
                _items = new T[capacity];
            }
        }

        public LowLevelStack(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            ICollection<T> c = collection as ICollection<T>;
            if (c != null)
            {
                int count = c.Count;
                if (count == 0)
                {
                    _items = s_emptyArray;
                }
                else
                {
                    _items = new T[count];
                    c.CopyTo(_items, 0);
                    _size = count;
                }
            }
            else
            {
                _size = 0;
                _items = s_emptyArray;

                using (IEnumerator<T> en = collection.GetEnumerator())
                {
                    while (en.MoveNext())
                    {
                        Push(en.Current);
                    }
                }
            }
        }

        public int Count
        {
            get
            {
                return _items.Length;
            }
        }

        public void Push(T item)
        {
            _size = _size + 1;
            Array.Resize(ref _items, _size);
            _items[_size - 1] = item;
            _version++;
        }

        public T Pop()
        {
            ThrowIfEmptyStack();

            _size = _size - 1;
            T item = _items[_size];
            Array.Resize(ref _items, _size);
            _version++;
            return item;
        }

        public bool TryPop(out T result)
        {
            if (_size == 0)
            {
                result = default;
                return false;
            }

            _size = _size - 1;
            result = _items[_size];
            Array.Resize(ref _items, _size);
            _version++;

            return true;
        }

        public T Peek()
        {
            ThrowIfEmptyStack();
            return _items[_size - 1];
        }

        public bool TryPeek(out T result)
        {
            if (_size == 0)
            {
                result = default;
                return false;
            }

            result = _items[_size - 1];
            _version++;
            return true;
        }

        private void ThrowIfEmptyStack()
        {
            if (_size == 0)
                throw new InvalidOperationException();
        }
    }
}
