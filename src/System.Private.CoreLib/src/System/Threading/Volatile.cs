// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            fixed (Boolean* p = &location)
            {
                return ((VolatileBoolean*)p)->Value;
            }
        }

        public static void Write(ref Boolean location, Boolean value)
        {
            fixed (Boolean* p = &location)
            {
                ((VolatileBoolean*)p)->Value = value;
            }
        }
        #endregion

        #region Byte
        private struct VolatileByte { public volatile Byte Value; }

        public static Byte Read(ref Byte location)
        {
            fixed (Byte* p = &location)
            {
                return ((VolatileByte*)p)->Value;
            }
        }

        public static void Write(ref Byte location, Byte value)
        {
            fixed (Byte* p = &location)
            {
                ((VolatileByte*)p)->Value = value;
            }
        }
        #endregion

        #region Double

        public static Double Read(ref Double location)
        {
            fixed (Double* p = &location)
            {
                Int64 result = Read(ref *(Int64*)p);
                return *(double*)&result;
            }
        }

        public static void Write(ref Double location, Double value)
        {
            fixed (Double* p = &location)
            {
                Write(ref *(Int64*)p, *(Int64*)&value);
            }
        }
        #endregion

        #region Int16
        private struct VolatileInt16 { public volatile Int16 Value; }

        public static Int16 Read(ref Int16 location)
        {
            fixed (Int16* p = &location)
            {
                return ((VolatileInt16*)p)->Value;
            }
        }

        public static void Write(ref Int16 location, Int16 value)
        {
            fixed (Int16* p = &location)
            {
                ((VolatileInt16*)p)->Value = value;
            }
        }
        #endregion

        #region Int32
        private struct VolatileInt32 { public volatile Int32 Value; }

        public static Int32 Read(ref Int32 location)
        {
            fixed (Int32* p = &location)
            {
                return ((VolatileInt32*)p)->Value;
            }
        }

        public static void Write(ref Int32 location, Int32 value)
        {
            fixed (Int32* p = &location)
            {
                ((VolatileInt32*)p)->Value = value;
            }
        }
        #endregion

        #region Int64

        public static Int64 Read(ref Int64 location)
        {
#if BIT64
            fixed (Int64* p = &location)
            {
                return (Int64)Read(ref *(IntPtr*)p);
            }
#else
            return Interlocked.CompareExchange(ref location, 0, 0);
#endif
        }

        public static void Write(ref Int64 location, Int64 value)
        {
#if BIT64
            fixed (Int64* p = &location)
            {
                Write(ref *(IntPtr*)p, (IntPtr)value);
            }
#else
            Interlocked.Exchange(ref location, value);
#endif
        }
        #endregion

        #region IntPtr
        private struct VolatileIntPtr { public volatile IntPtr Value; }

        public static IntPtr Read(ref IntPtr location)
        {
            fixed (IntPtr* p = &location)
            {
                return ((VolatileIntPtr*)p)->Value;
            }
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
            fixed (SByte* p = &location)
            {
                return ((VolatileSByte*)p)->Value;
            }
        }

        [CLSCompliant(false)]
        public static void Write(ref SByte location, SByte value)
        {
            fixed (SByte* p = &location)
            {
                ((VolatileSByte*)p)->Value = value;
            }
        }
        #endregion

        #region Single
        private struct VolatileSingle { public volatile Single Value; }

        public static Single Read(ref Single location)
        {
            fixed (Single* p = &location)
            {
                return ((VolatileSingle*)p)->Value;
            }
        }

        public static void Write(ref Single location, Single value)
        {
            fixed (Single* p = &location)
            {
                ((VolatileSingle*)p)->Value = value;
            }
        }
        #endregion

        #region UInt16
        private struct VolatileUInt16 { public volatile UInt16 Value; }

        [CLSCompliant(false)]
        public static UInt16 Read(ref UInt16 location)
        {
            fixed (UInt16* p = &location)
            {
                return ((VolatileUInt16*)p)->Value;
            }
        }

        [CLSCompliant(false)]
        public static void Write(ref UInt16 location, UInt16 value)
        {
            fixed (UInt16* p = &location)
            {
                ((VolatileUInt16*)p)->Value = value;
            }
        }
        #endregion

        #region UInt32
        private struct VolatileUInt32 { public volatile UInt32 Value; }

        [CLSCompliant(false)]
        public static UInt32 Read(ref UInt32 location)
        {
            fixed (UInt32* p = &location)
            {
                return ((VolatileUInt32*)p)->Value;
            }
        }

        [CLSCompliant(false)]
        public static void Write(ref UInt32 location, UInt32 value)
        {
            fixed (UInt32* p = &location)
            {
                ((VolatileUInt32*)p)->Value = value;
            }
        }
        #endregion

        #region UInt64

        [CLSCompliant(false)]
        public static UInt64 Read(ref UInt64 location)
        {
            fixed (UInt64* p = &location)
            {
                return (UInt64)Read(ref *(Int64*)p);
            }
        }

        [CLSCompliant(false)]
        public static void Write(ref UInt64 location, UInt64 value)
        {
            fixed (UInt64* p = &location)
            {
                Write(ref *(Int64*)p, (Int64)value);
            }
        }
        #endregion

        #region UIntPtr
        private struct VolatileUIntPtr { public volatile UIntPtr Value; }

        [CLSCompliant(false)]
        public static UIntPtr Read(ref UIntPtr location)
        {
            fixed (UIntPtr* p = &location)
            {
                return ((VolatileUIntPtr*)p)->Value;
            }
        }

        [CLSCompliant(false)]
        public static void Write(ref UIntPtr location, UIntPtr value)
        {
            fixed (UIntPtr* p = &location)
            {
                ((VolatileUIntPtr*)p)->Value = value;
            }
        }
        #endregion

        #region T

        public static T Read<T>(ref T location) where T : class
        {
            //@TODO: need intrinsic implementation of Volatile.Read<T>, or some way to take the address of a reference.
            return Interlocked.CompareExchange(ref location, null, null);
        }

        public static void Write<T>(ref T location, T value) where T : class
        {
            //@TODO: need intrinsic implementation of Volatile.Write<T>, or some way to take the address of a reference.
            Interlocked.Exchange(ref location, value);
        }
        #endregion
    }
}
