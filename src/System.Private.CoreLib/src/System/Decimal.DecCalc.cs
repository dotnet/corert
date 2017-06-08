// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;

namespace System
{
    public partial struct Decimal
    {
        // Low level accessors used by a DecCalc and formatting 
        internal uint High
        {
            get { return uhi; }
            set { uhi = value; }
        }

        internal uint Low
        {
            get { return ulo; }
            set { ulo = value; }
        }

        internal uint Mid
        {
            get { return umid; }
            set { umid = value; }
        }

        internal bool Sign
        {
            get { return (uflags & SignMask) != 0; }
            set { uflags = (uflags & ~SignMask) | (value ? SignMask : 0); }
        }

        internal int Scale
        {
            get { return (int)((uflags & ScaleMask) >> ScaleShift); }
            set { uflags = (uflags & ~ScaleMask) | ((uint)value << ScaleShift); }
        }

        private ulong Low64
        {
            get { return ((ulong)umid << 32) | ulo; }
            set { umid = (uint)(value >> 32); ulo = (uint)value; }
        }

        #region APIs need by number formatting.

        internal static uint DecDivMod1E9(ref Decimal value)
        {
            return DecCalc.DecDivMod1E9(ref value);
        }

        internal static void DecAddInt32(ref Decimal value, uint i)
        {
            DecCalc.DecAddInt32(ref value, i);
        }

        internal static void DecMul10(ref Decimal value)
        {
            DecCalc.DecMul10(ref value);
        }

        #endregion

        /// <summary>
        /// Class that contains all the mathematical calculations for decimal. Most of which have been ported from oleaut32.
        /// </summary>
        private class DecCalc
        {
            // Constant representing the negative number that is the closest possible
            // Decimal value to -0m.
            private const Decimal NearNegativeZero = -0.000000000000000000000000001m;

            // Constant representing the positive number that is the closest possible
            // Decimal value to +0m.
            private const Decimal NearPositiveZero = +0.000000000000000000000000001m;

            private const int DEC_SCALE_MAX = 28;

            private const uint OVFL_MAX_9_HI = 4;
            private const uint OVFL_MAX_9_MID = 1266874889;
            private const uint OVFL_MAX_9_LO = 3047500985;
            private const uint OVFL_MAX_5_HI = 42949;
            private const uint OVFL_MAX_1_HI = 429496729;

            private const uint SNGBIAS = 126;
            private const uint DBLBIAS = 1022;

            private const uint TenToPowerNine = 1000000000;
            private const uint TenToPowerTenDiv4 = 2500000000;
            private static readonly Split64 s_tenToPowerEighteen = new Split64() { int64 = 1000000000000000000 };

            // The maximum power of 10 that a 32 bit integer can store
            private const Int32 MaxInt32Scale = 9;

            // Fast access for 10^n where n is 0-9        
            private static UInt32[] s_powers10 = new UInt32[] {
                1,
                10,
                100,
                1000,
                10000,
                100000,
                1000000,
                10000000,
                100000000,
                1000000000
            };

            private static double[] s_doublePowers10 = new double[] {
                1, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9,
                1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19,
                1e20, 1e21, 1e22, 1e23, 1e24, 1e25, 1e26, 1e27, 1e28, 1e29,
                1e30, 1e31, 1e32, 1e33, 1e34, 1e35, 1e36, 1e37, 1e38, 1e39,
                1e40, 1e41, 1e42, 1e43, 1e44, 1e45, 1e46, 1e47, 1e48, 1e49,
                1e50, 1e51, 1e52, 1e53, 1e54, 1e55, 1e56, 1e57, 1e58, 1e59,
                1e60, 1e61, 1e62, 1e63, 1e64, 1e65, 1e66, 1e67, 1e68, 1e69,
                1e70, 1e71, 1e72, 1e73, 1e74, 1e75, 1e76, 1e77, 1e78, 1e79,
                1e80
            };

            // Value taken via reverse engineering the double that corrisponds to 2^65. (oleaut32 has ds2to64 = DEFDS(0, 0, DBLBIAS + 65, 0))
            private const double ds2to64 = 1.8446744073709552e+019;

            #region Decimal Math Helpers

            private static unsafe uint GetExponent(float f)
            {
                // Based on pulling out the exp from this single struct layout
                //typedef struct {
                //    ULONG mant:23;
                //    ULONG exp:8;
                //    ULONG sign:1;
                //} SNGSTRUCT;

                uint* pf = (uint*)&f;
                return (*pf >> 23) & 0xFFu;
            }

            private static unsafe uint GetExponent(double d)
            {
                // Based on pulling out the exp from this double struct layout
                //typedef struct {
                //   DWORDLONG mant:52;
                //   DWORDLONG signexp:12;
                // } DBLSTRUCT;

                ulong* pd = (ulong*)&d;
                return (uint)(*pd >> 52) & 0x7FFu;
            }

            // Use table but enable for computation if necessary
            private static double GetDoublePower10(int ix)
            {
                if (ix >= 0 && ix < s_doublePowers10.Length)
                    return s_doublePowers10[ix];
                return Math.Pow(10, ix);
            }

            private static ulong DivMod64by32(ulong num, uint den)
            {
                Split64 sdl = new Split64();

                sdl.Low32 = (uint)(num / den);
                sdl.High32 = (uint)(num % den);
                return sdl.int64;
            }

            private static ulong DivMod32by32(uint num, uint den)
            {
                Split64 sdl = new Split64();

                sdl.Low32 = num / den;
                sdl.High32 = num % den;
                return sdl.int64;
            }

            private static uint FullDiv64By32(ref ulong pdlNum, uint ulDen)
            {
                Split64 sdlTmp = new Split64();
                Split64 sdlRes = new Split64();

                sdlTmp.int64 = pdlNum;
                sdlRes.High32 = 0;

                if (sdlTmp.High32 >= ulDen)
                {
                    // DivMod64by32 returns quotient in Lo, remainder in Hi.
                    //
                    sdlRes.Low32 = sdlTmp.High32;
                    sdlRes.int64 = DivMod64by32(sdlRes.int64, ulDen);
                    sdlTmp.High32 = sdlRes.High32;
                    sdlRes.High32 = sdlRes.Low32;
                }

                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, ulDen);
                sdlRes.Low32 = sdlTmp.Low32;
                pdlNum = sdlRes.int64;
                return sdlTmp.High32;
            }

            private static ulong UInt32x32To64(uint a, uint b)
            {
                return (ulong)a * (ulong)b;
            }

            private static ulong UInt64x64To128(Split64 sdlOp1, Split64 sdlOp2, out ulong dlHi)
            {
                Split64 sdlTmp1 = new Split64();
                Split64 sdlTmp2 = new Split64();
                Split64 sdlTmp3 = new Split64();

                sdlTmp1.int64 = UInt32x32To64(sdlOp1.Low32, sdlOp2.Low32); // lo partial prod
                sdlTmp2.int64 = UInt32x32To64(sdlOp1.Low32, sdlOp2.High32); // mid 1 partial prod
                sdlTmp1.High32 += sdlTmp2.Low32;
                if (sdlTmp1.High32 < sdlTmp2.Low32)  // test for carry
                    sdlTmp2.High32++;
                sdlTmp3.int64 = UInt32x32To64(sdlOp1.High32, sdlOp2.High32) + sdlTmp2.High32;
                sdlTmp2.int64 = UInt32x32To64(sdlOp1.High32, sdlOp2.Low32);
                sdlTmp1.High32 += sdlTmp2.Low32;
                if (sdlTmp1.High32 < sdlTmp2.Low32)  // test for carry
                    sdlTmp2.High32++;
                sdlTmp3.int64 += sdlTmp2.High32;

                dlHi = sdlTmp3.int64;
                return sdlTmp1.int64;
            }

            /***
             * Div96By32
             *
             * Entry:
             *   rgulNum - Pointer to 96-bit dividend as array of ULONGs, least-sig first
             *   ulDen   - 32-bit divisor.
             *
             * Purpose:
             *   Do full divide, yielding 96-bit result and 32-bit remainder.
             *
             * Exit:
             *   Quotient overwrites dividend.
             *   Returns remainder.
             *
             * Exceptions:
             *   None.
             *
             ***********************************************************************/
            private static uint Div96By32(uint[] rgulNum, uint ulDen)
            {
                Split64 sdlTmp = new Split64();

                sdlTmp.High32 = 0;

                if (rgulNum[2] != 0)
                    goto Div3Word;

                if (rgulNum[1] >= ulDen)
                    goto Div2Word;

                sdlTmp.High32 = rgulNum[1];
                rgulNum[1] = 0;
                goto Div1Word;

            Div3Word:
                sdlTmp.Low32 = rgulNum[2];
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, ulDen);
                rgulNum[2] = sdlTmp.Low32;
            Div2Word:
                sdlTmp.Low32 = rgulNum[1];
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, ulDen);
                rgulNum[1] = sdlTmp.Low32;
            Div1Word:
                sdlTmp.Low32 = rgulNum[0];
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, ulDen);
                rgulNum[0] = sdlTmp.Low32;

                return sdlTmp.High32;
            }

            /***
             * Div96By64
             *
             * Entry:
             *   rgulNum - Pointer to 96-bit dividend as array of ULONGs, least-sig first
             *   sdlDen  - 64-bit divisor.
             *
             * Purpose:
             *   Do partial divide, yielding 32-bit result and 64-bit remainder.
             *   Divisor must be larger than upper 64 bits of dividend.
             *
             * Exit:
             *   Remainder overwrites lower 64-bits of dividend.
             *   Returns quotient.
             *
             * Exceptions:
             *   None.
             *
             ***********************************************************************/
            private static uint Div96By64(System.Collections.Generic.IList<uint> rgulNum, Split64 sdlDen)
            {
                Split64 sdlQuo = new Split64();
                Split64 sdlNum = new Split64();
                Split64 sdlProd = new Split64();

                sdlNum.Low32 = rgulNum[0];

                if (rgulNum[2] >= sdlDen.High32)
                {
                    // Divide would overflow.  Assume a quotient of 2^32, and set
                    // up remainder accordingly.
                    //
                    sdlNum.High32 = rgulNum[1] - sdlDen.Low32;
                    sdlQuo.Low32 = 0;

                    // Remainder went negative.  Add divisor back in until it's positive,
                    // a max of 2 times.
                    //
                    do
                    {
                        sdlQuo.Low32--;
                        sdlNum.int64 += sdlDen.int64;
                    } while (sdlNum.int64 >= sdlDen.int64);

                    goto Done;
                }

                // Hardware divide won't overflow
                //
                if (rgulNum[2] == 0 && rgulNum[1] < sdlDen.High32)
                    // Result is zero.  Entire dividend is remainder.
                    //
                    return 0;

                // DivMod64by32 returns quotient in Lo, remainder in Hi.
                //
                sdlQuo.Low32 = rgulNum[1];
                sdlQuo.High32 = rgulNum[2];
                sdlQuo.int64 = DivMod64by32(sdlQuo.int64, sdlDen.High32);
                sdlNum.High32 = sdlQuo.High32; // remainder

                // Compute full remainder, rem = dividend - (quo * divisor).
                //
                sdlProd.int64 = UInt32x32To64(sdlQuo.Low32, sdlDen.Low32); // quo * lo divisor
                sdlNum.int64 -= sdlProd.int64;

                if (sdlNum.int64 > ~sdlProd.int64)
                {
                    // Remainder went negative.  Add divisor back in until it's positive,
                    // a max of 2 times.
                    //
                    do
                    {
                        sdlQuo.Low32--;
                        sdlNum.int64 += sdlDen.int64;
                    } while (sdlNum.int64 >= sdlDen.int64);
                }

            Done:
                rgulNum[0] = sdlNum.Low32;
                rgulNum[1] = sdlNum.High32;
                return sdlQuo.Low32;
            }

            /***
             * Div128By96
             *
             * Entry:
             *   rgulNum - Pointer to 128-bit dividend as array of ULONGs, least-sig first
             *   rgulDen - Pointer to 96-bit divisor.
             *
             * Purpose:
             *   Do partial divide, yielding 32-bit result and 96-bit remainder.
             *   Top divisor ULONG must be larger than top dividend ULONG.  This is
             *   assured in the initial call because the divisor is normalized
             *   and the dividend can't be.  In subsequent calls, the remainder
             *   is multiplied by 10^9 (max), so it can be no more than 1/4 of
             *   the divisor which is effectively multiplied by 2^32 (4 * 10^9).
             *
             * Exit:
             *   Remainder overwrites lower 96-bits of dividend.
             *   Returns quotient.
             *
             * Exceptions:
             *   None.
             *
             ***********************************************************************/
            private static uint Div128By96(uint[] rgulNum, uint[] rgulDen)
            {
                Split64 sdlQuo = new Split64();
                Split64 sdlNum = new Split64();
                Split64 sdlProd1 = new Split64();
                Split64 sdlProd2 = new Split64();

                sdlNum.Low32 = rgulNum[0];
                sdlNum.High32 = rgulNum[1];

                if (rgulNum[3] == 0 && rgulNum[2] < rgulDen[2])
                    // Result is zero.  Entire dividend is remainder.
                    //
                    return 0;

                // DivMod64by32 returns quotient in Lo, remainder in Hi.
                //
                sdlQuo.Low32 = rgulNum[2];
                sdlQuo.High32 = rgulNum[3];
                sdlQuo.int64 = DivMod64by32(sdlQuo.int64, rgulDen[2]);

                // Compute full remainder, rem = dividend - (quo * divisor).
                //
                sdlProd1.int64 = UInt32x32To64(sdlQuo.Low32, rgulDen[0]); // quo * lo divisor
                sdlProd2.int64 = UInt32x32To64(sdlQuo.Low32, rgulDen[1]); // quo * mid divisor
                sdlProd2.int64 += sdlProd1.High32;
                sdlProd1.High32 = sdlProd2.Low32;

                sdlNum.int64 -= sdlProd1.int64;
                rgulNum[2] = sdlQuo.High32 - sdlProd2.High32; // sdlQuo.Hi is remainder

                // Propagate carries
                //
                bool fallthru = false;
                if (sdlNum.int64 > ~sdlProd1.int64)
                {
                    rgulNum[2]--;
                    if (rgulNum[2] >= ~sdlProd2.High32)
                        fallthru = true;
                }
                if (fallthru || rgulNum[2] > ~sdlProd2.High32)
                {
                    // Remainder went negative.  Add divisor back in until it's positive,
                    // a max of 2 times.
                    //
                    sdlProd1.Low32 = rgulDen[0];
                    sdlProd1.High32 = rgulDen[1];

                    for (;;)
                    {
                        sdlQuo.Low32--;
                        sdlNum.int64 += sdlProd1.int64;
                        rgulNum[2] += rgulDen[2];

                        if (sdlNum.int64 < sdlProd1.int64)
                        {
                            // Detected carry. Check for carry out of top
                            // before adding it in.
                            //
                            if (rgulNum[2]++ < rgulDen[2])
                                break;
                        }
                        if (rgulNum[2] < rgulDen[2])
                            break; // detected carry
                    }
                }

                rgulNum[0] = sdlNum.Low32;
                rgulNum[1] = sdlNum.High32;
                return sdlQuo.Low32;
            }

            /***
             * IncreaseScale
             *
             * Entry:
             *   rgulNum - Pointer to 96-bit number as array of ULONGs, least-sig first
             *   ulPwr   - Scale factor to multiply by
             *
             * Purpose:
             *   Multiply the two numbers.  The low 96 bits of the result overwrite
             *   the input.  The last 32 bits of the product are the return value.
             *
             * Exit:
             *   Returns highest 32 bits of product.
             *
             * Exceptions:
             *   None.
             *
             ***********************************************************************/
            private static uint IncreaseScale(uint[] rgulNum, uint ulPwr)
            {
                Split64 sdlTmp = new Split64();

                sdlTmp.int64 = UInt32x32To64(rgulNum[0], ulPwr);
                rgulNum[0] = sdlTmp.Low32;
                sdlTmp.int64 = UInt32x32To64(rgulNum[1], ulPwr) + sdlTmp.High32;
                rgulNum[1] = sdlTmp.Low32;
                sdlTmp.int64 = UInt32x32To64(rgulNum[2], ulPwr) + sdlTmp.High32;
                rgulNum[2] = sdlTmp.Low32;
                return sdlTmp.High32;
            }

            /***
            * ScaleResult
            *
            * Entry:
            *   rgulRes - Array of ULONGs with value, least-significant first.
            *   iHiRes  - Index of last non-zero value in rgulRes.
            *   iScale  - Scale factor for this value, range 0 - 2 * DEC_SCALE_MAX
            *
            * Purpose:
            *   See if we need to scale the result to fit it in 96 bits.
            *   Perform needed scaling.  Adjust scale factor accordingly.
            *
            * Exit:
            *   rgulRes updated in place, always 3 ULONGs.
            *   New scale factor returned, -1 if overflow error.
            *
            ***********************************************************************/
            private static int ScaleResult(uint[] rgulRes, int iHiRes, int iScale)
            {
                int iNewScale;
                int iCur;
                uint ulPwr;
                uint ulTmp;
                uint ulSticky;
                Split64 sdlTmp = new Split64();

                // See if we need to scale the result.  The combined scale must
                // be <= DEC_SCALE_MAX and the upper 96 bits must be zero.
                // 
                // Start by figuring a lower bound on the scaling needed to make
                // the upper 96 bits zero.  iHiRes is the index into rgulRes[]
                // of the highest non-zero ULONG.
                // 
                iNewScale = iHiRes * 32 - 64 - 1;
                if (iNewScale > 0)
                {
                    // Find the MSB.
                    //
                    ulTmp = rgulRes[iHiRes];
                    if ((ulTmp & 0xFFFF0000) == 0)
                    {
                        iNewScale -= 16;
                        ulTmp <<= 16;
                    }
                    if ((ulTmp & 0xFF000000) == 0)
                    {
                        iNewScale -= 8;
                        ulTmp <<= 8;
                    }
                    if ((ulTmp & 0xF0000000) == 0)
                    {
                        iNewScale -= 4;
                        ulTmp <<= 4;
                    }
                    if ((ulTmp & 0xC0000000) == 0)
                    {
                        iNewScale -= 2;
                        ulTmp <<= 2;
                    }
                    if ((ulTmp & 0x80000000) == 0)
                    {
                        iNewScale--;
                        ulTmp <<= 1;
                    }

                    // Multiply bit position by log10(2) to figure it's power of 10.
                    // We scale the log by 256.  log(2) = .30103, * 256 = 77.  Doing this 
                    // with a multiply saves a 96-byte lookup table.  The power returned
                    // is <= the power of the number, so we must add one power of 10
                    // to make it's integer part zero after dividing by 256.
                    // 
                    // Note: the result of this multiplication by an approximation of
                    // log10(2) have been exhaustively checked to verify it gives the 
                    // correct result.  (There were only 95 to check...)
                    // 
                    iNewScale = ((iNewScale * 77) >> 8) + 1;

                    // iNewScale = min scale factor to make high 96 bits zero, 0 - 29.
                    // This reduces the scale factor of the result.  If it exceeds the
                    // current scale of the result, we'll overflow.
                    // 
                    if (iNewScale > iScale)
                        return -1;
                }
                else
                    iNewScale = 0;

                // Make sure we scale by enough to bring the current scale factor
                // into valid range.
                //
                if (iNewScale < iScale - DEC_SCALE_MAX)
                    iNewScale = iScale - DEC_SCALE_MAX;

                if (iNewScale != 0)
                {
                    // Scale by the power of 10 given by iNewScale.  Note that this is 
                    // NOT guaranteed to bring the number within 96 bits -- it could 
                    // be 1 power of 10 short.
                    //
                    iScale -= iNewScale;
                    ulSticky = 0;
                    sdlTmp.High32 = 0; // initialize remainder

                    for (;;)
                    {
                        ulSticky |= sdlTmp.High32; // record remainder as sticky bit

                        if (iNewScale > MaxInt32Scale)
                            ulPwr = TenToPowerNine;
                        else
                            ulPwr = s_powers10[iNewScale];

                        // Compute first quotient.
                        // DivMod64by32 returns quotient in Lo, remainder in Hi.
                        //
                        sdlTmp.int64 = DivMod64by32(rgulRes[iHiRes], ulPwr);
                        rgulRes[iHiRes] = sdlTmp.Low32;
                        iCur = iHiRes - 1;

                        if (iCur >= 0)
                        {
                            // If first quotient was 0, update iHiRes.
                            //
                            if (sdlTmp.Low32 == 0)
                                iHiRes--;

                            // Compute subsequent quotients.
                            //
                            do
                            {
                                sdlTmp.Low32 = rgulRes[iCur];
                                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, ulPwr);
                                rgulRes[iCur] = sdlTmp.Low32;
                                iCur--;
                            } while (iCur >= 0);
                        }

                        iNewScale -= MaxInt32Scale;
                        if (iNewScale > 0)
                            continue; // scale some more

                        // If we scaled enough, iHiRes would be 2 or less.  If not,
                        // divide by 10 more.
                        //
                        if (iHiRes > 2)
                        {
                            iNewScale = 1;
                            iScale--;
                            continue; // scale by 10
                        }

                        // Round final result.  See if remainder >= 1/2 of divisor.
                        // If remainder == 1/2 divisor, round up if odd or sticky bit set.
                        //
                        ulPwr >>= 1;  // power of 10 always even
                        if (ulPwr <= sdlTmp.High32 && (ulPwr < sdlTmp.High32 ||
                                                        ((rgulRes[0] & 1) | ulSticky) != 0))
                        {
                            iCur = -1;
                            while (++rgulRes[++iCur] == 0)
                                ;

                            if (iCur > 2)
                            {
                                // The rounding caused us to carry beyond 96 bits. 
                                // Scale by 10 more.
                                //
                                iHiRes = iCur;
                                ulSticky = 0;  // no sticky bit
                                sdlTmp.High32 = 0; // or remainder
                                iNewScale = 1;
                                iScale--;
                                continue; // scale by 10
                            }
                        }

                        // We may have scaled it more than we planned.  Make sure the scale 
                        // factor hasn't gone negative, indicating overflow.
                        // 
                        if (iScale < 0)
                            return -1;

                        return iScale;
                    } // for(;;)
                }
                return iScale;
            }

            // Adjust the quotient to deal with an overflow. We need to divide by 10, 
            // feed in the high bit to undo the overflow and then round as required, 
            private static void OverflowUnscale(uint[] rgulQuo, bool fRemainder)
            {
                Split64 sdlTmp = new Split64();

                // We have overflown, so load the high bit with a one.
                sdlTmp.High32 = 1;
                sdlTmp.Low32 = rgulQuo[2];
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, 10);
                rgulQuo[2] = sdlTmp.Low32;
                sdlTmp.Low32 = rgulQuo[1];
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, 10);
                rgulQuo[1] = sdlTmp.Low32;
                sdlTmp.Low32 = rgulQuo[0];
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, 10);
                rgulQuo[0] = sdlTmp.Low32;
                // The remainder is the last digit that does not fit, so we can use it to work out if we need to round up
                if ((sdlTmp.High32 > 5) || ((sdlTmp.High32 == 5) && (fRemainder || (rgulQuo[0] & 1) != 0)))
                    Add32To96(rgulQuo, 1);
            }

            /***
            * SearchScale
            *
            * Entry:
            *   ulResHi - Top ULONG of quotient
            *   ulResMid - Middle ULONG of quotient
            *   ulResLo - Lower ULONG of quotient
            *   iScale  - Scale factor of quotient, range -DEC_SCALE_MAX to DEC_SCALE_MAX
            *
            * Purpose:
            *   Determine the max power of 10, <= 9, that the quotient can be scaled
            *   up by and still fit in 96 bits.
            *
            * Exit:
            *   Returns power of 10 to scale by, -1 if overflow error.
            *
            ***********************************************************************/
            private static int SearchScale(uint ulResHi, uint ulResMid, uint ulResLo, int iScale)
            {
                int iCurScale;

                // Quick check to stop us from trying to scale any more.
                //
                if (ulResHi > OVFL_MAX_1_HI || iScale >= DEC_SCALE_MAX)
                {
                    iCurScale = 0;
                    goto HaveScale;
                }

                if (iScale > DEC_SCALE_MAX - 9)
                {
                    // We can't scale by 10^9 without exceeding the max scale factor.
                    // See if we can scale to the max.  If not, we'll fall into
                    // standard search for scale factor.
                    //
                    iCurScale = DEC_SCALE_MAX - iScale;
                    if (ulResHi < PowerOvfl.Hi(iCurScale - 1))
                        goto HaveScale;

                    if (ulResHi == PowerOvfl.Hi(iCurScale - 1))
                    {
                        UpperEq(ulResMid, ulResLo, ref iCurScale);
                        goto HaveScale;
                    }
                }
                else if (ulResHi < OVFL_MAX_9_HI || (ulResHi == OVFL_MAX_9_HI &&
                                                     ulResMid < OVFL_MAX_9_MID) || (ulResHi == OVFL_MAX_9_HI && ulResMid == OVFL_MAX_9_MID && ulResLo <= OVFL_MAX_9_LO))
                    return 9;

                // Search for a power to scale by < 9.  Do a binary search
                // on PowerOvfl[].
                //
                iCurScale = 5;
                if (ulResHi < OVFL_MAX_5_HI)
                    iCurScale = 7;
                else if (ulResHi > OVFL_MAX_5_HI)
                    iCurScale = 3;
                else
                {
                    UpperEq(ulResMid, ulResLo, ref iCurScale);
                    goto HaveScale;
                }

                // iCurScale is 3 or 7.
                //
                if (ulResHi < PowerOvfl.Hi(iCurScale - 1))
                    iCurScale++;
                else if (ulResHi > PowerOvfl.Hi(iCurScale - 1))
                    iCurScale--;
                else
                {
                    UpperEq(ulResMid, ulResLo, ref iCurScale);
                    goto HaveScale;
                }

                // iCurScale is 2, 4, 6, or 8.
                //
                // In all cases, we already found we could not use the power one larger.
                // So if we can use this power, it is the biggest, and we're done.  If
                // we can't use this power, the one below it is correct for all cases 
                // unless it's 10^1 -- we might have to go to 10^0 (no scaling).
                // 
                if (ulResHi > PowerOvfl.Hi(iCurScale - 1))
                    iCurScale--;

                if (ulResHi == PowerOvfl.Hi(iCurScale - 1))
                    UpperEq(ulResMid, ulResLo, ref iCurScale);

                HaveScale:
                // iCurScale = largest power of 10 we can scale by without overflow, 
                // iCurScale < 9.  See if this is enough to make scale factor 
                // positive if it isn't already.
                // 
                if (iCurScale + iScale < 0)
                    iCurScale = -1;

                return iCurScale;
            }

            private static void UpperEq(uint ulResMid, uint ulResLo, ref int iCurScale)
            {
                if (ulResMid > PowerOvfl.Mid(iCurScale - 1) ||
                    (ulResMid == PowerOvfl.Mid(iCurScale - 1) && ulResLo > PowerOvfl.Lo(iCurScale - 1)))
                {
                    iCurScale--;
                }
            }

            // Add a 32 bit unsigned long to an array of 3 unsigned longs representing a 96 integer
            // Returns false if there is an overflow
            private static bool Add32To96(uint[] rgulNum, uint ulValue)
            {
                rgulNum[0] += ulValue;
                if (rgulNum[0] < ulValue)
                {
                    if (++rgulNum[1] == 0)
                    {
                        if (++rgulNum[2] == 0)
                            return false;
                    }
                }
                return true;
            }

            // DecAddSub adds or subtracts two decimal values.  On return, d1 contains the result
            // of the operation.  Passing in true for bSign means subtract and false means add.
            // 
            // Returns true if we overflow otherwise false.
            private static bool DecAddSub(ref Decimal d1, ref Decimal d2, bool bSign)
            {
                uint[] rgulNum = new uint[6];
                uint ulPwr;
                int iScale;
                int iHiProd;
                int iCur;
                Split64 sdlTmp = new Split64();
                Decimal result = new Decimal();
                Decimal tmp = new Decimal();

                bSign ^= d2.Sign ^ d1.Sign;

                if (d2.Scale == d1.Scale)
                {
                    // Scale factors are equal, no alignment necessary.
                    //
                    result.Sign = d1.Sign;
                    result.Scale = d1.Scale;

                    if (AlignedAdd(ref result, ref d1, ref d2, bSign))
                        return true;
                }
                else
                {
                    // Scale factors are not equal.  Assume that a larger scale
                    // factor (more decimal places) is likely to mean that number
                    // is smaller.  Start by guessing that the right operand has
                    // the larger scale factor.  The result will have the larger
                    // scale factor.
                    //
                    result.Scale = d2.Scale;  // scale factor of "smaller"
                    result.Sign = d1.Sign;    // but sign of "larger"
                    iScale = result.Scale - d1.Scale;

                    if (iScale < 0)
                    {
                        // Guessed scale factor wrong. Swap operands.
                        //
                        iScale = -iScale;
                        result.Scale = d1.Scale;
                        result.Sign ^= bSign;
                        tmp = d2;
                        d2 = d1;
                        d1 = tmp;
                    }

                    // d1 will need to be multiplied by 10^iScale so
                    // it will have the same scale as d2.  We could be
                    // extending it to up to 192 bits of precision.
                    //
                    if (iScale <= MaxInt32Scale)
                    {
                        // Scaling won't make it larger than 4 ULONGs
                        //
                        ulPwr = s_powers10[iScale];
                        tmp = UInt32x32To64(d1.Low, ulPwr);
                        sdlTmp.int64 = UInt32x32To64(d1.Mid, ulPwr);
                        sdlTmp.int64 += tmp.Mid;
                        tmp.Mid = sdlTmp.Low32;
                        tmp.High = sdlTmp.High32;
                        sdlTmp.int64 = UInt32x32To64(d1.High, ulPwr);
                        sdlTmp.int64 += tmp.High;
                        if (sdlTmp.High32 == 0)
                        {
                            // Result fits in 96 bits.  Use standard aligned add.
                            //
                            tmp.High = sdlTmp.Low32;
                            d1 = tmp;
                            if (AlignedAdd(ref result, ref d1, ref d2, bSign))
                                return true;
                            d1 = result;
                            return false;
                        }
                        rgulNum[0] = tmp.Low;
                        rgulNum[1] = tmp.Mid;
                        rgulNum[2] = sdlTmp.Low32;
                        rgulNum[3] = sdlTmp.High32;
                        iHiProd = 3;
                    }
                    else
                    {
                        // Have to scale by a bunch.  Move the number to a buffer
                        // where it has room to grow as it's scaled.
                        //
                        rgulNum[0] = d1.Low;
                        rgulNum[1] = d1.Mid;
                        rgulNum[2] = d1.High;
                        iHiProd = 2;

                        // Scan for zeros in the upper words.
                        //
                        if (rgulNum[2] == 0)
                        {
                            iHiProd = 1;
                            if (rgulNum[1] == 0)
                            {
                                iHiProd = 0;
                                if (rgulNum[0] == 0)
                                {
                                    // Left arg is zero, return right.
                                    //
                                    result.Low64 = d2.Low64;
                                    result.High = d2.High;
                                    result.Sign ^= bSign;
                                    d1 = result;
                                    return false;
                                }
                            }
                        }

                        // Scaling loop, up to 10^9 at a time.  iHiProd stays updated
                        // with index of highest non-zero ULONG.
                        //
                        for (; iScale > 0; iScale -= MaxInt32Scale)
                        {
                            if (iScale > MaxInt32Scale)
                                ulPwr = TenToPowerNine;
                            else
                                ulPwr = s_powers10[iScale];

                            sdlTmp.High32 = 0;
                            for (iCur = 0; iCur <= iHiProd; iCur++)
                            {
                                sdlTmp.int64 = UInt32x32To64(rgulNum[iCur], ulPwr) + sdlTmp.High32;
                                rgulNum[iCur] = sdlTmp.Low32;
                            }

                            if (sdlTmp.High32 != 0)
                                // We're extending the result by another ULONG.
                                rgulNum[++iHiProd] = sdlTmp.High32;
                        }
                    }

                    // Scaling complete, do the add.  Could be subtract if signs differ.
                    //
                    sdlTmp.Low32 = rgulNum[0];
                    sdlTmp.High32 = rgulNum[1];

                    if (bSign)
                    {
                        // Signs differ, subtract.
                        //
                        result.Low64 = sdlTmp.int64 - d2.Low64;
                        result.High = rgulNum[2] - d2.High;

                        // Propagate carry
                        //
                        if (result.Low64 > sdlTmp.int64)
                        {
                            result.High--;
                            if (result.High >= rgulNum[2])
                                if (LongSub(ref result, ref iHiProd, rgulNum))
                                {
                                    d1 = result;
                                    return false;
                                }
                        }
                        else if (result.High > rgulNum[2])
                        {
                            if (LongSub(ref result, ref iHiProd, rgulNum))
                            {
                                d1 = result;
                                return false;
                            }
                        }
                    }
                    else
                    {
                        // Signs the same, add.
                        //
                        result.Low64 = sdlTmp.int64 + d2.Low64;
                        result.High = rgulNum[2] + d2.High;

                        // Propagate carry
                        //
                        if (result.Low64 < sdlTmp.int64)
                        {
                            result.High++;
                            if (result.High <= rgulNum[2])
                                LongAdd(ref iHiProd, rgulNum);
                        }
                        else if (result.High < rgulNum[2])
                        {
                            LongAdd(ref iHiProd, rgulNum);
                        }
                    }

                    if (iHiProd > 2)
                    {
                        rgulNum[0] = result.Low;
                        rgulNum[1] = result.Mid;
                        rgulNum[2] = result.High;
                        int scale = ScaleResult(rgulNum, iHiProd, result.Scale);
                        if (scale == -1)
                            return true;
                        result.Scale = scale;

                        result.Low = rgulNum[0];
                        result.Mid = rgulNum[1];
                        result.High = rgulNum[2];
                    }
                }

                d1 = result;
                return false;
            }

            private static void SignFlip(ref Decimal value)
            {
                value.Low64 = (ulong)-(long)value.Low64;
                value.High = ~value.High;
                if (value.Low64 == 0)
                    value.High++;
                value.Sign ^= true;
            }

            // Returns true if we overflowed
            private static bool AlignedScale(ref Decimal value)
            {
                Split64 sdlTmp = new Split64();

                // Divide the value by 10, dropping the scale factor.
                // 
                if (value.Scale == 0)
                    return true;
                value.Scale--;

                sdlTmp.Low32 = value.High;
                sdlTmp.High32 = 1;
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, 10);
                value.High = sdlTmp.Low32;

                sdlTmp.Low32 = value.Mid;
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, 10);
                value.Mid = sdlTmp.Low32;

                sdlTmp.Low32 = value.Low;
                sdlTmp.int64 = DivMod64by32(sdlTmp.int64, 10);
                value.Low = sdlTmp.Low32;

                // See if we need to round up.
                //
                if (sdlTmp.High32 >= 5 && (sdlTmp.High32 > 5 || (value.Low & 1) != 0))
                {
                    value.Low64 = value.Low64 + 1;
                    if (value.Low64 == 0)
                        value.High++;
                }
                return false;
            }

            private static bool LongSub(ref Decimal value, ref int iHiProd, uint[] rgulNum)
            {
                // If rgulNum has more than 96 bits of precision, then we need to 
                // carry the subtraction into the higher bits.  If it doesn't, 
                // then we subtracted in the wrong order and have to flip the 
                // sign of the result.
                // 
                if (iHiProd <= 2)
                {
                    SignFlip(ref value);
                    return true;
                }

                int iCur = 3;
                while (rgulNum[iCur++]-- == 0)
                    ;
                if (rgulNum[iHiProd] == 0)
                    iHiProd--;

                return false;
            }

            private static void LongAdd(ref int iHiProd, uint[] rgulNum)
            {
                int iCur = 3;
                do
                {
                    if (iHiProd < iCur)
                    {
                        rgulNum[iCur] = 1;
                        iHiProd = iCur;
                        break;
                    }
                } while (++rgulNum[iCur++] == 0);
            }

            // Returns true if we overflowed
            private static bool AlignedAdd(ref Decimal value, ref Decimal d1, ref Decimal d2, bool bSign)
            {
                if (bSign)
                {
                    // Signs differ - subtract
                    //
                    value.Low64 = d1.Low64 - d2.Low64;
                    value.High = d1.High - d2.High;

                    // Propagate carry
                    //
                    if (value.Low64 > d1.Low64)
                    {
                        value.High--;
                        if (value.High >= d1.High)
                            SignFlip(ref value);
                    }
                    else if (value.High > d1.High)
                    {
                        // Got negative result.  Flip its sign.
                        //
                        SignFlip(ref value);
                    }
                }
                else
                {
                    // Signs are the same - add
                    //
                    value.Low64 = d1.Low64 + d2.Low64;
                    value.High = d1.High + d2.High;

                    // Propagate carry
                    //
                    if (value.Low64 < d1.Low64)
                    {
                        value.High++;
                        if (value.High <= d1.High)
                        {
                            if (AlignedScale(ref value))
                                return true;
                        }
                    }
                    else if (value.High < d1.High)
                    {
                        // The addition carried above 96 bits.  Divide the result by 10,
                        // dropping the scale factor.
                        // 
                        if (AlignedScale(ref value))
                            return true;
                    }
                }
                return false;
            }

            private static void RoundUp(uint[] rgulQuo, ref int iScale)
            {
                if (!Add32To96(rgulQuo, 1))
                {
                    if (iScale == 0)
                        throw new OverflowException(SR.Overflow_Decimal);
                    iScale--;
                    OverflowUnscale(rgulQuo, true);
                }
            }

            // Returns the absolute value of the given Decimal. If d is
            // positive, the result is d. If d is negative, the result
            // is -d.
            //
            private static Decimal Abs(Decimal d)
            {
                return new Decimal(d.lo, d.mid, d.hi, (int)(d.uflags & ~SignMask));
            }

            /***
* DecFixInt
*
*   input - Pointer to Decimal operand
*   result  - Pointer to Decimal result location
*   
* Purpose:
*   Chop the value to integer.    Return remainder so Int() function
*   can round down if non-zero.
*
* Exit:
*   Returns remainder.
*
* Exceptions:
*   None.
*
***********************************************************************/
            private static uint DecFixInt(ref Decimal input, ref Decimal result)
            {
                uint[] tmpNum = new uint[3];
                uint remainder;
                uint power;
                int scale;

                if (input.Scale > 0)
                {
                    tmpNum[0] = input.ulo;
                    tmpNum[1] = input.umid;
                    tmpNum[2] = input.uhi;
                    scale = input.Scale;
                    result.Sign = input.Sign;
                    remainder = 0;

                    do
                    {
                        if (scale > MaxInt32Scale)
                            power = TenToPowerNine;
                        else
                            power = s_powers10[scale];

                        remainder |= Div96By32(tmpNum, power);
                        scale -= MaxInt32Scale;
                    } while (scale > 0);

                    result.ulo = tmpNum[0];
                    result.umid = tmpNum[1];
                    result.uhi = tmpNum[2];
                    result.Scale = 0;

                    return remainder;
                }
                result = input;
                return 0;
            }

            #endregion

            //**********************************************************************
            // VarCyFromDec - Convert Currency to Decimal (similar to OleAut32 api.)
            //**********************************************************************
            internal static void VarCyFromDec(ref Decimal pdecIn, out long pcyOut)
            {
                if (!Decimal.IsValid(pdecIn.uflags))
                    throw new OverflowException(SR.Overflow_Currency);

                Split64 sdlTmp = default(Split64);

                int scale = pdecIn.Scale - 4; // the power of 10 to divide by
                if (scale == 0)
                {
                    if (pdecIn.High != 0 ||
                        (pdecIn.Mid >= 0x80000000U &&
                        (pdecIn.Mid != 0x80000000U || pdecIn.Low != 0 || !pdecIn.Sign)))
                        throw new OverflowException(SR.Overflow_Currency);

                    sdlTmp.Low32 = pdecIn.Low;
                    sdlTmp.High32 = pdecIn.Mid;

                    if (pdecIn.Sign)
                        pcyOut = -(long)sdlTmp.int64;
                    else
                        pcyOut = (long)sdlTmp.int64;
                    return;
                }

                // Need to scale to get 4 decimal places.  -4 <= scale <= 24.
                //
                if (scale < 0)
                {
                    Split64 sdlTmp1 = default(Split64);
                    sdlTmp1.int64 = UInt32x32To64(s_powers10[-scale], pdecIn.Mid);
                    sdlTmp.int64 = UInt32x32To64(s_powers10[-scale], pdecIn.Low);
                    sdlTmp.High32 += sdlTmp1.Low32;
                    if (pdecIn.High != 0 || sdlTmp1.High32 != 0 || sdlTmp1.Low32 > sdlTmp.High32)
                        throw new OverflowException(SR.Overflow_Currency);
                }
                else if (scale < 10)
                {
                    // DivMod64by32 returns the quotient in Lo, the remainder in Hi.
                    //
                    uint pwr = s_powers10[scale];
                    if (pdecIn.High >= pwr)
                        throw new OverflowException(SR.Overflow_Currency);

                    Split64 sdlTmp1 = default(Split64);
                    sdlTmp1.Low32 = pdecIn.Mid;
                    sdlTmp1.High32 = pdecIn.High;
                    sdlTmp1.int64 = DivMod64by32(sdlTmp1.int64, pwr);
                    sdlTmp.High32 = sdlTmp1.Low32;   // quotient to high half of result
                    sdlTmp1.Low32 = pdecIn.Low;      // extended remainder
                    sdlTmp1.int64 = DivMod64by32(sdlTmp1.int64, pwr);
                    sdlTmp.Low32 = sdlTmp1.Low32;    // quotient to low half of result

                    // Round result based on remainder in sdlTmp1.Hi.
                    //
                    pwr >>= 1;  // compare to power/2 (power always even)
                    if (sdlTmp1.High32 > pwr || (sdlTmp1.High32 == pwr && ((sdlTmp.Low32 & 1) != 0)))
                        sdlTmp.int64++;
                }
                else
                {
                    // We have a power of 10 in the range 10 - 24.  These powers do
                    // not fit in 32 bits.  We'll handle this by scaling 2 or 3 times,
                    // first by 10^10, then by the remaining amount (or 10^9, then
                    // the last bit).
                    //
                    // To scale by 10^10, we'll actually divide by 10^10/4, which fits
                    // in 32 bits.  The second scaling is multiplied by four
                    // to account for it, just barely assured of fitting in 32 bits
                    // (4E9 < 2^32).  Note that the upper third of the quotient is
                    // either zero or one, so we skip the divide step to calculate it.  
                    // (Max 4E9 divided by 2.5E9.)
                    //
                    // DivMod64by32 returns the quotient in Lo, the remainder in Hi.
                    //

                    const uint TenToTenDiv4 = 2500000000U;

                    Split64 sdlTmp1 = default(Split64);
                    if (pdecIn.High >= TenToTenDiv4)
                    {
                        sdlTmp.High32 = 1;                // upper 1st quotient
                        sdlTmp1.High32 = pdecIn.High - TenToTenDiv4;  // remainder
                    }
                    else
                    {
                        sdlTmp.High32 = 0;                // upper 1st quotient
                        sdlTmp1.High32 = pdecIn.High;     // remainder
                    }

                    sdlTmp1.Low32 = pdecIn.Mid;           // extended remainder
                    sdlTmp1.int64 = DivMod64by32(sdlTmp1.int64, TenToTenDiv4);
                    sdlTmp.Low32 = sdlTmp1.Low32;         // middle 1st quotient

                    sdlTmp1.Low32 = pdecIn.Low;           // extended remainder
                    sdlTmp1.int64 = DivMod64by32(sdlTmp1.int64, TenToTenDiv4);

                    uint pwr = s_powers10[Math.Min(scale - 10, 9)] << 2;
                    sdlTmp.int64 = DivMod64by32(sdlTmp.int64, pwr);
                    uint savedTmpLow32 = sdlTmp.Low32;    // upper 2nd quotient

                    sdlTmp.Low32 = sdlTmp1.Low32;         // extended remainder
                    sdlTmp.int64 = DivMod64by32(sdlTmp.int64, pwr);
                    sdlTmp1.Low32 = sdlTmp.High32;        // save final remainder
                    sdlTmp.High32 = savedTmpLow32;        // position high result

                    if (scale >= 20)
                    {
                        pwr = s_powers10[scale - 19];
                        sdlTmp.int64 = DivMod64by32(sdlTmp.int64, pwr);
                        sdlTmp1.High32 |= sdlTmp1.Low32;  // combine sticky bits
                        sdlTmp1.Low32 = sdlTmp.High32;    // final remainder
                        sdlTmp.High32 = 0;                // guaranteed result fits in 32 bits
                    }

                    // Round result based on remainder in sdlTmp1.Lo.  sdlTmp1.Hi is
                    // the remainder from the first division(s), representing sticky bits.
                    // Current result is in sdlTmp.

                    pwr >>= 1;  // compare to power/2 (power always even)
                    if (sdlTmp1.Low32 > pwr || (sdlTmp1.Low32 == pwr && (((sdlTmp.Low32 & 1) != 0) || sdlTmp1.High32 != 0)))
                        sdlTmp.int64++;
                }

                if (sdlTmp.High32 >= 0x80000000U &&
                   (sdlTmp.int64 != 0x8000000000000000LU || !pdecIn.Sign))
                    throw new OverflowException(SR.Overflow_Currency);

                if (pdecIn.Sign)
                    sdlTmp.int64 = (ulong)(-(long)sdlTmp.int64);

                pcyOut = (long)sdlTmp.int64;
            }

            //**********************************************************************
            // VarDecCmp - Decimal Compare updated to return values similar to ICompareTo
            //**********************************************************************
            internal static int VarDecCmp(ref Decimal pdecL, ref Decimal pdecR)
            {
                int signLeft = 0;
                int signRight = 0;

                if (pdecL.Low != 0 || pdecL.Mid != 0 || pdecL.High != 0)
                    signLeft = pdecL.Sign ? -1 : 1;

                if (pdecR.Low != 0 || pdecR.Mid != 0 || pdecR.High != 0)
                    signRight = pdecR.Sign ? -1 : 1;

                if (signLeft == signRight)
                {
                    if (signLeft == 0)    // both are zero
                        return 0; // return equal

                    Decimal decLAndResult = pdecL; // Copy the left and pass that to AddSub because it gets mutated.
                    DecAddSub(ref decLAndResult, ref pdecR, true); // Call DecAddSub instead of VarDecSub to avoid exceptions
                    if (decLAndResult.Low == 0 && decLAndResult.Mid == 0 && decLAndResult.High == 0)
                        return 0;
                    if (decLAndResult.Sign)
                        return -1;
                    return 1;
                }

                // Signs are different.  Used signed byte compares
                //
                if (signLeft > signRight)
                    return 1;
                return -1;
            }

            //**********************************************************************
            // VarDecMul - Decimal Multiply
            //**********************************************************************
            internal static void VarDecMul(ref Decimal pdecL, ref Decimal pdecR, out Decimal pdecRes)
            {
                Split64 sdlTmp = new Split64();
                Split64 sdlTmp2 = new Split64();
                Split64 sdlTmp3 = new Split64();
                int iScale;
                int iHiProd;
                uint ulPwr;
                uint ulRemLo;
                uint ulRemHi;
                uint[] rgulProd = new uint[6];

                pdecRes = new Decimal();
                iScale = pdecL.Scale + pdecR.Scale;

                if ((pdecL.High | pdecL.Mid | pdecR.High | pdecR.Mid) == 0)
                {
                    // Upper 64 bits are zero.
                    //
                    sdlTmp.int64 = UInt32x32To64(pdecL.Low, pdecR.Low);
                    if (iScale > DEC_SCALE_MAX)
                    {
                        // Result iScale is too big.  Divide result by power of 10 to reduce it.
                        // If the amount to divide by is > 19 the result is guaranteed
                        // less than 1/2.  [max value in 64 bits = 1.84E19]
                        //
                        iScale -= DEC_SCALE_MAX;
                        if (iScale > 19)
                        {
                            //DECIMAL_SETZERO(*pdecRes);
                            return;
                        }
                        if (iScale > MaxInt32Scale)
                        {
                            // Divide by 1E10 first, to get the power down to a 32-bit quantity.
                            // 1E10 itself doesn't fit in 32 bits, so we'll divide by 2.5E9 now
                            // then multiply the next divisor by 4 (which will be a max of 4E9).
                            // 
                            ulRemLo = FullDiv64By32(ref sdlTmp.int64, TenToPowerTenDiv4);
                            ulPwr = s_powers10[iScale - 10] << 2;
                        }
                        else
                        {
                            ulPwr = s_powers10[iScale];
                            ulRemLo = 0;
                        }

                        // Power to divide by fits in 32 bits.
                        //
                        ulRemHi = FullDiv64By32(ref sdlTmp.int64, ulPwr);

                        // Round result.  See if remainder >= 1/2 of divisor.
                        // Divisor is a power of 10, so it is always even.
                        //
                        ulPwr >>= 1;
                        if (ulRemHi >= ulPwr && (ulRemHi > ulPwr || (ulRemLo | (sdlTmp.Low32 & 1)) > 0))
                            sdlTmp.int64++;

                        iScale = DEC_SCALE_MAX;
                    }
                    pdecRes.Low64 = sdlTmp.int64;
                    pdecRes.High = 0;
                }
                else
                {
                    // At least one operand has bits set in the upper 64 bits.
                    //
                    // Compute and accumulate the 9 partial products into a 
                    // 192-bit (24-byte) result.
                    //
                    //        [l-h][l-m][l-l]      left high, middle, low
                    //         x    [r-h][r-m][r-l]      right high, middle, low
                    // ------------------------------
                    //
                    //             [0-h][0-l]      l-l * r-l
                    //        [1ah][1al]      l-l * r-m
                    //        [1bh][1bl]      l-m * r-l
                    //       [2ah][2al]          l-m * r-m
                    //       [2bh][2bl]          l-l * r-h
                    //       [2ch][2cl]          l-h * r-l
                    //      [3ah][3al]          l-m * r-h
                    //      [3bh][3bl]          l-h * r-m
                    // [4-h][4-l]              l-h * r-h
                    // ------------------------------
                    // [p-5][p-4][p-3][p-2][p-1][p-0]      prod[] array
                    //
                    sdlTmp.int64 = UInt32x32To64(pdecL.Low, pdecR.Low);
                    rgulProd[0] = sdlTmp.Low32;

                    sdlTmp2.int64 = UInt32x32To64(pdecL.Low, pdecR.Mid) + sdlTmp.High32;

                    sdlTmp.int64 = UInt32x32To64(pdecL.Mid, pdecR.Low);
                    sdlTmp.int64 += sdlTmp2.int64; // this could generate carry
                    rgulProd[1] = sdlTmp.Low32;
                    if (sdlTmp.int64 < sdlTmp2.int64) // detect carry
                        sdlTmp2.High32 = 1;
                    else
                        sdlTmp2.High32 = 0;
                    sdlTmp2.Low32 = sdlTmp.High32;

                    sdlTmp.int64 = UInt32x32To64(pdecL.Mid, pdecR.Mid) + sdlTmp2.int64;

                    if ((pdecL.High | pdecR.High) > 0)
                    {
                        // Highest 32 bits is non-zero.     Calculate 5 more partial products.
                        //
                        sdlTmp2.int64 = UInt32x32To64(pdecL.Low, pdecR.High);
                        sdlTmp.int64 += sdlTmp2.int64; // this could generate carry
                        if (sdlTmp.int64 < sdlTmp2.int64) // detect carry
                            sdlTmp3.High32 = 1;
                        else
                            sdlTmp3.High32 = 0;

                        sdlTmp2.int64 = UInt32x32To64(pdecL.High, pdecR.Low);
                        sdlTmp.int64 += sdlTmp2.int64; // this could generate carry
                        rgulProd[2] = sdlTmp.Low32;
                        if (sdlTmp.int64 < sdlTmp2.int64) // detect carry
                            sdlTmp3.High32++;
                        sdlTmp3.Low32 = sdlTmp.High32;

                        sdlTmp.int64 = UInt32x32To64(pdecL.Mid, pdecR.High);
                        sdlTmp.int64 += sdlTmp3.int64; // this could generate carry
                        if (sdlTmp.int64 < sdlTmp3.int64) // detect carry
                            sdlTmp3.High32 = 1;
                        else
                            sdlTmp3.High32 = 0;

                        sdlTmp2.int64 = UInt32x32To64(pdecL.High, pdecR.Mid);
                        sdlTmp.int64 += sdlTmp2.int64; // this could generate carry
                        rgulProd[3] = sdlTmp.Low32;
                        if (sdlTmp.int64 < sdlTmp2.int64) // detect carry
                            sdlTmp3.High32++;
                        sdlTmp3.Low32 = sdlTmp.High32;

                        sdlTmp.int64 = UInt32x32To64(pdecL.High, pdecR.High) + sdlTmp3.int64;
                        rgulProd[4] = sdlTmp.Low32;
                        rgulProd[5] = sdlTmp.High32;

                        iHiProd = 5;
                    }
                    else
                    {
                        rgulProd[2] = sdlTmp.Low32;
                        rgulProd[3] = sdlTmp.High32;
                        iHiProd = 3;
                    }

                    // Check for leading zero ULONGs on the product
                    //
                    while (rgulProd[iHiProd] == 0)
                    {
                        iHiProd--;
                        if (iHiProd < 0)
                            return;
                    }

                    iScale = ScaleResult(rgulProd, iHiProd, iScale);
                    if (iScale == -1)
                        throw new OverflowException(SR.Overflow_Decimal);

                    pdecRes.Low = rgulProd[0];
                    pdecRes.Mid = rgulProd[1];
                    pdecRes.High = rgulProd[2];
                }

                pdecRes.Sign = pdecR.Sign ^ pdecL.Sign;
                pdecRes.Scale = (char)iScale;
            }

            //**********************************************************************
            // VarDecFromR4 - Convert float to Decimal
            //**********************************************************************
            internal static void VarDecFromR4(float input, out Decimal pdecOut)
            {
                int iExp;    // number of bits to left of binary point
                int iPower;
                uint ulMant;
                double dbl;
                Split64 sdlLo = new Split64();
                Split64 sdlHi = new Split64();
                int lmax, cur;  // temps used during scale reduction

                pdecOut = new Decimal();

                // The most we can scale by is 10^28, which is just slightly more
                // than 2^93.  So a float with an exponent of -94 could just
                // barely reach 0.5, but smaller exponents will always round to zero.
                //
                iExp = (int)(GetExponent(input) - SNGBIAS);
                if (iExp < -94)
                    return; // result should be zeroed out

                if (iExp > 96)
                    throw new OverflowException(SR.Overflow_Decimal);

                // Round the input to a 7-digit integer.  The R4 format has
                // only 7 digits of precision, and we want to keep garbage digits
                // out of the Decimal were making.
                //
                // Calculate max power of 10 input value could have by multiplying 
                // the exponent by log10(2).  Using scaled integer multiplcation, 
                // log10(2) * 2 ^ 16 = .30103 * 65536 = 19728.3.
                //
                dbl = input;
                if (dbl < 0)
                    dbl *= -1;
                iPower = 6 - ((iExp * 19728) >> 16);

                if (iPower >= 0)
                {
                    // We have less than 7 digits, scale input up.
                    //
                    if (iPower > DEC_SCALE_MAX)
                        iPower = DEC_SCALE_MAX;

                    dbl = dbl * s_doublePowers10[iPower];
                }
                else
                {
                    if (iPower != -1 || dbl >= 1E7)
                        dbl = dbl / GetDoublePower10(-iPower);
                    else
                        iPower = 0; // didn't scale it
                }

                System.Diagnostics.Debug.Assert(dbl < 1E7);
                if (dbl < 1E6 && iPower < DEC_SCALE_MAX)
                {
                    dbl *= 10;
                    iPower++;
                    System.Diagnostics.Debug.Assert(dbl >= 1E6);
                }

                // Round to integer
                //
                ulMant = (uint)dbl;
                dbl -= (double)ulMant;  // difference between input & integer
                if (dbl > 0.5 || (dbl == 0.5) && (ulMant & 1) != 0)
                    ulMant++;

                if (ulMant == 0)
                    return;  // result should be zeroed out

                if (iPower < 0)
                {
                    // Add -iPower factors of 10, -iPower <= (29 - 7) = 22.
                    //
                    iPower = -iPower;
                    if (iPower < 10)
                    {
                        pdecOut.Low64 = UInt32x32To64(ulMant, s_powers10[iPower]);
                        pdecOut.High = 0;
                    }
                    else
                    {
                        // Have a big power of 10.
                        //
                        if (iPower > 18)
                        {
                            sdlLo.int64 = UInt32x32To64(ulMant, s_powers10[iPower - 18]);
                            ulong tmplong;
                            sdlLo.int64 = UInt64x64To128(sdlLo, s_tenToPowerEighteen, out tmplong);
                            sdlHi.int64 = tmplong;

                            if (sdlHi.High32 != 0)
                                throw new OverflowException(SR.Overflow_Decimal);
                        }
                        else
                        {
                            sdlLo.int64 = UInt32x32To64(ulMant, s_powers10[iPower - 9]);
                            sdlHi.int64 = UInt32x32To64(TenToPowerNine, sdlLo.High32);
                            sdlLo.int64 = UInt32x32To64(TenToPowerNine, sdlLo.Low32);
                            sdlHi.int64 += sdlLo.High32;
                            sdlLo.High32 = sdlHi.Low32;
                            sdlHi.Low32 = sdlHi.High32;
                        }
                        pdecOut.Low64 = sdlLo.int64;
                        pdecOut.High = sdlHi.Low32;
                    }
                    pdecOut.Scale = 0;
                }
                else
                {
                    // Factor out powers of 10 to reduce the scale, if possible.
                    // The maximum number we could factor out would be 6.  This
                    // comes from the fact we have a 7-digit number, and the 
                    // MSD must be non-zero -- but the lower 6 digits could be 
                    // zero.  Note also the scale factor is never negative, so
                    // we can't scale by any more than the power we used to
                    // get the integer.
                    //
                    // DivMod32by32 returns the quotient in Lo, the remainder in Hi.
                    //
                    lmax = iPower < 6 ? iPower : 6;

                    // lmax is the largest power of 10 to try, lmax <= 6.
                    // We'll try powers 4, 2, and 1 unless they're too big.
                    //
                    for (cur = 4; cur > 0; cur >>= 1)
                    {
                        if (cur > lmax)
                            continue;

                        sdlLo.int64 = DivMod32by32(ulMant, s_powers10[cur]);

                        if (sdlLo.High32 == 0)
                        {
                            ulMant = sdlLo.Low32;
                            iPower -= cur;
                            lmax -= cur;
                        }
                    }
                    pdecOut.Low = ulMant;
                    pdecOut.Mid = 0;
                    pdecOut.High = 0;
                    pdecOut.Scale = iPower;
                }

                pdecOut.Sign = input < 0;
            }

            //**********************************************************************
            // VarDecFromR8 - Convert double to Decimal
            //**********************************************************************
            internal static void VarDecFromR8(double input, out Decimal pdecOut)
            {
                int iExp;    // number of bits to left of binary point
                int iPower;  // power-of-10 scale factor
                Split64 sdlMant = new Split64();
                Split64 sdlLo = new Split64();
                double dbl;
                int lmax, cur;  // temps used during scale reduction
                uint ulPwrCur;
                uint ulQuo;

                pdecOut = new Decimal();

                // The most we can scale by is 10^28, which is just slightly more
                // than 2^93.  So a float with an exponent of -94 could just
                // barely reach 0.5, but smaller exponents will always round to zero.
                //
                iExp = (int)(GetExponent(input) - DBLBIAS);
                if (iExp < -94)
                    return;  // result should be zeroed out

                if (iExp > 96)
                    throw new OverflowException(SR.Overflow_Decimal);
                dbl = input;
                if (dbl < 0)
                    dbl *= -1;

                // Round the input to a 15-digit integer.  The R8 format has
                // only 15 digits of precision, and we want to keep garbage digits
                // out of the Decimal were making.
                //
                // Calculate max power of 10 input value could have by multiplying 
                // the exponent by log10(2).  Using scaled integer multiplcation, 
                // log10(2) * 2 ^ 16 = .30103 * 65536 = 19728.3.
                //

                iPower = 14 - ((iExp * 19728) >> 16);

                if (iPower >= 0)
                {
                    // We have less than 15 digits, scale input up.
                    //
                    if (iPower > DEC_SCALE_MAX)
                        iPower = DEC_SCALE_MAX;

                    dbl = dbl * s_doublePowers10[iPower];
                }
                else
                {
                    if (iPower != -1 || dbl >= 1E15)
                        dbl = dbl / GetDoublePower10(-iPower);
                    else
                        iPower = 0; // didn't scale it
                }

                System.Diagnostics.Debug.Assert(dbl < 1E15);
                if (dbl < 1E14 && iPower < DEC_SCALE_MAX)
                {
                    dbl *= 10;
                    iPower++;
                    System.Diagnostics.Debug.Assert(dbl >= 1E14);
                }

                // Round to int64
                //
                sdlMant.int64 = (ulong)dbl;
                dbl -= (double)sdlMant.int64;  // dif between input & integer
                if (dbl > 0.5 || dbl == 0.5 && (sdlMant.Low32 & 1) != 0)
                    sdlMant.int64++;

                if (sdlMant.int64 == 0)
                    return;  // result should be zeroed out

                if (iPower < 0)
                {
                    // Add -iPower factors of 10, -iPower <= (29 - 15) = 14.
                    //
                    iPower = -iPower;
                    if (iPower < 10)
                    {
                        sdlLo.int64 = UInt32x32To64(sdlMant.Low32, s_powers10[iPower]);
                        sdlMant.int64 = UInt32x32To64(sdlMant.High32, s_powers10[iPower]);
                        sdlMant.int64 += sdlLo.High32;
                        sdlLo.High32 = sdlMant.Low32;
                        sdlMant.Low32 = sdlMant.High32;
                    }
                    else
                    {
                        // Have a big power of 10.
                        //
                        System.Diagnostics.Debug.Assert(iPower <= 14);
                        ulong tmpValue;
                        sdlLo.int64 = UInt64x64To128(sdlMant, new Split64((ulong)s_doublePowers10[iPower]), out tmpValue);
                        sdlMant.int64 = tmpValue;

                        if (sdlMant.High32 != 0)
                            throw new OverflowException(SR.Overflow_Decimal);
                    }
                    pdecOut.Low64 = sdlLo.int64;
                    pdecOut.High = sdlMant.Low32;
                    pdecOut.Scale = 0;
                }
                else
                {
                    // Factor out powers of 10 to reduce the scale, if possible.
                    // The maximum number we could factor out would be 14.  This
                    // comes from the fact we have a 15-digit number, and the 
                    // MSD must be non-zero -- but the lower 14 digits could be 
                    // zero.  Note also the scale factor is never negative, so
                    // we can't scale by any more than the power we used to
                    // get the integer.
                    //
                    // DivMod64by32 returns the quotient in Lo, the remainder in Hi.
                    //
                    lmax = iPower < 14 ? iPower : 14;

                    // lmax is the largest power of 10 to try, lmax <= 14.
                    // We'll try powers 8, 4, 2, and 1 unless they're too big.
                    //
                    for (cur = 8; cur > 0; cur >>= 1)
                    {
                        if (cur > lmax)
                            continue;

                        ulPwrCur = s_powers10[cur];

                        if (sdlMant.High32 >= ulPwrCur)
                        {
                            // Overflow if we try to divide in one step.
                            //
                            sdlLo.int64 = DivMod64by32(sdlMant.High32, ulPwrCur);
                            ulQuo = sdlLo.Low32;
                            sdlLo.Low32 = sdlMant.Low32;
                            sdlLo.int64 = DivMod64by32(sdlLo.int64, ulPwrCur);
                        }
                        else
                        {
                            ulQuo = 0;
                            sdlLo.int64 = DivMod64by32(sdlMant.int64, ulPwrCur);
                        }

                        if (sdlLo.High32 == 0)
                        {
                            sdlMant.High32 = ulQuo;
                            sdlMant.Low32 = sdlLo.Low32;
                            iPower -= cur;
                            lmax -= cur;
                        }
                    }

                    pdecOut.High = 0;
                    pdecOut.Scale = iPower;
                    pdecOut.Low64 = sdlMant.int64;
                }

                pdecOut.Sign = input < 0;
            }

            //**********************************************************************
            // VarR4ToDec - Convert Decimal to float
            //**********************************************************************
            internal static float VarR4FromDec(ref Decimal pdecIn)
            {
                return (float)VarR8FromDec(ref pdecIn);
            }

            //**********************************************************************
            // VarR8ToDec - Convert Decimal to double
            //**********************************************************************
            internal static double VarR8FromDec(ref Decimal pdecIn)
            {
                double dbl = ((double)pdecIn.Low64 +
                    (double)pdecIn.High * ds2to64) / GetDoublePower10(pdecIn.Scale);

                if (pdecIn.Sign)
                    dbl = -dbl;

                return dbl;
            }

            // VarDecAdd divides two decimal values.  On return, d1 contains the result
            // of the operation
            internal static void VarDecAdd(ref Decimal d1, ref Decimal d2)
            {
                if (DecAddSub(ref d1, ref d2, false))
                    throw new OverflowException(SR.Overflow_Decimal);
            }

            // VarDecSub divides two decimal values.  On return, d1 contains the result
            // of the operation.
            internal static void VarDecSub(ref Decimal d1, ref Decimal d2)
            {
                if (DecAddSub(ref d1, ref d2, true))
                    throw new OverflowException(SR.Overflow_Decimal);
            }

            // VarDecDiv divides two decimal values.  On return, d1 contains the result
            // of the operation.
            internal static void VarDecDiv(ref Decimal d1, ref Decimal d2)
            {
                uint[] rgulQuo = new uint[3];
                uint[] rgulQuoSave = new uint[3];
                uint[] rgulRem = new uint[4];
                uint[] rgulDivisor = new uint[3];
                uint ulPwr;
                uint ulTmp;
                uint ulTmp1;
                Split64 sdlTmp = new Split64();
                Split64 sdlDivisor = new Split64();
                int iScale;
                int iCurScale;
                bool fUnscale;

                iScale = d1.Scale - d2.Scale;
                fUnscale = false;
                rgulDivisor[0] = d2.Low;
                rgulDivisor[1] = d2.Mid;
                rgulDivisor[2] = d2.High;

                if (rgulDivisor[1] == 0 && rgulDivisor[2] == 0)
                {
                    // Divisor is only 32 bits.  Easy divide.
                    //
                    if (rgulDivisor[0] == 0)
                        throw new DivideByZeroException(SR.Overflow_Decimal);

                    rgulQuo[0] = d1.Low;
                    rgulQuo[1] = d1.Mid;
                    rgulQuo[2] = d1.High;
                    rgulRem[0] = Div96By32(rgulQuo, rgulDivisor[0]);

                    for (;;)
                    {
                        if (rgulRem[0] == 0)
                        {
                            if (iScale < 0)
                            {
                                iCurScale = Math.Min(9, -iScale);
                                goto HaveScale;
                            }
                            break;
                        }

                        // We need to unscale if and only if we have a non-zero remainder
                        fUnscale = true;

                        // We have computed a quotient based on the natural scale 
                        // ( <dividend scale> - <divisor scale> ).  We have a non-zero 
                        // remainder, so now we should increase the scale if possible to 
                        // include more quotient bits.
                        // 
                        // If it doesn't cause overflow, we'll loop scaling by 10^9 and 
                        // computing more quotient bits as long as the remainder stays 
                        // non-zero.  If scaling by that much would cause overflow, we'll 
                        // drop out of the loop and scale by as much as we can.
                        // 
                        // Scaling by 10^9 will overflow if rgulQuo[2].rgulQuo[1] >= 2^32 / 10^9 
                        // = 4.294 967 296.  So the upper limit is rgulQuo[2] == 4 and 
                        // rgulQuo[1] == 0.294 967 296 * 2^32 = 1,266,874,889.7+.  Since 
                        // quotient bits in rgulQuo[0] could be all 1's, then 1,266,874,888 
                        // is the largest value in rgulQuo[1] (when rgulQuo[2] == 4) that is 
                        // assured not to overflow.
                        // 
                        iCurScale = SearchScale(rgulQuo[2], rgulQuo[1], rgulQuo[0], iScale);
                        if (iCurScale == 0)
                        {
                            // No more scaling to be done, but remainder is non-zero.
                            // Round quotient.
                            //
                            ulTmp = rgulRem[0] << 1;
                            if (ulTmp < rgulRem[0] || (ulTmp >= rgulDivisor[0] &&
                                                       (ulTmp > rgulDivisor[0] || (rgulQuo[0] & 1) != 0)))
                                RoundUp(rgulQuo, ref iScale);
                            break;
                        }

                        if (iCurScale < 0)
                            throw new OverflowException(SR.Overflow_Decimal);

                        HaveScale:
                        ulPwr = s_powers10[iCurScale];
                        iScale += iCurScale;

                        if (IncreaseScale(rgulQuo, ulPwr) != 0)
                            throw new OverflowException(SR.Overflow_Decimal);

                        sdlTmp.int64 = DivMod64by32(UInt32x32To64(rgulRem[0], ulPwr), rgulDivisor[0]);
                        rgulRem[0] = sdlTmp.High32;

                        if (!Add32To96(rgulQuo, sdlTmp.Low32))
                        {
                            if (iScale == 0)
                                throw new OverflowException(SR.Overflow_Decimal);
                            iScale--;
                            OverflowUnscale(rgulQuo, (rgulRem[0] != 0));
                            break;
                        }
                    } // for (;;)
                }
                else
                {
                    // Divisor has bits set in the upper 64 bits.
                    //
                    // Divisor must be fully normalized (shifted so bit 31 of the most 
                    // significant ULONG is 1).  Locate the MSB so we know how much to 
                    // normalize by.  The dividend will be shifted by the same amount so 
                    // the quotient is not changed.
                    //
                    if (rgulDivisor[2] == 0)
                        ulTmp = rgulDivisor[1];
                    else
                        ulTmp = rgulDivisor[2];

                    iCurScale = 0;
                    if ((ulTmp & 0xFFFF0000) == 0)
                    {
                        iCurScale += 16;
                        ulTmp <<= 16;
                    }
                    if ((ulTmp & 0xFF000000) == 0)
                    {
                        iCurScale += 8;
                        ulTmp <<= 8;
                    }
                    if ((ulTmp & 0xF0000000) == 0)
                    {
                        iCurScale += 4;
                        ulTmp <<= 4;
                    }
                    if ((ulTmp & 0xC0000000) == 0)
                    {
                        iCurScale += 2;
                        ulTmp <<= 2;
                    }
                    if ((ulTmp & 0x80000000) == 0)
                    {
                        iCurScale++;
                        ulTmp <<= 1;
                    }

                    // Shift both dividend and divisor left by iCurScale.
                    // 
                    sdlTmp.int64 = d1.Low64 << iCurScale;
                    rgulRem[0] = sdlTmp.Low32;
                    rgulRem[1] = sdlTmp.High32;
                    sdlTmp.Low32 = d1.Mid;
                    sdlTmp.High32 = d1.High;
                    sdlTmp.int64 <<= iCurScale;
                    rgulRem[2] = sdlTmp.High32;
                    rgulRem[3] = (d1.High >> (31 - iCurScale)) >> 1;

                    sdlDivisor.Low32 = rgulDivisor[0];
                    sdlDivisor.High32 = rgulDivisor[1];
                    sdlDivisor.int64 <<= iCurScale;

                    if (rgulDivisor[2] == 0)
                    {
                        // Have a 64-bit divisor in sdlDivisor.  The remainder 
                        // (currently 96 bits spread over 4 ULONGs) will be < divisor.
                        // 
                        sdlTmp.Low32 = rgulRem[2];
                        sdlTmp.High32 = rgulRem[3];

                        rgulQuo[2] = 0;
                        rgulQuo[1] = Div96By64(new ArraySegment<uint>(rgulRem, 1, 3), sdlDivisor);
                        rgulQuo[0] = Div96By64(rgulRem, sdlDivisor);

                        for (;;)
                        {
                            if ((rgulRem[0] | rgulRem[1]) == 0)
                            {
                                if (iScale < 0)
                                {
                                    iCurScale = Math.Min(9, -iScale);
                                    goto HaveScale64;
                                }
                                break;
                            }

                            // We need to unscale if and only if we have a non-zero remainder
                            fUnscale = true;

                            // Remainder is non-zero.  Scale up quotient and remainder by 
                            // powers of 10 so we can compute more significant bits.
                            // 
                            iCurScale = SearchScale(rgulQuo[2], rgulQuo[1], rgulQuo[0], iScale);
                            if (iCurScale == 0)
                            {
                                // No more scaling to be done, but remainder is non-zero.
                                // Round quotient.
                                //
                                sdlTmp.Low32 = rgulRem[0];
                                sdlTmp.High32 = rgulRem[1];
                                if (sdlTmp.High32 >= 0x80000000 || (sdlTmp.int64 <<= 1) > sdlDivisor.int64 ||
                                    (sdlTmp.int64 == sdlDivisor.int64 && (rgulQuo[0] & 1) != 0))
                                    RoundUp(rgulQuo, ref iScale);
                                break;
                            }

                            if (iCurScale < 0)
                                throw new OverflowException(SR.Overflow_Decimal);

                            HaveScale64:
                            ulPwr = s_powers10[iCurScale];
                            iScale += iCurScale;

                            if (IncreaseScale(rgulQuo, ulPwr) != 0)
                                throw new OverflowException(SR.Overflow_Decimal);

                            rgulRem[2] = 0;  // rem is 64 bits, IncreaseScale uses 96
                            IncreaseScale(rgulRem, ulPwr);
                            ulTmp = Div96By64(rgulRem, sdlDivisor);
                            if (!Add32To96(rgulQuo, ulTmp))
                            {
                                if (iScale == 0)
                                    throw new OverflowException(SR.Overflow_Decimal);
                                iScale--;
                                OverflowUnscale(rgulQuo, (rgulRem[0] != 0 || rgulRem[1] != 0));
                                break;
                            }
                        } // for (;;)
                    }
                    else
                    {
                        // Have a 96-bit divisor in rgulDivisor[].
                        //
                        // Start by finishing the shift left by iCurScale.
                        //
                        sdlTmp.Low32 = rgulDivisor[1];
                        sdlTmp.High32 = rgulDivisor[2];
                        sdlTmp.int64 <<= iCurScale;
                        rgulDivisor[0] = sdlDivisor.Low32;
                        rgulDivisor[1] = sdlDivisor.High32;
                        rgulDivisor[2] = sdlTmp.High32;

                        // The remainder (currently 96 bits spread over 4 ULONGs) 
                        // will be < divisor.
                        //
                        rgulQuo[2] = 0;
                        rgulQuo[1] = 0;
                        rgulQuo[0] = Div128By96(rgulRem, rgulDivisor);

                        for (;;)
                        {
                            if ((rgulRem[0] | rgulRem[1] | rgulRem[2]) == 0)
                            {
                                if (iScale < 0)
                                {
                                    iCurScale = Math.Min(9, -iScale);
                                    goto HaveScale96;
                                }
                                break;
                            }

                            // We need to unscale if and only if we have a non-zero remainder
                            fUnscale = true;

                            // Remainder is non-zero.  Scale up quotient and remainder by 
                            // powers of 10 so we can compute more significant bits.
                            //
                            iCurScale = SearchScale(rgulQuo[2], rgulQuo[1], rgulQuo[0], iScale);
                            if (iCurScale == 0)
                            {
                                // No more scaling to be done, but remainder is non-zero.
                                // Round quotient.
                                //
                                if (rgulRem[2] >= 0x80000000)
                                {
                                    RoundUp(rgulQuo, ref iScale);
                                    break;
                                }

                                ulTmp = (rgulRem[0] > 0x80000000) ? 1u : 0u;
                                ulTmp1 = (rgulRem[1] > 0x80000000) ? 1u : 0u;
                                rgulRem[0] <<= 1;
                                rgulRem[1] = (rgulRem[1] << 1) + ulTmp;
                                rgulRem[2] = (rgulRem[2] << 1) + ulTmp1;

                                if (rgulRem[2] > rgulDivisor[2] || rgulRem[2] == rgulDivisor[2] &&
                                    (rgulRem[1] > rgulDivisor[1] || rgulRem[1] == rgulDivisor[1] &&
                                     (rgulRem[0] > rgulDivisor[0] || rgulRem[0] == rgulDivisor[0] &&
                                      (rgulQuo[0] & 1) != 0)))
                                    RoundUp(rgulQuo, ref iScale);
                                break;
                            }

                            if (iCurScale < 0)
                                throw new OverflowException(SR.Overflow_Decimal);

                            HaveScale96:
                            ulPwr = s_powers10[iCurScale];
                            iScale += iCurScale;

                            if (IncreaseScale(rgulQuo, ulPwr) != 0)
                                throw new OverflowException(SR.Overflow_Decimal);

                            rgulRem[3] = IncreaseScale(rgulRem, ulPwr);
                            ulTmp = Div128By96(rgulRem, rgulDivisor);
                            if (!Add32To96(rgulQuo, ulTmp))
                            {
                                if (iScale == 0)
                                    throw new OverflowException(SR.Overflow_Decimal);
                                iScale--;
                                OverflowUnscale(rgulQuo, (rgulRem[0] != 0 || rgulRem[1] != 0 || rgulRem[2] != 0 || rgulRem[3] != 0));
                                break;
                            }
                        } // for (;;)
                    }
                }

                // We need to unscale if and only if we have a non-zero remainder
                if (fUnscale)
                {
                    // Try extracting any extra powers of 10 we may have 
                    // added.  We do this by trying to divide out 10^8, 10^4, 10^2, and 10^1.
                    // If a division by one of these powers returns a zero remainder, then
                    // we keep the quotient.  If the remainder is not zero, then we restore
                    // the previous value.
                    // 
                    // Since 10 = 2 * 5, there must be a factor of 2 for every power of 10
                    // we can extract.  We use this as a quick test on whether to try a
                    // given power.
                    // 
                    while ((rgulQuo[0] & 0xFF) == 0 && iScale >= 8)
                    {
                        rgulQuoSave[0] = rgulQuo[0];
                        rgulQuoSave[1] = rgulQuo[1];
                        rgulQuoSave[2] = rgulQuo[2];

                        if (Div96By32(rgulQuoSave, 100000000) == 0)
                        {
                            rgulQuo[0] = rgulQuoSave[0];
                            rgulQuo[1] = rgulQuoSave[1];
                            rgulQuo[2] = rgulQuoSave[2];
                            iScale -= 8;
                        }
                        else
                            break;
                    }

                    if ((rgulQuo[0] & 0xF) == 0 && iScale >= 4)
                    {
                        rgulQuoSave[0] = rgulQuo[0];
                        rgulQuoSave[1] = rgulQuo[1];
                        rgulQuoSave[2] = rgulQuo[2];

                        if (Div96By32(rgulQuoSave, 10000) == 0)
                        {
                            rgulQuo[0] = rgulQuoSave[0];
                            rgulQuo[1] = rgulQuoSave[1];
                            rgulQuo[2] = rgulQuoSave[2];
                            iScale -= 4;
                        }
                    }

                    if ((rgulQuo[0] & 3) == 0 && iScale >= 2)
                    {
                        rgulQuoSave[0] = rgulQuo[0];
                        rgulQuoSave[1] = rgulQuo[1];
                        rgulQuoSave[2] = rgulQuo[2];

                        if (Div96By32(rgulQuoSave, 100) == 0)
                        {
                            rgulQuo[0] = rgulQuoSave[0];
                            rgulQuo[1] = rgulQuoSave[1];
                            rgulQuo[2] = rgulQuoSave[2];
                            iScale -= 2;
                        }
                    }

                    if ((rgulQuo[0] & 1) == 0 && iScale >= 1)
                    {
                        rgulQuoSave[0] = rgulQuo[0];
                        rgulQuoSave[1] = rgulQuo[1];
                        rgulQuoSave[2] = rgulQuo[2];

                        if (Div96By32(rgulQuoSave, 10) == 0)
                        {
                            rgulQuo[0] = rgulQuoSave[0];
                            rgulQuo[1] = rgulQuoSave[1];
                            rgulQuo[2] = rgulQuoSave[2];
                            iScale -= 1;
                        }
                    }
                }

                d1.Sign = d1.Sign ^ d2.Sign;
                d1.High = rgulQuo[2];
                d1.Mid = rgulQuo[1];
                d1.Low = rgulQuo[0];
                d1.Scale = iScale;
            }

            //**********************************************************************
            // VarDecInt - Decimal Int (round down to integer)
            //**********************************************************************
            internal static void VarDecInt(ref Decimal d)
            {
                Decimal result = new Decimal();

                if (DecCalc.DecFixInt(ref d, ref result) != 0 && result.Sign)
                    // We have chopped off a non-zero amount from a negative value.  Since
                    // we round toward -infinity, we must increase the integer result by
                    // 1 to make it more negative.  This will never overflow because
                    // in order to have a remainder, we must have had a non-zero scale factor.
                    // Our scale factor is back to zero now.
                    // 
                    if (++result.Low64 == 0)
                        result.High++;

                d = result;
            }

            //**********************************************************************
            // VarDecFix - Decimal Fix (chop to integer)
            //**********************************************************************
            internal static void VarDecFix(ref Decimal d)
            {
                Decimal result = new Decimal();
                DecFixInt(ref d, ref result);
                d = result;
            }

            //**********************************************************************
            // VarDecRound - Decimal Round
            //**********************************************************************
            internal static void VarDecRound(ref Decimal input, int decimals, ref Decimal result)
            {
                uint[] tmpNum = new uint[3];
                uint remainder;
                uint sticky;
                uint power;
                int scale;

                System.Diagnostics.Debug.Assert(decimals >= 0);

                scale = input.Scale - decimals;
                if (scale > 0)
                {
                    tmpNum[0] = input.ulo;
                    tmpNum[1] = input.umid;
                    tmpNum[2] = input.uhi;
                    result.Sign = input.Sign;
                    remainder = sticky = 0;

                    do
                    {
                        sticky |= remainder;
                        if (scale > MaxInt32Scale)
                            power = TenToPowerNine;
                        else
                            power = s_powers10[scale];

                        remainder = Div96By32(tmpNum, power);
                        scale -= MaxInt32Scale;
                    } while (scale > 0);

                    // Now round.  ulRem has last remainder, ulSticky has sticky bits.        
                    // To do IEEE rounding, we add LSB of result to sticky bits so        
                    // either causes round up if remainder * 2 == last divisor.        
                    sticky |= tmpNum[0] & 1;
                    remainder = (remainder << 1) + (uint)(sticky != 0 ? 1 : 0);
                    if (power < remainder
                        && ++tmpNum[0] == 0
                        && ++tmpNum[1] == 0)
                        ++tmpNum[2];

                    result.ulo = tmpNum[0];
                    result.umid = tmpNum[1];
                    result.uhi = tmpNum[2];
                    result.Scale = decimals;
                    return;
                }

                result = input;
            }

            //**********************************************************************
            // VarDecMod - Computes the remainder between two decimals
            //**********************************************************************
            internal static Decimal VarDecMod(ref Decimal d1, ref Decimal d2)
            {
                // OleAut doesn't provide a VarDecMod.            

                // In the operation x % y the sign of y does not matter. Result will have the sign of x.
                d2.uflags = (d2.uflags & ~SignMask) | (d1.uflags & SignMask);


                // This piece of code is to work around the fact that Dividing a decimal with 28 digits number by decimal which causes
                // causes the result to be 28 digits, can cause to be incorrectly rounded up.
                // eg. Decimal.MaxValue / 2 * Decimal.MaxValue will overflow since the division by 2 was rounded instead of being truncked.
                if (Abs(d1) < Abs(d2))
                {
                    return d1;
                }
                d1 -= d2;

                if (d1 == 0)
                {
                    // The sign of D1 will be wrong here. Fall through so that we still get a DivideByZeroException
                    d1.uflags = (d1.uflags & ~SignMask) | (d2.uflags & SignMask);
                }

                // Formula:  d1 - (RoundTowardsZero(d1 / d2) * d2)            
                Decimal dividedResult = Truncate(d1 / d2);
                Decimal multipliedResult = dividedResult * d2;
                Decimal result = d1 - multipliedResult;
                // See if the result has crossed 0
                if ((d1.uflags & SignMask) != (result.uflags & SignMask))
                {
                    if (NearNegativeZero <= result && result <= NearPositiveZero)
                    {
                        // Certain Remainder operations on decimals with 28 significant digits round
                        // to [+-]0.000000000000000000000000001m instead of [+-]0m during the intermediate calculations. 
                        // 'zero' results just need their sign corrected.
                        result.uflags = (result.uflags & ~SignMask) | (d1.uflags & SignMask);
                    }
                    else
                    {
                        // If the division rounds up because it runs out of digits, the multiplied result can end up with a larger
                        // absolute value and the result of the formula crosses 0. To correct it can add the divisor back.
                        result += d2;
                    }
                }

                return result;
            }


            // This method does a 'raw' and 'unchecked' addition of a UInt32 to a Decimal in place. 
            // 'raw' means that it operates on the internal 96-bit unsigned integer value and 
            // ingores the sign and scale. This means that it is not equivalent to just adding
            // that number, as the sign and scale are effectively applied to the UInt32 value also.
            // 'unchecked' means that it does not fail if you overflow the 96 bit value.
            private static void InternalAddUInt32RawUnchecked(ref Decimal value, UInt32 i)
            {
                UInt32 v;
                UInt32 sum;
                v = value.ulo;
                sum = v + i;
                value.ulo = sum;
                if (sum < v || sum < i)
                {
                    v = value.umid;
                    sum = v + 1;
                    value.umid = sum;
                    if (sum < v || sum < 1)
                    {
                        value.uhi = value.uhi + 1;
                    }
                }
            }

            // This method does an in-place division of a decimal by a UInt32, returning the remainder. 
            // Although it does not operate on the sign or scale, this does not result in any 
            // caveat for the result. It is equivalent to dividing by that number.
            private static UInt32 InternalDivRemUInt32(ref Decimal value, UInt32 divisor)
            {
                UInt32 remainder = 0;
                UInt64 n;
                if (value.uhi != 0)
                {
                    n = value.uhi;
                    value.uhi = (UInt32)(n / divisor);
                    remainder = (UInt32)(n % divisor);
                }
                if (value.umid != 0 || remainder != 0)
                {
                    n = ((UInt64)remainder << 32) | value.umid;
                    value.umid = (UInt32)(n / divisor);
                    remainder = (UInt32)(n % divisor);
                }
                if (value.ulo != 0 || remainder != 0)
                {
                    n = ((UInt64)remainder << 32) | value.ulo;
                    value.ulo = (UInt32)(n / divisor);
                    remainder = (UInt32)(n % divisor);
                }
                return remainder;
            }

            // Does an in-place round the specified number of digits, rounding mid-point values
            // away from zero
            internal static void InternalRoundFromZero(ref Decimal d, int decimalCount)
            {
                Int32 scale = d.Scale;
                Int32 scaleDifference = scale - decimalCount;
                if (scaleDifference <= 0)
                {
                    return;
                }
                // Divide the value by 10^scaleDifference
                UInt32 lastRemainder;
                UInt32 lastDivisor;
                do
                {
                    Int32 diffChunk = (scaleDifference > MaxInt32Scale) ? MaxInt32Scale : scaleDifference;
                    lastDivisor = s_powers10[diffChunk];
                    lastRemainder = InternalDivRemUInt32(ref d, lastDivisor);
                    scaleDifference -= diffChunk;
                } while (scaleDifference > 0);

                // Round away from zero at the mid point
                if (lastRemainder >= (lastDivisor >> 1))
                {
                    InternalAddUInt32RawUnchecked(ref d, 1);
                }

                // the scale becomes the desired decimal count
                d.Scale = decimalCount;
            }

            #region Number Formatting helpers

            private static uint D32DivMod1E9(uint hi32, ref uint lo32)
            {
                ulong n = (ulong)hi32 << 32 | lo32;
                lo32 = (uint)(n / 1000000000);
                return (uint)(n % 1000000000);
            }

            internal static uint DecDivMod1E9(ref Decimal value)
            {
                return D32DivMod1E9(D32DivMod1E9(D32DivMod1E9(0,
                                                              ref value.uhi),
                                                 ref value.umid),
                                    ref value.ulo);
            }

            internal static void DecAddInt32(ref Decimal value, uint i)
            {
                if (D32AddCarry(ref value.ulo, i))
                {
                    if (D32AddCarry(ref value.umid, 1))
                        D32AddCarry(ref value.uhi, 1);
                }
            }

            private static bool D32AddCarry(ref uint value, uint i)
            {
                uint v = value;
                uint sum = v + i;
                value = sum;
                return (sum < v) || (sum < i);
            }

            internal static void DecMul10(ref Decimal value)
            {
                Decimal d = value;
                DecShiftLeft(ref value);
                DecShiftLeft(ref value);
                DecAdd(ref value, d);
                DecShiftLeft(ref value);
            }

            private static void DecShiftLeft(ref Decimal value)
            {
                uint c0 = (value.Low & 0x80000000) != 0 ? 1u : 0u;
                uint c1 = (value.Mid & 0x80000000) != 0 ? 1u : 0u;
                value.Low = value.Low << 1;
                value.Mid = (value.Mid << 1) | c0;
                value.High = (value.High << 1) | c1;
            }

            private static void DecAdd(ref Decimal value, Decimal d)
            {
                if (D32AddCarry(ref value.ulo, d.Low))
                {
                    if (D32AddCarry(ref value.umid, 1))
                        D32AddCarry(ref value.uhi, 1);
                }

                if (D32AddCarry(ref value.umid, d.Mid))
                    D32AddCarry(ref value.uhi, 1);

                D32AddCarry(ref value.uhi, d.High);
            }

            #endregion

            private struct Split64
            {
                internal ulong int64;

                public Split64(ulong value)
                {
                    int64 = value;
                }

                public uint Low32
                {
                    get { return (uint)int64; }
                    set { int64 = (int64 & 0xffffffff00000000) | value; }
                }

                public uint High32
                {
                    get { return (uint)(int64 >> 32); }
                    set { int64 = (int64 & 0x00000000ffffffff) | ((ulong)value << 32); }
                }
            }

            private static class PowerOvfl
            {
                private static uint[] s_powerOvfl =
                {
                    // This is a table of the largest values that can be in the upper two
                    // ULONGs of a 96-bit number that will not overflow when multiplied
                    // by a given power.  For the upper word, this is a table of 
                    // 2^32 / 10^n for 1 <= n <= 9.  For the lower word, this is the
                    // remaining fraction part * 2^32.  2^32 = 4294967296.
                    //
                    // Table logically consists of three components for each entry (high,
                    // mid and low) but we declare it as a flat array of uints since that's
                    // all C# will support. We hide this via the accessor methods on this
                    // class.
                    //
                    429496729, 2576980377, 2576980377,  // 10^1 remainder 0.6
                    42949672,  4123168604, 687194767,   // 10^2 remainder 0.16
                    4294967,   1271310319, 2645699854,  // 10^3 remainder 0.616
                    429496,    3133608139, 694066715,   // 10^4 remainder 0.1616
                    42949,     2890341191, 2216890319,  // 10^5 remainder 0.51616
                    4294,      4154504685, 2369172679,  // 10^6 remainder 0.551616
                    429,       2133437386, 4102387834,  // 10^7 remainder 0.9551616
                    42,        4078814305, 410238783,   // 10^8 remainder 0.09991616
                    4,         1266874889, 3047500985,  // 10^9 remainder 0.709551616
                };

                public static uint Hi(int index)
                {
                    return s_powerOvfl[index * 3];
                }

                public static uint Mid(int index)
                {
                    return s_powerOvfl[(index * 3) + 1];
                }

                public static uint Lo(int index)
                {
                    return s_powerOvfl[(index * 3) + 2];
                }
            }
        }
    }
}
