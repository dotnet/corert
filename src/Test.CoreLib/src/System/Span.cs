// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Internal.Runtime.CompilerHelpers;
using Internal.Runtime.CompilerServices;

namespace System
{
    public readonly ref struct Span<T>
    {
        internal readonly ByReference<T> _pointer;

        private readonly int _length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span(T[] array)
        {
            _pointer = new ByReference<T>(ref Unsafe.As<byte, T>(ref Unsafe.As<RawArrayData>(array).Data));
            _length = array.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span(void* pointer, int length)
        {
            _pointer = new ByReference<T>(ref Unsafe.As<byte, T>(ref *(byte*)pointer));
            _length = length;
        }

        // Constructor for internal use only.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span(ref T ptr, int length)
        {
            Debug.Assert(length >= 0);

            _pointer = new ByReference<T>(ref ptr);
            _length = length;
        }

        public ref T this[int index]
        {
            [Intrinsic]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelpers.ThrowIndexOutOfRangeException();
                return ref Unsafe.Add(ref _pointer.Value, index);
            }
        }

        public int Length
        {
            get => _length;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public ref struct Enumerator
        {
            private readonly Span<T> _span;

            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(Span<T> span)
            {
                _span = span;
                _index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _span.Length)
                {
                    _index = index;
                    return true;
                }

                return false;
            }

            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _span[_index];
            }
        }

        public ref T GetPinnableReference()
        {
            return ref _pointer.Value;
        }

        public static implicit operator ReadOnlySpan<T>(Span<T> span) => new ReadOnlySpan<T>(ref span._pointer.Value, span._length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> Slice(int start)
        {
            return new Span<T>(ref Unsafe.Add(ref _pointer.Value, start), _length - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> Slice(int start, int length)
        {
            return new Span<T>(ref Unsafe.Add(ref _pointer.Value, start), length);
        }
    }
}
