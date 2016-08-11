// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime;

namespace System.Threading
{
    //
    // Methods for accessing memory with volatile semantics.
    //
    public unsafe static class Volatile
    {
        #region Boolean
        private struct VolatileBoolean { public volatile Boolean Value; }

        public static Boolean Read(ref Boolean location)
        {
            return Unsafe.As<Boolean, VolatileBoolean>(ref location).Value;
        }

        public static void Write(ref Boolean location, Boolean value)
        {
            Unsafe.As<Boolean, VolatileBoolean>(ref location).Value = value;
        }
        #endregion

        #region Byte
        private struct VolatileByte { public volatile Byte Value; }

        public static Byte Read(ref Byte location)
        {
            return Unsafe.As<Byte, VolatileByte>(ref location).Value;
        }

        public static void Write(ref Byte location, Byte value)
        {
            Unsafe.As<Byte, VolatileByte>(ref location).Value = value;
        }
        #endregion

        #region Double
        public static Double Read(ref Double location)
        {
            Int64 result = Read(ref Unsafe.As<Double, Int64>(ref location));
            return *(double*)&result;
        }

        public static void Write(ref Double location, Double value)
        {
            Write(ref Unsafe.As<Double, Int64>(ref location), *(Int64*)&value);
        }
        #endregion

        #region Int16
        private struct VolatileInt16 { public volatile Int16 Value; }

        public static Int16 Read(ref Int16 location)
        {
            return Unsafe.As<Int16, VolatileInt16>(ref location).Value;
        }

        public static void Write(ref Int16 location, Int16 value)
        {
            Unsafe.As<Int16, VolatileInt16>(ref location).Value = value;
        }
        #endregion

        #region Int32
        private struct VolatileInt32 { public volatile Int32 Value; }

        public static Int32 Read(ref Int32 location)
        {
            return Unsafe.As<Int32, VolatileInt32>(ref location).Value;
        }

        public static void Write(ref Int32 location, Int32 value)
        {
            Unsafe.As<Int32, VolatileInt32>(ref location).Value = value;
        }
        #endregion

        #region Int64
        public static Int64 Read(ref Int64 location)
        {
#if BIT64
            return (Int64)Unsafe.As<Int64, VolatileIntPtr>(ref location).Value;
#else
            return Interlocked.CompareExchange(ref location, 0, 0);
#endif
        }

        public static void Write(ref Int64 location, Int64 value)
        {
#if BIT64
            Unsafe.As<Int64, VolatileIntPtr>(ref location).Value = (IntPtr)value;
#else
            Interlocked.Exchange(ref location, value);
#endif
        }
        #endregion

        #region IntPtr
        private struct VolatileIntPtr { public volatile IntPtr Value; }

        public static IntPtr Read(ref IntPtr location)
        {
            return Unsafe.As<IntPtr, VolatileIntPtr>(ref location).Value;
        }

        public static void Write(ref IntPtr location, IntPtr value)
        {
            fixed (IntPtr* p = &location)
            {
                ((VolatileIntPtr*)p)->Value = value;
            }
        }
        #endregion

        #region SByte
        private struct VolatileSByte { public volatile SByte Value; }

        [CLSCompliant(false)]
        public static SByte Read(ref SByte location)
        {
            return Unsafe.As<SByte, VolatileSByte>(ref location).Value;
        }

        [CLSCompliant(false)]
        public static void Write(ref SByte location, SByte value)
        {
            Unsafe.As<SByte, VolatileSByte>(ref location).Value = value;
        }
        #endregion

        #region Single
        private struct VolatileSingle { public volatile Single Value; }

        public static Single Read(ref Single location)
        {
            return Unsafe.As<Single, VolatileSingle>(ref location).Value;
        }

        public static void Write(ref Single location, Single value)
        {
            Unsafe.As<Single, VolatileSingle>(ref location).Value = value;
        }
        #endregion

        #region UInt16
        private struct VolatileUInt16 { public volatile UInt16 Value; }

        [CLSCompliant(false)]
        public static UInt16 Read(ref UInt16 location)
        {
            return Unsafe.As<UInt16, VolatileUInt16>(ref location).Value;
        }

        [CLSCompliant(false)]
        public static void Write(ref UInt16 location, UInt16 value)
        {
            Unsafe.As<UInt16, VolatileUInt16>(ref location).Value = value;
        }
        #endregion

        #region UInt32
        private struct VolatileUInt32 { public volatile UInt32 Value; }

        [CLSCompliant(false)]
        public static UInt32 Read(ref UInt32 location)
        {
            return Unsafe.As<UInt32, VolatileUInt32>(ref location).Value;
        }

        [CLSCompliant(false)]
        public static void Write(ref UInt32 location, UInt32 value)
        {
            Unsafe.As<UInt32, VolatileUInt32>(ref location).Value = value;
        }
        #endregion

        #region UInt64
        [CLSCompliant(false)]
        public static UInt64 Read(ref UInt64 location)
        {
            return (UInt64)Read(ref Unsafe.As<UInt64, Int64>(ref location));
        }

        [CLSCompliant(false)]
        public static void Write(ref UInt64 location, UInt64 value)
        {
            Write(ref Unsafe.As<UInt64, Int64>(ref location), (Int64)value);
        }
        #endregion

        #region UIntPtr
        private struct VolatileUIntPtr { public volatile UIntPtr Value; }

        [CLSCompliant(false)]
        public static UIntPtr Read(ref UIntPtr location)
        {
            return Unsafe.As<UIntPtr, VolatileUIntPtr>(ref location).Value;
        }

        [CLSCompliant(false)]
        public static void Write(ref UIntPtr location, UIntPtr value)
        {
            Unsafe.As<UIntPtr, VolatileUIntPtr>(ref location).Value = value;
        }
        #endregion

        #region T
        private struct VolatileObject { public volatile Object Value; }

        public static T Read<T>(ref T location) where T : class
        {
            return Unsafe.As<T>(Unsafe.As<T, VolatileObject>(ref location).Value);
        }

        public static void Write<T>(ref T location, T value) where T : class
        {
            Unsafe.As<T, VolatileObject>(ref location).Value = value;
        }
        #endregion
    }
}
