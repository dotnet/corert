// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Convenient wrapper for an array, an offset, and
**          a count.  Ideally used in streams & collections.
**          Net Classes will consume an array of these.
**
**
===========================================================*/

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace System
{
    // Note: users should make sure they copy the fields out of an ArraySegment onto their stack
    // then validate that the fields describe valid bounds within the array.  This must be done
    // because assignments to value types are not atomic, and also because one thread reading 
    // three fields from an ArraySegment may not see the same ArraySegment from one call to another
    // (ie, users could assign a new value to the old location).  

    public struct ArraySegment<T> : IList<T>, IReadOnlyList<T>
    {
        private T[] _array;
        private int _offset;
        private int _count;

        public ArraySegment(T[] array)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            Contract.EndContractBlock();

            _array = array;
            _offset = 0;
            _count = array.Length;
        }

        public ArraySegment(T[] array, int offset, int count)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
            if (array.Length - offset < count)
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            Contract.EndContractBlock();

            _array = array;
            _offset = offset;
            _count = count;
        }

        public T[] Array
        {
            get
            {
                Debug.Assert((null == _array && 0 == _offset && 0 == _count)
                                 || (null != _array && _offset >= 0 && _count >= 0 && _offset + _count <= _array.Length),
                                "ArraySegment is invalid");

                return _array;
            }
        }

        public int Offset
        {
            get
            {
                // Since copying value types is not atomic & callers cannot atomically 
                // read all three fields, we cannot guarantee that Offset is within 
                // the bounds of Array.  That is our intent, but let's not specify 
                // it as a postcondition - force callers to re-verify this themselves
                // after reading each field out of an ArraySegment into their stack.
                Contract.Ensures(Contract.Result<int>() >= 0);

                Debug.Assert((null == _array && 0 == _offset && 0 == _count)
                                 || (null != _array && _offset >= 0 && _count >= 0 && _offset + _count <= _array.Length),
                                "ArraySegment is invalid");

                return _offset;
            }
        }

        public int Count
        {
            get
            {
                // Since copying value types is not atomic & callers cannot atomically 
                // read all three fields, we cannot guarantee that Count is within 
                // the bounds of Array.  That's our intent, but let's not specify 
                // it as a postcondition - force callers to re-verify this themselves
                // after reading each field out of an ArraySegment into their stack.
                Contract.Ensures(Contract.Result<int>() >= 0);

                Debug.Assert((null == _array && 0 == _offset && 0 == _count)
                                  || (null != _array && _offset >= 0 && _count >= 0 && _offset + _count <= _array.Length),
                                "ArraySegment is invalid");

                return _count;
            }
        }

        public override int GetHashCode()
        {
            return null == _array
                        ? 0
                        : _array.GetHashCode() ^ _offset ^ _count;
        }

        public override bool Equals(Object obj)
        {
            if (obj is ArraySegment<T>)
                return Equals((ArraySegment<T>)obj);
            else
                return false;
        }

        public bool Equals(ArraySegment<T> obj)
        {
            return obj._array == _array && obj._offset == _offset && obj._count == _count;
        }

        public static bool operator ==(ArraySegment<T> a, ArraySegment<T> b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(ArraySegment<T> a, ArraySegment<T> b)
        {
            return !(a == b);
        }

        #region IList<T>
        T IList<T>.this[int index]
        {
            get
            {
                if (_array == null)
                    throw new InvalidOperationException(SR.InvalidOperation_NullArray);
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException("index");
                Contract.EndContractBlock();

                return _array[_offset + index];
            }

            set
            {
                if (_array == null)
                    throw new InvalidOperationException(SR.InvalidOperation_NullArray);
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException("index");
                Contract.EndContractBlock();

                _array[_offset + index] = value;
            }
        }

        int IList<T>.IndexOf(T item)
        {
            if (_array == null)
                throw new InvalidOperationException(SR.InvalidOperation_NullArray);
            Contract.EndContractBlock();

            int index = System.Array.IndexOf<T>(_array, item, _offset, _count);

            Debug.Assert(index == -1 ||
                            (index >= _offset && index < _offset + _count));

            return index >= 0 ? index - _offset : -1;
        }

        void IList<T>.Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }
        #endregion

        #region IReadOnlyList<T>
        T IReadOnlyList<T>.this[int index]
        {
            get
            {
                if (_array == null)
                    throw new InvalidOperationException(SR.InvalidOperation_NullArray);
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException("index");
                Contract.EndContractBlock();

                return _array[_offset + index];
            }
        }
        #endregion IReadOnlyList<T>

        #region ICollection<T>
        bool ICollection<T>.IsReadOnly
        {
            get
            {
                // the indexer setter does not throw an exception although IsReadOnly is true.
                // This is to match the behavior of arrays.
                return true;
            }
        }

        void ICollection<T>.Add(T item)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }

        bool ICollection<T>.Contains(T item)
        {
            if (_array == null)
                throw new InvalidOperationException(SR.InvalidOperation_NullArray);
            Contract.EndContractBlock();

            int index = System.Array.IndexOf<T>(_array, item, _offset, _count);

            Debug.Assert(index == -1 ||
                            (index >= _offset && index < _offset + _count));

            return index >= 0;
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            if (_array == null)
                throw new InvalidOperationException(SR.InvalidOperation_NullArray);
            System.Array.Copy(_array, _offset, array, arrayIndex, _count);
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }
        #endregion

        #region IEnumerable<T>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            if (_array == null)
                throw new InvalidOperationException(SR.InvalidOperation_NullArray);
            Contract.EndContractBlock();

            return new ArraySegmentEnumerator(this);
        }
        #endregion

        #region IEnumerable
        IEnumerator IEnumerable.GetEnumerator()
        {
            if (_array == null)
                throw new InvalidOperationException(SR.InvalidOperation_NullArray);
            Contract.EndContractBlock();

            return new ArraySegmentEnumerator(this);
        }
        #endregion

        private sealed class ArraySegmentEnumerator : IEnumerator<T>
        {
            private T[] _array;
            private int _start;
            private int _end;
            private int _current;

            internal ArraySegmentEnumerator(ArraySegment<T> arraySegment)
            {
                Debug.Assert(arraySegment.Array != null);
                Debug.Assert(arraySegment.Offset >= 0);
                Debug.Assert(arraySegment.Count >= 0);
                Debug.Assert(arraySegment.Offset + arraySegment.Count <= arraySegment.Array.Length);

                _array = arraySegment._array;
                _start = arraySegment._offset;
                _end = _start + arraySegment._count;
                _current = _start - 1;
            }

            public bool MoveNext()
            {
                if (_current < _end)
                {
                    _current++;
                    return (_current < _end);
                }
                return false;
            }

            public T Current
            {
                get
                {
                    if (_current < _start) throw new InvalidOperationException(SR.InvalidOperation_EnumNotStarted);
                    if (_current >= _end) throw new InvalidOperationException(SR.InvalidOperation_EnumEnded);
                    return _array[_current];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            void IEnumerator.Reset()
            {
                _current = _start - 1;
            }

            public void Dispose()
            {
            }
        }
    }
}
