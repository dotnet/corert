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
#pragma warning disable 0420
        private sealed partial class CachedData
        {
            private static TimeZoneInfo GetCurrentOneYearLocal()
            {
                // load the data from the OS
                TimeZoneInfo match;

                TimeZoneInformation timeZoneInformation;
                if (!GetTimeZoneInfo(out timeZoneInformation))
                    match = CreateCustomTimeZone(LocalId, TimeSpan.Zero, LocalId, LocalId);
                else
                    match = GetLocalTimeZoneFromWin32Data(timeZoneInformation, false);
                return match;
            }

            private volatile OffsetAndRule _oneYearLocalFromUtc;

            public OffsetAndRule GetOneYearLocalFromUtc(int year)
            {
                OffsetAndRule oneYearLocFromUtc = _oneYearLocalFromUtc;
                if (oneYearLocFromUtc == null || oneYearLocFromUtc.Year != year)
                {
                    TimeZoneInfo currentYear = GetCurrentOneYearLocal();
                    AdjustmentRule rule = currentYear._adjustmentRules == null ? null : currentYear._adjustmentRules[0];
                    oneYearLocFromUtc = new OffsetAndRule(year, currentYear.BaseUtcOffset, rule);
                    _oneYearLocalFromUtc = oneYearLocFromUtc;
                }
                return oneYearLocFromUtc;
            }
        }
#pragma warning restore 0420

        private sealed class OffsetAndRule
        {
            public readonly int Year;
            public readonly TimeSpan Offset;
            public readonly AdjustmentRule Rule;

            public OffsetAndRule(int year, TimeSpan offset, AdjustmentRule rule)
            {
                Year = year;
                Offset = offset;
                Rule = rule;
            }
        }

        private static bool GetTimeZoneInfo(out TimeZoneInformation timeZoneInfo)
        {
            TIME_DYNAMIC_ZONE_INFORMATION dtzi;
            long result = Interop.mincore.GetDynamicTimeZoneInformation(out dtzi);
            if (result == Interop.mincore.TIME_ZONE_ID_INVALID)
            {
                timeZoneInfo = null;
                return false;
            }

            timeZoneInfo = new TimeZoneInformation(dtzi);

            return true;
        }

        private TimeZoneInfo(TimeZoneInformation zone, bool dstDisabled)
        {
            if (string.IsNullOrEmpty(zone.StandardName))
            {
                _id = LocalId;  // the ID must contain at least 1 character - initialize m_id to "Local"
            }
            else
            {
                _id = zone.StandardName;
            }
            _baseUtcOffset = new TimeSpan(0, -(zone.Dtzi.Bias), 0);

            if (!dstDisabled)
            {
                // only create the adjustment rule if DST is enabled
                AdjustmentRule rule = CreateAdjustmentRuleFromTimeZoneInformation(zone, DateTime.MinValue.Date, DateTime.MaxValue.Date, zone.Dtzi.Bias);
                if (rule != null)
                {
                    _adjustmentRules = new AdjustmentRule[1];
                    _adjustmentRules[0] = rule;
                }
            }

            ValidateTimeZoneInfo(_id, _baseUtcOffset, _adjustmentRules, out _supportsDaylightSavingTime);
            _displayName = zone.StandardName;
            _standardDisplayName = zone.StandardName;
            _daylightDisplayName = zone.DaylightName;
        }

        private sealed class TimeZoneInformation
        {
            public string StandardName;
            public string DaylightName;
            public string TimeZoneKeyName;

            // we need to keep this one for subsequent interops.
            public TIME_DYNAMIC_ZONE_INFORMATION Dtzi;

            public unsafe TimeZoneInformation(TIME_DYNAMIC_ZONE_INFORMATION dtzi)
            {
                StandardName = new string(dtzi.StandardName);
                DaylightName = new string(dtzi.DaylightName);
                TimeZoneKeyName = new string(dtzi.TimeZoneKeyName);
                Dtzi = dtzi;
            }
        }

        //
        // TryGetTimeZone -
        //
        // Helper function for retrieving a TimeZoneInfo object by <time_zone_name>.
        //
        // This function may return null.
        //
        // assumes cachedData lock is taken
        //
        private static TimeZoneInfoResult TryGetTimeZone(ref TimeZoneInformation timeZoneInformation, bool dstDisabled, out TimeZoneInfo value, out Exception e, CachedData cachedData)
        {
            TimeZoneInfoResult result = TimeZoneInfoResult.Success;
            e = null;
            TimeZoneInfo match = null;

            // check the cache
            if (cachedData._systemTimeZones != null)
            {
                if (cachedData._systemTimeZones.TryGetValue(timeZoneInformation.TimeZoneKeyName, out match))
                {
                    if (dstDisabled && match._supportsDaylightSavingTime)
                    {
                        // we found a cache hit but we want a time zone without DST and this one has DST data
                        value = CreateCustomTimeZone(match._id, match._baseUtcOffset, match._displayName, match._standardDisplayName);
                    }
                    else
                    {
                        value = new TimeZoneInfo(match._id, match._baseUtcOffset, match._displayName, match._standardDisplayName,
                                              match._daylightDisplayName, match._adjustmentRules, false);
                    }
                    return result;
                }
            }

            // fall back to reading from the local machine 
            // when the cache is not fully populated               
            result = TryGetFullTimeZoneInformation(timeZoneInformation, out match, out e, timeZoneInformation.Dtzi.Bias);

            if (result == TimeZoneInfoResult.Success)
            {
                if (cachedData._systemTimeZones == null)
                    cachedData._systemTimeZones = new Dictionary<string, TimeZoneInfo>();

                cachedData._systemTimeZones.Add(timeZoneInformation.TimeZoneKeyName, match);

                if (dstDisabled && match._supportsDaylightSavingTime)
                {
                    // we found a cache hit but we want a time zone without DST and this one has DST data
                    value = CreateCustomTimeZone(match._id, match._baseUtcOffset, match._displayName, match._standardDisplayName);
                }
                else
                {
                    value = new TimeZoneInfo(match._id, match._baseUtcOffset, match._displayName, match._standardDisplayName,
                                            match._daylightDisplayName, match._adjustmentRules, false);
                }
            }
            else
            {
                value = null;
            }

            return result;
        }

        private static TimeZoneInfoResult TryGetFullTimeZoneInformation(TimeZoneInformation timeZoneInformation, out TimeZoneInfo value, out Exception e, int defaultBaseUtcOffset)
        {
            uint firstYear, lastYear;
            AdjustmentRule rule;
            AdjustmentRule[] zoneRules = null;

            value = null;
            e = null;

            //
            // First get the adjustment rules
            //

            if (Interop.mincore.GetDynamicTimeZoneInformationEffectiveYears(ref timeZoneInformation.Dtzi, out firstYear, out lastYear) != 0)
            {
                rule = CreateAdjustmentRuleFromTimeZoneInformation(timeZoneInformation, DateTime.MinValue.Date, DateTime.MaxValue.Date, defaultBaseUtcOffset);
                if (rule != null)
                {
                    zoneRules = new AdjustmentRule[1] { rule };
                }
            }
            else
            {
                if (firstYear == lastYear)
                {
                    // there is just 1 dynamic rule for this time zone.
                    rule = CreateAdjustmentRuleFromTimeZoneInformation(timeZoneInformation, DateTime.MinValue.Date, DateTime.MaxValue.Date, defaultBaseUtcOffset);
                    if (rule != null)
                    {
                        zoneRules = new AdjustmentRule[1] { rule };
                    }
                }
                else
                {
                    TIME_ZONE_INFORMATION tzdi = new TIME_ZONE_INFORMATION();
                    LowLevelList<AdjustmentRule> rules = new LowLevelList<AdjustmentRule>();

                    //
                    // First rule
                    //

                    if (!Interop.mincore.GetTimeZoneInformationForYear((ushort)firstYear, ref timeZoneInformation.Dtzi, out tzdi))
                    {
                        return TimeZoneInfoResult.InvalidTimeZoneException;
                    }
                    rule = CreateAdjustmentRuleFromTimeZoneInformation(ref tzdi, DateTime.MinValue.Date, new DateTime((int)firstYear, 12, 31), defaultBaseUtcOffset);
                    if (rule != null)
                    {
                        rules.Add(rule);
                    }

                    for (uint i = firstYear + 1; i < lastYear; i++)
                    {
                        if (!Interop.mincore.GetTimeZoneInformationForYear((ushort)i, ref timeZoneInformation.Dtzi, out tzdi))
                        {
                            return TimeZoneInfoResult.InvalidTimeZoneException;
                        }
                        rule = CreateAdjustmentRuleFromTimeZoneInformation(ref tzdi, new DateTime((int)i, 1, 1), new DateTime((int)i, 12, 31), defaultBaseUtcOffset);
                        if (rule != null)
                        {
                            rules.Add(rule);
                        }
                    }

                    //
                    // Last rule
                    //

                    if (!Interop.mincore.GetTimeZoneInformationForYear((ushort)lastYear, ref timeZoneInformation.Dtzi, out tzdi))
                    {
                        return TimeZoneInfoResult.InvalidTimeZoneException;
                    }
                    rule = CreateAdjustmentRuleFromTimeZoneInformation(ref tzdi, new DateTime((int)lastYear, 1, 1), DateTime.MaxValue.Date, defaultBaseUtcOffset);
                    if (rule != null)
                    {
                        rules.Add(rule);
                    }

                    if (rules.Count > 0)
                    {
                        zoneRules = rules.ToArray();
                    }
                }
            }

            //
            // Create TimeZoneInfo object
            // 
            try
            {
                // Note that all names we have are localized names as Windows always return the localized names
                value = new TimeZoneInfo(
                    timeZoneInformation.TimeZoneKeyName,
                    new TimeSpan(0, -(timeZoneInformation.Dtzi.Bias), 0),
                    timeZoneInformation.StandardName,   // we use the display name as the standared names
                    timeZoneInformation.StandardName,
                    timeZoneInformation.DaylightName,
                    zoneRules,
                    false);

                return System.TimeZoneInfo.TimeZoneInfoResult.Success;
            }
            catch (ArgumentException ex)
            {
                // TimeZoneInfo constructor can throw ArgumentException and InvalidTimeZoneException
                value = null;
                e = ex;
                return System.TimeZoneInfo.TimeZoneInfoResult.InvalidTimeZoneException;
            }
            catch (InvalidTimeZoneException ex)
            {
                // TimeZoneInfo constructor can throw ArgumentException and InvalidTimeZoneException
                value = null;
                e = ex;
                return System.TimeZoneInfo.TimeZoneInfoResult.InvalidTimeZoneException;
            }
        }

        //
        // CreateAdjustmentRuleFromTimeZoneInformation-
        //
        // Converts TimeZoneInformation to an AdjustmentRule
        //
        private static AdjustmentRule CreateAdjustmentRuleFromTimeZoneInformation(TimeZoneInformation timeZoneInformation, DateTime startDate, DateTime endDate, int defaultBaseUtcOffset)
        {
            bool supportsDst = (timeZoneInformation.Dtzi.StandardDate.wMonth != 0);

            if (!supportsDst)
            {
                if (timeZoneInformation.Dtzi.Bias == defaultBaseUtcOffset)
                {
                    // this rule will not contain any information to be used to adjust dates. just ignore it
                    return null;
                }

                return AdjustmentRule.CreateAdjustmentRule(
                    startDate,
                    endDate,
                    TimeSpan.Zero, // no daylight saving transition
                    TransitionTime.CreateFixedDateRule(DateTime.MinValue, 1, 1),
                    TransitionTime.CreateFixedDateRule(DateTime.MinValue.AddMilliseconds(1), 1, 1),
                    new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Dtzi.Bias, 0),  // Bias delta is all what we need from this rule
                    noDaylightTransitions: false);
            }

            //
            // Create an AdjustmentRule with TransitionTime objects
            //
            TransitionTime daylightTransitionStart;
            if (!TransitionTimeFromTimeZoneInformation(timeZoneInformation, out daylightTransitionStart, true /* start date */))
            {
                return null;
            }

            TransitionTime daylightTransitionEnd;
            if (!TransitionTimeFromTimeZoneInformation(timeZoneInformation, out daylightTransitionEnd, false /* end date */))
            {
                return null;
            }

            if (daylightTransitionStart.Equals(daylightTransitionEnd))
            {
                // this happens when the time zone does support DST but the OS has DST disabled
                return null;
            }

            return AdjustmentRule.CreateAdjustmentRule(
                startDate,
                endDate,
                new TimeSpan(0, -timeZoneInformation.Dtzi.DaylightBias, 0),
                (TransitionTime)daylightTransitionStart,
                (TransitionTime)daylightTransitionEnd,
                new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Dtzi.Bias, 0),
                noDaylightTransitions: false);
        }

        internal static AdjustmentRule CreateAdjustmentRuleFromTimeZoneInformation(ref TIME_ZONE_INFORMATION timeZoneInformation, DateTime startDate, DateTime endDate, int defaultBaseUtcOffset)
        {
            bool supportsDst = (timeZoneInformation.StandardDate.wMonth != 0);

            if (!supportsDst)
            {
                if (timeZoneInformation.Bias == defaultBaseUtcOffset)
                {
                    // this rule will not contain any information to be used to adjust dates. just ignore it
                    return null;
                }

                return AdjustmentRule.CreateAdjustmentRule(
                    startDate,
                    endDate,
                    TimeSpan.Zero, // no daylight saving transition
                    TransitionTime.CreateFixedDateRule(DateTime.MinValue, 1, 1),
                    TransitionTime.CreateFixedDateRule(DateTime.MinValue.AddMilliseconds(1), 1, 1),
                    new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Bias, 0),  // Bias delta is all what we need from this rule
                    noDaylightTransitions: false);
            }

            //
            // Create an AdjustmentRule with TransitionTime objects
            //
            TransitionTime daylightTransitionStart;
            if (!TransitionTimeFromTimeZoneInformation(timeZoneInformation, out daylightTransitionStart, true /* start date */))
            {
                return null;
            }

            TransitionTime daylightTransitionEnd;
            if (!TransitionTimeFromTimeZoneInformation(timeZoneInformation, out daylightTransitionEnd, false /* end date */))
            {
                return null;
            }

            if (daylightTransitionStart.Equals(daylightTransitionEnd))
            {
                // this happens when the time zone does support DST but the OS has DST disabled
                return null;
            }

            return AdjustmentRule.CreateAdjustmentRule(
                startDate,
                endDate,
                new TimeSpan(0, -timeZoneInformation.DaylightBias, 0),
                (TransitionTime)daylightTransitionStart,
                (TransitionTime)daylightTransitionEnd,
                new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Bias, 0),
                noDaylightTransitions: false);
        }

        private static bool TransitionTimeFromTimeZoneInformation(TimeZoneInformation timeZoneInformation, out TransitionTime transitionTime, bool readStartDate)
        {
            bool supportsDst = (timeZoneInformation.Dtzi.StandardDate.wMonth != 0);

            if (!supportsDst)
            {
                transitionTime = default(TransitionTime);
                return false;
            }

            if (readStartDate)
            {
                //
                // read the "daylightTransitionStart"
                //
                if (timeZoneInformation.Dtzi.DaylightDate.wYear == 0)
                {
                    transitionTime = TransitionTime.CreateFloatingDateRule(
                                     new DateTime(1,    /* year  */
                                                  1,    /* month */
                                                  1,    /* day   */
                                                  timeZoneInformation.Dtzi.DaylightDate.wHour,
                                                  timeZoneInformation.Dtzi.DaylightDate.wMinute,
                                                  timeZoneInformation.Dtzi.DaylightDate.wSecond,
                                                  timeZoneInformation.Dtzi.DaylightDate.wMilliseconds),
                                     timeZoneInformation.Dtzi.DaylightDate.wMonth,
                                     timeZoneInformation.Dtzi.DaylightDate.wDay,   /* Week 1-5 */
                                     (DayOfWeek)timeZoneInformation.Dtzi.DaylightDate.wDayOfWeek);
                }
                else
                {
                    transitionTime = TransitionTime.CreateFixedDateRule(
                                     new DateTime(1,    /* year  */
                                                  1,    /* month */
                                                  1,    /* day   */
                                                  timeZoneInformation.Dtzi.DaylightDate.wHour,
                                                  timeZoneInformation.Dtzi.DaylightDate.wMinute,
                                                  timeZoneInformation.Dtzi.DaylightDate.wSecond,
                                                  timeZoneInformation.Dtzi.DaylightDate.wMilliseconds),
                                     timeZoneInformation.Dtzi.DaylightDate.wMonth,
                                     timeZoneInformation.Dtzi.DaylightDate.wDay);
                }
            }
            else
            {
                //
                // read the "daylightTransitionEnd"
                //
                if (timeZoneInformation.Dtzi.StandardDate.wYear == 0)
                {
                    transitionTime = TransitionTime.CreateFloatingDateRule(
                                     new DateTime(1,    /* year  */
                                                  1,    /* month */
                                                  1,    /* day   */
                                                  timeZoneInformation.Dtzi.StandardDate.wHour,
                                                  timeZoneInformation.Dtzi.StandardDate.wMinute,
                                                  timeZoneInformation.Dtzi.StandardDate.wSecond,
                                                  timeZoneInformation.Dtzi.StandardDate.wMilliseconds),
                                     timeZoneInformation.Dtzi.StandardDate.wMonth,
                                     timeZoneInformation.Dtzi.StandardDate.wDay,   /* Week 1-5 */
                                     (DayOfWeek)timeZoneInformation.Dtzi.StandardDate.wDayOfWeek);
                }
                else
                {
                    transitionTime = TransitionTime.CreateFixedDateRule(
                                     new DateTime(1,    /* year  */
                                                  1,    /* month */
                                                  1,    /* day   */
                                                  timeZoneInformation.Dtzi.StandardDate.wHour,
                                                  timeZoneInformation.Dtzi.StandardDate.wMinute,
                                                  timeZoneInformation.Dtzi.StandardDate.wSecond,
                                                  timeZoneInformation.Dtzi.StandardDate.wMilliseconds),
                                     timeZoneInformation.Dtzi.StandardDate.wMonth,
                                     timeZoneInformation.Dtzi.StandardDate.wDay);
                }
            }

            return true;
        }

        //
        // TransitionTimeFromTimeZoneInformation -
        //
        // Converts a TimeZoneInformation (REG_TZI_FORMAT struct) to a TransitionTime
        //
        // * when the argument 'readStart' is true the corresponding daylightTransitionTimeStart field is read
        // * when the argument 'readStart' is false the corresponding dayightTransitionTimeEnd field is read
        //
        private static bool TransitionTimeFromTimeZoneInformation(TIME_ZONE_INFORMATION timeZoneInformation, out TransitionTime transitionTime, bool readStartDate)
        {
            //
            // SYSTEMTIME - 
            //
            // If the time zone does not support daylight saving time or if the caller needs
            // to disable daylight saving time, the wMonth member in the SYSTEMTIME structure
            // must be zero. If this date is specified, the DaylightDate value in the 
            // TIME_ZONE_INFORMATION structure must also be specified. Otherwise, the system 
            // assumes the time zone data is invalid and no changes will be applied.
            //
            bool supportsDst = (timeZoneInformation.StandardDate.wMonth != 0);

            if (!supportsDst)
            {
                transitionTime = default(TransitionTime);
                return false;
            }

            //
            // SYSTEMTIME -
            //
            // * FixedDateRule -
            //   If the Year member is not zero, the transition date is absolute; it will only occur one time
            //
            // * FloatingDateRule -
            //   To select the correct day in the month, set the Year member to zero, the Hour and Minute 
            //   members to the transition time, the DayOfWeek member to the appropriate weekday, and the
            //   Day member to indicate the occurence of the day of the week within the month (first through fifth).
            //
            //   Using this notation, specify the 2:00a.m. on the first Sunday in April as follows: 
            //   Hour      = 2, 
            //   Month     = 4,
            //   DayOfWeek = 0,
            //   Day       = 1.
            //
            //   Specify 2:00a.m. on the last Thursday in October as follows:
            //   Hour      = 2,
            //   Month     = 10,
            //   DayOfWeek = 4,
            //   Day       = 5.
            //
            if (readStartDate)
            {
                //
                // read the "daylightTransitionStart"
                //
                if (timeZoneInformation.DaylightDate.wYear == 0)
                {
                    transitionTime = TransitionTime.CreateFloatingDateRule(
                                     new DateTime(1,    /* year  */
                                                  1,    /* month */
                                                  1,    /* day   */
                                                  timeZoneInformation.DaylightDate.wHour,
                                                  timeZoneInformation.DaylightDate.wMinute,
                                                  timeZoneInformation.DaylightDate.wSecond,
                                                  timeZoneInformation.DaylightDate.wMilliseconds),
                                     timeZoneInformation.DaylightDate.wMonth,
                                     timeZoneInformation.DaylightDate.wDay,   /* Week 1-5 */
                                     (DayOfWeek)timeZoneInformation.DaylightDate.wDayOfWeek);
                }
                else
                {
                    transitionTime = TransitionTime.CreateFixedDateRule(
                                     new DateTime(1,    /* year  */
                                                  1,    /* month */
                                                  1,    /* day   */
                                                  timeZoneInformation.DaylightDate.wHour,
                                                  timeZoneInformation.DaylightDate.wMinute,
                                                  timeZoneInformation.DaylightDate.wSecond,
                                                  timeZoneInformation.DaylightDate.wMilliseconds),
                                     timeZoneInformation.DaylightDate.wMonth,
                                     timeZoneInformation.DaylightDate.wDay);
                }
            }
            else
            {
                //
                // read the "daylightTransitionEnd"
                //
                if (timeZoneInformation.StandardDate.wYear == 0)
                {
                    transitionTime = TransitionTime.CreateFloatingDateRule(
                                     new DateTime(1,    /* year  */
                                                  1,    /* month */
                                                  1,    /* day   */
                                                  timeZoneInformation.StandardDate.wHour,
                                                  timeZoneInformation.StandardDate.wMinute,
                                                  timeZoneInformation.StandardDate.wSecond,
                                                  timeZoneInformation.StandardDate.wMilliseconds),
                                     timeZoneInformation.StandardDate.wMonth,
                                     timeZoneInformation.StandardDate.wDay,   /* Week 1-5 */
                                     (DayOfWeek)timeZoneInformation.StandardDate.wDayOfWeek);
                }
                else
                {
                    transitionTime = TransitionTime.CreateFixedDateRule(
                                     new DateTime(1,    /* year  */
                                                  1,    /* month */
                                                  1,    /* day   */
                                                  timeZoneInformation.StandardDate.wHour,
                                                  timeZoneInformation.StandardDate.wMinute,
                                                  timeZoneInformation.StandardDate.wSecond,
                                                  timeZoneInformation.StandardDate.wMilliseconds),
                                     timeZoneInformation.StandardDate.wMonth,
                                     timeZoneInformation.StandardDate.wDay);
                }
            }

            return true;
        }

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

        /// <summary>
        /// Helper function used by 'GetLocalTimeZone()' - this function wraps a bunch of
        /// try/catch logic for handling the TimeZoneInfo private constructor that takes
        /// a Win32Native.TimeZoneInformation structure.
        /// </summary>
        private static TimeZoneInfo GetLocalTimeZoneFromWin32Data(TimeZoneInformation timeZoneInformation, bool dstDisabled)
        {
            // first try to create the TimeZoneInfo with the original 'dstDisabled' flag
            try
            {
                return new TimeZoneInfo(timeZoneInformation, dstDisabled);
            }
            catch (ArgumentException) { }
            catch (InvalidTimeZoneException) { }

            // if 'dstDisabled' was false then try passing in 'true' as a last ditch effort
            if (!dstDisabled)
            {
                try
                {
                    return new TimeZoneInfo(timeZoneInformation, dstDisabled: true);
                }
                catch (ArgumentException) { }
                catch (InvalidTimeZoneException) { }
            }

            // the data returned from Windows is completely bogus; return a dummy entry
            return CreateCustomTimeZone(LocalId, TimeSpan.Zero, LocalId, LocalId);
        }

        public static TimeZoneInfo FindSystemTimeZoneById(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }
            if (id.Length == 0 || id.Length > 255 || id.Contains("\0"))
            {
                throw new TimeZoneNotFoundException(SR.Format(SR.TimeZoneNotFound_MissingData, id));
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
            throw new TimeZoneNotFoundException(SR.Format(SR.TimeZoneNotFound_MissingData, id));
        }

        // DateTime.Now fast path that avoids allocating an historically accurate TimeZoneInfo.Local and just creates a 1-year (current year) accurate time zone
        internal static TimeSpan GetDateTimeNowUtcOffsetFromUtc(DateTime time, out bool isAmbiguousLocalDst)
        {
            bool isDaylightSavings = false;
            isAmbiguousLocalDst = false;
            TimeSpan baseOffset;
            int timeYear = time.Year;

            OffsetAndRule match = s_cachedData.GetOneYearLocalFromUtc(timeYear);
            baseOffset = match.Offset;

            if (match.Rule != null)
            {
                baseOffset = baseOffset + match.Rule.BaseUtcOffsetDelta;
                if (match.Rule.HasDaylightSaving)
                {
                    isDaylightSavings = GetIsDaylightSavingsFromUtc(time, timeYear, match.Offset, match.Rule, null, out isAmbiguousLocalDst, Local);
                    baseOffset += (isDaylightSavings ? match.Rule.DaylightDelta : TimeSpan.Zero /* FUTURE: rule.StandardDelta */);
                }
            }
            return baseOffset;
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
        private static unsafe bool FindMatchToCurrentTimeZone(TimeZoneInformation timeZoneInformation)
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

                    if (!string.IsNullOrEmpty(s) &&
                        EqualStandardDates(timeZoneInformation, ref tdzi) &&
                        (notSupportedDaylightSaving || EqualDaylightDates(timeZoneInformation, ref tdzi)) &&
                        string.Compare(s, timeZoneInformation.StandardName, StringComparison.Ordinal) == 0)
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
                return CreateCustomTimeZone(LocalId, TimeSpan.Zero, LocalId, LocalId);
            }

            bool dstDisabled = timeZoneInformation.Dtzi.DynamicDaylightTimeDisabled != 0;

            //// check to see if we can use the key name returned from the API call
            if (!string.IsNullOrEmpty(timeZoneInformation.TimeZoneKeyName) || FindMatchToCurrentTimeZone(timeZoneInformation))
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

        private static TimeZoneInfoResult TryGetTimeZoneFromLocalMachine(string id, out TimeZoneInfo value, out Exception e)
        {
            // This method should be unreachable
            Debug.Assert(false);

            e = null;
            value = null;
            return TimeZoneInfoResult.InvalidTimeZoneException;
        }
    }
}
