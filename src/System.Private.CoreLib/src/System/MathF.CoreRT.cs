// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
** Purpose: Some single-precision floating-point math operations
**
===========================================================*/

//This class contains only static members and doesn't require serialization.

using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    public static partial class MathF
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Acos(float x)
        {
            return (float)Math.Acos(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Asin(float x)
        {
            return (float)Math.Asin(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan(float x)
        {
            return (float)Math.Atan(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan2(float y, float x)
        {
            return (float)Math.Atan2(y, x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceiling(float x)
        {
            return (float)Math.Ceiling(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float x)
        {
            return (float)Math.Cos(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cosh(float x)
        {
            return (float)Math.Cosh(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float x)
        {
            return (float)Math.Exp(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float x)
        {
            return (float)Math.Floor(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log(float x)
        {
            return (float)Math.Log(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log10(float x)
        {
            return (float)Math.Log10(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow(float x, float y)
        {
            return (float)Math.Pow(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float x)
        {
            return (float)Math.Sin(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sinh(float x)
        {
            return (float)Math.Sinh(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sqrt(float x)
        {
            return (float)Math.Sqrt(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tan(float x)
        {
            return (float)Math.Tan(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tanh(float x)
        {
            return (float)Math.Tanh(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FMod(float x, float y)
        {
            return (float)RuntimeImports.fmod(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float ModF(float x, float* intptr)
        {
            //todo : https://github.com/dotnet/corert/issues/3167
            double d = x;
            double r = RuntimeImports.modf(d, &d);

            *intptr = (float)d;
            return (float)r;
        }
    }
}
