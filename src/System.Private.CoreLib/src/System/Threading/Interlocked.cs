// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace System.Threading
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;

    public static class Interlocked
    {
        #region CompareExchange

        [NonVersionable]
        [Intrinsic]
        public static int CompareExchange(ref int location1, int value, int comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [NonVersionable]
        [Intrinsic]
        internal static extern uint CompareExchange(ref uint location1, uint value, uint comparand);

        [NonVersionable]
        [Intrinsic]
        public static long CompareExchange(ref long location1, long value, long comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [NonVersionable]
        [Intrinsic]
        public static IntPtr CompareExchange(ref IntPtr location1, IntPtr value, IntPtr comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [NonVersionable]
        [Intrinsic]
        public static float CompareExchange(ref float location1, float value, float comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [NonVersionable]
        [Intrinsic]
        public static double CompareExchange(ref double location1, double value, double comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [NonVersionable]
        [Intrinsic]
        public static T CompareExchange<T>(ref T location1, T value, T comparand) where T : class
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange<T>(ref location1, value, comparand);
        }

        [NonVersionable]
        public static object CompareExchange(ref object location1, object value, object comparand)
        {
            return CompareExchange<object>(ref location1, value, comparand);
        }

        [NonVersionable]
        [Intrinsic]
        internal static extern T CompareExchange<T>(IntPtr location1, T value, T comparand) where T : class;

        #endregion

        #region Exchange

        [NonVersionable]
        [Intrinsic]
        public static int Exchange(ref int location1, int value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
        }

#if X86
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Exchange(ref long location1, long value)
        {
            long oldValue;

            do
            {
                oldValue = location1;
            } while (CompareExchange(ref location1, value, oldValue) != oldValue);

            return oldValue;
        }
#else
        [NonVersionable]
        [Intrinsic]
        public static long Exchange(ref long location1, long value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
        }
#endif

        [NonVersionable]
        [Intrinsic]
        public static IntPtr Exchange(ref IntPtr location1, IntPtr value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
        }

        [NonVersionable]
        [Intrinsic]
        public static float Exchange(ref float location1, float value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
        }

#if X86
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exchange(ref double location1, double value)
        {
            double oldValue;

            do
            {
                oldValue = location1;
            } while (CompareExchange(ref location1, value, oldValue) != oldValue);

            return oldValue;
        }
#else
        [NonVersionable]
        [Intrinsic]
        public static double Exchange(ref double location1, double value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
        }
#endif

        [NonVersionable]
        [Intrinsic]
        public static T Exchange<T>(ref T location1, T value) where T : class
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange<T>(ref location1, value);
        }

        [NonVersionable]
        [Intrinsic]
        internal static extern T Exchange<T>(IntPtr location1, T value) where T : class;

        [NonVersionable]
        public static object Exchange(ref object location1, object value)
        {
            return Exchange<object>(ref location1, value);
        }

        #endregion

        #region Increment

        [NonVersionable]
        [Intrinsic]
        public static int Increment(ref int location)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Increment(ref location);
        }

#if X86
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Increment(ref long location)
        {
            long oldValue;

            do
            {
                oldValue = location;
            } while (CompareExchange(ref location, oldValue + 1, oldValue) != oldValue);

            return oldValue + 1;
        }
#else
        [NonVersionable]
        [Intrinsic]
        public static long Increment(ref long location)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Increment(ref location);
        }
#endif

        #endregion

        #region Decrement

        [NonVersionable]
        [Intrinsic]
        public static int Decrement(ref int location)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Decrement(ref location);
        }

#if X86
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Decrement(ref long location)
        {
            long oldValue;

            do
            {
                oldValue = location;
            } while (CompareExchange(ref location, oldValue - 1, oldValue) != oldValue);

            return oldValue - 1;
        }
#else
        [NonVersionable]
        [Intrinsic]
        public static long Decrement(ref long location)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Decrement(ref location);
        }
#endif

        #endregion

        #region Add

        [NonVersionable]
        [Intrinsic]
        public static int Add(ref int location1, int value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Add(ref location1, value);
        }

#if X86
        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Add(ref long location1, long value)
        {
            long oldValue;

            do
            {
                oldValue = location1;
            } while (CompareExchange(ref location1, oldValue + value, oldValue) != oldValue);

            return oldValue + value;
        }
#else
        [NonVersionable]
        [Intrinsic]
        public static long Add(ref long location1, long value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Add(ref location1, value);
        }
#endif

        #endregion

        #region MemoryBarrier
        [NonVersionable]
        [Intrinsic]
        public static void MemoryBarrier()
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            MemoryBarrier();
        }
        #endregion

        #region Read
        [NonVersionable]
        public static long Read(ref long location)
        {
            return Interlocked.CompareExchange(ref location, 0, 0);
        }
        #endregion
    }
}
