// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;

namespace System.Collections.Generic
{
    internal static class EqualOnlyComparerHelper
    {
        public static bool Equals(sbyte x, sbyte y)
        {
            return x == y;
        }

        public static bool Equals(byte x, byte y)
        {
            return x == y;
        }

        public static bool Equals(short x, short y)
        {
            return x == y;
        }

        public static bool Equals(ushort x, ushort y)
        {
            return x == y;
        }

        public static bool Equals(int x, int y)
        {
            return x == y;
        }

        public static bool Equals(uint x, uint y)
        {
            return x == y;
        }

        public static bool Equals(long x, long y)
        {
            return x == y;
        }

        public static bool Equals(ulong x, ulong y)
        {
            return x == y;
        }

        public static bool Equals(IntPtr x, IntPtr y)
        {
            return x == y;
        }

        public static bool Equals(UIntPtr x, UIntPtr y)
        {
            return x == y;
        }

        public static bool Equals(float x, float y)
        {
            return x == y;
        }

        public static bool Equals(double x, double y)
        {
            return x == y;
        }

        public static bool Equals(decimal x, decimal y)
        {
            return x == y;
        }

        public static bool Equals(string x, string y)
        {
            return x == y;
        }
    }

    /// <summary>
    /// Minimum comparer for Array.IndexOf/Contains which each Array needs. So it's important to be small.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class EqualOnlyComparer<T>
    {
        // Force the compiler to inline this method. Normally the compiler will shy away from inlining such
        // a large function, however in this case the method compiles down to almost nothing so help the
        // compiler out a bit with this hint. Once the compiler supports bottom-up codegen analysis it should
        // inline this without a hint.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool Equals(T x, T y)
        {
            // Specialized Comparers
            if (typeof(T) == typeof(System.SByte))
                return EqualOnlyComparerHelper.Equals(((System.SByte)(object)(x)), ((System.SByte)(object)(y)));
            else if (typeof(T) == typeof(System.Byte))
                return EqualOnlyComparerHelper.Equals(((System.Byte)(object)(x)), ((System.Byte)(object)(y)));
            else if (typeof(T) == typeof(System.Int16))
                return EqualOnlyComparerHelper.Equals(((System.Int16)(object)(x)), ((System.Int16)(object)(y)));
            else if (typeof(T) == typeof(System.UInt16))
                return EqualOnlyComparerHelper.Equals(((System.UInt16)(object)(x)), ((System.UInt16)(object)(y)));
            else if (typeof(T) == typeof(System.Int32))
                return EqualOnlyComparerHelper.Equals(((System.Int32)(object)(x)), ((System.Int32)(object)(y)));
            else if (typeof(T) == typeof(System.UInt32))
                return EqualOnlyComparerHelper.Equals(((System.UInt32)(object)(x)), ((System.UInt32)(object)(y)));
            else if (typeof(T) == typeof(System.Int64))
                return EqualOnlyComparerHelper.Equals(((System.Int64)(object)(x)), ((System.Int64)(object)(y)));
            else if (typeof(T) == typeof(System.UInt64))
                return EqualOnlyComparerHelper.Equals(((System.UInt64)(object)(x)), ((System.UInt64)(object)(y)));
            else if (typeof(T) == typeof(System.IntPtr))
                return EqualOnlyComparerHelper.Equals(((System.IntPtr)(object)(x)), ((System.IntPtr)(object)(y)));
            else if (typeof(T) == typeof(System.UIntPtr))
                return EqualOnlyComparerHelper.Equals(((System.UIntPtr)(object)(x)), ((System.UIntPtr)(object)(y)));
            else if (typeof(T) == typeof(System.Single))
                return EqualOnlyComparerHelper.Equals(((System.Single)(object)(x)), ((System.Single)(object)(y)));
            else if (typeof(T) == typeof(System.Double))
                return EqualOnlyComparerHelper.Equals(((System.Double)(object)(x)), ((System.Double)(object)(y)));
            else if (typeof(T) == typeof(System.Decimal))
                return EqualOnlyComparerHelper.Equals(((System.Decimal)(object)(x)), ((System.Decimal)(object)(y)));
            else if (typeof(T) == typeof(System.String))
                return EqualOnlyComparerHelper.Equals(((System.String)(object)(x)), ((System.String)(object)(y)));

            // Default Comparer

            if (x == null)
            {
                return y == null;
            }

            if (y == null)
            {
                return false;
            }

            return x.Equals(y);
        }
    }
}
