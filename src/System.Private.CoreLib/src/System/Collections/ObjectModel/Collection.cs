// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;

using Internal.Runtime.Augments;

namespace System.Collections.ObjectModel
{
    [DebuggerTypeProxy(typeof(Mscorlib_CollectionDebugView<>))]
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public class Collection<T> : IList<T>, IList, IReadOnlyList<T>
    {
        private IList<T> items; // Do not rename (binary serialization)
        [NonSerialized]
        private Object _syncRoot;

        public Collection()
        {
            // We must implement our backing list using List<T>() as we have store apps that call Collection<T>.Items and cast
            // the result to List<T>.
            items = new List<T>();
        }

        public Collection(IList<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }
            items = list;
        }

        public int Count
        {
            get { return items.Count; }
        }

        protected IList<T> Items
        {
            get { return items; }
        }

        public T this[int index]
        {
            get { return items[index]; }
            set
            {
                if (items.IsReadOnly)
                {
                    throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
                }

                if (index < 0 || index >= items.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_ListItem);
                }

                SetItem(index, value);
            }
        }

        public void Add(T item)
        {
            if (items.IsReadOnly)
            {
                throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
            }

            int index = items.Count;
            InsertItem(index, item);
        }

        public void Clear()
        {
            if (items.IsReadOnly)
            {
                throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
            }

            ClearItems();
        }

        public void CopyTo(T[] array, int index)
        {
            items.CopyTo(array, index);
        }

        public bool Contains(T item)
        {
            return items.Contains(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            if (items.IsReadOnly)
            {
                throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
            }

            if (index < 0 || index > items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_ListInsert);
            }

            InsertItem(index, item);
        }

        public bool Remove(T item)
        {
            if (items.IsReadOnly)
            {
                throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
            }

            int index = items.IndexOf(item);
            if (index < 0) return false;
            RemoveItem(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            if (items.IsReadOnly)
            {
                throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
            }

            if (index < 0 || index >= items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_ListRemoveAt);
            }

            RemoveItem(index);
        }

        protected virtual void ClearItems()
        {
            items.Clear();
        }

        protected virtual void InsertItem(int index, T item)
        {
            items.Insert(index, item);
        }

        protected virtual void RemoveItem(int index)
        {
            items.RemoveAt(index);
        }

        protected virtual void SetItem(int index, T item)
        {
            items[index] = item;
        }

        bool ICollection<T>.IsReadOnly
        {
            get
            {
                return items.IsReadOnly;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)items).GetEnumerator();
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                {
                    ICollection c = items as ICollection;
                    if (c != null)
                    {
                        _syncRoot = c.SyncRoot;
                    }
                    else
                    {
                        System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);
                    }
                }
                return _syncRoot;
            }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Rank != 1)
            {
                throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);
            }

            if (array.GetLowerBound(0) != 0)
            {
                throw new ArgumentException(SR.Arg_NonZeroLowerBound);
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < Count)
            {
                throw new ArgumentException(SR.Arg_ArrayPlusOffTooSmall);
            }

            T[] tArray = array as T[];
            if (tArray != null)
            {
                items.CopyTo(tArray, index);
            }
            else
            {
                /* ProjectN port note: IsAssignable no longer available on Type surface area. This is a non-reliable check so we should be able to do without.
                //
                // Catch the obvious case assignment will fail.
                // We can found all possible problems by doing the check though.
                // For example, if the element type of the Array is derived from T,
                // we can't figure out if we can successfully copy the element beforehand.
                //
                IResolvedRuntimeType targetType = array.GetType().GetElementType().ResolvedType;
                IResolvedRuntimeType sourceType = typeof(T).ResolvedType;
                if(!(targetType.IsAssignableFrom(sourceType) || sourceType.IsAssignableFrom(targetType))) {
                    throw new ArgumentException(SR.Argument_InvalidArrayType);
                }
                */


                //
                // We can't cast array of value type to object[], so we don't support 
                // widening of primitive types here.
                //
                object[] objects = array as object[];
                if (objects == null)
                {
                    throw new ArgumentException(SR.Argument_InvalidArrayType);
                }

                int count = items.Count;
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        objects[index++] = items[i];
                    }
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException(SR.Argument_InvalidArrayType);
                }
            }
        }

        object IList.this[int index]
        {
            get { return items[index]; }
            set
            {
                if (value == null && !(default(T) == null))
                {
                    throw new ArgumentNullException(nameof(value));
                }

                try
                {
                    this[index] = (T)value;
                }
                catch (InvalidCastException)
                {
                    throw new ArgumentException(SR.Format(SR.Arg_WrongType, value, typeof(T)), nameof(value));
                }
            }
        }

        bool IList.IsReadOnly
        {
            get
            {
                return items.IsReadOnly;
            }
        }

        bool IList.IsFixedSize
        {
            get
            {
                // There is no IList<T>.IsFixedSize, so we must assume that only
                // readonly collections are fixed size, if our internal item 
                // collection does not implement IList.  Note that Array implements
                // IList, and therefore T[] and U[] will be fixed-size.
                IList list = items as IList;
                if (list != null)
                {
                    return list.IsFixedSize;
                }
                return items.IsReadOnly;
            }
        }

        int IList.Add(object value)
        {
            if (items.IsReadOnly)
            {
                throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
            }

            if (value == null && !(default(T) == null))
            {
                throw new ArgumentNullException(nameof(value));
            }

            try
            {
                Add((T)value);
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(SR.Format(SR.Arg_WrongType, value, typeof(T)), nameof(value));
            }

            return this.Count - 1;
        }

        bool IList.Contains(object value)
        {
            if (IsCompatibleObject(value))
            {
                return Contains((T)value);
            }
            return false;
        }

        int IList.IndexOf(object value)
        {
            if (IsCompatibleObject(value))
            {
                return IndexOf((T)value);
            }
            return -1;
        }

        void IList.Insert(int index, object value)
        {
            if (items.IsReadOnly)
            {
                throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
            }
            if (value == null && !(default(T) == null))
            {
                throw new ArgumentNullException(nameof(value));
            }

            try
            {
                Insert(index, (T)value);
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(SR.Format(SR.Arg_WrongType, value, typeof(T)), nameof(value));
            }
        }

        void IList.Remove(object value)
        {
            if (items.IsReadOnly)
            {
                throw new NotSupportedException(SR.NotSupported_ReadOnlyCollection);
            }

            if (IsCompatibleObject(value))
            {
                Remove((T)value);
            }
        }

        private static bool IsCompatibleObject(object value)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>. 
            return ((value is T) || (value == null && default(T) == null));
        }
    }
}
