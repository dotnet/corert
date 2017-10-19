// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace System.IO
{
    /// Perf notes: ReadXXX, WriteXXX (for basic types) acquire and release the 
    /// SafeBuffer pointer rather than relying on generic Read(T) from SafeBuffer because
    /// this gives better throughput; benchmarks showed about 12-15% better.
    public class UnmanagedMemoryAccessor : IDisposable
    {
        private SafeBuffer _buffer;
        private Int64 _offset;
        private Int64 _capacity;
        private FileAccess _access;
        private bool _isOpen;
        private bool _canRead;
        private bool _canWrite;

        /// <summary>
        /// Allows to efficiently read typed data from memory or SafeBuffer
        /// </summary>
        protected UnmanagedMemoryAccessor()
        {
            _isOpen = false;
        }

        #region SafeBuffer ctors and initializers
        /// <summary>
        /// Creates an instance over a slice of a SafeBuffer.
        /// </summary>
        /// <param name="buffer">Buffer containing raw bytes.</param>
        /// <param name="offset">First byte belonging to the slice.</param>
        /// <param name="capacity">Number of bytes in the slice.</param>
        // </SecurityKernel>
        public UnmanagedMemoryAccessor(SafeBuffer buffer, Int64 offset, Int64 capacity)
        {
            Initialize(buffer, offset, capacity, FileAccess.Read);
        }

        /// <summary>
        /// Creates an instance over a slice of a SafeBuffer.
        /// </summary>
        /// <param name="buffer">Buffer containing raw bytes.</param>
        /// <param name="offset">First byte belonging to the slice.</param>
        /// <param name="capacity">Number of bytes in the slice.</param>
        /// <param name="access">Access permissions.</param>
        public UnmanagedMemoryAccessor(SafeBuffer buffer, Int64 offset, Int64 capacity, FileAccess access)
        {
            Initialize(buffer, offset, capacity, access);
        }

        protected void Initialize(SafeBuffer buffer, Int64 offset, Int64 capacity, FileAccess access)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (buffer.ByteLength < (UInt64)(offset + capacity))
            {
                throw new ArgumentException(SR.Argument_OffsetAndCapacityOutOfBounds);
            }
            if (access < FileAccess.Read || access > FileAccess.ReadWrite)
            {
                throw new ArgumentOutOfRangeException(nameof(access));
            }

            if (_isOpen)
            {
                throw new InvalidOperationException(SR.InvalidOperation_CalledTwice);
            }

            unsafe
            {
                byte* pointer = null;

                try
                {
                    buffer.AcquirePointer(ref pointer);
                    if (((byte*)((Int64)pointer + offset + capacity)) < pointer)
                    {
                        throw new ArgumentException(SR.Argument_UnmanagedMemAccessorWrapAround);
                    }
                }
                finally
                {
                    if (pointer != null)
                    {
                        buffer.ReleasePointer();
                    }
                }
            }

            _offset = offset;
            _buffer = buffer;
            _capacity = capacity;
            _access = access;
            _isOpen = true;
            _canRead = (_access & FileAccess.Read) != 0;
            _canWrite = (_access & FileAccess.Write) != 0;
        }

        #endregion

        /// <summary>
        /// Number of bytes in the accessor.
        /// </summary>
        public Int64 Capacity
        {
            get
            {
                return _capacity;
            }
        }

        /// <summary>
        /// Returns true if the accessor can be read; otherwise returns false.
        /// </summary>
        public bool CanRead
        {
            get
            {
                return _isOpen && _canRead;
            }
        }

        /// <summary>
        /// Returns true if the accessor can be written to; otherwise returns false.
        /// </summary>
        public bool CanWrite
        {
            get
            {
                return _isOpen && _canWrite;
            }
        }

        /// <summary>
        /// Closes the accessor.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            _isOpen = false;
        }

        /// <summary>
        /// Closes the accessor.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns true if the accessor is open.
        /// </summary>
        protected bool IsOpen
        {
            get { return _isOpen; }
        }

        /// <summary>
        /// reads a Boolean value at given position
        /// </summary>
        public bool ReadBoolean(Int64 position)
        {
            return ReadByte(position) != 0;
        }

        /// <summary>
        /// reads a Byte value at given position
        /// </summary>
        public byte ReadByte(Int64 position)
        {
            int sizeOfType = sizeof(byte);
            EnsureSafeToRead(position, sizeOfType);

            byte result;
            unsafe
            {
                byte* pointer = null;

                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    result = *((byte*)(pointer + _offset + position));
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// reads a Char value at given position
        /// </summary>
        public char ReadChar(Int64 position)
        {
            return (char)ReadInt16(position);
        }

        /// <summary>
        /// reads an Int16 value at given position
        /// </summary>
        public Int16 ReadInt16(Int64 position)
        {
            int sizeOfType = sizeof(Int16);
            EnsureSafeToRead(position, sizeOfType);

            Int16 result;

            unsafe
            {
                byte* pointer = null;

                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    pointer += (_offset + position);


                    // check if pointer is aligned
                    if (((int)pointer & (sizeOfType - 1)) == 0)
                    {
                        result = *((Int16*)(pointer));
                    }
                    else
                    {
                        result = (Int16)(*pointer | *(pointer + 1) << 8);
                    }
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// reads an Int32 value at given position
        /// </summary>
        public Int32 ReadInt32(Int64 position)
        {
            int sizeOfType = sizeof(Int32);
            EnsureSafeToRead(position, sizeOfType);

            Int32 result;
            unsafe
            {
                byte* pointer = null;

                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    pointer += (_offset + position);


                    // check if pointer is aligned
                    if (((int)pointer & (sizeOfType - 1)) == 0)
                    {
                        result = *((Int32*)(pointer));
                    }
                    else
                    {
                        result = (Int32)(*pointer | *(pointer + 1) << 8 | *(pointer + 2) << 16 | *(pointer + 3) << 24);
                    }
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// reads an Int64 value at given position
        /// </summary>
        public Int64 ReadInt64(Int64 position)
        {
            int sizeOfType = sizeof(Int64);
            EnsureSafeToRead(position, sizeOfType);

            Int64 result;
            unsafe
            {
                byte* pointer = null;

                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    pointer += (_offset + position);

                    // check if pointer is aligned
                    if (((int)pointer & (sizeOfType - 1)) == 0)
                    {
                        result = *((Int64*)(pointer));
                    }
                    else
                    {
                        int lo = *pointer | *(pointer + 1) << 8 | *(pointer + 2) << 16 | *(pointer + 3) << 24;
                        int hi = *(pointer + 4) | *(pointer + 5) << 8 | *(pointer + 6) << 16 | *(pointer + 7) << 24;
                        result = (Int64)(((Int64)hi << 32) | (UInt32)lo);
                    }
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe Int32 UnsafeReadInt32(byte* pointer)
        {
            Int32 result;
            // check if pointer is aligned
            if (((int)pointer & (sizeof(Int32) - 1)) == 0)
            {
                result = *((Int32*)pointer);
            }
            else
            {
                result = (Int32)(*(pointer) | *(pointer + 1) << 8 | *(pointer + 2) << 16 | *(pointer + 3) << 24);
            }

            return result;
        }

        /// <summary>
        /// Reads a Decimal value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte of the value.</param>
        /// <returns></returns>
        public Decimal ReadDecimal(Int64 position)
        {
            const int ScaleMask = 0x00FF0000;
            const int SignMask = unchecked((int)0x80000000);

            int sizeOfType = sizeof(Decimal);
            EnsureSafeToRead(position, sizeOfType);

            unsafe
            {
                byte* pointer = null;
                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    pointer += (_offset + position);

                    int lo = UnsafeReadInt32(pointer);
                    int mid = UnsafeReadInt32(pointer + 4);
                    int hi = UnsafeReadInt32(pointer + 8);
                    int flags = UnsafeReadInt32(pointer + 12);

                    // Check for invalid Decimal values
                    if (!((flags & ~(SignMask | ScaleMask)) == 0 && (flags & ScaleMask) <= (28 << 16)))
                    {
                        throw new ArgumentException(SR.Arg_BadDecimal); // Throw same Exception type as Decimal(int[]) ctor for compat
                    }

                    bool isNegative = (flags & SignMask) != 0;
                    byte scale = (byte)(flags >> 16);

                    return new decimal(lo, mid, hi, isNegative, scale);
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }
        }

        /// <summary>
        /// reads a Single value at given position
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public Single ReadSingle(Int64 position)
        {
            unsafe
            {
                Int32 result = ReadInt32(position);
                return *((Single*)&result);
            }
        }

        /// <summary>
        /// reads a Double value at given position
        /// </summary>
        public Double ReadDouble(Int64 position)
        {
            unsafe
            {
                Int64 result = ReadInt64(position);
                return *((Double*)&result);
            }
        }

        /// <summary>
        /// Reads an SByte value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte of the value.</param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public SByte ReadSByte(Int64 position)
        {
            return (SByte)ReadByte(position);
        }

        /// <summary>
        /// reads a UInt16 value at given position
        /// </summary>
        [CLSCompliant(false)]
        public UInt16 ReadUInt16(Int64 position)
        {
            return (UInt16)ReadInt16(position);
        }

        /// <summary>
        /// Reads a UInt32 value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte of the value.</param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public UInt32 ReadUInt32(Int64 position)
        {
            return (UInt32)ReadInt32(position);
        }

        /// <summary>
        /// Reads a UInt64 value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte of the value.</param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public UInt64 ReadUInt64(Int64 position)
        {
            return (UInt64)ReadInt64(position);
        }

        /// <summary>
        // Reads a struct of type T from unmanaged memory, into the reference pointed to by ref value.  
        // Note: this method is not safe, since it overwrites the contents of a structure, it can be 
        // used to modify the private members of a struct.
        // This method is most performant when used with medium to large sized structs
        // (larger than 8 bytes -- though this is number is JIT and architecture dependent).   As 
        // such, it is best to use the ReadXXX methods for small standard types such as ints, longs, 
        // bools, etc.
        /// </summary>
        public void Read<T>(Int64 position, out T structure) where T : struct
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (!_isOpen)
            {
                throw new ObjectDisposedException(nameof(UnmanagedMemoryAccessor), SR.ObjectDisposed_ViewAccessorClosed);
            }
            if (!CanRead)
            {
                throw new NotSupportedException(SR.NotSupported_Reading);
            }

            uint sizeOfT = SafeBuffer.SizeOf<T>();
            if (position > _capacity - sizeOfT)
            {
                if (position >= _capacity)
                {
                    throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_PositionLessThanCapacityRequired);
                }
                else
                {
                    throw new ArgumentException(SR.Format(SR.Argument_NotEnoughBytesToRead, typeof(T)), nameof(position));
                }
            }

            structure = _buffer.Read<T>((UInt64)(_offset + position));
        }

        /// <summary>
        // Reads 'count' structs of type T from unmanaged memory, into 'array' starting at 'offset'.  
        // Note: this method is not safe, since it overwrites the contents of structures, it can 
        // be used to modify the private members of a struct.
        /// </summary>
        public int ReadArray<T>(Int64 position, T[] array, Int32 offset, Int32 count) where T : struct
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array), SR.ArgumentNull_Buffer);
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (array.Length - offset < count)
            {
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            }
            if (!CanRead)
            {
                if (!_isOpen)
                {
                    throw new ObjectDisposedException(nameof(UnmanagedMemoryAccessor), SR.ObjectDisposed_ViewAccessorClosed);
                }
                else
                {
                    throw new NotSupportedException(SR.NotSupported_Reading);
                }
            }
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            uint sizeOfT = SafeBuffer.AlignedSizeOf<T>();

            // only check position and ask for fewer Ts if count is too big
            if (position >= _capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_PositionLessThanCapacityRequired);
            }

            int n = count;
            long spaceLeft = _capacity - position;
            if (spaceLeft < 0)
            {
                n = 0;
            }
            else
            {
                ulong spaceNeeded = (ulong)(sizeOfT * count);
                if ((ulong)spaceLeft < spaceNeeded)
                {
                    n = (int)(spaceLeft / sizeOfT);
                }
            }

            _buffer.ReadArray<T>((UInt64)(_offset + position), array, offset, n);

            return n;
        }

        // ************** Write Methods ****************/

        // The following 13 WriteXXX methods write a value of type XXX into unmanaged memory at 'position'. 
        // The bounds of the unmanaged memory are checked against to ensure that there is enough 
        // space after 'position' to write a value of type XXX.  XXX can be a bool, byte, char, decimal, 
        // double, short, int, long, sbyte, float, ushort, uint, or ulong. 

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, bool value)
        {
            Write(position, (byte)(value ? 1 : 0));
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, byte value)
        {
            int sizeOfType = sizeof(byte);
            EnsureSafeToWrite(position, sizeOfType);

            unsafe
            {
                byte* pointer = null;

                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    *((byte*)(pointer + _offset + position)) = value;
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, char value)
        {
            Write(position, (Int16)value);
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, Int16 value)
        {
            int sizeOfType = sizeof(Int16);
            EnsureSafeToWrite(position, sizeOfType);

            unsafe
            {
                byte* pointer = null;

                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    pointer += (_offset + position);

                    // check if pointer is aligned
                    if (((int)pointer & (sizeOfType - 1)) == 0)
                    {
                        *((Int16*)pointer) = value;
                    }
                    else
                    {
                        *(pointer) = (byte)value;
                        *(pointer + 1) = (byte)(value >> 8);
                    }
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, Int32 value)
        {
            int sizeOfType = sizeof(Int32);
            EnsureSafeToWrite(position, sizeOfType);

            unsafe
            {
                byte* pointer = null;

                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    pointer += (_offset + position);

                    // check if pointer is aligned
                    if (((int)pointer & (sizeOfType - 1)) == 0)
                    {
                        *((Int32*)pointer) = value;
                    }
                    else
                    {
                        *(pointer) = (byte)value;
                        *(pointer + 1) = (byte)(value >> 8);
                        *(pointer + 2) = (byte)(value >> 16);
                        *(pointer + 3) = (byte)(value >> 24);
                    }
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, Int64 value)
        {
            int sizeOfType = sizeof(Int64);
            EnsureSafeToWrite(position, sizeOfType);

            unsafe
            {
                byte* pointer = null;

                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    pointer += (_offset + position);

                    // check if pointer is aligned
                    if (((int)pointer & (sizeOfType - 1)) == 0)
                    {
                        *((Int64*)pointer) = value;
                    }
                    else
                    {
                        *(pointer) = (byte)value;
                        *(pointer + 1) = (byte)(value >> 8);
                        *(pointer + 2) = (byte)(value >> 16);
                        *(pointer + 3) = (byte)(value >> 24);
                        *(pointer + 4) = (byte)(value >> 32);
                        *(pointer + 5) = (byte)(value >> 40);
                        *(pointer + 6) = (byte)(value >> 48);
                        *(pointer + 7) = (byte)(value >> 56);
                    }
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void UnsafeWriteInt32(byte* pointer, Int32 value)
        {
            // check if pointer is aligned
            if (((int)pointer & (sizeof(Int32) - 1)) == 0)
            {
                *((Int32*)pointer) = value;
            }
            else
            {
                *(pointer) = (byte)value;
                *(pointer + 1) = (byte)(value >> 8);
                *(pointer + 2) = (byte)(value >> 16);
                *(pointer + 3) = (byte)(value >> 24);
            }
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, Decimal value)
        {
            int sizeOfType = sizeof(Decimal);
            EnsureSafeToWrite(position, sizeOfType);

            unsafe
            {
                byte* pointer = null;
                try
                {
                    _buffer.AcquirePointer(ref pointer);
                    pointer += (_offset + position);

                    int* valuePtr = (int*)(&value);
                    int flags = *valuePtr;
                    int hi = *(valuePtr + 1);
                    int lo = *(valuePtr + 2);
                    int mid = *(valuePtr + 3);

                    UnsafeWriteInt32(pointer, lo);
                    UnsafeWriteInt32(pointer + 4, mid);
                    UnsafeWriteInt32(pointer + 8, hi);
                    UnsafeWriteInt32(pointer + 12, flags);
                }
                finally
                {
                    if (pointer != null)
                    {
                        _buffer.ReleasePointer();
                    }
                }
            }
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, Single value)
        {
            unsafe
            {
                Write(position, *(Int32*)&value);
            }
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        public void Write(Int64 position, Double value)
        {
            unsafe
            {
                Write(position, *(Int64*)&value);
            }
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        [CLSCompliant(false)]
        public void Write(Int64 position, SByte value)
        {
            Write(position, (byte)value);
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        [CLSCompliant(false)]
        public void Write(Int64 position, UInt16 value)
        {
            Write(position, (Int16)value);
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        [CLSCompliant(false)]
        public void Write(Int64 position, UInt32 value)
        {
            Write(position, (Int32)value);
        }

        /// <summary>
        /// Writes the value at the specified position.
        /// </summary>
        /// <param name="position">The position of the first byte.</param>
        /// <param name="value">Value to be written to the memory</param>
        [CLSCompliant(false)]
        public void Write(Int64 position, UInt64 value)
        {
            Write(position, (Int64)value);
        }

        /// <summary>
        // Writes the struct pointed to by ref value into unmanaged memory.  Note that this method
        // is most performant when used with medium to large sized structs (larger than 8 bytes 
        // though this is number is JIT and architecture dependent).   As such, it is best to use 
        // the WriteX methods for small standard types such as ints, longs, bools, etc.
        /// </summary>
        public void Write<T>(Int64 position, ref T structure) where T : struct
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (!_isOpen)
            {
                throw new ObjectDisposedException(nameof(UnmanagedMemoryAccessor), SR.ObjectDisposed_ViewAccessorClosed);
            }
            if (!CanWrite)
            {
                throw new NotSupportedException(SR.NotSupported_Writing);
            }

            uint sizeOfT = SafeBuffer.SizeOf<T>();
            if (position > _capacity - sizeOfT)
            {
                if (position >= _capacity)
                {
                    throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_PositionLessThanCapacityRequired);
                }
                else
                {
                    throw new ArgumentException(SR.Format(SR.Argument_NotEnoughBytesToWrite, typeof(T)), nameof(position));
                }
            }

            _buffer.Write<T>((UInt64)(_offset + position), structure);
        }

        /// <summary>
        // Writes 'count' structs of type T from 'array' (starting at 'offset') into unmanaged memory. 
        /// </summary>
        public void WriteArray<T>(Int64 position, T[] array, Int32 offset, Int32 count) where T : struct
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array), SR.ArgumentNull_Buffer);
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (array.Length - offset < count)
            {
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            }
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (position >= Capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_PositionLessThanCapacityRequired);
            }

            if (!_isOpen)
            {
                throw new ObjectDisposedException(nameof(UnmanagedMemoryAccessor), SR.ObjectDisposed_ViewAccessorClosed);
            }
            if (!CanWrite)
            {
                throw new NotSupportedException(SR.NotSupported_Writing);
            }

            _buffer.WriteArray<T>((UInt64)(_offset + position), array, offset, count);
        }

        private void EnsureSafeToRead(Int64 position, int sizeOfType)
        {
            if (!_isOpen)
            {
                throw new ObjectDisposedException(nameof(UnmanagedMemoryAccessor), SR.ObjectDisposed_ViewAccessorClosed);
            }
            if (!_canRead)
            {
                throw new NotSupportedException(SR.NotSupported_Reading);
            }
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (position > _capacity - sizeOfType)
            {
                if (position >= _capacity)
                {
                    throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_PositionLessThanCapacityRequired);
                }
                else
                {
                    throw new ArgumentException(SR.Argument_NotEnoughBytesToRead, nameof(position));
                }
            }
        }

        private void EnsureSafeToWrite(Int64 position, int sizeOfType)
        {
            if (!_isOpen)
            {
                throw new ObjectDisposedException(nameof(UnmanagedMemoryAccessor), SR.ObjectDisposed_ViewAccessorClosed);
            }
            if (!_canWrite)
            {
                throw new NotSupportedException(SR.NotSupported_Writing);
            }
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (position > _capacity - sizeOfType)
            {
                if (position >= _capacity)
                {
                    throw new ArgumentOutOfRangeException(nameof(position), SR.ArgumentOutOfRange_PositionLessThanCapacityRequired);
                }
                else
                {
                    throw new ArgumentException(SR.Argument_NotEnoughBytesToWrite, nameof(position));
                }
            }
        }
    }
}
