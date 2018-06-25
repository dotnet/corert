// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

using Internal.Runtime.CompilerServices;

namespace System.Threading
{
    public static class Interlocked
    {
#if PROJECTN

        #region CompareExchange

        [Intrinsic]
        public static int CompareExchange(ref int location1, int value, int comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static long CompareExchange(ref long location1, long value, long comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static IntPtr CompareExchange(ref IntPtr location1, IntPtr value, IntPtr comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static float CompareExchange(ref float location1, float value, float comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static double CompareExchange(ref double location1, double value, double comparand)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static T CompareExchange<T>(ref T location1, T value, T comparand) where T : class
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return CompareExchange<T>(ref location1, value, comparand);
        }

        public static object CompareExchange(ref object location1, object value, object comparand)
        {
            return CompareExchange<object>(ref location1, value, comparand);
        }

        #endregion

        #region Exchange

        [Intrinsic]
        public static int Exchange(ref int location1, int value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
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
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
        }
#endif

        [Intrinsic]
        public static IntPtr Exchange(ref IntPtr location1, IntPtr value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
        }

        [Intrinsic]
        public static float Exchange(ref float location1, float value)
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
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
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange(ref location1, value);
        }
#endif

        [Intrinsic]
        public static T Exchange<T>(ref T location1, T value) where T : class
        {
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            return Exchange<T>(ref location1, value);
        }

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
            // This is actually an intrinsic and not a recursive function call.
            // We have it here so that you can do "ldftn" on the method or reflection invoke it.
            MemoryBarrier();
        }
        #endregion

        #region Read
        public static long Read(ref long location)
        {
            return Interlocked.CompareExchange(ref location, 0, 0);
        }
        #endregion

#else // PROJECTN

        #region CompareExchange

        [Intrinsic]
        public static int CompareExchange(ref int location1, int value, int comparand)
        {
            return RuntimeImports.InterlockedCompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static long CompareExchange(ref long location1, long value, long comparand)
        {
            return RuntimeImports.InterlockedCompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static IntPtr CompareExchange(ref IntPtr location1, IntPtr value, IntPtr comparand)
        {
            return RuntimeImports.InterlockedCompareExchange(ref location1, value, comparand);
        }

        [Intrinsic]
        public static unsafe float CompareExchange(ref float location1, float value, float comparand)
        {
            float ret;
            *(int*)&ret = CompareExchange(ref Unsafe.As<float, int>(ref location1), *(int*)&value, *(int*)&comparand);
            return ret;
        }

        [Intrinsic]
        public static unsafe double CompareExchange(ref double location1, double value, double comparand)
        {
            double ret;
            *(long*)&ret = CompareExchange(ref Unsafe.As<double, long>(ref location1), *(long*)&value, *(long*)&comparand);
            return ret;
        }

        [Intrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CompareExchange<T>(ref T location1, T value, T comparand) where T : class
        {
            return Unsafe.As<T>(RuntimeImports.InterlockedCompareExchange(ref Unsafe.As<T, object>(ref location1), value, comparand));
        }

        [Intrinsic]
        public static object CompareExchange(ref object location1, object value, object comparand)
        {
            return RuntimeImports.InterlockedCompareExchange(ref location1, value, comparand);
        }

        #endregion

        #region Exchange

        [Intrinsic]
        public static int Exchange(ref int location1, int value)
        {
            int oldValue;

            do
            {
                oldValue = location1;
            } while (CompareExchange(ref location1, value, oldValue) != oldValue);

            return oldValue;
        }

        [Intrinsic]
        public static long Exchange(ref long location1, long value)
        {
            long oldValue;

            do
            {
                oldValue = location1;
            } while (CompareExchange(ref location1, value, oldValue) != oldValue);

            return oldValue;
        }

        [Intrinsic]
        public static IntPtr Exchange(ref IntPtr location1, IntPtr value)
        {
            IntPtr oldValue;

            do
            {
                oldValue = location1;
            } while (CompareExchange(ref location1, value, oldValue) != oldValue);

            return oldValue;
        }

        [Intrinsic]
        public static unsafe float Exchange(ref float location1, float value)
        {
            float ret;
            *(int*)&ret = Exchange(ref Unsafe.As<float, int>(ref location1), *(int*)&value);
            return ret;
        }

        [Intrinsic]
        public static unsafe double Exchange(ref double location1, double value)
        {
            double ret;
            *(long*)&ret = Exchange(ref Unsafe.As<double, long>(ref location1), *(long*)&value);
            return ret;
        }

        [Intrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Exchange<T>(ref T location1, T value) where T : class
        {
            return Unsafe.As<T>(RuntimeImports.InterlockedExchange(ref Unsafe.As<T, object>(ref location1), value));
        }

        [Intrinsic]
        public static object Exchange(ref object location1, object value)
        {
            return RuntimeImports.InterlockedExchange(ref location1, value);
        }

        #endregion

        #region Increment

        [Intrinsic]
        public static int Increment(ref int location)
        {
            return ExchangeAdd(ref location, 1) + 1;
        }

        [Intrinsic]
        public static long Increment(ref long location)
        {
            return ExchangeAdd(ref location, 1) + 1;
        }

        #endregion

        #region Decrement

        [Intrinsic]
        public static int Decrement(ref int location)
        {
            return ExchangeAdd(ref location, -1) - 1;
        }

        [Intrinsic]
        public static long Decrement(ref long location)
        {
            return ExchangeAdd(ref location, -1) - 1;
        }

        #endregion

        #region Add

        [Intrinsic]
        public static int Add(ref int location1, int value)
        {
            return ExchangeAdd(ref location1, value) + value;
        }

        [Intrinsic]
        public static long Add(ref long location1, long value)
        {
            return ExchangeAdd(ref location1, value) + value;
        }

        [Intrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ExchangeAdd(ref int location1, int value)
        {
            int oldValue;

            do
            {
                oldValue = location1;
            } while (CompareExchange(ref location1, oldValue + value, oldValue) != oldValue);

            return oldValue;
        }

        [Intrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ExchangeAdd(ref long location1, long value)
        {
            long oldValue;

            do
            {
                oldValue = location1;
            } while (CompareExchange(ref location1, oldValue + value, oldValue) != oldValue);

            return oldValue;
        }

        #endregion

        #region MemoryBarrier
        [Intrinsic]
        public static void MemoryBarrier()
        {
            RuntimeImports.MemoryBarrier();
        }
        #endregion

        #region Read
        public static long Read(ref long location)
        {
            return CompareExchange(ref location, 0, 0);
        }
        #endregion

#endif // PROJECTN

        public static void MemoryBarrierProcessWide()
        {
            RuntimeImports.RhFlushProcessWriteBuffers();
        }
    }
}
