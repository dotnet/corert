// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Threading
{
    /// <summary>
    /// An array intended to be used for thread-specific collections representing wait handles used for multi-wait operations by
    /// the thread, to avoid allocations for each multi-wait. Has an initial capacity and grows up to a capacity of
    /// <see cref="WaitHandle.MaxWaitHandles"/>. Does not provide a count; the user is responsible for managing the array
    /// contents and track the count of elements that are actually used.
    /// </summary>
    internal struct WaitHandleArray<T>
    {
        private const int MaximumCapacity = WaitHandle.MaxWaitHandles;
        private const int InitialCapacity = 4; // should cover most typical cases

        private T[] _items;

        public WaitHandleArray(Func<int, T> elementInitializer)
        {
            Debug.Assert((MaximumCapacity & (MaximumCapacity - 1)) == 0); // is a power of 2
            Debug.Assert((InitialCapacity & (InitialCapacity - 1)) == 0); // is a power of 2
            Debug.Assert(InitialCapacity < MaximumCapacity);

            // Precreating these prevents waits from having to throw <see cref="OutOfMemoryException"/> in most typical cases
            _items = new T[InitialCapacity];

            if (elementInitializer != null)
            {
                for (int i = 0; i < InitialCapacity; ++i)
                {
                    _items[i] = elementInitializer(i);
                }
            }
        }

        public T[] Items
        {
            get
            {
                Debug.Assert(_items != null);
                return _items;
            }
        }

        [Conditional("DEBUG")]
        public void VerifyElementsAreDefault()
        {
            Debug.Assert(_items != null);

            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < _items.Length; ++i)
            {
                Debug.Assert(comparer.Equals(_items[i], default(T)));
            }
        }

        public void EnsureCapacity(int requiredCapacity, Func<int, T> elementInitializer = null)
        {
            Debug.Assert(requiredCapacity > 0);
            Debug.Assert(requiredCapacity <= MaximumCapacity);

            Debug.Assert(_items != null);

            if (requiredCapacity > _items.Length)
            {
                Grow(requiredCapacity, elementInitializer);
            }
        }

        private void Grow(int requiredCapacity, Func<int, T> elementInitializer = null)
        {
            Debug.Assert(requiredCapacity > _items.Length);
            Debug.Assert(requiredCapacity <= MaximumCapacity);

            Debug.Assert(_items != null);

            int oldCapacity = _items.Length;
            int newCapacity = oldCapacity;
            do
            {
                newCapacity <<= 1;
            } while (newCapacity < requiredCapacity);
            Debug.Assert(newCapacity <= MaximumCapacity);

            var newItems = new T[newCapacity];

            if (elementInitializer != null)
            {
                for (int i = 0; i < oldCapacity; ++i)
                {
                    newItems[i] = _items[i];
                }

                // Run the element initializers before changing the array. If an initializer fails, we'll try the resize again
                // next time.
                for (int i = oldCapacity; i < newCapacity; ++i)
                {
                    newItems[i] = elementInitializer(i);
                }
            }

            _items = newItems;
        }
    }
}
