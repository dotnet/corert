// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// Math helpers for generated code. The helpers marked with [RuntimeExport] and the type
    /// itself need to be public because they constitute a public contract with the .NET Native toolchain.
    /// </summary>
    [CLSCompliant(false)]
    public static class MathHelpers
    {
#if !BIT64
        //
        // 64-bit checked multiplication for 32-bit platforms
        //

        // Helper to multiply two 32-bit uints
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static UInt64 Mul32x32To64(UInt32 a, UInt32 b)
        {
            return a * (UInt64)b;
        }

        // Helper to get high 32-bit of 64-bit int
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static UInt32 Hi32Bits(Int64 a)
        {
            return (UInt32)(a >> 32);
        }

        // Helper to get high 32-bit of 64-bit int
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static UInt32 Hi32Bits(UInt64 a)
        {
            return (UInt32)(a >> 32);
        }

        [RuntimeExport("LMulOvf")]
        public static Int64 LMulOvf(Int64 i, Int64 j)
        {
            Int64 ret;

            // Remember the sign of the result
            Int32 sign = (Int32)(Hi32Bits(i) ^ Hi32Bits(j));

            // Convert to unsigned multiplication
            if (i < 0) i = -i;
            if (j < 0) j = -j;

            // Get the upper 32 bits of the numbers
            UInt32 val1High = Hi32Bits(i);
            UInt32 val2High = Hi32Bits(j);

            UInt64 valMid;

            if (val1High == 0) {
                // Compute the 'middle' bits of the long multiplication
                valMid = Mul32x32To64(val2High, (UInt32)i);
            }
            else {
                if (val2High != 0)
                    goto ThrowExcep;
                // Compute the 'middle' bits of the long multiplication
                valMid = Mul32x32To64(val1High, (UInt32)j);
            }

            // See if any bits after bit 32 are set
            if (Hi32Bits(valMid) != 0)
                goto ThrowExcep;

            ret = (Int64)(Mul32x32To64((UInt32)i, (UInt32)j) + (valMid << 32));

            // check for overflow
            if (Hi32Bits(ret) < (UInt32)valMid)
                goto ThrowExcep;

            if (sign >= 0) {
                // have we spilled into the sign bit?
                if (ret < 0)
                    goto ThrowExcep;
            }
            else {
                ret = -ret;
                // have we spilled into the sign bit?
                if (ret > 0)
                    goto ThrowExcep;
            }
            return ret;

        ThrowExcep:
            return ThrowLngOvf();
        }

        [RuntimeExport("ULMulOvf")]
        public static UInt64 ULMulOvf(UInt64 i, UInt64 j)
        {
            UInt64 ret;

            // Get the upper 32 bits of the numbers
            UInt32 val1High = Hi32Bits(i);
            UInt32 val2High = Hi32Bits(j);

            UInt64 valMid;

            if (val1High == 0) {
                if (val2High == 0)
                    return Mul32x32To64((UInt32)i, (UInt32)j);
                // Compute the 'middle' bits of the long multiplication
                valMid = Mul32x32To64(val2High, (UInt32)i);
            }
            else {
                if (val2High != 0)
                    goto ThrowExcep;
                // Compute the 'middle' bits of the long multiplication
                valMid = Mul32x32To64(val1High, (UInt32)j);
            }

                // See if any bits after bit 32 are set
            if (Hi32Bits(valMid) != 0)
                goto ThrowExcep;

            ret = Mul32x32To64((UInt32)i, (UInt32)j) + (valMid << 32);

            // check for overflow
            if (Hi32Bits(ret) < (UInt32)valMid)
                goto ThrowExcep;
            return ret;

        ThrowExcep:
            return ThrowULngOvf();
        }
#endif // BIT64

        [RuntimeExport("Dbl2IntOvf")]
        public static int Dbl2IntOvf(double val)
        {
            const double two31 = 2147483648.0;

            // Note that this expression also works properly for val = NaN case
            if (val > -two31 - 1 && val < two31)
                return unchecked((int)val);

            return ThrowIntOvf();
        }

        [RuntimeExport("Dbl2LngOvf")]
        public static long Dbl2LngOvf(double val)
        {
            const double two63  = 2147483648.0 * 4294967296.0;

            // Note that this expression also works properly for val = NaN case
            // We need to compare with the very next double to two63. 0x402 is epsilon to get us there.
            if (val > -two63 - 0x402 && val < two63)
                return unchecked((long)val);

            return ThrowLngOvf();
        }

        [RuntimeExport("Dbl2ULngOvf")]
        public static ulong Dbl2ULngOvf(double val)
        {
            const double two64  = 2.0* 2147483648.0 * 4294967296.0;
 
            // Note that this expression also works properly for val = NaN case
            if (val < two64)
                return unchecked((ulong)val);

            return ThrowULngOvf();
        }

        [RuntimeExport("Flt2IntOvf")]
        public static int Flt2IntOvf(float val)
        {
            const double two31 = 2147483648.0;

            // Note that this expression also works properly for val = NaN case
            if (val > -two31 - 1 && val < two31)
                return ((int)val);

            return ThrowIntOvf();
        }

        [RuntimeExport("Flt2LngOvf")]
        public static long Flt2LngOvf(float val)
        {
            const double two63 = 2147483648.0 * 4294967296.0;

            // Note that this expression also works properly for val = NaN case
            // We need to compare with the very next double to two63. 0x402 is epsilon to get us there.
            if (val > -two63 - 0x402 && val < two63)
                return ((Int64)val);

            return ThrowIntOvf();
        }

        //
        // Matching return types of throw helpers enables tailcalling them. It improves performance 
        // of the hot path because of it does not need to raise full stackframe.
        //

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ThrowIntOvf()
        {
            throw new OverflowException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ThrowLngOvf()
        {
            throw new OverflowException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ulong ThrowULngOvf()
        {
            throw new OverflowException();
        }
    }
}
