// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

namespace System.Collections.Generic
{
    internal static class EqualOnlyComparerHelper
    {
        public static bool Equals(SByte x, SByte y)
        {
            return x == y;
        }

        public static bool Equals(Byte x, Byte y)
        {
            return x == y;
        }

        public static bool Equals(Int16 x, Int16 y)
        {
            return x == y;
        }

        public static bool Equals(UInt16 x, UInt16 y)
        {
            return x == y;
        }

        public static bool Equals(Int32 x, Int32 y)
        {
            return x == y;
        }

        public static bool Equals(UInt32 x, UInt32 y)
        {
            return x == y;
        }

        public static bool Equals(Int64 x, Int64 y)
        {
            return x == y;
        }

        public static bool Equals(UInt64 x, UInt64 y)
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

        public static bool Equals(Single x, Single y)
        {
            return x == y;
        }

        public static bool Equals(Double x, Double y)
        {
            return x == y;
        }

        public static bool Equals(Decimal x, Decimal y)
        {
            return x == y;
        }

        public static bool Equals(String x, String y)
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
                return EqualOnlyComparerHelper.Equals(((System.SByte)(Object)(x)), ((System.SByte)(Object)(y)));
            else if (typeof(T) == typeof(System.Byte))
                return EqualOnlyComparerHelper.Equals(((System.Byte)(Object)(x)), ((System.Byte)(Object)(y)));
            else if (typeof(T) == typeof(System.Int16))
                return EqualOnlyComparerHelper.Equals(((System.Int16)(Object)(x)), ((System.Int16)(Object)(y)));
            else if (typeof(T) == typeof(System.UInt16))
                return EqualOnlyComparerHelper.Equals(((System.UInt16)(Object)(x)), ((System.UInt16)(Object)(y)));
            else if (typeof(T) == typeof(System.Int32))
                return EqualOnlyComparerHelper.Equals(((System.Int32)(Object)(x)), ((System.Int32)(Object)(y)));
            else if (typeof(T) == typeof(System.UInt32))
                return EqualOnlyComparerHelper.Equals(((System.UInt32)(Object)(x)), ((System.UInt32)(Object)(y)));
            else if (typeof(T) == typeof(System.Int64))
                return EqualOnlyComparerHelper.Equals(((System.Int64)(Object)(x)), ((System.Int64)(Object)(y)));
            else if (typeof(T) == typeof(System.UInt64))
                return EqualOnlyComparerHelper.Equals(((System.UInt64)(Object)(x)), ((System.UInt64)(Object)(y)));
            else if (typeof(T) == typeof(System.IntPtr))
                return EqualOnlyComparerHelper.Equals(((System.IntPtr)(Object)(x)), ((System.IntPtr)(Object)(y)));
            else if (typeof(T) == typeof(System.UIntPtr))
                return EqualOnlyComparerHelper.Equals(((System.UIntPtr)(Object)(x)), ((System.UIntPtr)(Object)(y)));
            else if (typeof(T) == typeof(System.Single))
                return EqualOnlyComparerHelper.Equals(((System.Single)(Object)(x)), ((System.Single)(Object)(y)));
            else if (typeof(T) == typeof(System.Double))
                return EqualOnlyComparerHelper.Equals(((System.Double)(Object)(x)), ((System.Double)(Object)(y)));
            else if (typeof(T) == typeof(System.Decimal))
                return EqualOnlyComparerHelper.Equals(((System.Decimal)(Object)(x)), ((System.Decimal)(Object)(y)));
            else if (typeof(T) == typeof(System.String))
                return EqualOnlyComparerHelper.Equals(((System.String)(Object)(x)), ((System.String)(Object)(y)));

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

    internal class ObjectEqualityComparer : IEqualityComparer
    {
        protected ObjectEqualityComparer()
        {
        }

        public static ObjectEqualityComparer Default
        {
            get
            {
                if (s_default == null)
                {
                    s_default = new ObjectEqualityComparer();
                }

                return s_default;
            }
        }

        private static volatile ObjectEqualityComparer s_default;

        int IEqualityComparer.GetHashCode(object obj)
        {
            if (obj == null)
                return 0;
            else return obj.GetHashCode();
        }

        bool IEqualityComparer.Equals(object x, object y)
        {
            if (x == null)
                return y == null;

            if (y == null)
                return false;

            return x.Equals(y);
        }
    }
}