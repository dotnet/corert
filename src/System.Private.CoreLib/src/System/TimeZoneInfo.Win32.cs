// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: 
** This class is used to represent a Dynamic TimeZone.  It
** has methods for converting a DateTime between TimeZones,
** and for reading TimeZone data from the Windows Registry
**
**
============================================================*/

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Threading;

using TIME_ZONE_INFORMATION = Interop.mincore.TIME_ZONE_INFORMATION;
using TIME_DYNAMIC_ZONE_INFORMATION = Interop.mincore.TIME_DYNAMIC_ZONE_INFORMATION;
using REGISTRY_TIME_ZONE_INFORMATION = Interop.mincore.REGISTRY_TIME_ZONE_INFORMATION;

namespace System
{
    sealed public partial class TimeZoneInfo
    {
        // registry constants for the 'Time Zones' hive
        //
        private const string c_timeZonesRegistryHive = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones";
        private const string c_timeZonesRegistryHivePermissionList = @"HKEY_LOCAL_MACHINE\" + c_timeZonesRegistryHive;
        private const string c_displayValue = "Display";
        private const string c_daylightValue = "Dlt";
        private const string c_standardValue = "Std";
        private const string c_muiDisplayValue = "MUI_Display";
        private const string c_muiDaylightValue = "MUI_Dlt";
        private const string c_muiStandardValue = "MUI_Std";
        private const string c_timeZoneInfoValue = "TZI";
        private const string c_firstEntryValue = "FirstEntry";
        private const string c_lastEntryValue = "LastEntry";

        private const int c_maxKeyLength = 255;

        private const int c_regByteLength = 44;

        // Number of 100ns ticks per time unit
        private const long c_ticksPerMillisecond = 10000;
        private const long c_ticksPerSecond = c_ticksPerMillisecond * 1000;
        private const long c_ticksPerMinute = c_ticksPerSecond * 60;
        private const long c_ticksPerHour = c_ticksPerMinute * 60;
        private const long c_ticksPerDay = c_ticksPerHour * 24;
        private const long c_ticksPerDayRange = c_ticksPerDay - c_ticksPerMillisecond;

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

            using (RegistryKey reg = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE).OpenSubKey(c_timeZonesRegistryHive, writable: false))
            {
                if (reg != null)
                {
                    foreach (string keyName in reg.GetSubKeyNames())
                    {
                        TimeZoneInfo value;
                        Exception ex;
                        TryGetTimeZone(keyName, false, out value, out ex, cachedData);  // populate the cache
                    }
                }
            }
        }

        // -------- SECTION: constructors -----------------*
        // 
        // TimeZoneInfo -
        //
        // private ctor
        //
        private unsafe TimeZoneInfo(TIME_ZONE_INFORMATION zone, Boolean dstDisabled)
        {
            if (String.IsNullOrEmpty(new String(zone.StandardName)))
            {
                _id = c_localId;  // the ID must contain at least 1 character - initialize _id to "Local"
            }
            else
            {
                _id = new String(zone.StandardName);
            }
            _baseUtcOffset = new TimeSpan(0, -(zone.Bias), 0);

            if (!dstDisabled)
            {
                // only create the adjustment rule if DST is enabled
                REGISTRY_TIME_ZONE_INFORMATION regZone = new REGISTRY_TIME_ZONE_INFORMATION(zone);
                AdjustmentRule rule = CreateAdjustmentRuleFromTimeZoneInformation(regZone, DateTime.MinValue.Date, DateTime.MaxValue.Date, zone.Bias);
                if (rule != null)
                {
                    _adjustmentRules = new AdjustmentRule[1];
                    _adjustmentRules[0] = rule;
                }
            }

            ValidateTimeZoneInfo(_id, _baseUtcOffset, _adjustmentRules, out _supportsDaylightSavingTime);
            _displayName = new String(zone.StandardName);
            _standardDisplayName = new String(zone.StandardName);
            _daylightDisplayName = new String(zone.DaylightName);
        }

        // ----- SECTION: internal static utility methods ----------------*

        //
        // CheckDaylightSavingTimeNotSupported -
        //
        // Helper function to check if the current TimeZoneInformation struct does not support DST.  This
        // check returns true when the DaylightDate == StandardDate
        //
        // This check is only meant to be used for "Local".
        //
        private static Boolean CheckDaylightSavingTimeNotSupported(TIME_ZONE_INFORMATION timeZone)
        {
            return (timeZone.DaylightDate.wYear == timeZone.StandardDate.wYear
                    && timeZone.DaylightDate.wMonth == timeZone.StandardDate.wMonth
                    && timeZone.DaylightDate.wDayOfWeek == timeZone.StandardDate.wDayOfWeek
                    && timeZone.DaylightDate.wDay == timeZone.StandardDate.wDay
                    && timeZone.DaylightDate.wHour == timeZone.StandardDate.wHour
                    && timeZone.DaylightDate.wMinute == timeZone.StandardDate.wMinute
                    && timeZone.DaylightDate.wSecond == timeZone.StandardDate.wSecond
                    && timeZone.DaylightDate.wMilliseconds == timeZone.StandardDate.wMilliseconds);
        }

        //
        // CreateAdjustmentRuleFromTimeZoneInformation-
        //
        // Converts a REGISTRY_TIME_ZONE_INFORMATION (REG_TZI_FORMAT struct) to an AdjustmentRule
        //
        private static AdjustmentRule CreateAdjustmentRuleFromTimeZoneInformation(REGISTRY_TIME_ZONE_INFORMATION timeZoneInformation, DateTime startDate, DateTime endDate, int defaultBaseUtcOffset)
        {
            AdjustmentRule rule;
            bool supportsDst = (timeZoneInformation.StandardDate.wMonth != 0);

            if (!supportsDst)
            {
                if (timeZoneInformation.Bias == defaultBaseUtcOffset)
                {
                    // this rule will not contain any information to be used to adjust dates. just ignore it
                    return null;
                }

                return rule = AdjustmentRule.CreateAdjustmentRule(
                    startDate,
                    endDate,
                    TimeSpan.Zero, // no daylight saving transition
                    TransitionTime.CreateFixedDateRule(DateTime.MinValue, 1, 1),
                    TransitionTime.CreateFixedDateRule(DateTime.MinValue.AddMilliseconds(1), 1, 1),
                    new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Bias, 0));  // Bias delta is all what we need from this rule
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

            rule = AdjustmentRule.CreateAdjustmentRule(
                startDate,
                endDate,
                new TimeSpan(0, -timeZoneInformation.DaylightBias, 0),
                (TransitionTime)daylightTransitionStart,
                (TransitionTime)daylightTransitionEnd,
                new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Bias, 0));

            return rule;
        }

        //
        // FindIdFromTimeZoneInformation -
        //
        // Helper function that searches the registry for a time zone entry
        // that matches the TimeZoneInformation struct
        //
        private static String FindIdFromTimeZoneInformation(TIME_ZONE_INFORMATION timeZone, out Boolean dstDisabled)
        {
            dstDisabled = false;

            using (RegistryKey key = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE).OpenSubKey(
                              c_timeZonesRegistryHive,
                              false
                              ))
            {
                if (key == null)
                {
                    return null;
                }
                foreach (string keyName in key.GetSubKeyNames())
                {
                    if (TryCompareTimeZoneInformationToRegistry(timeZone, keyName, out dstDisabled))
                    {
                        return keyName;
                    }
                }
            }
            return null;
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
        static unsafe private TimeZoneInfo GetLocalTimeZone(CachedData cachedData)
        {
            String id = null;

            //
            // Try using the "kernel32!GetDynamicTimeZoneInformation" API to get the "id"
            //
            Interop.mincore.TIME_DYNAMIC_ZONE_INFORMATION dynamicTimeZoneInformation =
                new Interop.mincore.TIME_DYNAMIC_ZONE_INFORMATION();

            // call kernel32!GetDynamicTimeZoneInformation...
            long result = Interop.mincore.GetDynamicTimeZoneInformation(out dynamicTimeZoneInformation);
            if (result == Interop.mincore.TIME_ZONE_ID_INVALID)
            {
                // return a dummy entry
                return CreateCustomTimeZone(c_localId, TimeSpan.Zero, c_localId, c_localId);
            }

            TIME_ZONE_INFORMATION timeZoneInformation =
                new TIME_ZONE_INFORMATION(dynamicTimeZoneInformation);

            Boolean dstDisabled = dynamicTimeZoneInformation.DynamicDaylightTimeDisabled != 0;

            // check to see if we can use the key name returned from the API call
            if (!String.IsNullOrEmpty(new String(dynamicTimeZoneInformation.TimeZoneKeyName)))
            {
                TimeZoneInfo zone;
                Exception ex;

                if (TryGetTimeZone(new String(dynamicTimeZoneInformation.TimeZoneKeyName), dstDisabled, out zone, out ex, cachedData) == TimeZoneInfoResult.Success)
                {
                    // successfully loaded the time zone from the registry
                    return zone;
                }
            }

            // the key name was not returned or it pointed to a bogus entry - search for the entry ourselves                
            id = FindIdFromTimeZoneInformation(timeZoneInformation, out dstDisabled);

            if (id != null)
            {
                TimeZoneInfo zone;
                Exception ex;
                if (TryGetTimeZone(id, dstDisabled, out zone, out ex, cachedData) == TimeZoneInfoResult.Success)
                {
                    // successfully loaded the time zone from the registry
                    return zone;
                }
            }

            // We could not find the data in the registry.  Fall back to using
            // the data from the Win32 API
            return GetLocalTimeZoneFromWin32Data(timeZoneInformation, dstDisabled);
        }

        //
        // GetLocalTimeZoneFromWin32Data -
        //
        // Helper function used by 'GetLocalTimeZone()' - this function wraps a bunch of
        // try/catch logic for handling the TimeZoneInfo private constructor that takes
        // a TIME_ZONE_INFORMATION structure.
        //
        private static TimeZoneInfo GetLocalTimeZoneFromWin32Data(TIME_ZONE_INFORMATION timeZoneInformation, Boolean dstDisabled)
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
                    return new TimeZoneInfo(timeZoneInformation, true);
                }
                catch (ArgumentException) { }
                catch (InvalidTimeZoneException) { }
            }

            // the data returned from Windows is completely bogus; return a dummy entry
            return CreateCustomTimeZone(c_localId, TimeSpan.Zero, c_localId, c_localId);
        }

        //
        // FindSystemTimeZoneById -
        //
        // Helper function for retrieving a TimeZoneInfo object by <time_zone_name>.
        // This function wraps the logic necessary to keep the private 
        // SystemTimeZones cache in working order
        //
        // This function will either return a valid TimeZoneInfo instance or 
        // it will throw 'InvalidTimeZoneException' / 'TimeZoneNotFoundException'.
        //
        public static TimeZoneInfo FindSystemTimeZoneById(string id)
        {
            // Special case for Utc as it will not exist in the dictionary with the rest
            // of the system time zones.  There is no need to do this check for Local.Id
            // since Local is a real time zone that exists in the dictionary cache
            if (String.Compare(id, c_utcId, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return TimeZoneInfo.Utc;
            }

            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }
            if (id.Length == 0 || id.Length > c_maxKeyLength || id.Contains("\0"))
            {
                throw new TimeZoneNotFoundException(String.Format(SR.TimeZoneNotFound_MissingRegistryData, id));
            }

            TimeZoneInfo value;
            Exception e;

            TimeZoneInfoResult result;

            CachedData cachedData = s_cachedData;

            lock (cachedData)
            {
                result = TryGetTimeZone(id, false, out value, out e, cachedData);
            }

            if (result == TimeZoneInfoResult.Success)
            {
                return value;
            }
            else if (result == TimeZoneInfoResult.InvalidTimeZoneException)
            {
                throw new InvalidTimeZoneException(String.Format(SR.InvalidTimeZone_InvalidRegistryData, id), e);
            }
            else if (result == TimeZoneInfoResult.SecurityException)
            {
                throw new SecurityException(String.Format(SR.Security_CannotReadRegistryData, id), e);
            }
            else
            {
                throw new TimeZoneNotFoundException(String.Format(SR.TimeZoneNotFound_MissingRegistryData, id), e);
            }
        }

        //
        // TransitionTimeFromTimeZoneInformation -
        //
        // Converts a REGISTRY_TIME_ZONE_INFORMATION (REG_TZI_FORMAT struct) to a TransitionTime
        //
        // * when the argument 'readStart' is true the corresponding daylightTransitionTimeStart field is read
        // * when the argument 'readStart' is false the corresponding dayightTransitionTimeEnd field is read
        //
        private static bool TransitionTimeFromTimeZoneInformation(REGISTRY_TIME_ZONE_INFORMATION timeZoneInformation, out TransitionTime transitionTime, bool readStartDate)
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

        //
        // TryCreateAdjustmentRules -
        //
        // Helper function that takes 
        //  1. a string representing a <time_zone_name> registry key name
        //  2. a RegistryTimeZoneInformation struct containing the default rule
        //  3. an AdjustmentRule[] out-parameter
        // 
        // returns 
        //     TimeZoneInfoResult.InvalidTimeZoneException,
        //     TimeZoneInfoResult.TimeZoneNotFoundException,
        //     TimeZoneInfoResult.Success
        //                             
        // Optional, Dynamic Time Zone Registry Data
        // -=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
        //
        // HKLM 
        //     Software 
        //         Microsoft 
        //             Windows NT 
        //                 CurrentVersion 
        //                     Time Zones 
        //                         <time_zone_name>
        //                             Dynamic DST
        // * "FirstEntry" REG_DWORD "1980"
        //                           First year in the table. If the current year is less than this value,
        //                           this entry will be used for DST boundaries
        // * "LastEntry"  REG_DWORD "2038"
        //                           Last year in the table. If the current year is greater than this value,
        //                           this entry will be used for DST boundaries"
        // * "<year1>"    REG_BINARY REG_TZI_FORMAT
        //                       See REGISTRY_TIME_ZONE_INFORMATION
        // * "<year2>"    REG_BINARY REG_TZI_FORMAT 
        //                       See REGISTRY_TIME_ZONE_INFORMATION
        // * "<year3>"    REG_BINARY REG_TZI_FORMAT
        //                       See REGISTRY_TIME_ZONE_INFORMATION
        //
        // This method expects that its caller has already Asserted RegistryPermission.Read
        //
        private static bool TryCreateAdjustmentRules(string id, REGISTRY_TIME_ZONE_INFORMATION defaultTimeZoneInformation, out AdjustmentRule[] rules, out Exception e, int defaultBaseUtcOffset)
        {
            e = null;

            try
            {
                using (RegistryKey dynamicKey = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE).OpenSubKey(
                                   c_timeZonesRegistryHive + "\\" + id + "\\Dynamic DST",
                                   false
                                   ))
                {
                    if (dynamicKey == null)
                    {
                        AdjustmentRule rule = CreateAdjustmentRuleFromTimeZoneInformation(
                                              defaultTimeZoneInformation, DateTime.MinValue.Date, DateTime.MaxValue.Date, defaultBaseUtcOffset);

                        if (rule == null)
                        {
                            rules = null;
                        }
                        else
                        {
                            rules = new AdjustmentRule[1];
                            rules[0] = rule;
                        }

                        return true;
                    }

                    //
                    // loop over all of the "<time_zone_name>\Dynamic DST" hive entries
                    //
                    // read FirstEntry  {MinValue      - (year1, 12, 31)}
                    // read MiddleEntry {(yearN, 1, 1) - (yearN, 12, 31)}
                    // read LastEntry   {(yearN, 1, 1) - MaxValue       }

                    // read the FirstEntry and LastEntry key values (ex: "1980", "2038")
                    Int32 first = (Int32)dynamicKey.GetValue(c_firstEntryValue, -1, RegistryValueOptions.None);
                    Int32 last = (Int32)dynamicKey.GetValue(c_lastEntryValue, -1, RegistryValueOptions.None);

                    if (first == -1 || last == -1 || first > last)
                    {
                        rules = null;
                        return false;
                    }

                    // read the first year entry
                    REGISTRY_TIME_ZONE_INFORMATION dtzi;
                    Byte[] regValue = dynamicKey.GetValue(first.ToString(CultureInfo.InvariantCulture), null, RegistryValueOptions.None) as Byte[];
                    if (regValue == null || regValue.Length != c_regByteLength)
                    {
                        rules = null;
                        return false;
                    }
                    dtzi = new REGISTRY_TIME_ZONE_INFORMATION(regValue);

                    if (first == last)
                    {
                        // there is just 1 dynamic rule for this time zone.
                        AdjustmentRule rule = CreateAdjustmentRuleFromTimeZoneInformation(dtzi, DateTime.MinValue.Date, DateTime.MaxValue.Date, defaultBaseUtcOffset);

                        if (rule == null)
                        {
                            rules = null;
                        }
                        else
                        {
                            rules = new AdjustmentRule[1];
                            rules[0] = rule;
                        }

                        return true;
                    }

                    List<AdjustmentRule> rulesList = new List<AdjustmentRule>(1);

                    // there are more than 1 dynamic rules for this time zone.
                    AdjustmentRule firstRule = CreateAdjustmentRuleFromTimeZoneInformation(
                                              dtzi,
                                              DateTime.MinValue.Date,        // MinValue
                                              new DateTime(first, 12, 31),   // December 31, <FirstYear>
                                              defaultBaseUtcOffset);
                    if (firstRule != null)
                    {
                        rulesList.Add(firstRule);
                    }

                    // read the middle year entries
                    for (Int32 i = first + 1; i < last; i++)
                    {
                        regValue = dynamicKey.GetValue(i.ToString(CultureInfo.InvariantCulture), null, RegistryValueOptions.None) as Byte[];
                        if (regValue == null || regValue.Length != c_regByteLength)
                        {
                            rules = null;
                            return false;
                        }
                        dtzi = new REGISTRY_TIME_ZONE_INFORMATION(regValue);
                        AdjustmentRule middleRule = CreateAdjustmentRuleFromTimeZoneInformation(
                                                  dtzi,
                                                  new DateTime(i, 1, 1),    // January  01, <Year>
                                                  new DateTime(i, 12, 31),  // December 31, <Year>
                                                  defaultBaseUtcOffset);
                        if (middleRule != null)
                        {
                            rulesList.Add(middleRule);
                        }
                    }
                    // read the last year entry
                    regValue = dynamicKey.GetValue(last.ToString(CultureInfo.InvariantCulture), null, RegistryValueOptions.None) as Byte[];
                    dtzi = new REGISTRY_TIME_ZONE_INFORMATION(regValue);
                    if (regValue == null || regValue.Length != c_regByteLength)
                    {
                        rules = null;
                        return false;
                    }
                    AdjustmentRule lastRule = CreateAdjustmentRuleFromTimeZoneInformation(
                                              dtzi,
                                              new DateTime(last, 1, 1),    // January  01, <LastYear>
                                              DateTime.MaxValue.Date,      // MaxValue
                                              defaultBaseUtcOffset);
                    if (lastRule != null)
                    {
                        rulesList.Add(lastRule);
                    }

                    // convert the ArrayList to an AdjustmentRule array
                    rules = rulesList.ToArray();
                    if (rules != null && rules.Length == 0)
                    {
                        rules = null;
                    }
                } // end of: using (RegistryKey dynamicKey...
            }
            catch (InvalidCastException ex)
            {
                // one of the RegistryKey.GetValue calls could not be cast to an expected value type
                rules = null;
                e = ex;
                return false;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                rules = null;
                e = ex;
                return false;
            }
            catch (ArgumentException ex)
            {
                rules = null;
                e = ex;
                return false;
            }
            return true;
        }

        //
        // TryCompareStandardDate -
        //
        // Helper function that compares the StandardBias and StandardDate portion a
        // TimeZoneInformation struct to a time zone registry entry
        //
        private static Boolean TryCompareStandardDate(TIME_ZONE_INFORMATION timeZone, REGISTRY_TIME_ZONE_INFORMATION registryTimeZoneInfo)
        {
            return timeZone.Bias == registryTimeZoneInfo.Bias
                   && timeZone.StandardBias == registryTimeZoneInfo.StandardBias
                   && timeZone.StandardDate.wYear == registryTimeZoneInfo.StandardDate.wYear
                   && timeZone.StandardDate.wMonth == registryTimeZoneInfo.StandardDate.wMonth
                   && timeZone.StandardDate.wDayOfWeek == registryTimeZoneInfo.StandardDate.wDayOfWeek
                   && timeZone.StandardDate.wDay == registryTimeZoneInfo.StandardDate.wDay
                   && timeZone.StandardDate.wHour == registryTimeZoneInfo.StandardDate.wHour
                   && timeZone.StandardDate.wMinute == registryTimeZoneInfo.StandardDate.wMinute
                   && timeZone.StandardDate.wSecond == registryTimeZoneInfo.StandardDate.wSecond
                   && timeZone.StandardDate.wMilliseconds == registryTimeZoneInfo.StandardDate.wMilliseconds;
        }

        //
        // TryCompareTimeZoneInformationToRegistry -
        //
        // Helper function that compares a TimeZoneInformation struct to a time zone registry entry
        //
        static unsafe private Boolean TryCompareTimeZoneInformationToRegistry(TIME_ZONE_INFORMATION timeZone, string id, out Boolean dstDisabled)
        {
            dstDisabled = false;
            using (RegistryKey key = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE).OpenSubKey(
                                  c_timeZonesRegistryHive + "\\" + id,
                                  false
                                  ))
            {
                if (key == null)
                {
                    return false;
                }

                REGISTRY_TIME_ZONE_INFORMATION registryTimeZoneInfo;
                Byte[] regValue = (Byte[])key.GetValue(c_timeZoneInfoValue, null, RegistryValueOptions.None) as Byte[];
                if (regValue == null || regValue.Length != c_regByteLength) return false;
                registryTimeZoneInfo = new REGISTRY_TIME_ZONE_INFORMATION(regValue);

                //
                // first compare the bias and standard date information between the data from the Win32 API
                // and the data from the registry...
                //
                Boolean result = TryCompareStandardDate(timeZone, registryTimeZoneInfo);

                if (!result)
                {
                    return false;
                }

                result = dstDisabled || CheckDaylightSavingTimeNotSupported(timeZone)
                         //
                         // since Daylight Saving Time is not "disabled", do a straight comparision between
                         // the Win32 API data and the registry data ...
                         //
                         || (timeZone.DaylightBias == registryTimeZoneInfo.DaylightBias
                            && timeZone.DaylightDate.wYear == registryTimeZoneInfo.DaylightDate.wYear
                            && timeZone.DaylightDate.wMonth == registryTimeZoneInfo.DaylightDate.wMonth
                            && timeZone.DaylightDate.wDayOfWeek == registryTimeZoneInfo.DaylightDate.wDayOfWeek
                            && timeZone.DaylightDate.wDay == registryTimeZoneInfo.DaylightDate.wDay
                            && timeZone.DaylightDate.wHour == registryTimeZoneInfo.DaylightDate.wHour
                            && timeZone.DaylightDate.wMinute == registryTimeZoneInfo.DaylightDate.wMinute
                            && timeZone.DaylightDate.wSecond == registryTimeZoneInfo.DaylightDate.wSecond
                            && timeZone.DaylightDate.wMilliseconds == registryTimeZoneInfo.DaylightDate.wMilliseconds);

                // Finally compare the "StandardName" string value...
                //
                // we do not compare "DaylightName" as this TimeZoneInformation field may contain
                // either "StandardName" or "DaylightName" depending on the time of year and current machine settings
                //
                if (result)
                {
                    String registryStandardName = key.GetValue(c_standardValue, String.Empty, RegistryValueOptions.None) as String;
                    result = String.Compare(registryStandardName, new String(timeZone.StandardName), StringComparison.Ordinal) == 0;
                }
                return result;
            }
        }

        //
        // TryGetLocalizedNameByMuiNativeResource -
        //
        // Helper function for retrieving a localized string resource via MUI.
        // The function expects a string in the form: "@resource.dll, -123"
        //
        // "resource.dll" is a language-neutral portable executable (LNPE) file in
        // the %windir%\system32 directory.  The OS is queried to find the best-fit
        // localized resource file for this LNPE (ex: %windir%\system32\en-us\resource.dll.mui).
        // If a localized resource file exists, we LoadString resource ID "123" and
        // return it to our caller.
        //
        // <SecurityKernel Critical="True" Ring="0">
        // <CallsSuppressUnmanagedCode Name="Interop.mincore.GetFileMUIPath(System.Int32,System.String,System.Text.StringBuilder,System.Int32&,System.Text.StringBuilder,System.Int32&,System.Int64&):System.Boolean" />
        // <ReferencesCritical Name="Method: TryGetLocalizedNameByNativeResource(String, Int32):String" Ring="1" />
        // </SecurityKernel>
        private static string TryGetLocalizedNameByMuiNativeResource(string resource)
        {
            if (String.IsNullOrEmpty(resource))
            {
                return String.Empty;
            }

            // parse "@tzres.dll, -100"
            // 
            // filePath   = "C:\Windows\System32\tzres.dll"
            // resourceId = -100
            //
            string[] resources = resource.Split(',');
            if (resources.Length != 2)
            {
                return String.Empty;
            }

            string filePath;
            int resourceId;

            // get the path to Windows\System32
            StringBuilder sb = new StringBuilder(Interop.mincore.MAX_PATH);
            int r = Interop.mincore.GetSystemDirectory(sb, Interop.mincore.MAX_PATH);
            string system32 = sb.ToString();

            // trim the string "@tzres.dll" => "tzres.dll"
            string tzresDll = resources[0].TrimStart('@');

            try
            {
                filePath = system32 + "\\" + tzresDll;
            }
            catch (ArgumentException)
            {
                //  there were probably illegal characters in the path
                return String.Empty;
            }

            if (!Int32.TryParse(resources[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out resourceId))
            {
                return String.Empty;
            }
            resourceId = -resourceId;


            try
            {
                StringBuilder fileMuiPath = StringBuilderCache.Acquire(Interop.mincore.MAX_PATH);
                fileMuiPath.Length = Interop.mincore.MAX_PATH;
                int fileMuiPathLength = Interop.mincore.MAX_PATH;
                int languageLength = 0;
                Int64 enumerator = 0;

                Boolean succeeded = Interop.mincore.GetFileMUIPath(
                                        Interop.mincore.MUI_PREFERRED_UI_LANGUAGES,
                                        filePath, null /* language */, ref languageLength,
                                        fileMuiPath, ref fileMuiPathLength, ref enumerator);
                if (!succeeded)
                {
                    StringBuilderCache.Release(fileMuiPath);
                    return String.Empty;
                }
                return TryGetLocalizedNameByNativeResource(StringBuilderCache.GetStringAndRelease(fileMuiPath), resourceId);
            }
            catch
            {
                return String.Empty;
            }
        }

        //
        // TryGetLocalizedNameByNativeResource -
        //
        // Helper function for retrieving a localized string resource via a native resource DLL.
        // The function expects a string in the form: "C:\Windows\System32\en-us\resource.dll"
        //
        // "resource.dll" is a language-specific resource DLL.
        // If the localized resource DLL exists, LoadString(resource) is returned.
        //
        static unsafe private string TryGetLocalizedNameByNativeResource(string filePath, int resource)
        {
            using (SafeLibraryHandle handle =
                       Interop.Kernel32.LoadLibraryEx(filePath, IntPtr.Zero, Interop.Kernel32.LOAD_LIBRARY_AS_DATAFILE))
            {
                if (!handle.IsInvalid)
                {
                    const int LoadStringMaxLength = 500;

                    StringBuilder localizedResource = new StringBuilder(LoadStringMaxLength);

                    int result = Interop.User32.LoadString(handle, resource,
                                     localizedResource, LoadStringMaxLength);

                    if (result != 0)
                    {
                        return localizedResource.ToString();
                    }
                }
            }

            return String.Empty;
        }

        //
        // TryGetLocalizedNamesByRegistryKey -
        //
        // Helper function for retrieving the DisplayName, StandardName, and DaylightName from the registry
        //
        // The function first checks the MUI_ key-values, and if they exist, it loads the strings from the MUI
        // resource dll(s).  When the keys do not exist, the function falls back to reading from the standard
        // key-values
        //
        // This method expects that its caller has already Asserted RegistryPermission.Read
        //
        private static Boolean TryGetLocalizedNamesByRegistryKey(RegistryKey key, out String displayName, out String standardName, out String daylightName)
        {
            displayName = String.Empty;
            standardName = String.Empty;
            daylightName = String.Empty;

            // read the MUI_ registry keys
            String displayNameMuiResource = key.GetValue(c_muiDisplayValue, String.Empty, RegistryValueOptions.None) as String;
            String standardNameMuiResource = key.GetValue(c_muiStandardValue, String.Empty, RegistryValueOptions.None) as String;
            String daylightNameMuiResource = key.GetValue(c_muiDaylightValue, String.Empty, RegistryValueOptions.None) as String;

            // try to load the strings from the native resource DLL(s)
            if (!String.IsNullOrEmpty(displayNameMuiResource))
            {
                displayName = TryGetLocalizedNameByMuiNativeResource(displayNameMuiResource);
            }

            if (!String.IsNullOrEmpty(standardNameMuiResource))
            {
                standardName = TryGetLocalizedNameByMuiNativeResource(standardNameMuiResource);
            }

            if (!String.IsNullOrEmpty(daylightNameMuiResource))
            {
                daylightName = TryGetLocalizedNameByMuiNativeResource(daylightNameMuiResource);
            }

            // fallback to using the standard registry keys
            if (String.IsNullOrEmpty(displayName))
            {
                displayName = key.GetValue(c_displayValue, String.Empty, RegistryValueOptions.None) as String;
            }
            if (String.IsNullOrEmpty(standardName))
            {
                standardName = key.GetValue(c_standardValue, String.Empty, RegistryValueOptions.None) as String;
            }
            if (String.IsNullOrEmpty(daylightName))
            {
                daylightName = key.GetValue(c_daylightValue, String.Empty, RegistryValueOptions.None) as String;
            }

            return true;
        }

        //
        // TryGetTimeZoneByRegistryKey -
        //
        // Helper function that takes a string representing a <time_zone_name> registry key name
        // and returns a TimeZoneInfo instance.
        // 
        // returns 
        //     TimeZoneInfoResult.InvalidTimeZoneException,
        //     TimeZoneInfoResult.TimeZoneNotFoundException,
        //     TimeZoneInfoResult.SecurityException,
        //     TimeZoneInfoResult.Success
        // 
        //
        // Standard Time Zone Registry Data
        // -=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
        // HKLM 
        //     Software 
        //         Microsoft 
        //             Windows NT 
        //                 CurrentVersion 
        //                     Time Zones 
        //                         <time_zone_name>
        // * STD,         REG_SZ "Standard Time Name" 
        //                       (For OS installed zones, this will always be English)
        // * MUI_STD,     REG_SZ "@tzres.dll,-1234" 
        //                       Indirect string to localized resource for Standard Time,
        //                       add "%windir%\system32\" after "@"
        // * DLT,         REG_SZ "Daylight Time Name"
        //                       (For OS installed zones, this will always be English)
        // * MUI_DLT,     REG_SZ "@tzres.dll,-1234"
        //                       Indirect string to localized resource for Daylight Time,
        //                       add "%windir%\system32\" after "@"
        // * Display,     REG_SZ "Display Name like (GMT-8:00) Pacific Time..."
        // * MUI_Display, REG_SZ "@tzres.dll,-1234"
        //                       Indirect string to localized resource for the Display,
        //                       add "%windir%\system32\" after "@"
        // * TZI,         REG_BINARY REG_TZI_FORMAT
        //                       See REGISTRY_TIME_ZONE_INFORMATION
        //
        private static TimeZoneInfoResult TryGetTimeZoneByRegistryKey(string id, out TimeZoneInfo value, out Exception e)
        {
            e = null;

            using (RegistryKey key = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE).OpenSubKey(
                                  c_timeZonesRegistryHive + "\\" + id,
                                  false
                                  ))
            {
                if (key == null)
                {
                    value = null;
                    return TimeZoneInfoResult.TimeZoneNotFoundException;
                }

                REGISTRY_TIME_ZONE_INFORMATION defaultTimeZoneInformation;
                Byte[] regValue = key.GetValue(c_timeZoneInfoValue, null, RegistryValueOptions.None) as Byte[];
                if (regValue == null || regValue.Length != c_regByteLength)
                {
                    // the registry value could not be cast to a byte array
                    value = null;
                    return TimeZoneInfoResult.InvalidTimeZoneException;
                }
                defaultTimeZoneInformation = new REGISTRY_TIME_ZONE_INFORMATION(regValue);

                AdjustmentRule[] adjustmentRules;
                if (!TryCreateAdjustmentRules(id, defaultTimeZoneInformation, out adjustmentRules, out e, defaultTimeZoneInformation.Bias))
                {
                    value = null;
                    return TimeZoneInfoResult.InvalidTimeZoneException;
                }

                string displayName;
                string standardName;
                string daylightName;

                if (!TryGetLocalizedNamesByRegistryKey(key, out displayName, out standardName, out daylightName))
                {
                    value = null;
                    return TimeZoneInfoResult.InvalidTimeZoneException;
                }

                try
                {
                    value = new TimeZoneInfo(
                        id,
                        new TimeSpan(0, -(defaultTimeZoneInformation.Bias), 0),
                        displayName,
                        standardName,
                        daylightName,
                        adjustmentRules,
                        false);

                    return TimeZoneInfoResult.Success;
                }
                catch (ArgumentException ex)
                {
                    // TimeZoneInfo constructor can throw ArgumentException and InvalidTimeZoneException
                    value = null;
                    e = ex;
                    return TimeZoneInfoResult.InvalidTimeZoneException;
                }
                catch (InvalidTimeZoneException ex)
                {
                    // TimeZoneInfo constructor can throw ArgumentException and InvalidTimeZoneException
                    value = null;
                    e = ex;
                    return TimeZoneInfoResult.InvalidTimeZoneException;
                }
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
        private static TimeZoneInfoResult TryGetTimeZone(string id, Boolean dstDisabled, out TimeZoneInfo value, out Exception e, CachedData cachedData)
        {
            TimeZoneInfoResult result = TimeZoneInfoResult.Success;
            e = null;
            TimeZoneInfo match = null;

            // check the cache
            if (cachedData._systemTimeZones != null)
            {
                if (cachedData._systemTimeZones.TryGetValue(id, out match))
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
            if (!cachedData._allSystemTimeZonesRead)
            {
                result = TryGetTimeZoneByRegistryKey(id, out match, out e);
                if (result == TimeZoneInfoResult.Success)
                {
                    if (cachedData._systemTimeZones == null)
                        cachedData._systemTimeZones = new LowLevelDictionaryWithIEnumerable<System.TimeZoneInfo.CachedData.OrdinalIgnoreCaseString, TimeZoneInfo>();

                    cachedData._systemTimeZones.Add(id, match);

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
            }
            else
            {
                result = TimeZoneInfoResult.TimeZoneNotFoundException;
                value = null;
            }

            return result;
        }
    } // TimezoneInfo
} // namespace System
