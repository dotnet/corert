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
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using TIME_ZONE_INFORMATION = Interop.mincore.TIME_ZONE_INFORMATION;
using TIME_DYNAMIC_ZONE_INFORMATION = Interop.mincore.TIME_DYNAMIC_ZONE_INFORMATION;

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


    [TypeForwardedFrom("System.Core, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")]
    sealed public class TimeZoneInfo : IEquatable<TimeZoneInfo>
    {
        // ---- SECTION:  members supporting exposed properties -------------*
        private String _id;
        private String _displayName;
        private String _standardDisplayName;
        private String _daylightDisplayName;
        private TimeSpan _baseUtcOffset;
        private Boolean _supportsDaylightSavingTime;
        private AdjustmentRule[] _adjustmentRules;

        // constants for TimeZoneInfo.Local and TimeZoneInfo.Utc
        private const string c_utcId = "UTC";
        private const string c_localId = "Local";


        //
        // All cached data are encapsulated in a helper class to allow consistent view even when the data are refreshed using ClearCachedData()
        //
        // For example, TimeZoneInfo.Local can be cleared by another thread calling TimeZoneInfo.ClearCachedData. Without the consistent snapshot, 
        // there is a chance that the internal ConvertTime calls will throw since 'source' won't be reference equal to the new TimeZoneInfo.Local.
        //
#pragma warning disable 0420
        private class CachedData
        {
            private volatile TimeZoneInfo _localTimeZone;
            private volatile TimeZoneInfo _utcTimeZone;
            private Lock _lock = new Lock();

            private TimeZoneInfo CreateLocal()
            {
                using (LockHolder.Hold(_lock))
                {
                    TimeZoneInfo timeZone = _localTimeZone;
                    if (timeZone == null)
                    {
                        timeZone = TimeZoneInfo.GetLocalTimeZone(this);

                        // this step is to break the reference equality
                        // between TimeZoneInfo.Local and a second time zone
                        // such as "Pacific Standard Time"
                        timeZone = new TimeZoneInfo(
                                            timeZone._id,
                                            timeZone._baseUtcOffset,
                                            timeZone._displayName,
                                            timeZone._standardDisplayName,
                                            timeZone._daylightDisplayName,
                                            timeZone._adjustmentRules,
                                            false);

                        _localTimeZone = timeZone;
                    }
                    return timeZone;
                }
            }

            public TimeZoneInfo Local
            {
                get
                {
                    TimeZoneInfo timeZone = _localTimeZone;
                    if (timeZone == null)
                    {
                        timeZone = CreateLocal();
                    }
                    return timeZone;
                }
            }

            private TimeZoneInfo CreateUtc()
            {
                using (LockHolder.Hold(_lock))
                {
                    TimeZoneInfo timeZone = _utcTimeZone;
                    if (timeZone == null)
                    {
                        timeZone = CreateCustomTimeZone(c_utcId, TimeSpan.Zero, c_utcId, c_utcId);
                        _utcTimeZone = timeZone;
                    }
                    return timeZone;
                }
            }

            public TimeZoneInfo Utc
            {
                get
                {
                    Contract.Ensures(Contract.Result<TimeZoneInfo>() != null);

                    TimeZoneInfo timeZone = _utcTimeZone;
                    if (timeZone == null)
                    {
                        timeZone = CreateUtc();
                    }
                    return timeZone;
                }
            }

            //
            // GetCorrespondingKind-
            //
            // Helper function that returns the corresponding DateTimeKind for this TimeZoneInfo
            //
            public DateTimeKind GetCorrespondingKind(TimeZoneInfo timeZone)
            {
                DateTimeKind kind;

                //
                // we check reference equality to see if 'this' is the same as
                // TimeZoneInfo.Local or TimeZoneInfo.Utc.  This check is needed to 
                // support setting the DateTime Kind property to 'Local' or
                // 'Utc' on the ConverTime(...) return value.  
                //
                // Using reference equality instead of value equality was a 
                // performance based design compromise.  The reference equality
                // has much greater performance, but it reduces the number of
                // returned DateTime's that can be properly set as 'Local' or 'Utc'.
                //
                // For example, the user could be converting to the TimeZoneInfo returned
                // by FindSystemTimeZoneById("Pacific Standard Time") and their local
                // machine may be in Pacific time.  If we used value equality to determine
                // the corresponding Kind then this conversion would be tagged as 'Local';
                // where as we are currently tagging the returned DateTime as 'Unspecified'
                // in this example.  Only when the user passes in TimeZoneInfo.Local or
                // TimeZoneInfo.Utc to the ConvertTime(...) methods will this check succeed.
                //
                if ((object)timeZone == (object)_utcTimeZone)
                {
                    kind = DateTimeKind.Utc;
                }
                else if ((object)timeZone == (object)_localTimeZone)
                {
                    kind = DateTimeKind.Local;
                }
                else
                {
                    kind = DateTimeKind.Unspecified;
                }

                return kind;
            }

            public LowLevelDictionary<string, TimeZoneInfo> m_systemTimeZones;
            private volatile ReadOnlyCollection<TimeZoneInfo> _readonlySystemTimeZones;
            public void EnumerateSystemTimes()
            {
                LowLevelListWithIList<TimeZoneInfo> enumeratedTimeZoneList = new LowLevelListWithIList<TimeZoneInfo>();
                uint index = 0;
                TIME_DYNAMIC_ZONE_INFORMATION tdzi;
                while (Interop.mincore.EnumDynamicTimeZoneInformation(index, out tdzi) != Interop.mincore.Errors.ERROR_NO_MORE_ITEMS)
                {
                    TimeZoneInformation timeZoneInformation = new TimeZoneInformation(tdzi);
                    TimeZoneInfo value;
                    Exception e;
                    TimeZoneInfoResult result = TryGetTimeZone(ref timeZoneInformation, false, out value, out e, s_cachedData);
                    if (result == TimeZoneInfoResult.Success)
                    {
                        enumeratedTimeZoneList.Add(value);
                    }
                    index++;
                }
                TimeZoneInfo[] array = enumeratedTimeZoneList.ToArray();
                Array.Sort(array, new TimeZoneInfoComparer());
                _readonlySystemTimeZones = new ReadOnlyCollection<TimeZoneInfo>(array);
            }

            public bool AreSystemTimesEnumerated { get { return _readonlySystemTimeZones != null; } }

            public ReadOnlyCollection<TimeZoneInfo> GetOrCreateReadonlySystemTimes()
            {
                if (_readonlySystemTimeZones == null)
                {
                    EnumerateSystemTimes();
                }
                return _readonlySystemTimeZones;
            }

            private static TimeZoneInfo GetCurrentOneYearLocal()
            {
                // load the data from the OS
                TimeZoneInfo match;

                TimeZoneInformation timeZoneInformation;
                if (!GetTimeZoneInfo(out timeZoneInformation))
                    match = CreateCustomTimeZone(c_localId, TimeSpan.Zero, c_localId, c_localId);
                else
                    match = GetLocalTimeZoneFromWin32Data(timeZoneInformation, false);
                return match;
            }

            private volatile OffsetAndRule _oneYearLocalFromUtc;

            public OffsetAndRule GetOneYearLocalFromUtc(int year)
            {
                OffsetAndRule oneYearLocFromUtc = _oneYearLocalFromUtc;
                if (oneYearLocFromUtc == null || oneYearLocFromUtc.year != year)
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

        private static CachedData s_cachedData = new CachedData();

        private class OffsetAndRule
        {
            public int year;
            public TimeSpan offset;
            public AdjustmentRule rule;
            public OffsetAndRule(int year, TimeSpan offset, AdjustmentRule rule)
            {
                this.year = year;
                this.offset = offset;
                this.rule = rule;
            }
        }

        // used by GetUtcOffsetFromUtc (DateTime.Now, DateTime.ToLocalTime) for max/min whole-day range checks
        private static DateTime s_maxDateOnly = new DateTime(9999, 12, 31);
        private static DateTime s_minDateOnly = new DateTime(1, 1, 2);

        public String Id
        {
            get
            {
                return _id;
            }
        }

        public String DisplayName
        {
            get
            {
                return (_displayName == null ? String.Empty : _displayName);
            }
        }

        public String StandardName
        {
            get
            {
                return (_standardDisplayName == null ? String.Empty : _standardDisplayName);
            }
        }

        public String DaylightName
        {
            get
            {
                return (_daylightDisplayName == null ? String.Empty : _daylightDisplayName);
            }
        }

        public TimeSpan BaseUtcOffset
        {
            get
            {
                return _baseUtcOffset;
            }
        }

        public Boolean SupportsDaylightSavingTime
        {
            get
            {
                return _supportsDaylightSavingTime;
            }
        }

        //
        // GetAmbiguousTimeOffsets -
        //
        // returns an array of TimeSpan objects representing all of
        // possible UTC offset values for this ambiguous time
        //
        public TimeSpan[] GetAmbiguousTimeOffsets(DateTimeOffset dateTimeOffset)
        {
            if (!SupportsDaylightSavingTime)
            {
                throw new ArgumentException(SR.Argument_DateTimeOffsetIsNotAmbiguous, "dateTimeOffset");
            }
            Contract.EndContractBlock();

            DateTime adjustedTime = (TimeZoneInfo.ConvertTime(dateTimeOffset, this)).DateTime;

            Boolean isAmbiguous = false;
            AdjustmentRule rule = GetAdjustmentRuleForTime(adjustedTime);
            if (rule != null && rule.HasDaylightSaving)
            {
                DaylightTime daylightTime = GetDaylightTime(adjustedTime.Year, rule);
                isAmbiguous = GetIsAmbiguousTime(adjustedTime, rule, daylightTime);
            }

            if (!isAmbiguous)
            {
                throw new ArgumentException(SR.Argument_DateTimeOffsetIsNotAmbiguous, "dateTimeOffset");
            }

            // the passed in dateTime is ambiguous in this TimeZoneInfo instance
            TimeSpan[] timeSpans = new TimeSpan[2];
            TimeSpan actualUtcOffset = _baseUtcOffset + rule.BaseUtcOffsetDelta;

            // the TimeSpan array must be sorted from least to greatest
            if (rule.DaylightDelta > TimeSpan.Zero)
            {
                timeSpans[0] = actualUtcOffset; // FUTURE:  + rule.StandardDelta;
                timeSpans[1] = actualUtcOffset + rule.DaylightDelta;
            }
            else
            {
                timeSpans[0] = actualUtcOffset + rule.DaylightDelta;
                timeSpans[1] = actualUtcOffset; // FUTURE: + rule.StandardDelta;
            }
            return timeSpans;
        }


        public TimeSpan[] GetAmbiguousTimeOffsets(DateTime dateTime)
        {
            if (!SupportsDaylightSavingTime)
            {
                throw new ArgumentException(SR.Argument_DateTimeIsNotAmbiguous, "dateTime");
            }
            Contract.EndContractBlock();

            DateTime adjustedTime;
            if (dateTime.Kind == DateTimeKind.Local)
            {
                CachedData cachedData = s_cachedData;
                adjustedTime = TimeZoneInfo.ConvertTime(dateTime, cachedData.Local, this, TimeZoneInfoOptions.None, cachedData);
            }
            else if (dateTime.Kind == DateTimeKind.Utc)
            {
                CachedData cachedData = s_cachedData;
                adjustedTime = TimeZoneInfo.ConvertTime(dateTime, cachedData.Utc, this, TimeZoneInfoOptions.None, cachedData);
            }
            else
            {
                adjustedTime = dateTime;
            }

            Boolean isAmbiguous = false;
            AdjustmentRule rule = GetAdjustmentRuleForTime(adjustedTime);
            if (rule != null && rule.HasDaylightSaving)
            {
                DaylightTime daylightTime = GetDaylightTime(adjustedTime.Year, rule);
                isAmbiguous = GetIsAmbiguousTime(adjustedTime, rule, daylightTime);
            }

            if (!isAmbiguous)
            {
                throw new ArgumentException(SR.Argument_DateTimeIsNotAmbiguous, "dateTime");
            }

            // the passed in dateTime is ambiguous in this TimeZoneInfo instance
            TimeSpan[] timeSpans = new TimeSpan[2];
            TimeSpan actualUtcOffset = _baseUtcOffset + rule.BaseUtcOffsetDelta;

            // the TimeSpan array must be sorted from least to greatest
            if (rule.DaylightDelta > TimeSpan.Zero)
            {
                timeSpans[0] = actualUtcOffset; // FUTURE:  + rule.StandardDelta;
                timeSpans[1] = actualUtcOffset + rule.DaylightDelta;
            }
            else
            {
                timeSpans[0] = actualUtcOffset + rule.DaylightDelta;
                timeSpans[1] = actualUtcOffset; // FUTURE: + rule.StandardDelta;
            }
            return timeSpans;
        }

        //
        // GetUtcOffset -
        //
        // returns the Universal Coordinated Time (UTC) Offset
        // for the current TimeZoneInfo instance.
        //
        public TimeSpan GetUtcOffset(DateTimeOffset dateTimeOffset)
        {
            return GetUtcOffsetFromUtc(dateTimeOffset.UtcDateTime, this);
        }


        public TimeSpan GetUtcOffset(DateTime dateTime)
        {
            return GetUtcOffset(dateTime, TimeZoneInfoOptions.NoThrowOnInvalidTime, s_cachedData);
        }

        // Shortcut for TimeZoneInfo.Local.GetUtcOffset
        internal static TimeSpan GetLocalUtcOffset(DateTime dateTime, TimeZoneInfoOptions flags)
        {
            CachedData cachedData = s_cachedData;
            return cachedData.Local.GetUtcOffset(dateTime, flags, cachedData);
        }

        internal TimeSpan GetUtcOffset(DateTime dateTime, TimeZoneInfoOptions flags)
        {
            return GetUtcOffset(dateTime, flags, s_cachedData);
        }

        private TimeSpan GetUtcOffset(DateTime dateTime, TimeZoneInfoOptions flags, CachedData cachedData)
        {
            if (dateTime.Kind == DateTimeKind.Local)
            {
                if (cachedData.GetCorrespondingKind(this) != DateTimeKind.Local)
                {
                    //
                    // normal case of converting from Local to Utc and then getting the offset from the UTC DateTime
                    //
                    DateTime adjustedTime = TimeZoneInfo.ConvertTime(dateTime, cachedData.Local, cachedData.Utc, flags);
                    return GetUtcOffsetFromUtc(adjustedTime, this);
                }
                //
                // Fall through for TimeZoneInfo.Local.GetUtcOffset(date)
                // to handle an edge case with Invalid-Times for DateTime formatting:
                //
                // Consider the invalid PST time "2007-03-11T02:00:00.0000000-08:00"
                //
                // By directly calling GetUtcOffset instead of converting to UTC and then calling GetUtcOffsetFromUtc
                // the correct invalid offset of "-08:00" is returned.  In the normal case of converting to UTC as an 
                // interim-step, the invalid time is adjusted into a *valid* UTC time which causes a change in output:
                //
                // 1) invalid PST time "2007-03-11T02:00:00.0000000-08:00"
                // 2) converted to UTC "2007-03-11T10:00:00.0000000Z"
                // 3) offset returned  "2007-03-11T03:00:00.0000000-07:00"
                //
            }
            else if (dateTime.Kind == DateTimeKind.Utc)
            {
                if (cachedData.GetCorrespondingKind(this) == DateTimeKind.Utc)
                {
                    return _baseUtcOffset;
                }
                else
                {
                    //
                    // passing in a UTC dateTime to a non-UTC TimeZoneInfo instance is a
                    // special Loss-Less case.
                    //
                    return GetUtcOffsetFromUtc(dateTime, this);
                }
            }

            return GetUtcOffset(dateTime, this, flags);
        }

        //
        // IsAmbiguousTime -
        //
        // returns true if the time is during the ambiguous time period
        // for the current TimeZoneInfo instance.
        //
        public Boolean IsAmbiguousTime(DateTimeOffset dateTimeOffset)
        {
            if (!_supportsDaylightSavingTime)
            {
                return false;
            }

            DateTimeOffset adjustedTime = TimeZoneInfo.ConvertTime(dateTimeOffset, this);
            return IsAmbiguousTime(adjustedTime.DateTime);
        }


        public Boolean IsAmbiguousTime(DateTime dateTime)
        {
            return IsAmbiguousTime(dateTime, TimeZoneInfoOptions.NoThrowOnInvalidTime);
        }

        internal Boolean IsAmbiguousTime(DateTime dateTime, TimeZoneInfoOptions flags)
        {
            if (!_supportsDaylightSavingTime)
            {
                return false;
            }

            DateTime adjustedTime;
            if (dateTime.Kind == DateTimeKind.Local)
            {
                CachedData cachedData = s_cachedData;
                adjustedTime = TimeZoneInfo.ConvertTime(dateTime, cachedData.Local, this, flags, cachedData);
            }
            else if (dateTime.Kind == DateTimeKind.Utc)
            {
                CachedData cachedData = s_cachedData;
                adjustedTime = TimeZoneInfo.ConvertTime(dateTime, cachedData.Utc, this, flags, cachedData);
            }
            else
            {
                adjustedTime = dateTime;
            }


            AdjustmentRule rule = GetAdjustmentRuleForTime(adjustedTime);
            if (rule != null && rule.HasDaylightSaving)
            {
                DaylightTime daylightTime = GetDaylightTime(adjustedTime.Year, rule);
                return GetIsAmbiguousTime(adjustedTime, rule, daylightTime);
            }
            return false;
        }



        //
        // IsDaylightSavingTime -
        //
        // Returns true if the time is during Daylight Saving time
        // for the current TimeZoneInfo instance.
        //
        public Boolean IsDaylightSavingTime(DateTimeOffset dateTimeOffset)
        {
            Boolean isDaylightSavingTime;
            GetUtcOffsetFromUtc(dateTimeOffset.UtcDateTime, this, out isDaylightSavingTime);
            return isDaylightSavingTime;
        }


        public Boolean IsDaylightSavingTime(DateTime dateTime)
        {
            return IsDaylightSavingTime(dateTime, TimeZoneInfoOptions.NoThrowOnInvalidTime, s_cachedData);
        }

        internal Boolean IsDaylightSavingTime(DateTime dateTime, TimeZoneInfoOptions flags)
        {
            return IsDaylightSavingTime(dateTime, flags, s_cachedData);
        }

        private Boolean IsDaylightSavingTime(DateTime dateTime, TimeZoneInfoOptions flags, CachedData cachedData)
        {
            //
            //    dateTime.Kind is UTC, then time will be converted from UTC
            //        into current instance's timezone
            //    dateTime.Kind is Local, then time will be converted from Local 
            //        into current instance's timezone
            //    dateTime.Kind is UnSpecified, then time is already in
            //        current instance's timezone
            //
            // Our DateTime handles ambiguous times, (one is in the daylight and
            // one is in standard.) If a new DateTime is constructed during ambiguous 
            // time, it is defaulted to "Standard" (i.e. this will return false).
            // For Invalid times, we will return false

            if (!_supportsDaylightSavingTime || _adjustmentRules == null)
            {
                return false;
            }

            DateTime adjustedTime;
            //
            // handle any Local/Utc special cases...
            //
            if (dateTime.Kind == DateTimeKind.Local)
            {
                adjustedTime = TimeZoneInfo.ConvertTime(dateTime, cachedData.Local, this, flags, cachedData);
            }
            else if (dateTime.Kind == DateTimeKind.Utc)
            {
                if (cachedData.GetCorrespondingKind(this) == DateTimeKind.Utc)
                {
                    // simple always false case: TimeZoneInfo.Utc.IsDaylightSavingTime(dateTime, flags);
                    return false;
                }
                else
                {
                    //
                    // passing in a UTC dateTime to a non-UTC TimeZoneInfo instance is a
                    // special Loss-Less case.
                    //
                    Boolean isDaylightSavings;
                    GetUtcOffsetFromUtc(dateTime, this, out isDaylightSavings);
                    return isDaylightSavings;
                }
            }
            else
            {
                adjustedTime = dateTime;
            }

            //
            // handle the normal cases...
            //
            AdjustmentRule rule = GetAdjustmentRuleForTime(adjustedTime);
            if (rule != null && rule.HasDaylightSaving)
            {
                DaylightTime daylightTime = GetDaylightTime(adjustedTime.Year, rule);
                return GetIsDaylightSavings(adjustedTime, rule, daylightTime, flags);
            }
            else
            {
                return false;
            }
        }


        //
        // IsInvalidTime -
        //
        // returns true when dateTime falls into a "hole in time".
        //
        public Boolean IsInvalidTime(DateTime dateTime)
        {
            Boolean isInvalid = false;

            if ((dateTime.Kind == DateTimeKind.Unspecified)
            || (dateTime.Kind == DateTimeKind.Local && s_cachedData.GetCorrespondingKind(this) == DateTimeKind.Local))
            {
                // only check Unspecified and (Local when this TimeZoneInfo instance is Local)
                AdjustmentRule rule = GetAdjustmentRuleForTime(dateTime);


                if (rule != null && rule.HasDaylightSaving)
                {
                    DaylightTime daylightTime = GetDaylightTime(dateTime.Year, rule);
                    isInvalid = GetIsInvalidTime(dateTime, rule, daylightTime);
                }
                else
                {
                    isInvalid = false;
                }
            }

            return isInvalid;
        }

        //
        // ClearCachedData -
        //
        // Clears data from static members
        //
        static internal void ClearCachedData()
        {
            // Clear a fresh instance of cached data
            s_cachedData = new CachedData();
        }

        //
        // ConvertTime -
        //
        // Converts the value of the dateTime object from sourceTimeZone to destinationTimeZone
        //

        static public DateTimeOffset ConvertTime(DateTimeOffset dateTimeOffset, TimeZoneInfo destinationTimeZone)
        {
            if (destinationTimeZone == null)
            {
                throw new ArgumentNullException("destinationTimeZone");
            }

            Contract.EndContractBlock();
            // calculate the destination time zone offset
            DateTime utcDateTime = dateTimeOffset.UtcDateTime;
            TimeSpan destinationOffset = GetUtcOffsetFromUtc(utcDateTime, destinationTimeZone);

            // check for overflow
            Int64 ticks = utcDateTime.Ticks + destinationOffset.Ticks;

            if (ticks > DateTimeOffset.MaxValue.Ticks)
            {
                return DateTimeOffset.MaxValue;
            }
            else if (ticks < DateTimeOffset.MinValue.Ticks)
            {
                return DateTimeOffset.MinValue;
            }
            else
            {
                return new DateTimeOffset(ticks, destinationOffset);
            }
        }

        static public DateTime ConvertTime(DateTime dateTime, TimeZoneInfo sourceTimeZone, TimeZoneInfo destinationTimeZone)
        {
            return ConvertTime(dateTime, sourceTimeZone, destinationTimeZone, TimeZoneInfoOptions.None, s_cachedData);
        }

        static public DateTime ConvertTime(DateTime dateTime, TimeZoneInfo destinationTimeZone)
        {
            if (destinationTimeZone == null)
            {
                throw new ArgumentNullException("destinationTimeZone");
            }
            Contract.EndContractBlock();

            // Special case to give a way clearing the cache without exposing ClearCachedData()
            if (dateTime.Ticks == 0)
            {
                ClearCachedData();
            }

            CachedData cachedData = s_cachedData;
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return ConvertTime(dateTime, cachedData.Utc, destinationTimeZone, TimeZoneInfoOptions.None, cachedData);
            }
            else
            {
                return ConvertTime(dateTime, cachedData.Local, destinationTimeZone, TimeZoneInfoOptions.None, cachedData);
            }
        }

        static internal DateTime ConvertTime(DateTime dateTime, TimeZoneInfo sourceTimeZone, TimeZoneInfo destinationTimeZone, TimeZoneInfoOptions flags)
        {
            return ConvertTime(dateTime, sourceTimeZone, destinationTimeZone, flags, s_cachedData);
        }

        static private DateTime ConvertTime(DateTime dateTime, TimeZoneInfo sourceTimeZone, TimeZoneInfo destinationTimeZone, TimeZoneInfoOptions flags, CachedData cachedData)
        {
            if (sourceTimeZone == null)
            {
                throw new ArgumentNullException("sourceTimeZone");
            }

            if (destinationTimeZone == null)
            {
                throw new ArgumentNullException("destinationTimeZone");
            }
            Contract.EndContractBlock();

            DateTimeKind sourceKind = cachedData.GetCorrespondingKind(sourceTimeZone);
            if (((flags & TimeZoneInfoOptions.NoThrowOnInvalidTime) == 0) && (dateTime.Kind != DateTimeKind.Unspecified) && (dateTime.Kind != sourceKind))
            {
                throw new ArgumentException(SR.Argument_ConvertMismatch, "sourceTimeZone");
            }

            //
            // check to see if the DateTime is in an invalid time range.  This check
            // requires the current AdjustmentRule and DaylightTime - which are also
            // needed to calculate 'sourceOffset' in the normal conversion case.
            // By calculating the 'sourceOffset' here we improve the
            // performance for the normal case at the expense of the 'ArgumentException'
            // case and Loss-less Local special cases.
            //
            AdjustmentRule sourceRule = sourceTimeZone.GetAdjustmentRuleForTime(dateTime);
            TimeSpan sourceOffset = sourceTimeZone.BaseUtcOffset;

            if (sourceRule != null)
            {
                sourceOffset = sourceOffset + sourceRule.BaseUtcOffsetDelta;
                if (sourceRule.HasDaylightSaving)
                {
                    Boolean sourceIsDaylightSavings = false;
                    DaylightTime sourceDaylightTime = GetDaylightTime(dateTime.Year, sourceRule);

                    // 'dateTime' might be in an invalid time range since it is in an AdjustmentRule
                    // period that supports DST 
                    if (((flags & TimeZoneInfoOptions.NoThrowOnInvalidTime) == 0) && GetIsInvalidTime(dateTime, sourceRule, sourceDaylightTime))
                    {
                        throw new ArgumentException(SR.Argument_DateTimeIsInvalid, "dateTime");
                    }
                    sourceIsDaylightSavings = GetIsDaylightSavings(dateTime, sourceRule, sourceDaylightTime, flags);

                    // adjust the sourceOffset according to the Adjustment Rule / Daylight Saving Rule
                    sourceOffset += (sourceIsDaylightSavings ? sourceRule.DaylightDelta : TimeSpan.Zero /*FUTURE: sourceRule.StandardDelta*/);
                }
            }

            DateTimeKind targetKind = cachedData.GetCorrespondingKind(destinationTimeZone);

            // handle the special case of Loss-less Local->Local and UTC->UTC)
            if (dateTime.Kind != DateTimeKind.Unspecified && sourceKind != DateTimeKind.Unspecified
                && sourceKind == targetKind)
            {
                return dateTime;
            }

            Int64 utcTicks = dateTime.Ticks - sourceOffset.Ticks;

            // handle the normal case by converting from 'source' to UTC and then to 'target'
            Boolean isAmbiguousLocalDst = false;
            DateTime targetConverted = ConvertUtcToTimeZone(utcTicks, destinationTimeZone, out isAmbiguousLocalDst);

            if (targetKind == DateTimeKind.Local)
            {
                // Because the ticks conversion between UTC and local is lossy, we need to capture whether the 
                // time is in a repeated hour so that it can be passed to the DateTime constructor.
                return new DateTime(targetConverted.Ticks, DateTimeKind.Local, isAmbiguousLocalDst);
            }
            else
            {
                return new DateTime(targetConverted.Ticks, targetKind);
            }
        }


        static internal DateTime ConvertTimeToUtc(DateTime dateTime, TimeZoneInfoOptions flags)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return dateTime;
            }
            CachedData cachedData = s_cachedData;
            return ConvertTime(dateTime, cachedData.Local, cachedData.Utc, flags, cachedData);
        }


        //
        // IEquatable.Equals -
        //
        // returns value equality.  Equals does not compare any localizable
        // String objects (DisplayName, StandardName, DaylightName).
        //
        public bool Equals(TimeZoneInfo other)
        {
            return (other != null && String.Compare(_id, other._id, StringComparison.OrdinalIgnoreCase) == 0 && HasSameRules(other));
        }


        //
        // GetHashCode -
        //
        public override int GetHashCode()
        {
            return _id.ToUpperInvariant().GetHashCode();
        }

        public static ReadOnlyCollection<TimeZoneInfo> GetSystemTimeZones()
        {
            return s_cachedData.GetOrCreateReadonlySystemTimes();
        }

        public static TimeZoneInfo FindSystemTimeZoneById(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }
            else if (id.Length == 0 || id.Length > 255 || id.Contains("\0"))
            {
                throw new ArgumentException(String.Format(SR.Argument_TimeZoneNotFound, id));
            }

            //
            // Check first the Utc Ids and return the cached one because in GetCorrespondingKind 
            // we use reference equality
            // 

            if (id.Equals(TimeZoneInfo.Utc.Id, StringComparison.OrdinalIgnoreCase))
                return TimeZoneInfo.Utc;

            TimeZoneInfo value;
            CachedData cache = s_cachedData;
            // Use the current cache if it exists
            if (cache.m_systemTimeZones != null)
            {
                if (cache.m_systemTimeZones.TryGetValue(id, out value))
                {
                    return value;
                }
            }
            // See if the cache was fully filled, if not, fill it then check again.
            if (!cache.AreSystemTimesEnumerated)
            {
                cache.EnumerateSystemTimes();
                if (cache.m_systemTimeZones.TryGetValue(id, out value))
                {
                    return value;
                }
            }
            throw new ArgumentException(String.Format(SR.Argument_TimeZoneNotFound, id));
        }

        //
        // HasSameRules -
        //
        // Value equality on the "adjustmentRules" array
        //
        internal Boolean HasSameRules(TimeZoneInfo other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            // check the utcOffset and supportsDaylightSavingTime members
            Contract.EndContractBlock();

            if (_baseUtcOffset != other._baseUtcOffset
            || _supportsDaylightSavingTime != other._supportsDaylightSavingTime)
            {
                return false;
            }

            bool sameRules;
            AdjustmentRule[] currentRules = _adjustmentRules;
            AdjustmentRule[] otherRules = other._adjustmentRules;

            sameRules = (currentRules == null && otherRules == null)
                      || (currentRules != null && otherRules != null);

            if (!sameRules)
            {
                // AdjustmentRule array mismatch
                return false;
            }

            if (currentRules != null)
            {
                if (currentRules.Length != otherRules.Length)
                {
                    // AdjustmentRule array length mismatch
                    return false;
                }

                for (int i = 0; i < currentRules.Length; i++)
                {
                    if (!(currentRules[i]).Equals(otherRules[i]))
                    {
                        // AdjustmentRule value-equality mismatch
                        return false;
                    }
                }
            }
            return sameRules;
        }

        private const long TIME_ZONE_ID_INVALID = -1;

        internal static bool GetTimeZoneInfo(out TimeZoneInformation timeZoneInfo)
        {
            TIME_DYNAMIC_ZONE_INFORMATION dtzi = new TIME_DYNAMIC_ZONE_INFORMATION();
            long result = Interop.mincore.GetDynamicTimeZoneInformation(out dtzi);
            if (result == TIME_ZONE_ID_INVALID)
            {
                timeZoneInfo = null;
                return false;
            }

            timeZoneInfo = new TimeZoneInformation(dtzi);

            return true;
        }

        //
        // Local -
        //
        // returns a TimeZoneInfo instance that represents the local time on the machine.
        // Accessing this property may throw InvalidTimeZoneException or COMException
        // if the machine is in an unstable or corrupt state.
        //
        static public TimeZoneInfo Local
        {
            get
            {
                Contract.Ensures(Contract.Result<TimeZoneInfo>() != null);
                return s_cachedData.Local;
            }
        }

        //
        // ToString -
        //
        // returns the DisplayName: 
        // "(GMT-08:00) Pacific Time (US & Canada); Tijuana"
        //
        public override string ToString()
        {
            return this.DisplayName;
        }


        //
        // Utc -
        //
        // returns a TimeZoneInfo instance that represents Universal Coordinated Time (UTC)
        //
        static public TimeZoneInfo Utc
        {
            get
            {
                Contract.Ensures(Contract.Result<TimeZoneInfo>() != null);
                return s_cachedData.Utc;
            }
        }


        // -------- SECTION: constructors -----------------*
        // 
        // TimeZoneInfo -
        //
        // private ctor
        //
        private TimeZoneInfo(TimeZoneInformation zone, Boolean dstDisabled)
        {
            if (String.IsNullOrEmpty(zone.StandardName))
            {
                _id = c_localId;  // the ID must contain at least 1 character - initialize m_id to "Local"
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


        internal TimeZoneInfo(
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

        // -------- SECTION: factory methods -----------------*

        //
        // CreateCustomTimeZone -
        // 
        // returns a simple TimeZoneInfo instance that does
        // not support Daylight Saving Time
        //
        static internal TimeZoneInfo CreateCustomTimeZone(
                String id,
                TimeSpan baseUtcOffset,
                String displayName,
                  String standardDisplayName)
        {
            return new TimeZoneInfo(
                           id,
                           baseUtcOffset,
                           displayName,
                           standardDisplayName,
                           standardDisplayName,
                           null,
                           false);
        }



        // ----- SECTION: private serialization instance methods  ----------------*




        // ----- SECTION: internal instance utility methods ----------------*


        // assumes dateTime is in the current time zone's time
        private AdjustmentRule GetAdjustmentRuleForTime(DateTime dateTime)
        {
            if (_adjustmentRules == null || _adjustmentRules.Length == 0)
            {
                return null;
            }

            // Only check the whole-date portion of the dateTime -
            // This is because the AdjustmentRule DateStart & DateEnd are stored as
            // Date-only values {4/2/2006 - 10/28/2006} but actually represent the
            // time span {4/2/2006@00:00:00.00000 - 10/28/2006@23:59:59.99999}
            DateTime date = dateTime.Date;

            for (int i = 0; i < _adjustmentRules.Length; i++)
            {
                if (_adjustmentRules[i].DateStart <= date && _adjustmentRules[i].DateEnd >= date)
                {
                    return _adjustmentRules[i];
                }
            }

            return null;
        }

        //
        // ConvertUtcToTimeZone -
        //
        // Helper function that converts a dateTime from UTC into the destinationTimeZone
        //
        // * returns DateTime.MaxValue when the converted value is too large
        // * returns DateTime.MinValue when the converted value is too small
        //
        static private DateTime ConvertUtcToTimeZone(Int64 ticks, TimeZoneInfo destinationTimeZone, out Boolean isAmbiguousLocalDst)
        {
            DateTime utcConverted;
            DateTime localConverted;

            // utcConverted is used to calculate the UTC offset in the destinationTimeZone
            if (ticks > DateTime.MaxValue.Ticks)
            {
                utcConverted = DateTime.MaxValue;
            }
            else if (ticks < DateTime.MinValue.Ticks)
            {
                utcConverted = DateTime.MinValue;
            }
            else
            {
                utcConverted = new DateTime(ticks);
            }

            // verify the time is between MinValue and MaxValue in the new time zone
            TimeSpan offset = GetUtcOffsetFromUtc(utcConverted, destinationTimeZone, out isAmbiguousLocalDst);
            ticks += offset.Ticks;

            if (ticks > DateTime.MaxValue.Ticks)
            {
                localConverted = DateTime.MaxValue;
            }
            else if (ticks < DateTime.MinValue.Ticks)
            {
                localConverted = DateTime.MinValue;
            }
            else
            {
                localConverted = new DateTime(ticks);
            }
            return localConverted;
        }


        //
        // CreateAdjustmentRuleFromTimeZoneInformation-
        //
        // Converts TimeZoneInformation to an AdjustmentRule
        //
        static internal AdjustmentRule CreateAdjustmentRuleFromTimeZoneInformation(TimeZoneInformation timeZoneInformation, DateTime startDate, DateTime endDate, int defaultBaseUtcOffset)
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
                    new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Dtzi.Bias, 0));  // Bias delta is all what we need from this rule
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
                new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Dtzi.Bias, 0));
        }

        static internal AdjustmentRule CreateAdjustmentRuleFromTimeZoneInformation(ref TIME_ZONE_INFORMATION timeZoneInformation, DateTime startDate, DateTime endDate, int defaultBaseUtcOffset)
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

            return AdjustmentRule.CreateAdjustmentRule(
                startDate,
                endDate,
                new TimeSpan(0, -timeZoneInformation.DaylightBias, 0),
                (TransitionTime)daylightTransitionStart,
                (TransitionTime)daylightTransitionEnd,
                new TimeSpan(0, defaultBaseUtcOffset - timeZoneInformation.Bias, 0));
        }

        //
        // GetDaylightTime -
        //
        // Helper function that returns a DaylightTime from a year and AdjustmentRule
        //
        static private DaylightTime GetDaylightTime(Int32 year, AdjustmentRule rule)
        {
            TimeSpan delta = rule.DaylightDelta;
            DateTime startTime = TransitionTimeToDateTime(year, rule.DaylightTransitionStart);
            DateTime endTime = TransitionTimeToDateTime(year, rule.DaylightTransitionEnd);
            return new DaylightTime(startTime, endTime, delta);
        }



        //
        // GetIsDaylightSavings -
        //
        // Helper function that checks if a given dateTime is in Daylight Saving Time (DST)
        // This function assumes the dateTime and AdjustmentRule are both in the same time zone
        //
        static private Boolean GetIsDaylightSavings(DateTime time, AdjustmentRule rule, DaylightTime daylightTime, TimeZoneInfoOptions flags)
        {
            if (rule == null)
            {
                return false;
            }

            DateTime startTime;
            DateTime endTime;

            if (time.Kind == DateTimeKind.Local)
            {
                // startTime and endTime represent the period from either the start of DST to the end and ***includes*** the 
                // potentially overlapped times
                startTime = rule.IsStartDateMarkerForBeginningOfYear() ? new DateTime(daylightTime.Start.Year, 1, 1, 0, 0, 0) : daylightTime.Start + daylightTime.Delta;
                endTime = rule.IsEndDateMarkerForEndOfYear() ? new DateTime(daylightTime.End.Year + 1, 1, 1, 0, 0, 0).AddTicks(-1) : daylightTime.End;
            }
            else
            {
                // startTime and endTime represent the period from either the start of DST to the end and 
                // ***does not include*** the potentially overlapped times
                //
                //         -=-=-=-=-=- Pacific Standard Time -=-=-=-=-=-=-
                //    April 2, 2006                            October 29, 2006
                // 2AM            3AM                        1AM              2AM
                // |      +1 hr     |                        |       -1 hr      |
                // | <invalid time> |                        | <ambiguous time> |
                //                  [========== DST ========>)
                //
                //        -=-=-=-=-=- Some Weird Time Zone -=-=-=-=-=-=-
                //    April 2, 2006                          October 29, 2006
                // 1AM              2AM                    2AM              3AM
                // |      -1 hr       |                      |       +1 hr      |
                // | <ambiguous time> |                      |  <invalid time>  |
                //                    [======== DST ========>)
                //
                Boolean invalidAtStart = rule.DaylightDelta > TimeSpan.Zero;
                startTime = rule.IsStartDateMarkerForBeginningOfYear() ? new DateTime(daylightTime.Start.Year, 1, 1, 0, 0, 0) : daylightTime.Start + (invalidAtStart ? rule.DaylightDelta : TimeSpan.Zero); /* FUTURE: - rule.StandardDelta; */
                endTime = rule.IsEndDateMarkerForEndOfYear() ? new DateTime(daylightTime.End.Year + 1, 1, 1, 0, 0, 0).AddTicks(-1) : daylightTime.End + (invalidAtStart ? -rule.DaylightDelta : TimeSpan.Zero);
            }

            Boolean isDst = CheckIsDst(startTime, time, endTime, false);

            // If this date was previously converted from a UTC date and we were able to detect that the local
            // DateTime would be ambiguous, this data is stored in the DateTime to resolve this ambiguity. 
            if (isDst && time.Kind == DateTimeKind.Local)
            {
                // For normal time zones, the ambiguous hour is the last hour of daylight saving when you wind the 
                // clock back. It is theoretically possible to have a positive delta, (which would really be daylight
                // reduction time), where you would have to wind the clock back in the begnning.
                if (GetIsAmbiguousTime(time, rule, daylightTime))
                {
                    isDst = time.IsAmbiguousDaylightSavingTime();
                }
            }

            return isDst;
        }


        //
        // GetIsDaylightSavingsFromUtc -
        //
        // Helper function that checks if a given dateTime is in Daylight Saving Time (DST)
        // This function assumes the dateTime is in UTC and AdjustmentRule is in a different time zone
        //
        static private Boolean GetIsDaylightSavingsFromUtc(DateTime time, Int32 Year, TimeSpan utc, AdjustmentRule rule, out Boolean isAmbiguousLocalDst, TimeZoneInfo zone)
        {
            isAmbiguousLocalDst = false;

            if (rule == null)
            {
                return false;
            }

            // Get the daylight changes for the year of the specified time.
            TimeSpan offset = utc + rule.BaseUtcOffsetDelta; /* FUTURE: + rule.StandardDelta; */
            DaylightTime daylightTime = GetDaylightTime(Year, rule);

            // The start and end times represent the range of universal times that are in DST for that year.                
            // Within that there is an ambiguous hour, usually right at the end, but at the beginning in
            // the unusual case of a negative daylight savings delta.
            // We need to handle the case if the current rule has daylight saving end by the end of year. If so, we need to check if next year starts with daylight saving on  
            // and get the actual daylight saving end time. Here is example for such case:
            //      Converting the UTC datetime "12/31/2011 8:00:00 PM" to "(UTC+03:00) Moscow, St. Petersburg, Volgograd (RTZ 2)" zone. 
            //      In 2011 the daylight saving will go through the end of the year. If we use the end of 2011 as the daylight saving end, 
            //      that will fail the conversion because the UTC time +4 hours (3 hours for the zone UTC offset and 1 hour for daylight saving) will move us to the next year "1/1/2012 12:00 AM", 
            //      checking against the end of 2011 will tell we are not in daylight saving which is wrong and the conversion will be off by one hour.
            // Note we handle the similar case when rule year start with daylight saving and previous year end with daylight saving.

            bool ignoreYearAdjustment = false;
            DateTime startTime;
            if (rule.IsStartDateMarkerForBeginningOfYear() && daylightTime.Start.Year > DateTime.MinValue.Year)
            {
                AdjustmentRule previousYearRule = zone.GetAdjustmentRuleForTime(new DateTime(daylightTime.Start.Year - 1, 12, 31));
                if (previousYearRule != null && previousYearRule.IsEndDateMarkerForEndOfYear())
                {
                    DaylightTime previousDaylightTime = GetDaylightTime(daylightTime.Start.Year - 1, previousYearRule);
                    startTime = previousDaylightTime.Start - utc - previousYearRule.BaseUtcOffsetDelta;
                    ignoreYearAdjustment = true;
                }
                else
                {
                    startTime = new DateTime(daylightTime.Start.Year, 1, 1, 0, 0, 0) - offset;
                }
            }
            else
            {
                startTime = daylightTime.Start - offset;
            }

            DateTime endTime;
            if (rule.IsEndDateMarkerForEndOfYear() && daylightTime.End.Year < DateTime.MaxValue.Year)
            {
                AdjustmentRule nextYearRule = zone.GetAdjustmentRuleForTime(new DateTime(daylightTime.End.Year + 1, 1, 1));
                if (nextYearRule != null && nextYearRule.IsStartDateMarkerForBeginningOfYear())
                {
                    if (nextYearRule.IsEndDateMarkerForEndOfYear())
                    {// next year end with daylight saving on too
                        endTime = new DateTime(daylightTime.End.Year + 1, 12, 31) - utc - nextYearRule.BaseUtcOffsetDelta - nextYearRule.DaylightDelta;
                    }
                    else
                    {
                        DaylightTime nextdaylightTime = GetDaylightTime(daylightTime.End.Year + 1, nextYearRule);
                        endTime = nextdaylightTime.End - utc - nextYearRule.BaseUtcOffsetDelta - nextYearRule.DaylightDelta;
                    }
                    ignoreYearAdjustment = true;
                }
                else
                {
                    endTime = new DateTime(daylightTime.End.Year + 1, 1, 1, 0, 0, 0).AddTicks(-1) - offset - rule.DaylightDelta; ;
                }
            }
            else
            {
                endTime = daylightTime.End - offset - rule.DaylightDelta;
            }

            DateTime ambiguousStart;
            DateTime ambiguousEnd;
            if (daylightTime.Delta.Ticks > 0)
            {
                ambiguousStart = endTime - daylightTime.Delta;
                ambiguousEnd = endTime;
            }
            else
            {
                ambiguousStart = startTime;
                ambiguousEnd = startTime - daylightTime.Delta;
            }

            Boolean isDst = CheckIsDst(startTime, time, endTime, ignoreYearAdjustment);

            // See if the resulting local time becomes ambiguous. This must be captured here or the
            // DateTime will not be able to round-trip back to UTC accurately.
            if (isDst)
            {
                isAmbiguousLocalDst = (time >= ambiguousStart && time < ambiguousEnd);

                if (!isAmbiguousLocalDst && ambiguousStart.Year != ambiguousEnd.Year)
                {
                    // there exists an extreme corner case where the start or end period is on a year boundary and
                    // because of this the comparison above might have been performed for a year-early or a year-later
                    // than it should have been.
                    DateTime ambiguousStartModified;
                    DateTime ambiguousEndModified;
                    try
                    {
                        ambiguousStartModified = ambiguousStart.AddYears(1);
                        ambiguousEndModified = ambiguousEnd.AddYears(1);
                        isAmbiguousLocalDst = (time >= ambiguousStart && time < ambiguousEnd);
                    }
                    catch (ArgumentOutOfRangeException) { }

                    if (!isAmbiguousLocalDst)
                    {
                        try
                        {
                            ambiguousStartModified = ambiguousStart.AddYears(-1);
                            ambiguousEndModified = ambiguousEnd.AddYears(-1);
                            isAmbiguousLocalDst = (time >= ambiguousStart && time < ambiguousEnd);
                        }
                        catch (ArgumentOutOfRangeException) { }
                    }
                }
            }

            return isDst;
        }


        static private Boolean CheckIsDst(DateTime startTime, DateTime time, DateTime endTime, bool ignoreYearAdjustment)
        {
            Boolean isDst;

            if (!ignoreYearAdjustment)
            {
                int startTimeYear = startTime.Year;
                int endTimeYear = endTime.Year;

                if (startTimeYear != endTimeYear)
                {
                    endTime = endTime.AddYears(startTimeYear - endTimeYear);
                }

                int timeYear = time.Year;

                if (startTimeYear != timeYear)
                {
                    time = time.AddYears(startTimeYear - timeYear);
                }
            }

            if (startTime > endTime)
            {
                // In southern hemisphere, the daylight saving time starts later in the year, and ends in the beginning of next year.
                // Note, the summer in the southern hemisphere begins late in the year.
                isDst = (time < endTime || time >= startTime);
            }
            else
            {
                // In northern hemisphere, the daylight saving time starts in the middle of the year.
                isDst = (time >= startTime && time < endTime);
            }
            return isDst;
        }


        //
        // GetIsAmbiguousTime(DateTime dateTime, AdjustmentRule rule, DaylightTime daylightTime) -
        //
        // returns true when the dateTime falls into an ambiguous time range.
        // For example, in Pacific Standard Time on Sunday, October 29, 2006 time jumps from
        // 2AM to 1AM.  This means the timeline on Sunday proceeds as follows:
        // 12AM ... [1AM ... 1:59:59AM -> 1AM ... 1:59:59AM] 2AM ... 3AM ...
        //
        // In this example, any DateTime values that fall into the [1AM - 1:59:59AM] range
        // are ambiguous; as it is unclear if these times are in Daylight Saving Time.
        //
        static private Boolean GetIsAmbiguousTime(DateTime time, AdjustmentRule rule, DaylightTime daylightTime)
        {
            Boolean isAmbiguous = false;
            if (rule == null || rule.DaylightDelta == TimeSpan.Zero)
            {
                return isAmbiguous;
            }

            DateTime startAmbiguousTime;
            DateTime endAmbiguousTime;

            // if at DST start we transition forward in time then there is an ambiguous time range at the DST end
            if (rule.DaylightDelta > TimeSpan.Zero)
            {
                if (rule.IsEndDateMarkerForEndOfYear())
                {
                    // year end with daylight on so there is no ambiguous time
                    return false;
                }
                startAmbiguousTime = daylightTime.End;
                endAmbiguousTime = daylightTime.End - rule.DaylightDelta; /* FUTURE: + rule.StandardDelta; */
            }
            else
            {
                if (rule.IsStartDateMarkerForBeginningOfYear())
                {
                    // year start with daylight on so there is no ambiguous time
                    return false;
                }
                startAmbiguousTime = daylightTime.Start;
                endAmbiguousTime = daylightTime.Start + rule.DaylightDelta; /* FUTURE: - rule.StandardDelta; */
            }

            isAmbiguous = (time >= endAmbiguousTime && time < startAmbiguousTime);

            if (!isAmbiguous && startAmbiguousTime.Year != endAmbiguousTime.Year)
            {
                // there exists an extreme corner case where the start or end period is on a year boundary and
                // because of this the comparison above might have been performed for a year-early or a year-later
                // than it should have been.
                DateTime startModifiedAmbiguousTime;
                DateTime endModifiedAmbiguousTime;
                try
                {
                    startModifiedAmbiguousTime = startAmbiguousTime.AddYears(1);
                    endModifiedAmbiguousTime = endAmbiguousTime.AddYears(1);
                    isAmbiguous = (time >= endModifiedAmbiguousTime && time < startModifiedAmbiguousTime);
                }
                catch (ArgumentOutOfRangeException) { }

                if (!isAmbiguous)
                {
                    try
                    {
                        startModifiedAmbiguousTime = startAmbiguousTime.AddYears(-1);
                        endModifiedAmbiguousTime = endAmbiguousTime.AddYears(-1);
                        isAmbiguous = (time >= endModifiedAmbiguousTime && time < startModifiedAmbiguousTime);
                    }
                    catch (ArgumentOutOfRangeException) { }
                }
            }
            return isAmbiguous;
        }



        //
        // GetIsInvalidTime -
        //
        // Helper function that checks if a given DateTime is in an invalid time ("time hole")
        // A "time hole" occurs at a DST transition point when time jumps forward;
        // For example, in Pacific Standard Time on Sunday, April 2, 2006 time jumps from
        // 1:59:59.9999999 to 3AM.  The time range 2AM to 2:59:59.9999999AM is the "time hole".
        // A "time hole" is not limited to only occurring at the start of DST, and may occur at
        // the end of DST as well.
        //
        static private Boolean GetIsInvalidTime(DateTime time, AdjustmentRule rule, DaylightTime daylightTime)
        {
            Boolean isInvalid = false;
            if (rule == null || rule.DaylightDelta == TimeSpan.Zero)
            {
                return isInvalid;
            }

            DateTime startInvalidTime;
            DateTime endInvalidTime;

            // if at DST start we transition forward in time then there is an ambiguous time range at the DST end
            if (rule.DaylightDelta < TimeSpan.Zero)
            {
                // if the year ends with daylight saving on then there cannot be any time-hole's in that year.
                if (rule.IsEndDateMarkerForEndOfYear())
                    return false;

                startInvalidTime = daylightTime.End;
                endInvalidTime = daylightTime.End - rule.DaylightDelta; /* FUTURE: + rule.StandardDelta; */
            }
            else
            {
                // if the year starts with daylight saving on then there cannot be any time-hole's in that year.
                if (rule.IsStartDateMarkerForBeginningOfYear())
                    return false;

                startInvalidTime = daylightTime.Start;
                endInvalidTime = daylightTime.Start + rule.DaylightDelta; /* FUTURE: - rule.StandardDelta; */
            }

            isInvalid = (time >= startInvalidTime && time < endInvalidTime);

            if (!isInvalid && startInvalidTime.Year != endInvalidTime.Year)
            {
                // there exists an extreme corner case where the start or end period is on a year boundary and
                // because of this the comparison above might have been performed for a year-early or a year-later
                // than it should have been.
                DateTime startModifiedInvalidTime;
                DateTime endModifiedInvalidTime;
                try
                {
                    startModifiedInvalidTime = startInvalidTime.AddYears(1);
                    endModifiedInvalidTime = endInvalidTime.AddYears(1);
                    isInvalid = (time >= startModifiedInvalidTime && time < endModifiedInvalidTime);
                }
                catch (ArgumentOutOfRangeException) { }

                if (!isInvalid)
                {
                    try
                    {
                        startModifiedInvalidTime = startInvalidTime.AddYears(-1);
                        endModifiedInvalidTime = endInvalidTime.AddYears(-1);
                        isInvalid = (time >= startModifiedInvalidTime && time < endModifiedInvalidTime);
                    }
                    catch (ArgumentOutOfRangeException) { }
                }
            }
            return isInvalid;
        }

        static private bool EqualStandardDates(TimeZoneInformation timeZone, ref TIME_DYNAMIC_ZONE_INFORMATION tdzi)
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

        static private bool EqualDaylightDates(TimeZoneInformation timeZone, ref TIME_DYNAMIC_ZONE_INFORMATION tdzi)
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

        static private bool CheckDaylightSavingTimeNotSupported(TimeZoneInformation timeZone)
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
        static internal unsafe bool FindMatchToCurrentTimeZone(TimeZoneInformation timeZoneInformation)
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

        static private TimeZoneInfo GetLocalTimeZone(CachedData cachedData)
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

        //
        // GetLocalTimeZoneFromWin32Data -
        //
        // Helper function used by 'GetLocalTimeZone()' - this function wraps a bunch of
        // try/catch logic for handling the TimeZoneInfo private constructor that takes
        // a Win32Native.TimeZoneInformation structure.
        //
        static private TimeZoneInfo GetLocalTimeZoneFromWin32Data(TimeZoneInformation timeZoneInformation, Boolean dstDisabled)
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
        // GetUtcOffset -
        //
        // Helper function that calculates the UTC offset for a dateTime in a timeZone.
        // This function assumes that the dateTime is already converted into the timeZone.
        //
        static private TimeSpan GetUtcOffset(DateTime time, TimeZoneInfo zone, TimeZoneInfoOptions flags)
        {
            TimeSpan baseOffset = zone.BaseUtcOffset;
            AdjustmentRule rule = zone.GetAdjustmentRuleForTime(time);

            if (rule != null)
            {
                baseOffset = baseOffset + rule.BaseUtcOffsetDelta;
                if (rule.HasDaylightSaving)
                {
                    DaylightTime daylightTime = GetDaylightTime(time.Year, rule);
                    Boolean isDaylightSavings = GetIsDaylightSavings(time, rule, daylightTime, flags);
                    baseOffset += (isDaylightSavings ? rule.DaylightDelta : TimeSpan.Zero /* FUTURE: rule.StandardDelta */);
                }
            }

            return baseOffset;
        }

        //
        // GetUtcOffsetFromUtc -
        //
        // Helper function that calculates the UTC offset for a UTC-dateTime in a timeZone.
        // This function assumes that the dateTime is represented in UTC and has *not*
        // already been converted into the timeZone.
        //
        static private TimeSpan GetUtcOffsetFromUtc(DateTime time, TimeZoneInfo zone)
        {
            Boolean isDaylightSavings;
            return GetUtcOffsetFromUtc(time, zone, out isDaylightSavings);
        }

        static private TimeSpan GetUtcOffsetFromUtc(DateTime time, TimeZoneInfo zone, out Boolean isDaylightSavings)
        {
            Boolean isAmbiguousLocalDst;
            return GetUtcOffsetFromUtc(time, zone, out isDaylightSavings, out isAmbiguousLocalDst);
        }

        // DateTime.Now fast path that avoids allocating an historically accurate TimeZoneInfo.Local and just creates a 1-year (current year) accurate time zone
        static internal TimeSpan GetDateTimeNowUtcOffsetFromUtc(DateTime time, out Boolean isAmbiguousLocalDst)
        {
            Boolean isDaylightSavings = false;
            isAmbiguousLocalDst = false;
            TimeSpan baseOffset;
            int timeYear = time.Year;

            OffsetAndRule match = s_cachedData.GetOneYearLocalFromUtc(timeYear);
            baseOffset = match.offset;

            if (match.rule != null)
            {
                baseOffset = baseOffset + match.rule.BaseUtcOffsetDelta;
                if (match.rule.HasDaylightSaving)
                {
                    isDaylightSavings = GetIsDaylightSavingsFromUtc(time, timeYear, match.offset, match.rule, out isAmbiguousLocalDst, TimeZoneInfo.Local);
                    baseOffset += (isDaylightSavings ? match.rule.DaylightDelta : TimeSpan.Zero /* FUTURE: rule.StandardDelta */);
                }
            }
            return baseOffset;
        }

        static internal TimeSpan GetUtcOffsetFromUtc(DateTime time, TimeZoneInfo zone, out Boolean isDaylightSavings, out Boolean isAmbiguousLocalDst)
        {
            isDaylightSavings = false;
            isAmbiguousLocalDst = false;
            TimeSpan baseOffset = zone.BaseUtcOffset;
            Int32 year;
            AdjustmentRule rule;

            if (time > s_maxDateOnly)
            {
                rule = zone.GetAdjustmentRuleForTime(DateTime.MaxValue);
                year = 9999;
            }
            else if (time < s_minDateOnly)
            {
                rule = zone.GetAdjustmentRuleForTime(DateTime.MinValue);
                year = 1;
            }
            else
            {
                DateTime targetTime = time + baseOffset;

                // As we get the associated rule using the adjusted targetTime, we should use the adjusted year (targetTime.Year) too as after adding the baseOffset, 
                // sometimes the year value can change if the input datetime was very close to the beginning or the end of the year. Examples of such cases:
                //      “Libya Standard Time” when used with the date 2011-12-31T23:59:59.9999999Z
                //      "W. Australia Standard Time" used with date 2005-12-31T23:59:00.0000000Z
                year = targetTime.Year;

                rule = zone.GetAdjustmentRuleForTime(targetTime);
            }

            if (rule != null)
            {
                baseOffset = baseOffset + rule.BaseUtcOffsetDelta;
                if (rule.HasDaylightSaving)
                {
                    isDaylightSavings = GetIsDaylightSavingsFromUtc(time, year, zone._baseUtcOffset, rule, out isAmbiguousLocalDst, zone);
                    baseOffset += (isDaylightSavings ? rule.DaylightDelta : TimeSpan.Zero /* FUTURE: rule.StandardDelta */);
                }
            }

            return baseOffset;
        }


        //
        // TransitionTimeFromTimeZoneInformation -
        //
        // Converts a TimeZoneInformation (REG_TZI_FORMAT struct) to a TransitionTime
        //
        // * when the argument 'readStart' is true the corresponding daylightTransitionTimeStart field is read
        // * when the argument 'readStart' is false the corresponding dayightTransitionTimeEnd field is read
        //
        static private bool TransitionTimeFromTimeZoneInformation(TIME_ZONE_INFORMATION timeZoneInformation, out TransitionTime transitionTime, bool readStartDate)
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
        // Overloaded method which take TimeZoneInformation
        //

        static private bool TransitionTimeFromTimeZoneInformation(TimeZoneInformation timeZoneInformation, out TransitionTime transitionTime, bool readStartDate)
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
        // TransitionTimeToDateTime -
        //
        // Helper function that converts a year and TransitionTime into a DateTime
        //
        static private DateTime TransitionTimeToDateTime(Int32 year, TransitionTime transitionTime)
        {
            DateTime value;
            DateTime timeOfDay = transitionTime.TimeOfDay;

            if (transitionTime.IsFixedDateRule)
            {
                // create a DateTime from the passed in year and the properties on the transitionTime

                // if the day is out of range for the month then use the last day of the month
                Int32 day = DateTime.DaysInMonth(year, transitionTime.Month);

                value = new DateTime(year, transitionTime.Month, (day < transitionTime.Day) ? day : transitionTime.Day,
                            timeOfDay.Hour, timeOfDay.Minute, timeOfDay.Second, timeOfDay.Millisecond);
            }
            else
            {
                if (transitionTime.Week <= 4)
                {
                    //
                    // Get the (transitionTime.Week)th Sunday.
                    //
                    value = new DateTime(year, transitionTime.Month, 1,
                            timeOfDay.Hour, timeOfDay.Minute, timeOfDay.Second, timeOfDay.Millisecond);

                    int dayOfWeek = (int)value.DayOfWeek;
                    int delta = (int)transitionTime.DayOfWeek - dayOfWeek;
                    if (delta < 0)
                    {
                        delta += 7;
                    }
                    delta += 7 * (transitionTime.Week - 1);

                    if (delta > 0)
                    {
                        value = value.AddDays(delta);
                    }
                }
                else
                {
                    //
                    // If TransitionWeek is greater than 4, we will get the last week.
                    //
                    Int32 daysInMonth = DateTime.DaysInMonth(year, transitionTime.Month);
                    value = new DateTime(year, transitionTime.Month, daysInMonth,
                            timeOfDay.Hour, timeOfDay.Minute, timeOfDay.Second, timeOfDay.Millisecond);

                    // This is the day of week for the last day of the month.
                    int dayOfWeek = (int)value.DayOfWeek;
                    int delta = dayOfWeek - (int)transitionTime.DayOfWeek;
                    if (delta < 0)
                    {
                        delta += 7;
                    }

                    if (delta > 0)
                    {
                        value = value.AddDays(-delta);
                    }
                }
            }
            return value;
        }

        internal static TimeZoneInfoResult TryGetFullTimeZoneInformation(TimeZoneInformation timeZoneInformation, out TimeZoneInfo value, out Exception e, int defaultBaseUtcOffset)
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
                Debug.Assert(firstYear <= lastYear);
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
        // TryGetTimeZone -
        //
        // Helper function for retrieving a TimeZoneInfo object by <time_zone_name>.
        //
        // This function may return null.
        //
        // assumes cachedData lock is taken
        //
        static private TimeZoneInfoResult TryGetTimeZone(ref TimeZoneInformation timeZoneInformation, Boolean dstDisabled, out TimeZoneInfo value, out Exception e, CachedData cachedData)
        {
            TimeZoneInfoResult result = TimeZoneInfoResult.Success;
            e = null;
            TimeZoneInfo match = null;

            Debug.Assert(!String.IsNullOrEmpty(timeZoneInformation.TimeZoneKeyName));

            // check the cache
            if (cachedData.m_systemTimeZones != null)
            {
                if (cachedData.m_systemTimeZones.TryGetValue(timeZoneInformation.TimeZoneKeyName, out match))
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
                if (cachedData.m_systemTimeZones == null)
                    cachedData.m_systemTimeZones = new LowLevelDictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);

                cachedData.m_systemTimeZones.Add(timeZoneInformation.TimeZoneKeyName, match);

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

        //
        // UtcOffsetOutOfRange -
        //
        // Helper function that validates the TimeSpan is within +/- 14.0 hours
        //
        [Pure]
        static internal Boolean UtcOffsetOutOfRange(TimeSpan offset)
        {
            return (offset.TotalHours < -14.0 || offset.TotalHours > 14.0);
        }


        //
        // ValidateTimeZoneInfo -
        //
        // Helper function that performs all of the validation checks for the 
        // factory methods and deserialization callback
        //
        // returns a Boolean indicating whether the AdjustmentRule[] supports DST
        //
        static private void ValidateTimeZoneInfo(
                String id,
                TimeSpan baseUtcOffset,
                AdjustmentRule[] adjustmentRules,
                out Boolean adjustmentRulesSupportDst)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            if (id.Length == 0)
            {
                throw new ArgumentException(SR.Format(SR.Argument_InvalidId, id), "id");
            }

            if (UtcOffsetOutOfRange(baseUtcOffset))
            {
                throw new ArgumentOutOfRangeException("baseUtcOffset", SR.ArgumentOutOfRange_UtcOffset);
            }

            if (baseUtcOffset.Ticks % TimeSpan.TicksPerMinute != 0)
            {
                throw new ArgumentException(SR.Argument_TimeSpanHasSeconds, "baseUtcOffset");
            }
            Contract.EndContractBlock();

            adjustmentRulesSupportDst = false;

            //
            // "adjustmentRules" can either be null or a valid array of AdjustmentRule objects.
            // A valid array is one that does not contain any null elements and all elements
            // are sorted in chronological order
            //

            if (adjustmentRules != null && adjustmentRules.Length != 0)
            {
                adjustmentRulesSupportDst = true;
                AdjustmentRule prev = null;
                AdjustmentRule current = null;
                for (int i = 0; i < adjustmentRules.Length; i++)
                {
                    prev = current;
                    current = adjustmentRules[i];

                    if (current == null)
                    {
                        throw new InvalidTimeZoneException(SR.Argument_AdjustmentRulesNoNulls);
                    }

                    // FUTURE: check to see if this rule supports Daylight Saving Time
                    // adjustmentRulesSupportDst = adjustmentRulesSupportDst || current.SupportsDaylightSavingTime;
                    // FUTURE: test baseUtcOffset + current.StandardDelta

                    if (UtcOffsetOutOfRange(baseUtcOffset + current.DaylightDelta))
                    {
                        throw new InvalidTimeZoneException(SR.ArgumentOutOfRange_UtcOffsetAndDaylightDelta);
                    }


                    if (prev != null && current.DateStart <= prev.DateEnd)
                    {
                        // verify the rules are in chronological order and the DateStart/DateEnd do not overlap
                        throw new InvalidTimeZoneException(SR.Argument_AdjustmentRulesOutOfOrder);
                    }
                }
            }
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

        [TypeForwardedFrom("System.Core, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")]
        sealed internal class AdjustmentRule : IEquatable<AdjustmentRule>
        {
            // ---- SECTION:  members supporting exposed properties -------------*
            private DateTime _dateStart;
            private DateTime _dateEnd;
            private TimeSpan _daylightDelta;
            private TransitionTime _daylightTransitionStart;
            private TransitionTime _daylightTransitionEnd;
            private TimeSpan _baseUtcOffsetDelta;   // delta from the default Utc offset (utcOffset = defaultUtcOffset + m_baseUtcOffsetDelta)


            // ---- SECTION: public properties --------------*
            public DateTime DateStart
            {
                get
                {
                    return _dateStart;
                }
            }

            public DateTime DateEnd
            {
                get
                {
                    return _dateEnd;
                }
            }

            public TimeSpan DaylightDelta
            {
                get
                {
                    return _daylightDelta;
                }
            }


            public TransitionTime DaylightTransitionStart
            {
                get
                {
                    return _daylightTransitionStart;
                }
            }


            public TransitionTime DaylightTransitionEnd
            {
                get
                {
                    return _daylightTransitionEnd;
                }
            }

            internal TimeSpan BaseUtcOffsetDelta
            {
                get
                {
                    return _baseUtcOffsetDelta;
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

            //IEquatable<AdjustmentRule>
            public bool Equals(AdjustmentRule other)
            {
                bool equals = (other != null
                     && _dateStart == other._dateStart
                     && _dateEnd == other._dateEnd
                     && _daylightDelta == other._daylightDelta
                     && _baseUtcOffsetDelta == other._baseUtcOffsetDelta);

                equals = equals && _daylightTransitionEnd.Equals(other._daylightTransitionEnd)
                         && _daylightTransitionStart.Equals(other._daylightTransitionStart);

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


        [TypeForwardedFrom("System.Core, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")]
        internal struct TransitionTime : IEquatable<TransitionTime>
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


            [Pure]
            public bool Equals(TransitionTime other)
            {
                bool equal = (_isFixedDateRule == other._isFixedDateRule
                             && _timeOfDay == other._timeOfDay
                             && _month == other._month);

                if (equal)
                {
                    if (other._isFixedDateRule)
                    {
                        equal = (_day == other._day);
                    }
                    else
                    {
                        equal = (_week == other._week
                            && _dayOfWeek == other._dayOfWeek);
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
                            m_timeOfDay = new DateTime();
                            m_month = 0;
                            m_week  = 0;
                            m_day   = 0;
                            m_dayOfWeek = DayOfWeek.Sunday;
                            m_isFixedDateRule = false;
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
        }

        private class TimeZoneInfoComparer : IComparer<TimeZoneInfo>
        {
            int System.Collections.Generic.IComparer<TimeZoneInfo>.Compare(TimeZoneInfo x, TimeZoneInfo y)
            {
                // sort by BaseUtcOffset first and by DisplayName second - this is similar to the Windows Date/Time control panel
                int comparison = x.BaseUtcOffset.CompareTo(y.BaseUtcOffset);
                return comparison == 0 ? String.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : comparison;
            }
        }

        internal class TimeZoneInformation
        {
            public string StandardName;
            public string DaylightName;
            public string TimeZoneKeyName;

            // we need to keep this one for subsequent interops.
            public TIME_DYNAMIC_ZONE_INFORMATION Dtzi;

            public unsafe TimeZoneInformation(TIME_DYNAMIC_ZONE_INFORMATION dtzi)
            {
                StandardName = new String(dtzi.StandardName);
                DaylightName = new String(dtzi.DaylightName);
                TimeZoneKeyName = new String(dtzi.TimeZoneKeyName);
                Dtzi = dtzi;
            }
        }

        internal enum TimeZoneInfoResult
        {
            Success = 0,
            TimeZoneNotFoundException = 1,
            InvalidTimeZoneException = 2,
            SecurityException = 3
        };
    } // TimezoneInfo
} // namespace System
