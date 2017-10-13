// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: 
** This class is used to represent a Dynamic TimeZone.  It
** has methods for converting a DateTime between TimeZones.
**
**
============================================================*/

using Microsoft.Win32;
using System;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using TIME_ZONE_INFORMATION = Interop.mincore.TIME_ZONE_INFORMATION;
using TIME_DYNAMIC_ZONE_INFORMATION = Interop.mincore.TIME_DYNAMIC_ZONE_INFORMATION;

namespace System
{
    sealed public partial class TimeZoneInfo
    {
        // ---- SECTION: public methods --------------*

        //
        // GetAdjustmentRules -
        //
        // returns a cloned array of AdjustmentRule objects
        //
        public AdjustmentRule[] GetAdjustmentRules()
        {
            if (_adjustmentRules == null)
            {
                return Array.Empty<AdjustmentRule>();
            }

            return (AdjustmentRule[])_adjustmentRules.Clone();
        }

        private static void PopulateAllSystemTimeZones(CachedData cachedData)
        {
            Debug.Assert(Monitor.IsEntered(cachedData));

            uint index = 0;
            TIME_DYNAMIC_ZONE_INFORMATION tdzi;
            while (Interop.mincore.EnumDynamicTimeZoneInformation(index, out tdzi) != Interop.Errors.ERROR_NO_MORE_ITEMS)
            {
                TimeZoneInformation timeZoneInformation = new TimeZoneInformation(tdzi);
                TimeZoneInfo value;
                Exception e;
                TimeZoneInfoResult result = TryGetTimeZone(ref timeZoneInformation, false, out value, out e, cachedData);
                index++;
            }
        }

        public static TimeZoneInfo FindSystemTimeZoneById(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }
            if (id.Length == 0 || id.Length > 255 || id.Contains("\0"))
            {
                throw new TimeZoneNotFoundException(SR.Format(SR.TimeZoneNotFound_MissingRegistryData, id));
            }

            //
            // Check first the Utc Ids and return the cached one because in GetCorrespondingKind 
            // we use reference equality
            // 

            if (id.Equals(TimeZoneInfo.Utc.Id, StringComparison.OrdinalIgnoreCase))
                return TimeZoneInfo.Utc;

            TimeZoneInfo value;
            CachedData cache = s_cachedData;

            lock (cache)
            {
                // Use the current cache if it exists
                if (cache._systemTimeZones != null)
                {
                    if (cache._systemTimeZones.TryGetValue(id, out value))
                    {
                        return value;
                    }
                }
                // See if the cache was fully filled, if not, fill it then check again.
                if (!cache._allSystemTimeZonesRead)
                {
                    PopulateAllSystemTimeZones(cache);
                    cache._allSystemTimeZonesRead = true;
                    if (cache._systemTimeZones != null && cache._systemTimeZones.TryGetValue(id, out value))
                    {
                        return value;
                    }
                }
            }
            throw new TimeZoneNotFoundException(SR.Format(SR.TimeZoneNotFound_MissingRegistryData, id));
        }

        private static bool EqualStandardDates(TimeZoneInformation timeZone, ref TIME_DYNAMIC_ZONE_INFORMATION tdzi)
        {
            return timeZone.Dtzi.Bias == tdzi.Bias
                   && timeZone.Dtzi.StandardBias == tdzi.StandardBias
                   && timeZone.Dtzi.StandardDate.wYear == tdzi.StandardDate.wYear
                   && timeZone.Dtzi.StandardDate.wMonth == tdzi.StandardDate.wMonth
                   && timeZone.Dtzi.StandardDate.wDayOfWeek == tdzi.StandardDate.wDayOfWeek
                   && timeZone.Dtzi.StandardDate.wDay == tdzi.StandardDate.wDay
                   && timeZone.Dtzi.StandardDate.wHour == tdzi.StandardDate.wHour
                   && timeZone.Dtzi.StandardDate.wMinute == tdzi.StandardDate.wMinute
                   && timeZone.Dtzi.StandardDate.wSecond == tdzi.StandardDate.wSecond
                   && timeZone.Dtzi.StandardDate.wMilliseconds == tdzi.StandardDate.wMilliseconds;
        }

        private static bool EqualDaylightDates(TimeZoneInformation timeZone, ref TIME_DYNAMIC_ZONE_INFORMATION tdzi)
        {
            return timeZone.Dtzi.DaylightBias == tdzi.DaylightBias
                    && timeZone.Dtzi.DaylightDate.wYear == tdzi.DaylightDate.wYear
                    && timeZone.Dtzi.DaylightDate.wMonth == tdzi.DaylightDate.wMonth
                    && timeZone.Dtzi.DaylightDate.wDayOfWeek == tdzi.DaylightDate.wDayOfWeek
                    && timeZone.Dtzi.DaylightDate.wDay == tdzi.DaylightDate.wDay
                    && timeZone.Dtzi.DaylightDate.wHour == tdzi.DaylightDate.wHour
                    && timeZone.Dtzi.DaylightDate.wMinute == tdzi.DaylightDate.wMinute
                    && timeZone.Dtzi.DaylightDate.wSecond == tdzi.DaylightDate.wSecond
                    && timeZone.Dtzi.DaylightDate.wMilliseconds == tdzi.DaylightDate.wMilliseconds;
        }

        private static bool CheckDaylightSavingTimeNotSupported(TimeZoneInformation timeZone)
        {
            return (timeZone.Dtzi.DaylightDate.wYear == timeZone.Dtzi.StandardDate.wYear
                    && timeZone.Dtzi.DaylightDate.wMonth == timeZone.Dtzi.StandardDate.wMonth
                    && timeZone.Dtzi.DaylightDate.wDayOfWeek == timeZone.Dtzi.StandardDate.wDayOfWeek
                    && timeZone.Dtzi.DaylightDate.wDay == timeZone.Dtzi.StandardDate.wDay
                    && timeZone.Dtzi.DaylightDate.wHour == timeZone.Dtzi.StandardDate.wHour
                    && timeZone.Dtzi.DaylightDate.wMinute == timeZone.Dtzi.StandardDate.wMinute
                    && timeZone.Dtzi.DaylightDate.wSecond == timeZone.Dtzi.StandardDate.wSecond
                    && timeZone.Dtzi.DaylightDate.wMilliseconds == timeZone.Dtzi.StandardDate.wMilliseconds);
        }

        //
        // enumerate all time zones till find a match and with valid key name
        //
        internal static unsafe bool FindMatchToCurrentTimeZone(TimeZoneInformation timeZoneInformation)
        {
            uint index = 0;
            uint result = 0; // ERROR_SUCCESS
            bool notSupportedDaylightSaving = CheckDaylightSavingTimeNotSupported(timeZoneInformation);
            TIME_DYNAMIC_ZONE_INFORMATION tdzi = new TIME_DYNAMIC_ZONE_INFORMATION();

            while (result == 0)
            {
                result = Interop.mincore.EnumDynamicTimeZoneInformation(index, out tdzi);
                if (result == 0)
                {
                    string s = new String(tdzi.StandardName);

                    if (!String.IsNullOrEmpty(s) &&
                        EqualStandardDates(timeZoneInformation, ref tdzi) &&
                        (notSupportedDaylightSaving || EqualDaylightDates(timeZoneInformation, ref tdzi)) &&
                        String.Compare(s, timeZoneInformation.StandardName, StringComparison.Ordinal) == 0)
                    {
                        // found a match
                        timeZoneInformation.TimeZoneKeyName = s;
                        return true;
                    }
                }
                index++;
            }

            return false;
        }

        //
        // GetLocalTimeZone -
        //
        // Helper function for retrieving the local system time zone.
        //
        // returns a new TimeZoneInfo instance
        //
        // may throw COMException, TimeZoneNotFoundException, InvalidTimeZoneException
        //
        // assumes cachedData lock is taken
        //

        private static TimeZoneInfo GetLocalTimeZone(CachedData cachedData)
        {
            ////
            //// Try using the "mincore!GetDynamicTimeZoneInformation" API to get the "id"
            ////
            TimeZoneInformation timeZoneInformation;
            if (!GetTimeZoneInfo(out timeZoneInformation))
            {
                return CreateCustomTimeZone(c_localId, TimeSpan.Zero, c_localId, c_localId);
            }

            Boolean dstDisabled = timeZoneInformation.Dtzi.DynamicDaylightTimeDisabled != 0;

            //// check to see if we can use the key name returned from the API call
            if (!String.IsNullOrEmpty(timeZoneInformation.TimeZoneKeyName) || FindMatchToCurrentTimeZone(timeZoneInformation))
            {
                TimeZoneInfo zone = null;
                Exception ex;

                if (TryGetTimeZone(ref timeZoneInformation, dstDisabled, out zone, out ex, cachedData) == TimeZoneInfoResult.Success)
                {
                    return zone;
                }
            }

            // Fall back to using the data from the Win32 API
            return GetLocalTimeZoneFromWin32Data(timeZoneInformation, dstDisabled);
        }
    } // TimezoneInfo
} // namespace System
