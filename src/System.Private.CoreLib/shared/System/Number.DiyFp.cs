// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Numerics;
using Internal.NativeFormat;

namespace System
{
    internal static partial class Number
    {
        // This is a port of the `DiyFp` implementation here: https://github.com/google/double-conversion/blob/a711666ddd063eb1e4b181a6cb981d39a1fc8bac/double-conversion/diy-fp.h
        // The backing structure and how it is used is described in more detail here: http://www.cs.tufts.edu/~nr/cs257/archive/florian-loitsch/printf.pdf

        // This "Do It Yourself Floating Point" class implements a floating-point number with a ulong significand and an int exponent.
        // Normalized DiyFp numbers will have the most significant bit of the significand set.
        // Multiplication and Subtraction do not normalize their results.
        // DiyFp are not designed to contain special doubles (NaN and Infinity).
        internal readonly ref struct DiyFp
        {
            public const int DoubleImplicitBitIndex = 52;
            public const int SingleImplicitBitIndex = 23;

            public const int SignificandSize = 64;

            public readonly ulong f;
            public readonly int e;

            // Computes the two boundaries of value.
            //
            // The bigger boundary (mPlus) is normalized.
            // The lower boundary has the same exponent as mPlus.
            //
            // Precondition:
            //  The value encoded by value must be greater than 0.
            public static DiyFp CreateAndGetBoundaries(double value, out DiyFp mMinus, out DiyFp mPlus)
            {
                var result = new DiyFp(value);
                result.GetBoundaries(DoubleImplicitBitIndex, out mMinus, out mPlus);
                return result;
            }

            // Computes the two boundaries of value.
            //
            // The bigger boundary (mPlus) is normalized.
            // The lower boundary has the same exponent as mPlus.
            //
            // Precondition:
            //  The value encoded by value must be greater than 0.
            public static DiyFp CreateAndGetBoundaries(float value, out DiyFp mMinus, out DiyFp mPlus)
            {
                var result = new DiyFp(value);
                result.GetBoundaries(SingleImplicitBitIndex, out mMinus, out mPlus);
                return result;
            }

            public DiyFp(double value)
            {
                Debug.Assert(double.IsFinite(value));
                Debug.Assert(value > 0.0);
                f = ExtractFractionAndBiasedExponent(value, out e);
            }

            public DiyFp(float value)
            {
                Debug.Assert(float.IsFinite(value));
                Debug.Assert(value > 0.0f);
                f = ExtractFractionAndBiasedExponent(value, out e);
            }

            public DiyFp(ulong f, int e)
            {
                this.f = f;
                this.e = e;
            }

            public DiyFp Multiply(in DiyFp other)
            {
                // Simply "emulates" a 128-bit multiplication
                //
                // However: the resulting number only contains 64-bits. The least
                // signficant 64-bits are only used for rounding the most significant
                // 64-bits.

                X2.PrintLine("Multiply f:");
                X2.PrintLine(f.ToString());

                uint a = (uint)(f >> 32);
                X2.PrintLine("Multiply a:");
                X2.PrintLine(a.ToString());
                uint b = (uint)(f);
                X2.PrintLine("Multiply b:");
                X2.PrintLine(b.ToString());

                uint c = (uint)(other.f >> 32);
                X2.PrintLine("Multiply c:");
                X2.PrintLine(c.ToString());
                uint d = (uint)(other.f);
                X2.PrintLine("Multiply d:");
                X2.PrintLine(d.ToString());

                ulong ac = ((ulong)(a) * c);
                X2.PrintLine("Multiply ac:");
                X2.PrintLine(ac.ToString());
                ulong bc = ((ulong)(b) * c);
                X2.PrintLine("Multiply bc:");
                X2.PrintLine(bc.ToString());
                ulong ad = ((ulong)(a) * d);
                X2.PrintLine("Multiply ad:");
                X2.PrintLine(ad.ToString());
                ulong bd = ((ulong)(b) * d);
                X2.PrintLine("Multiply bd:");
                X2.PrintLine(bd.ToString());

                ulong tmp = (bd >> 32) + (uint)(ad) + (uint)(bc);
                X2.PrintLine("Multiply tmp:");
                X2.PrintLine(tmp.ToString());

                // By adding (1UL << 31) to tmp, we round the final result.
                // Halfway cases will be rounded up.

                tmp += (1U << 31);
                X2.PrintLine("Multiply tmp += 1 <<31:");
                X2.PrintLine(tmp.ToString());
                ulong ul = ac + (ad >> 32) + (bc >> 32) + (tmp >> 32);
                X2.PrintLine("Multiply ul:");
                X2.PrintLine(ul.ToString());

                return new DiyFp(ac + (ad >> 32) + (bc >> 32) + (tmp >> 32), e + other.e + SignificandSize);
            }

            public DiyFp Normalize()
            {
                // This method is mainly called for normalizing boundaries.
                //
                // We deviate from the reference implementation by just using
                // our LeadingZeroCount function so that we only need to shift
                // and subtract once.

                Debug.Assert(f != 0);
                int lzcnt = BitOperations.LeadingZeroCount(f);
                return new DiyFp(f << lzcnt, e - lzcnt);
            }

            // The exponents of both numbers must be the same.
            // The significand of 'this' must be bigger than the significand of 'other'.
            // The result will not be normalized.
            public DiyFp Subtract(in DiyFp other)
            {
                Debug.Assert(e == other.e);
                Debug.Assert(f >= other.f);
                return new DiyFp(f - other.f, e);
            }

            private void GetBoundaries(int implicitBitIndex, out DiyFp mMinus, out DiyFp mPlus)
            {
                X2.PrintLine("GetBoundaries f");
                X2.PrintLine(f.ToString());
                X2.PrintLine("GetBoundaries e");
                X2.PrintUint(e);
                mPlus = new DiyFp((f << 1) + 1, e - 1).Normalize();

                // The boundary is closer if the sigificand is of the form:
                //      f == 2^p-1
                //
                // Think of v = 1000e10 and v- = 9999e9
                // Then the boundary == (v - v-) / 2 is not just at a distance of 1e9 but at a distance of 1e8.
                // The only exception is for the smallest normal, where the largest denormal is at the same distance as its successor.
                //
                // Note: denormals have the same exponent as the smallest normals.

                // We deviate from the reference implementation by just checking if the significand has only the implicit bit set.
                // In this scenario, we know that all the explicit bits are 0 and that the unbiased exponent is non-zero.
                X2.PrintLine("GetBoundaries mplus e");
                X2.PrintUint(mPlus.e);
                if (f == (1UL << implicitBitIndex))
                {
                    X2.PrintLine("GetBoundaries 1UL << implicitBitIndex");

                    mMinus = new DiyFp((f << 2) - 1, e - 2);
                }
                else
                {
                    X2.PrintLine("GetBoundaries else 1UL << implicitBitIndex");

                    mMinus = new DiyFp((f << 1) - 1, e - 1);
                }

                X2.PrintLine("GetBoundaries");
                X2.PrintLine(mMinus.f.ToString());
                X2.PrintUint(mMinus.e);
                X2.PrintUint(mPlus.e);
                mMinus = new DiyFp(mMinus.f << (mMinus.e - mPlus.e), mPlus.e);
            }
        }
    }
}
