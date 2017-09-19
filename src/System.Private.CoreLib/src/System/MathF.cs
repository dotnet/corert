// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    public static class MathF
    {
        public const float PI = (float)Math.PI;

        public const float E = 2.71828183f;

        private static float singleRoundLimit = 1e8f;

        private const int maxRoundingDigits = 6;

        // This table is required for the Round function which can specify the number of digits to round to
        private static float[] roundPower10Single = new float[] {
            1e0f, 1e1f, 1e2f, 1e3f, 1e4f, 1e5f, 1e6f
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Abs(float x)
        {
            return Math.Abs(x);
        }

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
        public static float Cos(float x)
        {
            return (float)Math.Cos(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceiling(float x)
        {
            return (float)Math.Ceiling(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cosh(float x) { return (float)Math.Cosh(x); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float x) { return (float)Math.Exp(x); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float x) { return (float)Math.Floor(x); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log(float x)
        {
            return (float)Math.Log(x);
        }
        public static float Log(float x, float y)
        {
            if (float.IsNaN(x))
            {
                return x; // IEEE 754-2008: NaN payload must be preserved
            }

            if (float.IsNaN(y))
            {
                return y; // IEEE 754-2008: NaN payload must be preserved
            }

            if (y == 1)
            {
                return float.NaN;
            }

            if ((x != 1) && ((y == 0) || float.IsPositiveInfinity(y)))
            {
                return float.NaN;
            }

            return Log(x) / Log(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log10(float x)
        {
            return (float)Math.Log10(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Max(float x, float y)
        {
            return Math.Max(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Min(float x, float y)
        {
            return Math.Min(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow(float x, float y)
        {
            return (float)Math.Pow(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float x)
        {
            return (float)Math.Round(x);
        }

        public static float Round(float x, int digits)
        {
            if ((digits < 0) || (digits > maxRoundingDigits))
            {
                throw new ArgumentOutOfRangeException(nameof(digits), SR.ArgumentOutOfRange_RoundingDigits);
            }

            return InternalRound(x, digits, MidpointRounding.ToEven);
        }

        public static float Round(float x, int digits, MidpointRounding mode)
        {
            if ((digits < 0) || (digits > maxRoundingDigits))
            {
                throw new ArgumentOutOfRangeException(nameof(digits), SR.ArgumentOutOfRange_RoundingDigits);
            }

            if (mode < MidpointRounding.ToEven || mode > MidpointRounding.AwayFromZero)
            {
                throw new ArgumentException(SR.Format(SR.Argument_InvalidEnum, mode, nameof(MidpointRounding)), nameof(mode));
            }

            return InternalRound(x, digits, mode);
        }

        public static float Round(float x, MidpointRounding mode)
        {
            if (mode < MidpointRounding.ToEven || mode > MidpointRounding.AwayFromZero)
            {
                throw new ArgumentException(SR.Format(SR.Argument_InvalidEnum, mode, nameof(MidpointRounding)), nameof(mode));
            }

            return InternalRound(x, 0, mode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float IEEERemainder(float x, float y)
        {
            return (float)Math.IEEERemainder(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float x)
        {
            return (float)Math.Sin(x);
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
        public static int Sign(float x)
        {
            return Math.Sign(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sinh(float x)
        {
            return (float)Math.Sinh(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tanh(float x)
        {
            return (float)Math.Tanh(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Truncate(float x) => InternalTruncate(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float SplitFractionSingle(float* x)
        {
            //todo : https://github.com/dotnet/corert/issues/3167
            double d = *x;
            double r = RuntimeImports.modf(d, &d);

            *x = (float)d;
            return (float)r;
        }

        private unsafe static float InternalTruncate(float x)
        {
            SplitFractionSingle(&x);
            return x;
        }

        private static unsafe float InternalRound(float x, int digits, MidpointRounding mode)
        {
            if (Abs(x) < singleRoundLimit)
            {
                var power10 = roundPower10Single[digits];

                x *= power10;

                if (mode == MidpointRounding.AwayFromZero)
                {
                    var fraction = SplitFractionSingle(&x);

                    if (Abs(fraction) >= 0.5f)
                    {
                        x += Sign(fraction);
                    }
                }
                else
                {
                    x = Round(x);
                }

                x /= power10;
            }

            return x;
        }
    }
}
