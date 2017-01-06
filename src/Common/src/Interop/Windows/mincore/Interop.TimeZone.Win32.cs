// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct REGISTRY_TIME_ZONE_INFORMATION
        {
            public Int32 Bias;
            public Int32 StandardBias;
            public Int32 DaylightBias;
            public SYSTEMTIME StandardDate;
            public SYSTEMTIME DaylightDate;

            public REGISTRY_TIME_ZONE_INFORMATION(TIME_ZONE_INFORMATION tzi)
            {
                Bias = tzi.Bias;
                StandardDate = tzi.StandardDate;
                StandardBias = tzi.StandardBias;
                DaylightDate = tzi.DaylightDate;
                DaylightBias = tzi.DaylightBias;
            }

            public REGISTRY_TIME_ZONE_INFORMATION(Byte[] bytes)
            {
                //
                // typedef struct _REG_TZI_FORMAT {
                // [00-03]    LONG Bias;
                // [04-07]    LONG StandardBias;
                // [08-11]    LONG DaylightBias;
                // [12-27]    SYSTEMTIME StandardDate;
                // [12-13]        WORD wYear;
                // [14-15]        WORD wMonth;
                // [16-17]        WORD wDayOfWeek;
                // [18-19]        WORD wDay;
                // [20-21]        WORD wHour;
                // [22-23]        WORD wMinute;
                // [24-25]        WORD wSecond;
                // [26-27]        WORD wMilliseconds;
                // [28-43]    SYSTEMTIME DaylightDate;
                // [28-29]        WORD wYear;
                // [30-31]        WORD wMonth;
                // [32-33]        WORD wDayOfWeek;
                // [34-35]        WORD wDay;
                // [36-37]        WORD wHour;
                // [38-39]        WORD wMinute;
                // [40-41]        WORD wSecond;
                // [42-43]        WORD wMilliseconds;
                // } REG_TZI_FORMAT;
                //
                if (bytes == null || bytes.Length != 44)
                {
                    throw new ArgumentException(SR.Argument_InvalidREG_TZI_FORMAT, nameof(bytes));
                }
                Bias = ToInt32(bytes, 0);
                StandardBias = ToInt32(bytes, 4);
                DaylightBias = ToInt32(bytes, 8);

                StandardDate.wYear = (ushort)ToInt16(bytes, 12);
                StandardDate.wMonth = (ushort)ToInt16(bytes, 14);
                StandardDate.wDayOfWeek = (ushort)ToInt16(bytes, 16);
                StandardDate.wDay = (ushort)ToInt16(bytes, 18);
                StandardDate.wHour = (ushort)ToInt16(bytes, 20);
                StandardDate.wMinute = (ushort)ToInt16(bytes, 22);
                StandardDate.wSecond = (ushort)ToInt16(bytes, 24);
                StandardDate.wMilliseconds = (ushort)ToInt16(bytes, 26);

                DaylightDate.wYear = (ushort)ToInt16(bytes, 28);
                DaylightDate.wMonth = (ushort)ToInt16(bytes, 30);
                DaylightDate.wDayOfWeek = (ushort)ToInt16(bytes, 32);
                DaylightDate.wDay = (ushort)ToInt16(bytes, 34);
                DaylightDate.wHour = (ushort)ToInt16(bytes, 36);
                DaylightDate.wMinute = (ushort)ToInt16(bytes, 38);
                DaylightDate.wSecond = (ushort)ToInt16(bytes, 40);
                DaylightDate.wMilliseconds = (ushort)ToInt16(bytes, 42);
            }

            private static short ToInt16(byte[] value, int startIndex)
            {
                return (short)(value[startIndex] | (value[startIndex + 1] << 8));
            }

            private static int ToInt32(byte[] value, int startIndex)
            {
                return value[startIndex] | (value[startIndex + 1] << 8) | (value[startIndex + 2] << 16) | (value[startIndex + 3] << 24);
            }
        }
    }
}
