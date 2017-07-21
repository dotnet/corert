// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime;

namespace System.Globalization
{
    internal partial class FormatProvider
    {
        private partial class Number
        {
            private static unsafe void DoubleToNumber(double value, int precision, ref NumberBuffer number)
            {
                number.precision = precision;
                if (DoubleHelper.Exponent(value) == 0x7ff)
                {
                    number.scale = DoubleHelper.Mantissa(value) != 0 ? SCALE_NAN : SCALE_INF;
                    number.sign = DoubleHelper.Sign(value);
                    number.digits[0] = '\0';
                }
                else
                {
                    byte* src = stackalloc byte[_CVTBUFSIZE];
                    int sign;
                    fixed (NumberBuffer* pNumber = &number)
                    {
                        RuntimeImports._ecvt_s(src, _CVTBUFSIZE, value, precision, &pNumber->scale, &sign);
                    }
                    number.sign = sign != 0;

                    char* dst = number.digits;
                    if ((char)*src != '0')
                    {
                        while (*src != 0)
                            *dst++ = (char)*src++;
                    }
                    *dst = '\0';
                }
            }
        }
    }
}


