// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// Math helpers for generated code.
    /// </summary>
    internal static class MathHelpers
    {
        private static int Dbl2IntOvf(double val)
        {
            const double two31 = 2147483648.0;

            // Note that this expression also works properly for val = NaN case
            if (val > -two31 - 1 && val < two31)
                return unchecked((int)val);

            return ThrowInfOvf();
        }

        private static long Dbl2LngOvf(double val)
        {
            const double two63  = 2147483648.0 * 4294967296.0;

            // Note that this expression also works properly for val = NaN case
            // We need to compare with the very next double to two63. 0x402 is epsilon to get us there.
            if (val > -two63 - 0x402 && val < two63)
                return unchecked((long)val);

            return ThrowLngOvf();
        }

        private static ulong Dbl2ULngOvf(double val)
        {
            const double two64  = 2.0* 2147483648.0 * 4294967296.0;
 
            // Note that this expression also works properly for val = NaN case
            if (val < two64)
                return unchecked((ulong)val);

            return ThrowULngOvf();
        }

        //
        // Matching return types of throw helpers enables tailcalling them. It improves performance 
        // of the hot path because of it does not need to raise full stackframe.
        //
 
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ThrowInfOvf()
        {
            throw new IndexOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ThrowLngOvf()
        {
            throw new IndexOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ulong ThrowULngOvf()
        {
            throw new IndexOutOfRangeException();
        }
    }
}
