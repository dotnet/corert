// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace System.Threading
{
    public static class Interlocked
    {
        #region CompareExchange

        [Intrinsic]
        public static int CompareExchange(ref int location1, int value, int comparand)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            if (oldValue == comparand)
                location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
#endif
        }

        [Intrinsic]
        internal static uint CompareExchange(ref uint location1, uint value, uint comparand)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            if (oldValue == comparand)
                location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
#endif
        }

        [Intrinsic]
        public static long CompareExchange(ref long location1, long value, long comparand)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            if (oldValue == comparand)
                location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
#endif
        }

        [Intrinsic]
        public static IntPtr CompareExchange(ref IntPtr location1, IntPtr value, IntPtr comparand)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            if (oldValue == comparand)
                location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
#endif
        }

        [Intrinsic]
        public static float CompareExchange(ref float location1, float value, float comparand)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            if (oldValue == comparand)
                location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
#endif
        }

        [Intrinsic]
        public static double CompareExchange(ref double location1, double value, double comparand)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            if (oldValue == comparand)
                location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
#endif
        }

        [Intrinsic]
        public static T CompareExchange<T>(ref T location1, T value, T comparand) where T : class
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            if (oldValue == comparand)
                location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange<T>(ref location1, value, comparand);
#endif
        }

        public static object CompareExchange(ref object location1, object value, object comparand)
        {
            return CompareExchange<object>(ref location1, value, comparand);
        }

        [Intrinsic]
        internal static extern T CompareExchange<T>(IntPtr location1, T value, T comparand) where T : class;

#endregion

#region Exchange

        [Intrinsic]
        public static int Exchange(ref int location1, int value)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
#endif
        }

#if X86
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
        [Intrinsic]
        public static long Exchange(ref long location1, long value)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
#endif
        }
#endif

        [Intrinsic]
        public static IntPtr Exchange(ref IntPtr location1, IntPtr value)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
#endif
        }

        [Intrinsic]
        public static float Exchange(ref float location1, float value)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
#endif
        }

#if X86
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
        [Intrinsic]
        public static double Exchange(ref double location1, double value)
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
#endif
        }
#endif

        [Intrinsic]
        public static T Exchange<T>(ref T location1, T value) where T : class
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
            var oldValue = location1;
            location1 = value;
            return oldValue;
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange<T>(ref location1, value);
#endif
        }

        [Intrinsic]
        internal static extern T Exchange<T>(IntPtr location1, T value) where T : class;

        public static object Exchange(ref object location1, object value)
        {
            return Exchange<object>(ref location1, value);
        }

        #endregion

        #region Increment

        [Intrinsic]
        public static int Increment(ref int location)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Increment(ref location);
        }

#if X86
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

        [Intrinsic]
        public static int Decrement(ref int location)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Decrement(ref location);
        }

#if X86
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

        [Intrinsic]
        public static int Add(ref int location1, int value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Add(ref location1, value);
        }

#if X86
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
        [Intrinsic]
        public static void MemoryBarrier()
        {
#if CORERT
            // CORERT-TODO: Implement interlocked intrinsics
#else
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            MemoryBarrier();
#endif
        }
        #endregion

        #region Read
        public static long Read(ref long location)
        {
            return Interlocked.CompareExchange(ref location, 0, 0);
        }
        #endregion
    }
}
