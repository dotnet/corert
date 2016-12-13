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
using System.Diagnostics.Contracts;
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
    //
    // DateTime uses TimeZoneInfo under the hood for IsDaylightSavingTime, IsAmbiguousTime, and GetUtcOffset.
    // These TimeZoneInfo APIs can throw ArgumentException when an Invalid-Time is passed in.  To avoid this
    // unwanted behavior in DateTime public APIs, DateTime internally passes the
    // TimeZoneInfoOptions.NoThrowOnInvalidTime flag to internal TimeZoneInfo APIs.
    //
    // In the future we can consider exposing similar options on the public TimeZoneInfo APIs if there is enough
    // demand for this alternate behavior.
    //
    [Flags]
    internal enum TimeZoneInfoOptions
    {
        None = 1,
        NoThrowOnInvalidTime = 2
    };


    [Serializable]
    sealed public partial class TimeZoneInfo : IEquatable<TimeZoneInfo>
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
                return new AdjustmentRule[0];
            }
            else {
                return (AdjustmentRule[])_adjustmentRules.Clone();
            }
        }

        //
        // ConvertTimeBySystemTimeZoneId -
        //
        // Converts the value of a DateTime object from sourceTimeZone to destinationTimeZone
        //
        static public DateTimeOffset ConvertTimeBySystemTimeZoneId(DateTimeOffset dateTimeOffset, String destinationTimeZoneId) {
            return ConvertTime(dateTimeOffset, FindSystemTimeZoneById(destinationTimeZoneId));
        }

        static public DateTime ConvertTimeBySystemTimeZoneId(DateTime dateTime, String destinationTimeZoneId) {
            return ConvertTime(dateTime, FindSystemTimeZoneById(destinationTimeZoneId));
        }

        static public DateTime ConvertTimeBySystemTimeZoneId(DateTime dateTime, String sourceTimeZoneId, String destinationTimeZoneId) {
            if (dateTime.Kind == DateTimeKind.Local && String.Compare(sourceTimeZoneId, TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase) == 0) {
                // TimeZoneInfo.Local can be cleared by another thread calling TimeZoneInfo.ClearCachedData.
                // Take snapshot of cached data to guarantee this method will not be impacted by the ClearCachedData call.
                // Without the snapshot, there is a chance that ConvertTime will throw since 'source' won't
                // be reference equal to the new TimeZoneInfo.Local
                //
                CachedData cachedData = s_cachedData;
                return ConvertTime(dateTime, cachedData.Local, FindSystemTimeZoneById(destinationTimeZoneId), TimeZoneInfoOptions.None, cachedData);
            }    
            else if (dateTime.Kind == DateTimeKind.Utc && String.Compare(sourceTimeZoneId, TimeZoneInfo.Utc.Id, StringComparison.OrdinalIgnoreCase) == 0) {
                // TimeZoneInfo.Utc can be cleared by another thread calling TimeZoneInfo.ClearCachedData.
                // Take snapshot of cached data to guarantee this method will not be impacted by the ClearCachedData call.
                // Without the snapshot, there is a chance that ConvertTime will throw since 'source' won't
                // be reference equal to the new TimeZoneInfo.Utc
                //
                CachedData cachedData = s_cachedData;
                return ConvertTime(dateTime, cachedData.Utc, FindSystemTimeZoneById(destinationTimeZoneId), TimeZoneInfoOptions.None, cachedData);
            }
            else {
                return ConvertTime(dateTime, FindSystemTimeZoneById(sourceTimeZoneId), FindSystemTimeZoneById(destinationTimeZoneId));
            }
        }

        //
        // ConvertTimeFromUtc -
        //
        // Converts the value of a DateTime object from Coordinated Universal Time (UTC) to
        // the destinationTimeZone.
        //
        static public DateTime ConvertTimeFromUtc(DateTime dateTime, TimeZoneInfo destinationTimeZone)
        {
            CachedData cachedData = s_cachedData;
            return ConvertTime(dateTime, cachedData.Utc, destinationTimeZone, TimeZoneInfoOptions.None, cachedData);
        }


        //
        // ConvertTimeToUtc -
        //
        // Converts the value of a DateTime object to Coordinated Universal Time (UTC).
        //
        static public DateTime ConvertTimeToUtc(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return dateTime;
            }
            CachedData cachedData = s_cachedData;
            return ConvertTime(dateTime, cachedData.Local, cachedData.Utc, TimeZoneInfoOptions.None, cachedData);
        }

        static public DateTime ConvertTimeToUtc(DateTime dateTime, TimeZoneInfo sourceTimeZone)
        {
            CachedData cachedData = s_cachedData;
            return ConvertTime(dateTime, sourceTimeZone, cachedData.Utc, TimeZoneInfoOptions.None, cachedData);
        }

        public override bool Equals(object obj)
        {
            TimeZoneInfo tzi = obj as TimeZoneInfo;
            if (null == tzi)
            {
                return false;
            }
            return Equals(tzi);
        }

        //    
        // FromSerializedString -
        //
        static public TimeZoneInfo FromSerializedString(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (source.Length == 0)
            {
                throw new ArgumentException(SR.Argument_InvalidSerializedString, "source");
            }
            Contract.EndContractBlock();

            return StringSerializer.GetDeserializedTimeZoneInfo(source);
        }


        //
        // GetHashCode -
        //
        public override int GetHashCode()
        {
            return _id.ToUpper().GetHashCode();
        }

        //
        // GetSystemTimeZones -
        //
        // returns a ReadOnlyCollection<TimeZoneInfo> containing all valid TimeZone's
        // from the local machine.  The entries in the collection are sorted by
        // 'DisplayName'.
        //
        // This method does *not* throw TimeZoneNotFoundException or
        // InvalidTimeZoneException.
        //
        // <SecurityKernel Critical="True" Ring="0">
        // <Asserts Name="Imperative: System.Security.PermissionSet" />
        // </SecurityKernel>
        static public ReadOnlyCollection<TimeZoneInfo> GetSystemTimeZones()
        {

            CachedData cachedData = s_cachedData;

            lock (cachedData)
            {
                if (cachedData._readOnlySystemTimeZones == null)
                {
                    using (RegistryKey reg = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE).OpenSubKey(
                                        c_timeZonesRegistryHive,
                                        false
                                        ))
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
                        cachedData._allSystemTimeZonesRead = true;
                    }

                    List<TimeZoneInfo> list = new List<TimeZoneInfo>();
                    if (cachedData._systemTimeZones != null)
                    {
                        // return a collection of the cached system time zones
                        foreach (var pair in cachedData._systemTimeZones)
                        {
                            list.Add(pair.Value);
                        }
                    }

                    // sort and copy the TimeZoneInfo's into a ReadOnlyCollection for the user
                    list.Sort(new TimeZoneInfoComparer());

                    cachedData._readOnlySystemTimeZones = new ReadOnlyCollection<TimeZoneInfo>(list);
                }
            }
            return cachedData._readOnlySystemTimeZones;
        }


        //
        // ToSerializedString -
        //
        // "TimeZoneInfo"           := TimeZoneInfo Data;[AdjustmentRule Data 1];...;[AdjustmentRule Data N]
        //
        // "TimeZoneInfo Data"      := <_id>;<_baseUtcOffset>;<_displayName>;
        //                          <_standardDisplayName>;<_daylightDispayName>;
        //
        // "AdjustmentRule Data" := <DateStart>;<DateEnd>;<DaylightDelta>;
        //                          [TransitionTime Data DST Start]
        //                          [TransitionTime Data DST End]
        //
        // "TransitionTime Data" += <DaylightStartTimeOfDat>;<Month>;<Week>;<DayOfWeek>;<Day>
        //
        public String ToSerializedString()
        {
            return StringSerializer.GetSerializedString(this);
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
            else {
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

        private TimeZoneInfo(
                String id,
                TimeSpan baseUtcOffset,
                String displayName,
                String standardDisplayName,
                String daylightDisplayName,
                AdjustmentRule[] adjustmentRules,
                Boolean disableDaylightSavingTime)
        {

            Boolean adjustmentRulesSupportDst;
            ValidateTimeZoneInfo(id, baseUtcOffset, adjustmentRules, out adjustmentRulesSupportDst);

            if (!disableDaylightSavingTime && adjustmentRules != null && adjustmentRules.Length > 0)
            {
                _adjustmentRules = (AdjustmentRule[])adjustmentRules.Clone();
            }

            _id = id;
            _baseUtcOffset = baseUtcOffset;
            _displayName = displayName;
            _standardDisplayName = standardDisplayName;
            _daylightDisplayName = (disableDaylightSavingTime ? null : daylightDisplayName);
            _supportsDaylightSavingTime = adjustmentRulesSupportDst && !disableDaylightSavingTime;
        }

        //
        // CreateCustomTimeZone -
        // 
        // returns a TimeZoneInfo instance that may
        // support Daylight Saving Time
        //
        static public TimeZoneInfo CreateCustomTimeZone(
                String id,
                TimeSpan baseUtcOffset,
                String displayName,
                String standardDisplayName,
                String daylightDisplayName,
                AdjustmentRule[] adjustmentRules)
        {

            return new TimeZoneInfo(
                           id,
                           baseUtcOffset,
                           displayName,
                           standardDisplayName,
                           daylightDisplayName,
                           adjustmentRules,
                           false);
        }


        //
        // CreateCustomTimeZone -
        // 
        // returns a TimeZoneInfo instance that may
        // support Daylight Saving Time
        //
        // This class factory method is identical to the
        // TimeZoneInfo private constructor
        //
        static public TimeZoneInfo CreateCustomTimeZone(
                String id,
                TimeSpan baseUtcOffset,
                String displayName,
                String standardDisplayName,
                String daylightDisplayName,
                AdjustmentRule[] adjustmentRules,
                Boolean disableDaylightSavingTime)
        {

            return new TimeZoneInfo(
                            id,
                            baseUtcOffset,
                            displayName,
                            standardDisplayName,
                            daylightDisplayName,
                            adjustmentRules,
                            disableDaylightSavingTime);
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
        static private Boolean CheckDaylightSavingTimeNotSupported(TIME_ZONE_INFORMATION timeZone)
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
        static private AdjustmentRule CreateAdjustmentRuleFromTimeZoneInformation(REGISTRY_TIME_ZONE_INFORMATION timeZoneInformation, DateTime startDate, DateTime endDate, int defaultBaseUtcOffset)
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
        static private String FindIdFromTimeZoneInformation(TIME_ZONE_INFORMATION timeZone, out Boolean dstDisabled)
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
        static private TimeZoneInfo GetLocalTimeZoneFromWin32Data(TIME_ZONE_INFORMATION timeZoneInformation, Boolean dstDisabled)
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
        static public TimeZoneInfo FindSystemTimeZoneById(string id) {

            // Special case for Utc as it will not exist in the dictionary with the rest
            // of the system time zones.  There is no need to do this check for Local.Id
            // since Local is a real time zone that exists in the dictionary cache
            if (String.Compare(id, c_utcId, StringComparison.OrdinalIgnoreCase) == 0) {
                return TimeZoneInfo.Utc;
            }

            if (id == null) {
                throw new ArgumentNullException("id");
            }
            else if (id.Length == 0 || id.Length > c_maxKeyLength || id.Contains("\0")) {
                throw new TimeZoneNotFoundException(String.Format(SR.TimeZoneNotFound_MissingRegistryData, id));
            }

            TimeZoneInfo value;
            Exception e;

            TimeZoneInfoResult result;

            CachedData cachedData = s_cachedData;

            lock (cachedData) {
                result = TryGetTimeZone(id, false, out value, out e, cachedData);
            }

            if (result == TimeZoneInfoResult.Success) {
                return value;
            }
            else if (result == TimeZoneInfoResult.InvalidTimeZoneException) {
                throw new InvalidTimeZoneException(String.Format(SR.InvalidTimeZone_InvalidRegistryData, id), e);
            }
            else if (result == TimeZoneInfoResult.SecurityException) {
                throw new SecurityException(String.Format(SR.Security_CannotReadRegistryData, id), e);
            }
            else {
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
        static private bool TransitionTimeFromTimeZoneInformation(REGISTRY_TIME_ZONE_INFORMATION timeZoneInformation, out TransitionTime transitionTime, bool readStartDate)
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
                else {
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
            else {
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
                else {
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
        static private bool TryCreateAdjustmentRules(string id, REGISTRY_TIME_ZONE_INFORMATION defaultTimeZoneInformation, out AdjustmentRule[] rules, out Exception e, int defaultBaseUtcOffset)
        {
            e = null;

            try
            {
                using (RegistryKey dynamicKey = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE).OpenSubKey(
                                   String.Format(CultureInfo.InvariantCulture, "{0}\\{1}\\Dynamic DST",
                                       c_timeZonesRegistryHive, id),
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
                        else {
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
                        else {
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
        static private Boolean TryCompareStandardDate(TIME_ZONE_INFORMATION timeZone, REGISTRY_TIME_ZONE_INFORMATION registryTimeZoneInfo)
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
                              String.Format(CultureInfo.InvariantCulture, "{0}\\{1}",
                                  c_timeZonesRegistryHive, id),
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
        static private string TryGetLocalizedNameByMuiNativeResource(string resource)
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
            string[] resources = resource.Split(new char[] { ',' }, StringSplitOptions.None);
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
            string tzresDll = resources[0].TrimStart(new char[] { '@' });

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
            fixed (char* file = filePath.ToCharArray())
            {
                using (SafeLibraryHandle handle =
                           new SafeLibraryHandle(Interop.mincore.LoadLibraryEx(file, IntPtr.Zero, Interop.mincore.LOAD_LIBRARY_AS_DATAFILE)))
                {

                    if (!handle.IsInvalid)
                    {
                        StringBuilder localizedResource = StringBuilderCache.Acquire(Interop.mincore.LOAD_STRING_MAX_LENGTH);
                        localizedResource.Length = Interop.mincore.LOAD_STRING_MAX_LENGTH;

                        int result = Interop.mincore.LoadString(handle, resource,
                                         localizedResource, localizedResource.Length);

                        if (result != 0)
                        {
                            return StringBuilderCache.GetStringAndRelease(localizedResource);
                        }
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
        static private Boolean TryGetLocalizedNamesByRegistryKey(RegistryKey key, out String displayName, out String standardName, out String daylightName)
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
        static private TimeZoneInfoResult TryGetTimeZoneByRegistryKey(string id, out TimeZoneInfo value, out Exception e)
        {
            e = null;

            using (RegistryKey key = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE).OpenSubKey(
                              String.Format(CultureInfo.InvariantCulture, "{0}\\{1}",
                                  c_timeZonesRegistryHive, id),
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
        static private TimeZoneInfoResult TryGetTimeZone(string id, Boolean dstDisabled, out TimeZoneInfo value, out Exception e, CachedData cachedData)
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
                    else {
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
                    else {
                        value = new TimeZoneInfo(match._id, match._baseUtcOffset, match._displayName, match._standardDisplayName,
                                              match._daylightDisplayName, match._adjustmentRules, false);
                    }
                }
                else {
                    value = null;
                }
            }
            else {
                result = TimeZoneInfoResult.TimeZoneNotFoundException;
                value = null;
            }

            return result;
        }

        /*============================================================
        **
        ** Class: TimeZoneInfo.AdjustmentRule
        **
        **
        ** Purpose: 
        ** This class is used to represent a Dynamic TimeZone.  It
        ** has methods for converting a DateTime to UTC from local time
        ** and to local time from UTC and methods for getting the 
        ** standard name and daylight name of the time zone.  
        **
        **
        ============================================================*/
        [Serializable]
        [TypeForwardedFrom("System.Core, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")]
        sealed public class AdjustmentRule : IEquatable<AdjustmentRule>, ISerializable, IDeserializationCallback
        {

            // ---- SECTION:  members supporting exposed properties -------------*
            private DateTime _dateStart;
            private DateTime _dateEnd;
            private TimeSpan _daylightDelta;
            private TransitionTime _daylightTransitionStart;
            private TransitionTime _daylightTransitionEnd;
            private TimeSpan _baseUtcOffsetDelta;   // delta from the default Utc offset (utcOffset = defaultUtcOffset + _baseUtcOffsetDelta)


            // ---- SECTION: public properties --------------*
            public DateTime DateStart
            {
                get
                {
                    return this._dateStart;
                }
            }

            public DateTime DateEnd
            {
                get
                {
                    return this._dateEnd;
                }
            }

            public TimeSpan DaylightDelta
            {
                get
                {
                    return this._daylightDelta;
                }
            }


            public TransitionTime DaylightTransitionStart
            {
                get
                {
                    return this._daylightTransitionStart;
                }
            }


            public TransitionTime DaylightTransitionEnd
            {
                get
                {
                    return this._daylightTransitionEnd;
                }
            }

            internal TimeSpan BaseUtcOffsetDelta
            {
                get
                {
                    return this._baseUtcOffsetDelta;
                }
            }

            internal bool HasDaylightSaving
            {
                get
                {
                    return this.DaylightDelta != TimeSpan.Zero ||
                            this.DaylightTransitionStart.TimeOfDay != DateTime.MinValue ||
                            this.DaylightTransitionEnd.TimeOfDay != DateTime.MinValue.AddMilliseconds(1);
                }
            }

            // ---- SECTION: public methods --------------*

            // IEquatable<AdjustmentRule>
            public bool Equals(AdjustmentRule other)
            {
                bool equals = (other != null
                     && this._dateStart == other._dateStart
                     && this._dateEnd == other._dateEnd
                     && this._daylightDelta == other._daylightDelta
                     && this._baseUtcOffsetDelta == other._baseUtcOffsetDelta);

                equals = equals && this._daylightTransitionEnd.Equals(other._daylightTransitionEnd)
                         && this._daylightTransitionStart.Equals(other._daylightTransitionStart);

                return equals;
            }


            public override int GetHashCode()
            {
                return _dateStart.GetHashCode();
            }



            // -------- SECTION: constructors -----------------*

            private AdjustmentRule() { }


            // -------- SECTION: factory methods -----------------*

            static public AdjustmentRule CreateAdjustmentRule(
                             DateTime dateStart,
                             DateTime dateEnd,
                             TimeSpan daylightDelta,
                             TransitionTime daylightTransitionStart,
                             TransitionTime daylightTransitionEnd)
            {

                ValidateAdjustmentRule(dateStart, dateEnd, daylightDelta,
                                       daylightTransitionStart, daylightTransitionEnd);

                AdjustmentRule rule = new AdjustmentRule();

                rule._dateStart = dateStart;
                rule._dateEnd = dateEnd;
                rule._daylightDelta = daylightDelta;
                rule._daylightTransitionStart = daylightTransitionStart;
                rule._daylightTransitionEnd = daylightTransitionEnd;
                rule._baseUtcOffsetDelta = TimeSpan.Zero;

                return rule;
            }

            static internal AdjustmentRule CreateAdjustmentRule(
                             DateTime dateStart,
                             DateTime dateEnd,
                             TimeSpan daylightDelta,
                             TransitionTime daylightTransitionStart,
                             TransitionTime daylightTransitionEnd,
                             TimeSpan baseUtcOffsetDelta)
            {
                AdjustmentRule rule = CreateAdjustmentRule(dateStart, dateEnd, daylightDelta, daylightTransitionStart, daylightTransitionEnd);
                rule._baseUtcOffsetDelta = baseUtcOffsetDelta;
                return rule;
            }

            // ----- SECTION: internal utility methods ----------------*

            //
            // When Windows sets the daylight transition start Jan 1st at 12:00 AM, it means the year starts with the daylight saving on. 
            // We have to special case this value and not adjust it when checking if any date is in the daylight saving period. 
            //
            internal bool IsStartDateMarkerForBeginningOfYear()
            {
                return DaylightTransitionStart.Month == 1 && DaylightTransitionStart.Day == 1 && DaylightTransitionStart.TimeOfDay.Hour == 0 &&
                       DaylightTransitionStart.TimeOfDay.Minute == 0 && DaylightTransitionStart.TimeOfDay.Second == 0 &&
                       _dateStart.Year == _dateEnd.Year;
            }

            //
            // When Windows sets the daylight transition end Jan 1st at 12:00 AM, it means the year ends with the daylight saving on. 
            // We have to special case this value and not adjust it when checking if any date is in the daylight saving period. 
            //
            internal bool IsEndDateMarkerForEndOfYear()
            {
                return DaylightTransitionEnd.Month == 1 && DaylightTransitionEnd.Day == 1 && DaylightTransitionEnd.TimeOfDay.Hour == 0 &&
                       DaylightTransitionEnd.TimeOfDay.Minute == 0 && DaylightTransitionEnd.TimeOfDay.Second == 0 &&
                       _dateStart.Year == _dateEnd.Year;
            }

            //
            // ValidateAdjustmentRule -
            //
            // Helper function that performs all of the validation checks for the 
            // factory methods and deserialization callback
            //
            static private void ValidateAdjustmentRule(
                             DateTime dateStart,
                             DateTime dateEnd,
                             TimeSpan daylightDelta,
                             TransitionTime daylightTransitionStart,
                             TransitionTime daylightTransitionEnd)
            {


                if (dateStart.Kind != DateTimeKind.Unspecified)
                {
                    throw new ArgumentException(SR.Argument_DateTimeKindMustBeUnspecified, "dateStart");
                }

                if (dateEnd.Kind != DateTimeKind.Unspecified)
                {
                    throw new ArgumentException(SR.Argument_DateTimeKindMustBeUnspecified, "dateEnd");
                }

                if (daylightTransitionStart.Equals(daylightTransitionEnd))
                {
                    throw new ArgumentException(SR.Argument_TransitionTimesAreIdentical,
                                                "daylightTransitionEnd");
                }


                if (dateStart > dateEnd)
                {
                    throw new ArgumentException(SR.Argument_OutOfOrderDateTimes, "dateStart");
                }

                if (TimeZoneInfo.UtcOffsetOutOfRange(daylightDelta))
                {
                    throw new ArgumentOutOfRangeException("daylightDelta", daylightDelta,
                        SR.ArgumentOutOfRange_UtcOffset);
                }

                if (daylightDelta.Ticks % TimeSpan.TicksPerMinute != 0)
                {
                    throw new ArgumentException(SR.Argument_TimeSpanHasSeconds,
                        "daylightDelta");
                }

                if (dateStart.TimeOfDay != TimeSpan.Zero)
                {
                    throw new ArgumentException(SR.Argument_DateTimeHasTimeOfDay,
                        "dateStart");
                }

                if (dateEnd.TimeOfDay != TimeSpan.Zero)
                {
                    throw new ArgumentException(SR.Argument_DateTimeHasTimeOfDay,
                        "dateEnd");
                }
                Contract.EndContractBlock();
            }



            // ----- SECTION: private serialization instance methods  ----------------*

            void IDeserializationCallback.OnDeserialization(Object sender)
            {
                // OnDeserialization is called after each instance of this class is deserialized.
                // This callback method performs AdjustmentRule validation after being deserialized.

                try
                {
                    ValidateAdjustmentRule(_dateStart, _dateEnd, _daylightDelta,
                                           _daylightTransitionStart, _daylightTransitionEnd);
                }
                catch (ArgumentException e)
                {
                    throw new SerializationException(SR.Serialization_InvalidData, e);
                }
            }

            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                if (info == null)
                {
                    throw new ArgumentNullException("info");
                }
                Contract.EndContractBlock();

                info.AddValue("DateStart", _dateStart);
                info.AddValue("DateEnd", _dateEnd);
                info.AddValue("DaylightDelta", _daylightDelta);
                info.AddValue("DaylightTransitionStart", _daylightTransitionStart);
                info.AddValue("DaylightTransitionEnd", _daylightTransitionEnd);
                info.AddValue("BaseUtcOffsetDelta", _baseUtcOffsetDelta);
            }

            AdjustmentRule(SerializationInfo info, StreamingContext context)
            {
                if (info == null)
                {
                    throw new ArgumentNullException("info");
                }

                _dateStart = (DateTime)info.GetValue("DateStart", typeof(DateTime));
                _dateEnd = (DateTime)info.GetValue("DateEnd", typeof(DateTime));
                _daylightDelta = (TimeSpan)info.GetValue("DaylightDelta", typeof(TimeSpan));
                _daylightTransitionStart = (TransitionTime)info.GetValue("DaylightTransitionStart", typeof(TransitionTime));
                _daylightTransitionEnd = (TransitionTime)info.GetValue("DaylightTransitionEnd", typeof(TransitionTime));

                object o = info.GetValueNoThrow("BaseUtcOffsetDelta", typeof(TimeSpan));
                if (o != null)
                {
                    _baseUtcOffsetDelta = (TimeSpan)o;
                }
            }
        }


        /*============================================================
        **
        ** Class: TimeZoneInfo.TransitionTime
        **
        **
        ** Purpose: 
        ** This class is used to represent a Dynamic TimeZone.  It
        ** has methods for converting a DateTime to UTC from local time
        ** and to local time from UTC and methods for getting the 
        ** standard name and daylight name of the time zone.  
        **
        **
        ============================================================*/
        [Serializable]
        [TypeForwardedFrom("System.Core, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")]
        public struct TransitionTime : IEquatable<TransitionTime>, ISerializable, IDeserializationCallback
        {

            // ---- SECTION:  members supporting exposed properties -------------*
            private DateTime _timeOfDay;
            private byte _month;
            private byte _week;
            private byte _day;
            private DayOfWeek _dayOfWeek;
            private Boolean _isFixedDateRule;


            // ---- SECTION: public properties --------------*
            public DateTime TimeOfDay
            {
                get
                {
                    return _timeOfDay;
                }
            }

            public Int32 Month
            {
                get
                {
                    return (int)_month;
                }
            }


            public Int32 Week
            {
                get
                {
                    return (int)_week;
                }
            }

            public Int32 Day
            {
                get
                {
                    return (int)_day;
                }
            }

            public DayOfWeek DayOfWeek
            {
                get
                {
                    return _dayOfWeek;
                }
            }

            public Boolean IsFixedDateRule
            {
                get
                {
                    return _isFixedDateRule;
                }
            }

            // ---- SECTION: public methods --------------*
            [Pure]
            public override bool Equals(Object obj)
            {
                if (obj is TransitionTime)
                {
                    return Equals((TransitionTime)obj);
                }
                return false;
            }

            public static bool operator ==(TransitionTime t1, TransitionTime t2)
            {
                return t1.Equals(t2);
            }

            public static bool operator !=(TransitionTime t1, TransitionTime t2)
            {
                return (!t1.Equals(t2));
            }

            [Pure]
            public bool Equals(TransitionTime other)
            {

                bool equal = (this._isFixedDateRule == other._isFixedDateRule
                             && this._timeOfDay == other._timeOfDay
                             && this._month == other._month);

                if (equal)
                {
                    if (other._isFixedDateRule)
                    {
                        equal = (this._day == other._day);
                    }
                    else {
                        equal = (this._week == other._week
                            && this._dayOfWeek == other._dayOfWeek);
                    }
                }
                return equal;
            }


            public override int GetHashCode()
            {
                return ((int)_month ^ (int)_week << 8);
            }


            // -------- SECTION: constructors -----------------*
            /*
                        private TransitionTime() {           
                            _timeOfDay = new DateTime();
                            _month = 0;
                            _week  = 0;
                            _day   = 0;
                            _dayOfWeek = DayOfWeek.Sunday;
                            _isFixedDateRule = false;
                        }
            */


            // -------- SECTION: factory methods -----------------*


            static public TransitionTime CreateFixedDateRule(
                    DateTime timeOfDay,
                    Int32 month,
                    Int32 day)
            {

                return CreateTransitionTime(timeOfDay, month, 1, day, DayOfWeek.Sunday, true);
            }


            static public TransitionTime CreateFloatingDateRule(
                    DateTime timeOfDay,
                    Int32 month,
                    Int32 week,
                    DayOfWeek dayOfWeek)
            {

                return CreateTransitionTime(timeOfDay, month, week, 1, dayOfWeek, false);
            }


            static private TransitionTime CreateTransitionTime(
                    DateTime timeOfDay,
                    Int32 month,
                    Int32 week,
                    Int32 day,
                    DayOfWeek dayOfWeek,
                    Boolean isFixedDateRule)
            {

                ValidateTransitionTime(timeOfDay, month, week, day, dayOfWeek);

                TransitionTime t = new TransitionTime();
                t._isFixedDateRule = isFixedDateRule;
                t._timeOfDay = timeOfDay;
                t._dayOfWeek = dayOfWeek;
                t._day = (byte)day;
                t._week = (byte)week;
                t._month = (byte)month;

                return t;
            }


            // ----- SECTION: internal utility methods ----------------*

            //
            // ValidateTransitionTime -
            //
            // Helper function that validates a TransitionTime instance
            //
            static private void ValidateTransitionTime(
                    DateTime timeOfDay,
                    Int32 month,
                    Int32 week,
                    Int32 day,
                    DayOfWeek dayOfWeek)
            {

                if (timeOfDay.Kind != DateTimeKind.Unspecified)
                {
                    throw new ArgumentException(SR.Argument_DateTimeKindMustBeUnspecified, "timeOfDay");
                }

                // Month range 1-12
                if (month < 1 || month > 12)
                {
                    throw new ArgumentOutOfRangeException("month", SR.ArgumentOutOfRange_MonthParam);
                }

                // Day range 1-31
                if (day < 1 || day > 31)
                {
                    throw new ArgumentOutOfRangeException("day", SR.ArgumentOutOfRange_DayParam);
                }

                // Week range 1-5
                if (week < 1 || week > 5)
                {
                    throw new ArgumentOutOfRangeException("week", SR.ArgumentOutOfRange_Week);
                }

                // DayOfWeek range 0-6
                if ((int)dayOfWeek < 0 || (int)dayOfWeek > 6)
                {
                    throw new ArgumentOutOfRangeException("dayOfWeek", SR.ArgumentOutOfRange_DayOfWeek);
                }
                Contract.EndContractBlock();

                if (timeOfDay.Year != 1 || timeOfDay.Month != 1
                || timeOfDay.Day != 1 || (timeOfDay.Ticks % TimeSpan.TicksPerMillisecond != 0))
                {
                    throw new ArgumentException(SR.Argument_DateTimeHasTicks, "timeOfDay");
                }
            }

            void IDeserializationCallback.OnDeserialization(Object sender)
            {
                // OnDeserialization is called after each instance of this class is deserialized.
                // This callback method performs TransitionTime validation after being deserialized.

                try
                {
                    ValidateTransitionTime(_timeOfDay, (Int32)_month, (Int32)_week, (Int32)_day, _dayOfWeek);
                }
                catch (ArgumentException e)
                {
                    throw new SerializationException(SR.Serialization_InvalidData, e);
                }
            }

            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                if (info == null)
                {
                    throw new ArgumentNullException("info");
                }
                Contract.EndContractBlock();

                info.AddValue("TimeOfDay", _timeOfDay);
                info.AddValue("Month", _month);
                info.AddValue("Week", _week);
                info.AddValue("Day", _day);
                info.AddValue("DayOfWeek", _dayOfWeek);
                info.AddValue("IsFixedDateRule", _isFixedDateRule);
            }

            TransitionTime(SerializationInfo info, StreamingContext context)
            {
                if (info == null)
                {
                    throw new ArgumentNullException("info");
                }

                _timeOfDay = (DateTime)info.GetValue("TimeOfDay", typeof(DateTime));
                _month = (byte)info.GetValue("Month", typeof(byte));
                _week = (byte)info.GetValue("Week", typeof(byte));
                _day = (byte)info.GetValue("Day", typeof(byte));
                _dayOfWeek = (DayOfWeek)info.GetValue("DayOfWeek", typeof(DayOfWeek));
                _isFixedDateRule = (Boolean)info.GetValue("IsFixedDateRule", typeof(Boolean));
            }
        }


        /*============================================================
        **
        ** Class: TimeZoneInfo.StringSerializer
        **
        **
        ** Purpose: 
        ** This class is used to serialize and deserialize TimeZoneInfo
        ** objects based on the custom string serialization format
        **
        **
        ============================================================*/
        sealed private class StringSerializer
        {

            // ---- SECTION: private members  -------------*
            private enum State
            {
                Escaped = 0,
                NotEscaped = 1,
                StartOfToken = 2,
                EndOfLine = 3
            }

            private String _serializedText;
            private int _currentTokenStartIndex;
            private State _state;

            // the majority of the strings contained in the OS time zones fit in 64 chars
            private const int initialCapacityForString = 64;
            private const char esc = '\\';
            private const char sep = ';';
            private const char lhs = '[';
            private const char rhs = ']';
            private const string escString = "\\";
            private const string sepString = ";";
            private const string lhsString = "[";
            private const string rhsString = "]";
            private const string escapedEsc = "\\\\";
            private const string escapedSep = "\\;";
            private const string escapedLhs = "\\[";
            private const string escapedRhs = "\\]";
            private const string dateTimeFormat = "MM:dd:yyyy";
            private const string timeOfDayFormat = "HH:mm:ss.FFF";


            // ---- SECTION: public static methods --------------*

            //
            // GetSerializedString -
            //
            // static method that creates the custom serialized string
            // representation of a TimeZoneInfo instance
            //
            static public String GetSerializedString(TimeZoneInfo zone)
            {
                StringBuilder serializedText = StringBuilderCache.Acquire();

                //
                // <_id>;<_baseUtcOffset>;<_displayName>;<_standardDisplayName>;<_daylightDispayName>
                //
                serializedText.Append(SerializeSubstitute(zone.Id));
                serializedText.Append(sep);
                serializedText.Append(SerializeSubstitute(
                           zone.BaseUtcOffset.TotalMinutes.ToString(CultureInfo.InvariantCulture)));
                serializedText.Append(sep);
                serializedText.Append(SerializeSubstitute(zone.DisplayName));
                serializedText.Append(sep);
                serializedText.Append(SerializeSubstitute(zone.StandardName));
                serializedText.Append(sep);
                serializedText.Append(SerializeSubstitute(zone.DaylightName));
                serializedText.Append(sep);

                AdjustmentRule[] rules = zone.GetAdjustmentRules();

                if (rules != null && rules.Length > 0)
                {
                    for (int i = 0; i < rules.Length; i++)
                    {
                        AdjustmentRule rule = rules[i];

                        serializedText.Append(lhs);
                        serializedText.Append(SerializeSubstitute(rule.DateStart.ToString(
                                                dateTimeFormat, DateTimeFormatInfo.InvariantInfo)));
                        serializedText.Append(sep);
                        serializedText.Append(SerializeSubstitute(rule.DateEnd.ToString(
                                                dateTimeFormat, DateTimeFormatInfo.InvariantInfo)));
                        serializedText.Append(sep);
                        serializedText.Append(SerializeSubstitute(rule.DaylightDelta.TotalMinutes.ToString(CultureInfo.InvariantCulture)));
                        serializedText.Append(sep);
                        // serialize the TransitionTime's
                        SerializeTransitionTime(rule.DaylightTransitionStart, serializedText);
                        serializedText.Append(sep);
                        SerializeTransitionTime(rule.DaylightTransitionEnd, serializedText);
                        serializedText.Append(sep);
                        if (rule.BaseUtcOffsetDelta != TimeSpan.Zero)
                        { // Serialize it only when BaseUtcOffsetDelta has a value to reduce the impact of adding rule.BaseUtcOffsetDelta
                            serializedText.Append(SerializeSubstitute(rule.BaseUtcOffsetDelta.TotalMinutes.ToString(CultureInfo.InvariantCulture)));
                            serializedText.Append(sep);
                        }
                        serializedText.Append(rhs);
                    }
                }
                serializedText.Append(sep);
                return StringBuilderCache.GetStringAndRelease(serializedText);
            }


            //
            // GetDeserializedTimeZoneInfo -
            //
            // static method that instantiates a TimeZoneInfo from a custom serialized
            // string
            //
            static public TimeZoneInfo GetDeserializedTimeZoneInfo(String source)
            {
                StringSerializer s = new StringSerializer(source);

                String id = s.GetNextStringValue(false);
                TimeSpan baseUtcOffset = s.GetNextTimeSpanValue(false);
                String displayName = s.GetNextStringValue(false);
                String standardName = s.GetNextStringValue(false);
                String daylightName = s.GetNextStringValue(false);
                AdjustmentRule[] rules = s.GetNextAdjustmentRuleArrayValue(false);

                try
                {
                    return TimeZoneInfo.CreateCustomTimeZone(id, baseUtcOffset, displayName, standardName, daylightName, rules);
                }
                catch (ArgumentException ex)
                {
                    throw new SerializationException(SR.Serialization_InvalidData, ex);
                }
                catch (InvalidTimeZoneException ex)
                {
                    throw new SerializationException(SR.Serialization_InvalidData, ex);
                }
            }

            // ---- SECTION: public instance methods --------------*


            // -------- SECTION: constructors -----------------*

            //
            // StringSerializer -
            //
            // private constructor - used by GetDeserializedTimeZoneInfo()
            //
            private StringSerializer(String str)
            {
                _serializedText = str;
                _state = State.StartOfToken;
            }



            // ----- SECTION: internal static utility methods ----------------*

            //
            // SerializeSubstitute -
            //
            // returns a new string with all of the reserved sub-strings escaped
            //
            // ";" -> "\;"
            // "[" -> "\["
            // "]" -> "\]"
            // "\" -> "\\"
            //
            static private String SerializeSubstitute(String text)
            {
                text = text.Replace(escString, escapedEsc);
                text = text.Replace(lhsString, escapedLhs);
                text = text.Replace(rhsString, escapedRhs);
                return text.Replace(sepString, escapedSep);
            }


            //
            // SerializeTransitionTime -
            //
            // Helper method to serialize a TimeZoneInfo.TransitionTime object
            //
            static private void SerializeTransitionTime(TransitionTime time, StringBuilder serializedText)
            {
                serializedText.Append(lhs);
                Int32 fixedDate = (time.IsFixedDateRule ? 1 : 0);
                serializedText.Append(fixedDate.ToString(CultureInfo.InvariantCulture));
                serializedText.Append(sep);

                if (time.IsFixedDateRule)
                {
                    serializedText.Append(SerializeSubstitute(time.TimeOfDay.ToString(timeOfDayFormat, DateTimeFormatInfo.InvariantInfo)));
                    serializedText.Append(sep);
                    serializedText.Append(SerializeSubstitute(time.Month.ToString(CultureInfo.InvariantCulture)));
                    serializedText.Append(sep);
                    serializedText.Append(SerializeSubstitute(time.Day.ToString(CultureInfo.InvariantCulture)));
                    serializedText.Append(sep);
                }
                else {
                    serializedText.Append(SerializeSubstitute(time.TimeOfDay.ToString(timeOfDayFormat, DateTimeFormatInfo.InvariantInfo)));
                    serializedText.Append(sep);
                    serializedText.Append(SerializeSubstitute(time.Month.ToString(CultureInfo.InvariantCulture)));
                    serializedText.Append(sep);
                    serializedText.Append(SerializeSubstitute(time.Week.ToString(CultureInfo.InvariantCulture)));
                    serializedText.Append(sep);
                    serializedText.Append(SerializeSubstitute(((int)time.DayOfWeek).ToString(CultureInfo.InvariantCulture)));
                    serializedText.Append(sep);
                }
                serializedText.Append(rhs);
            }

            //
            // VerifyIsEscapableCharacter -
            //
            // Helper function to determine if the passed in string token is allowed to be preceeded by an escape sequence token
            //
            static private void VerifyIsEscapableCharacter(char c)
            {
                if (c != esc && c != sep && c != lhs && c != rhs)
                {
                    throw new SerializationException(String.Format(SR.Serialization_InvalidEscapeSequence, c));
                }
            }

            // ----- SECTION: internal instance utility methods ----------------*

            //
            // SkipVersionNextDataFields -
            //
            // Helper function that reads past "v.Next" data fields.  Receives a "depth" parameter indicating the
            // current relative nested bracket depth that _currentTokenStartIndex is at.  The function ends
            // successfully when "depth" returns to zero (0).
            //
            //
            private void SkipVersionNextDataFields(Int32 depth /* starting depth in the nested brackets ('[', ']')*/)
            {
                if (_currentTokenStartIndex < 0 || _currentTokenStartIndex >= _serializedText.Length)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }
                State tokenState = State.NotEscaped;

                // walk the serialized text, building up the token as we go...
                for (int i = _currentTokenStartIndex; i < _serializedText.Length; i++)
                {
                    if (tokenState == State.Escaped)
                    {
                        VerifyIsEscapableCharacter(_serializedText[i]);
                        tokenState = State.NotEscaped;
                    }
                    else if (tokenState == State.NotEscaped)
                    {
                        switch (_serializedText[i])
                        {
                            case esc:
                                tokenState = State.Escaped;
                                break;

                            case lhs:
                                depth++;
                                break;
                            case rhs:
                                depth--;
                                if (depth == 0)
                                {
                                    _currentTokenStartIndex = i + 1;
                                    if (_currentTokenStartIndex >= _serializedText.Length)
                                    {
                                        _state = State.EndOfLine;
                                    }
                                    else {
                                        _state = State.StartOfToken;
                                    }
                                    return;
                                }
                                break;

                            case '\0':
                                // invalid character
                                throw new SerializationException(SR.Serialization_InvalidData);

                            default:
                                break;
                        }
                    }
                }

                throw new SerializationException(SR.Serialization_InvalidData);
            }


            //
            // GetNextStringValue -
            //
            // Helper function that reads a string token from the serialized text.  The function
            // updates the _currentTokenStartIndex to point to the next token on exit.  Also _state
            // is set to either State.StartOfToken or State.EndOfLine on exit.
            //
            // The function takes a parameter "canEndWithoutSeparator".  
            //
            // * When set to 'false' the function requires the string token end with a ";".
            // * When set to 'true' the function requires that the string token end with either
            //   ";", State.EndOfLine, or "]".  In the case that "]" is the terminal case the
            //   _currentTokenStartIndex is left pointing at index "]" to allow the caller to update
            //   its depth logic.
            //
            private String GetNextStringValue(Boolean canEndWithoutSeparator)
            {

                // first verify the internal state of the object
                if (_state == State.EndOfLine)
                {
                    if (canEndWithoutSeparator)
                    {
                        return null;
                    }
                    else {
                        throw new SerializationException(SR.Serialization_InvalidData);
                    }
                }
                if (_currentTokenStartIndex < 0 || _currentTokenStartIndex >= _serializedText.Length)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }
                State tokenState = State.NotEscaped;
                StringBuilder token = StringBuilderCache.Acquire(initialCapacityForString);

                // walk the serialized text, building up the token as we go...
                for (int i = _currentTokenStartIndex; i < _serializedText.Length; i++)
                {
                    if (tokenState == State.Escaped)
                    {
                        VerifyIsEscapableCharacter(_serializedText[i]);
                        token.Append(_serializedText[i]);
                        tokenState = State.NotEscaped;
                    }
                    else if (tokenState == State.NotEscaped)
                    {
                        switch (_serializedText[i])
                        {
                            case esc:
                                tokenState = State.Escaped;
                                break;

                            case lhs:
                                // '[' is an unexpected character
                                throw new SerializationException(SR.Serialization_InvalidData);

                            case rhs:
                                if (canEndWithoutSeparator)
                                {
                                    // if ';' is not a required terminal then treat ']' as a terminal
                                    // leave _currentTokenStartIndex pointing to ']' so our callers can handle
                                    // this special case
                                    _currentTokenStartIndex = i;
                                    _state = State.StartOfToken;
                                    return token.ToString();
                                }
                                else {
                                    // ']' is an unexpected character
                                    throw new SerializationException(SR.Serialization_InvalidData);
                                }

                            case sep:
                                _currentTokenStartIndex = i + 1;
                                if (_currentTokenStartIndex >= _serializedText.Length)
                                {
                                    _state = State.EndOfLine;
                                }
                                else {
                                    _state = State.StartOfToken;
                                }
                                return StringBuilderCache.GetStringAndRelease(token);

                            case '\0':
                                // invalid character
                                throw new SerializationException(SR.Serialization_InvalidData);

                            default:
                                token.Append(_serializedText[i]);
                                break;
                        }
                    }
                }
                //
                // we are at the end of the line
                //
                if (tokenState == State.Escaped)
                {
                    // we are at the end of the serialized text but we are in an escaped state
                    throw new SerializationException(String.Format(SR.Serialization_InvalidEscapeSequence, String.Empty));
                }

                if (!canEndWithoutSeparator)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }
                _currentTokenStartIndex = _serializedText.Length;
                _state = State.EndOfLine;
                return StringBuilderCache.GetStringAndRelease(token);
            }

            //
            // GetNextDateTimeValue -
            //
            // Helper function to read a DateTime token.  Takes a boolean "canEndWithoutSeparator"
            // and a "format" string.
            //
            private DateTime GetNextDateTimeValue(Boolean canEndWithoutSeparator, string format)
            {
                String token = GetNextStringValue(canEndWithoutSeparator);
                DateTime time;
                if (!DateTime.TryParseExact(token, format, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None, out time))
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }
                return time;
            }

            //
            // GetNextTimeSpanValue -
            //
            // Helper function to read a DateTime token.  Takes a boolean "canEndWithoutSeparator".
            //
            private TimeSpan GetNextTimeSpanValue(Boolean canEndWithoutSeparator)
            {
                Int32 token = GetNextInt32Value(canEndWithoutSeparator);

                try
                {
                    return new TimeSpan(0 /* hours */, token /* minutes */, 0 /* seconds */);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    throw new SerializationException(SR.Serialization_InvalidData, e);
                }
            }


            //
            // GetNextInt32Value -
            //
            // Helper function to read an Int32 token.  Takes a boolean "canEndWithoutSeparator".
            //
            private Int32 GetNextInt32Value(Boolean canEndWithoutSeparator)
            {
                String token = GetNextStringValue(canEndWithoutSeparator);
                Int32 value;
                if (!Int32.TryParse(token, NumberStyles.AllowLeadingSign /* "[sign]digits" */, CultureInfo.InvariantCulture, out value))
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }
                return value;
            }


            //
            // GetNextAdjustmentRuleArrayValue -
            //
            // Helper function to read an AdjustmentRule[] token.  Takes a boolean "canEndWithoutSeparator".
            //
            private AdjustmentRule[] GetNextAdjustmentRuleArrayValue(Boolean canEndWithoutSeparator)
            {
                List<AdjustmentRule> rules = new List<AdjustmentRule>(1);
                int count = 0;

                // individual AdjustmentRule array elements do not require semicolons
                AdjustmentRule rule = GetNextAdjustmentRuleValue(true);
                while (rule != null)
                {
                    rules.Add(rule);
                    count++;

                    rule = GetNextAdjustmentRuleValue(true);
                }

                if (!canEndWithoutSeparator)
                {
                    // the AdjustmentRule array must end with a separator
                    if (_state == State.EndOfLine)
                    {
                        throw new SerializationException(SR.Serialization_InvalidData);
                    }
                    if (_currentTokenStartIndex < 0 || _currentTokenStartIndex >= _serializedText.Length)
                    {
                        throw new SerializationException(SR.Serialization_InvalidData);
                    }
                }

                return (count != 0 ? rules.ToArray() : null);
            }

            //
            // GetNextAdjustmentRuleValue -
            //
            // Helper function to read an AdjustmentRule token.  Takes a boolean "canEndWithoutSeparator".
            //
            private AdjustmentRule GetNextAdjustmentRuleValue(Boolean canEndWithoutSeparator)
            {
                // first verify the internal state of the object
                if (_state == State.EndOfLine)
                {
                    if (canEndWithoutSeparator)
                    {
                        return null;
                    }
                    else {
                        throw new SerializationException(SR.Serialization_InvalidData);
                    }
                }

                if (_currentTokenStartIndex < 0 || _currentTokenStartIndex >= _serializedText.Length)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }

                // check to see if the very first token we see is the separator
                if (_serializedText[_currentTokenStartIndex] == sep)
                {
                    return null;
                }

                // verify the current token is a left-hand-side marker ("[")
                if (_serializedText[_currentTokenStartIndex] != lhs)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }
                _currentTokenStartIndex++;

                DateTime dateStart = GetNextDateTimeValue(false, dateTimeFormat);
                DateTime dateEnd = GetNextDateTimeValue(false, dateTimeFormat);
                TimeSpan daylightDelta = GetNextTimeSpanValue(false);
                TransitionTime daylightStart = GetNextTransitionTimeValue(false);
                TransitionTime daylightEnd = GetNextTransitionTimeValue(false);
                TimeSpan baseUtcOffsetDelta = TimeSpan.Zero;
                // verify that the string is now at the right-hand-side marker ("]") ...

                if (_state == State.EndOfLine || _currentTokenStartIndex >= _serializedText.Length)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }

                // Check if we have baseUtcOffsetDelta in the serialized string and then deserialize it
                if ((_serializedText[_currentTokenStartIndex] >= '0' && _serializedText[_currentTokenStartIndex] <= '9') ||
                    _serializedText[_currentTokenStartIndex] == '-' || _serializedText[_currentTokenStartIndex] == '+')
                {
                    baseUtcOffsetDelta = GetNextTimeSpanValue(false);
                }

                if (_state == State.EndOfLine || _currentTokenStartIndex >= _serializedText.Length)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }

                if (_serializedText[_currentTokenStartIndex] != rhs)
                {
                    // skip ahead of any "v.Next" data at the end of the AdjustmentRule
                    //
                    // FUTURE: if the serialization format is extended in the future then this
                    // code section will need to be changed to read the new fields rather
                    // than just skipping the data at the end of the [AdjustmentRule].
                    SkipVersionNextDataFields(1);
                }
                else {
                    _currentTokenStartIndex++;
                }

                // create the AdjustmentRule from the deserialized fields ...

                AdjustmentRule rule;
                try
                {
                    rule = AdjustmentRule.CreateAdjustmentRule(dateStart, dateEnd, daylightDelta, daylightStart, daylightEnd, baseUtcOffsetDelta);
                }
                catch (ArgumentException e)
                {
                    throw new SerializationException(SR.Serialization_InvalidData, e);
                }

                // finally set the state to either EndOfLine or StartOfToken for the next caller
                if (_currentTokenStartIndex >= _serializedText.Length)
                {
                    _state = State.EndOfLine;
                }
                else {
                    _state = State.StartOfToken;
                }
                return rule;
            }


            //
            // GetNextTransitionTimeValue -
            //
            // Helper function to read a TransitionTime token.  Takes a boolean "canEndWithoutSeparator".
            //
            private TransitionTime GetNextTransitionTimeValue(Boolean canEndWithoutSeparator)
            {

                // first verify the internal state of the object

                if (_state == State.EndOfLine
                    || (_currentTokenStartIndex < _serializedText.Length
                        && _serializedText[_currentTokenStartIndex] == rhs))
                {
                    //
                    // we are at the end of the line or we are starting at a "]" character
                    //
                    throw new SerializationException(SR.Serialization_InvalidData);
                }

                if (_currentTokenStartIndex < 0 || _currentTokenStartIndex >= _serializedText.Length)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }

                // verify the current token is a left-hand-side marker ("[")

                if (_serializedText[_currentTokenStartIndex] != lhs)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }
                _currentTokenStartIndex++;

                Int32 isFixedDate = GetNextInt32Value(false);

                if (isFixedDate != 0 && isFixedDate != 1)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }

                TransitionTime transition;

                DateTime timeOfDay = GetNextDateTimeValue(false, timeOfDayFormat);
                timeOfDay = new DateTime(1, 1, 1, timeOfDay.Hour, timeOfDay.Minute, timeOfDay.Second, timeOfDay.Millisecond);

                Int32 month = GetNextInt32Value(false);

                if (isFixedDate == 1)
                {
                    Int32 day = GetNextInt32Value(false);

                    try
                    {
                        transition = TransitionTime.CreateFixedDateRule(timeOfDay, month, day);
                    }
                    catch (ArgumentException e)
                    {
                        throw new SerializationException(SR.Serialization_InvalidData, e);
                    }
                }
                else {
                    Int32 week = GetNextInt32Value(false);
                    Int32 dayOfWeek = GetNextInt32Value(false);

                    try
                    {
                        transition = TransitionTime.CreateFloatingDateRule(timeOfDay, month, week, (DayOfWeek)dayOfWeek);
                    }
                    catch (ArgumentException e)
                    {
                        throw new SerializationException(SR.Serialization_InvalidData, e);
                    }

                }

                // verify that the string is now at the right-hand-side marker ("]") ...

                if (_state == State.EndOfLine || _currentTokenStartIndex >= _serializedText.Length)
                {
                    throw new SerializationException(SR.Serialization_InvalidData);
                }

                if (_serializedText[_currentTokenStartIndex] != rhs)
                {
                    // skip ahead of any "v.Next" data at the end of the AdjustmentRule
                    //
                    // FUTURE: if the serialization format is extended in the future then this
                    // code section will need to be changed to read the new fields rather
                    // than just skipping the data at the end of the [TransitionTime].
                    SkipVersionNextDataFields(1);
                }
                else {
                    _currentTokenStartIndex++;
                }

                // check to see if the string is now at the separator (";") ...
                Boolean sepFound = false;
                if (_currentTokenStartIndex < _serializedText.Length
                    && _serializedText[_currentTokenStartIndex] == sep)
                {
                    // handle the case where we ended on a ";"
                    _currentTokenStartIndex++;
                    sepFound = true;
                }

                if (!sepFound && !canEndWithoutSeparator)
                {
                    // we MUST end on a separator
                    throw new SerializationException(SR.Serialization_InvalidData);
                }


                // finally set the state to either EndOfLine or StartOfToken for the next caller
                if (_currentTokenStartIndex >= _serializedText.Length)
                {
                    _state = State.EndOfLine;
                }
                else {
                    _state = State.StartOfToken;
                }
                return transition;
            }
        }
    } // TimezoneInfo
} // namespace System
