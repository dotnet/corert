// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Versioning;

#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else
using nint = System.Int32;
using nuint = System.UInt32;
#endif

namespace System.Runtime.CompilerServices
{
    //
    // Subsetted clone of System.Runtime.CompilerServices.Unsafe for internal runtime use.
    // Keep in sync with https://github.com/dotnet/corefx/tree/master/src/System.Runtime.CompilerServices.Unsafe.
    // 

    /// <summary>
    /// Contains generic, low-level functionality for manipulating pointers.
    /// </summary>
    [CLSCompliant(false)]
    public static class Unsafe
    {
        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the given location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T Read<T>(void* source)
        {
            return Unsafe.As<byte, T>(ref *(byte*)source);
        }

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the given location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T Read<T>(ref byte source)
        {
            return Unsafe.As<byte, T>(ref source);
        }

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the given location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T ReadUnaligned<T>(void* source)
        {
            return Unsafe.As<byte, T>(ref *(byte*)source);
        }

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the given location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T ReadUnaligned<T>(ref byte source)
        {
            return Unsafe.As<byte, T>(ref source);
        }

        /// <summary>
        /// Writes a value of type <typeparamref name="T"/> to the given location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Write<T>(void* source, T value)
        {
            Unsafe.As<byte, T>(ref *(byte*)source) = value;
        }

        /// <summary>
        /// Writes a value of type <typeparamref name="T"/> to the given location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Write<T>(ref byte source, T value)
        {
            Unsafe.As<byte, T>(ref source) = value;
        }

        /// <summary>
        /// Writes a value of type <typeparamref name="T"/> to the given location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteUnaligned<T>(void* source, T value)
        {
            Unsafe.As<byte, T>(ref *(byte*)source) = value;
        }

        /// <summary>
        /// Writes a value of type <typeparamref name="T"/> to the given location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteUnaligned<T>(ref byte source, T value)
        {
            Unsafe.As<byte, T>(ref source) = value;
        }

        /// <summary>
        /// Returns a pointer to the given by-ref parameter.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void* AsPointer<T>(ref T source)
        {
            // This method is implemented by the toolchain
            throw new PlatformNotSupportedException();

            // ldarg.0
            // conv.u
            // ret
        }

        /// <summary>
        /// Returns the size of an object of the given type parameter.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>()
        {
            // This method is implemented by the toolchain
            throw new PlatformNotSupportedException();

            // sizeof !!0
            // ret
        }

        /// <summary>
        /// Casts the given object to the specified type, performs no dynamic type checking.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T As<T>(Object value) where T : class
        {
            // This method is implemented by the toolchain
            throw new PlatformNotSupportedException();

            // ldarg.0
            // ret
        }

        /// <summary>
        /// Reinterprets the given reference as a reference to a value of type <typeparamref name="TTo"/>.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TTo As<TFrom, TTo>(ref TFrom source)
        {
            // This method is implemented by the toolchain
            throw new PlatformNotSupportedException();

            // ldarg.0
            // ret
        }

        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ref T AddByteOffset<T>(ref T source, nuint byteOffset)
        {
            return ref AddByteOffset(ref source, (IntPtr)(void*)byteOffset);
        }

        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AddByteOffset<T>(ref T source, IntPtr byteOffset)
        {
            // This method is implemented by the toolchain
            throw new PlatformNotSupportedException();

            // ldarg.0
            // ldarg.1
            // add
            // ret
        }

        /// <summary>
        /// Adds an element offset to the given reference.
        /// </summary>
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Add<T>(ref T source, int elementOffset)
        {
            return ref AddByteOffset(ref source, (IntPtr)(elementOffset * (nint)SizeOf<T>()));
        }

        /// <summary>
        /// Determines whether the specified references point to the same location.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreSame<T>(ref T left, ref T right)
        {
            // This method is implemented by the toolchain
            throw new PlatformNotSupportedException();

            // ldarg.0
            // ldarg.1
            // ceq
            // ret
        }

        /// <summary>
        /// Initializes a block of memory at the given location with a given initial value 
        /// without assuming architecture dependent alignment of the address.
        /// </summary>
        [Intrinsic]
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount)
        {
            for (uint i = 0; i < byteCount; i++)
                AddByteOffset(ref startAddress, i) = value;
        }
    }
}
