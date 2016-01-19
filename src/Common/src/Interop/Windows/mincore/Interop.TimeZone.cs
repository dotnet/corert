// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        internal struct SYSTEMTIME
        {
            internal ushort wYear;
            internal ushort wMonth;
            internal ushort wDayOfWeek;
            internal ushort wDay;
            internal ushort wHour;
            internal ushort wMinute;
            internal ushort wSecond;
            internal ushort wMilliseconds;
        }

        internal struct TIME_DYNAMIC_ZONE_INFORMATION
        {
            internal int Bias;
            internal fixed char StandardName[32];
            internal SYSTEMTIME StandardDate;
            internal int StandardBias;
            internal fixed char DaylightName[32];
            internal SYSTEMTIME DaylightDate;
            internal int DaylightBias;
            internal fixed char TimeZoneKeyName[128];
            internal byte DynamicDaylightTimeDisabled;
        }

        internal struct TIME_ZONE_INFORMATION
        {
            internal int Bias;
            internal fixed char StandardName[32];
            internal SYSTEMTIME StandardDate;
            internal int StandardBias;
            internal fixed char DaylightName[32];
            internal SYSTEMTIME DaylightDate;
            internal int DaylightBias;
        }

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint EnumDynamicTimeZoneInformation(uint dwIndex, out TIME_DYNAMIC_ZONE_INFORMATION lpTimeZoneInformation);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint GetDynamicTimeZoneInformation(out TIME_DYNAMIC_ZONE_INFORMATION pTimeZoneInformation);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint GetDynamicTimeZoneInformationEffectiveYears(ref TIME_DYNAMIC_ZONE_INFORMATION lpTimeZoneInformation, out uint FirstYear, out uint LastYear);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static bool GetTimeZoneInformationForYear(ushort wYear, ref TIME_DYNAMIC_ZONE_INFORMATION pdtzi, out TIME_ZONE_INFORMATION ptzi);
    }
}
