// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct TIME_ZONE_INFORMATION
        {
            internal int Bias;
            internal fixed char StandardName[32];
            internal SYSTEMTIME StandardDate;
            internal int StandardBias;
            internal fixed char DaylightName[32];
            internal SYSTEMTIME DaylightDate;
            internal int DaylightBias;

            public unsafe TIME_ZONE_INFORMATION(TIME_DYNAMIC_ZONE_INFORMATION dtzi)
            {
                Bias = dtzi.Bias;
                fixed (char* standard = StandardName)
                {
                    for (int i = 0; i < 32; ++i)
                    {
                        standard[i] = dtzi.StandardName[i];
                    }
                }
                fixed (char* daylight = DaylightName)
                {
                    for (int i = 0; i < 32; ++i)
                    {
                        daylight[i] = dtzi.DaylightName[i];
                    }
                }
                StandardDate = dtzi.StandardDate;
                StandardBias = dtzi.StandardBias;
                DaylightDate = dtzi.DaylightDate;
                DaylightBias = dtzi.DaylightBias;
            }
        }

        // TimeZone
        internal const int TIME_ZONE_ID_INVALID = -1;
        internal const int TIME_ZONE_ID_UNKNOWN = 0;
        internal const int TIME_ZONE_ID_STANDARD = 1;
        internal const int TIME_ZONE_ID_DAYLIGHT = 2;

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint EnumDynamicTimeZoneInformation(uint dwIndex, out TIME_DYNAMIC_ZONE_INFORMATION lpTimeZoneInformation);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint GetDynamicTimeZoneInformation(out TIME_DYNAMIC_ZONE_INFORMATION pTimeZoneInformation);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static uint GetDynamicTimeZoneInformationEffectiveYears(ref TIME_DYNAMIC_ZONE_INFORMATION lpTimeZoneInformation, out uint FirstYear, out uint LastYear);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal extern static bool GetTimeZoneInformationForYear(ushort wYear, ref TIME_DYNAMIC_ZONE_INFORMATION pdtzi, out TIME_ZONE_INFORMATION ptzi);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll")]
        internal static extern int GetTimeZoneInformation(out TIME_ZONE_INFORMATION lpTimeZoneInformation);
    }
}
