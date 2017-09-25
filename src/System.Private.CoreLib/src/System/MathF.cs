// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    public static class MathF
    {
        private static float singleRoundLimit = 1e8f;

        private const int maxRoundingDigits = 6;

        // This table is required for the Round function which can specify the number of digits to round to
        private static float[] roundPower10Single = new float[] {
            1e0f, 1e1f, 1e2f, 1e3f, 1e4f, 1e5f, 1e6f
        };

        public const float PI = 3.14159265f;

        public const float E = 2.71828183f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Abs(float x)
        {
            return Math.Abs(x);
        }

        [Intrinsic]
        public static float Acos(float x)
        {
            return RuntimeImports.acosf(x);
        }

        [Intrinsic]
        public static float Asin(float x)
        {
            return RuntimeImports.asinf(x);
        }

        [Intrinsic]
        public static float Atan(float x)
        {
            return RuntimeImports.atanf(x);
        }

        [Intrinsic]
        public static float Atan2(float y, float x)
        {
            return RuntimeImports.atan2f(y, x);
        }

        [Intrinsic]
        public static float Cos(float x)
        {
            return RuntimeImports.cosf(x);
        }

        [Intrinsic]
        public static float Ceiling(float x)
        {
            return RuntimeImports.ceilf(x);
        }

        [Intrinsic]
        public static float Cosh(float x)
        {
            return RuntimeImports.coshf(x);
        }

        [Intrinsic]
        public static float Exp(float x)
        {
            return RuntimeImports.expf(x);
        }

        [Intrinsic]
        public static float Floor(float x)
        {
            return RuntimeImports.floorf(x);
        }

        [Intrinsic]
        public static float Log(float x)
        {
            return RuntimeImports.logf(x);
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

        [Intrinsic]
        public static float Log10(float x)
        {
            return RuntimeImports.log10f(x);
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

        [Intrinsic]
        public static float Pow(float x, float y)
        {
            return RuntimeImports.powf(x, y);
        }

        [Intrinsic]
        public static float Round(float x)
        {
            // If the number has no fractional part do nothing
            // This shortcut is necessary to workaround precision loss in borderline cases on some platforms
            if (x == (float)((int)x))
            {
                return x;
            }

            // We had a number that was equally close to 2 integers.
            // We need to return the even one.

            float flrTempVal = Floor(x + 0.5f);

            if ((x == (Floor(x) + 0.5f)) && (RuntimeImports.fmodf(flrTempVal, 2.0f) != 0))
            {
                flrTempVal -= 1.0f;
            }

            return CopySign(flrTempVal, x);
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

        public static float IEEERemainder(float x, float y)
        {
            if (float.IsNaN(x))
            {
                return x; // IEEE 754-2008: NaN payload must be preserved
            }
 
            if (float.IsNaN(y))
            {
                return y; // IEEE 754-2008: NaN payload must be preserved
            }
 
            var regularMod = x % y;
 
            if (float.IsNaN(regularMod))
            {
                return float.NaN;
            }
 
            if ((regularMod == 0) && float.IsNegative(x))
            {
                return float.NegativeZero;
            }
 
            var alternativeResult = (regularMod - (Abs(y) * Sign(x)));
 
            if (Abs(alternativeResult) == Abs(regularMod))
            {
                var divisionResult = x / y;
                var roundedResult = Round(divisionResult);
 
                if (Abs(roundedResult) > Abs(divisionResult))
                {
                    return alternativeResult;
                }
                else
                {
                    return regularMod;
                }
            }
 
            if (Abs(alternativeResult) < Abs(regularMod))
            {
                return alternativeResult;
            }
            else
            {
                return regularMod;
            }
        }

        [Intrinsic]
        public static float Sin(float x)
        {
            return RuntimeImports.sinf(x);
        }

        [Intrinsic]
        public static float Sqrt(float x)
        {
            return RuntimeImports.sqrtf(x);
        }

        [Intrinsic]
        public static float Tan(float x)
        {
            return RuntimeImports.tanf(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(float x)
        {
            return Math.Sign(x);
        }

        [Intrinsic]
        public static float Sinh(float x)
        {
            return RuntimeImports.sinhf(x);
        }

        [Intrinsic]
        public static float Tanh(float x)
        {
            return RuntimeImports.tanhf(x);
        }

        public static unsafe float Truncate(float x)
        {
            RuntimeImports.modff(x, &x);
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
                    var fraction = RuntimeImports.modff(x, &x);

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
        
        private static unsafe float CopySign(float x, float y)
        {
            var xbits = BitConverter.SingleToInt32Bits(x);
            var ybits = BitConverter.SingleToInt32Bits(y);

            // If the sign bits of x and y are not the same,
            // flip the sign bit of x and return the new value;
            // otherwise, just return x

            if (((xbits ^ ybits) >> 31) != 0)
            {
                return BitConverter.Int32BitsToSingle(xbits ^ int.MinValue);
            }

            return x;
        }
    }
}
