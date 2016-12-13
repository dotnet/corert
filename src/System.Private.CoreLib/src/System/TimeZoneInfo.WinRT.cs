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

    sealed public partial class TimeZoneInfo
    {
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
                throw new ArgumentNullException(nameof(id));
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
            if (cache._systemTimeZones != null)
            {
                if (cache._systemTimeZones.TryGetValue(id, out value))
                {
                    return value;
                }
            }
            // See if the cache was fully filled, if not, fill it then check again.
            if (!cache.AreSystemTimesEnumerated)
            {
                cache.EnumerateSystemTimes();
                if (cache._systemTimeZones.TryGetValue(id, out value))
                {
                    return value;
                }
            }
            throw new ArgumentException(String.Format(SR.Argument_TimeZoneNotFound, id));
        }

        private const long TIME_ZONE_ID_INVALID = -1;

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
                    throw new ArgumentException(SR.Argument_DateTimeKindMustBeUnspecified, nameof(dateStart));
                }

                if (dateEnd.Kind != DateTimeKind.Unspecified)
                {
                    throw new ArgumentException(SR.Argument_DateTimeKindMustBeUnspecified, nameof(dateEnd));
                }

                if (daylightTransitionStart.Equals(daylightTransitionEnd))
                {
                    throw new ArgumentException(SR.Argument_TransitionTimesAreIdentical,
                                                nameof(daylightTransitionEnd));
                }


                if (dateStart > dateEnd)
                {
                    throw new ArgumentException(SR.Argument_OutOfOrderDateTimes, nameof(dateStart));
                }

                if (TimeZoneInfo.UtcOffsetOutOfRange(daylightDelta))
                {
                    throw new ArgumentOutOfRangeException(nameof(daylightDelta), daylightDelta,
                        SR.ArgumentOutOfRange_UtcOffset);
                }

                if (daylightDelta.Ticks % TimeSpan.TicksPerMinute != 0)
                {
                    throw new ArgumentException(SR.Argument_TimeSpanHasSeconds,
                        nameof(daylightDelta));
                }

                if (dateStart.TimeOfDay != TimeSpan.Zero)
                {
                    throw new ArgumentException(SR.Argument_DateTimeHasTimeOfDay,
                        nameof(dateStart));
                }

                if (dateEnd.TimeOfDay != TimeSpan.Zero)
                {
                    throw new ArgumentException(SR.Argument_DateTimeHasTimeOfDay,
                        nameof(dateEnd));
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
                    throw new ArgumentException(SR.Argument_DateTimeKindMustBeUnspecified, nameof(timeOfDay));
                }

                // Month range 1-12
                if (month < 1 || month > 12)
                {
                    throw new ArgumentOutOfRangeException(nameof(month), SR.ArgumentOutOfRange_MonthParam);
                }

                // Day range 1-31
                if (day < 1 || day > 31)
                {
                    throw new ArgumentOutOfRangeException(nameof(day), SR.ArgumentOutOfRange_DayParam);
                }

                // Week range 1-5
                if (week < 1 || week > 5)
                {
                    throw new ArgumentOutOfRangeException(nameof(week), SR.ArgumentOutOfRange_Week);
                }

                // DayOfWeek range 0-6
                if ((int)dayOfWeek < 0 || (int)dayOfWeek > 6)
                {
                    throw new ArgumentOutOfRangeException(nameof(dayOfWeek), SR.ArgumentOutOfRange_DayOfWeek);
                }
                Contract.EndContractBlock();

                if (timeOfDay.Year != 1 || timeOfDay.Month != 1
                || timeOfDay.Day != 1 || (timeOfDay.Ticks % TimeSpan.TicksPerMillisecond != 0))
                {
                    throw new ArgumentException(SR.Argument_DateTimeHasTicks, nameof(timeOfDay));
                }
            }
        }
    } // TimezoneInfo
} // namespace System
