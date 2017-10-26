// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: 
** This class is used to represent a Dynamic TimeZone.  It
** has methods for converting a DateTime between TimeZones
**
**
============================================================*/

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
    sealed public partial class TimeZoneInfo
    {
        // use for generating multi-year DST periods
        private static readonly TransitionTime s_transition5_15 = TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1), 05, 15);
        private static readonly TransitionTime s_transition7_15 = TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1), 07, 15);
        private static readonly TransitionTime s_transition10_15 = TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1), 10, 15);
        private static readonly TransitionTime s_transition12_15 = TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1), 12, 15);

        private TimeZoneInfo(Byte[] data, Boolean dstDisabled)
        {
            TZifHead t;
            DateTime[] dts;
            Byte[] typeOfLocalTime;
            TZifType[] transitionType;
            String zoneAbbreviations;
            Boolean[] StandardTime;
            Boolean[] GmtTime;

            // parse the raw TZif bytes; this method can throw ArgumentException when the data is malformed.
            TZif_ParseRaw(data, out t, out dts, out typeOfLocalTime, out transitionType, out zoneAbbreviations, out StandardTime, out GmtTime);

            _id = c_localId;
            _displayName = c_localId;
            _baseUtcOffset = TimeSpan.Zero;

            // find the best matching baseUtcOffset and display strings based on the current utcNow value
            DateTime utcNow = DateTime.UtcNow;
            for (int i = 0; i < dts.Length && dts[i] <= utcNow; i++)
            {
                int type = typeOfLocalTime[i];
                if (!transitionType[type].IsDst)
                {
                    _baseUtcOffset = transitionType[type].UtcOffset;
                    _standardDisplayName = TZif_GetZoneAbbreviation(zoneAbbreviations, transitionType[type].AbbreviationIndex);
                }
                else
                {
                    _daylightDisplayName = TZif_GetZoneAbbreviation(zoneAbbreviations, transitionType[type].AbbreviationIndex);
                }
            }

            if (dts.Length == 0)
            {
                // time zones like Africa/Bujumbura and Etc/GMT* have no transition times but still contain
                // TZifType entries that may contain a baseUtcOffset and display strings
                for (int i = 0; i < transitionType.Length; i++)
                {
                    if (!transitionType[i].IsDst)
                    {
                        _baseUtcOffset = transitionType[i].UtcOffset;
                        _standardDisplayName = TZif_GetZoneAbbreviation(zoneAbbreviations, transitionType[i].AbbreviationIndex);
                    }
                    else
                    {
                        _daylightDisplayName = TZif_GetZoneAbbreviation(zoneAbbreviations, transitionType[i].AbbreviationIndex);
                    }
                }
            }
            _id = _standardDisplayName;
            _displayName = _standardDisplayName;

            // TZif supports seconds-level granularity with offsets but TimeZoneInfo only supports minutes since it aligns
            // with DateTimeOffset, SQL Server, and the W3C XML Specification
            if (_baseUtcOffset.Ticks % TimeSpan.TicksPerMinute != 0)
            {
                _baseUtcOffset = new TimeSpan(_baseUtcOffset.Hours, _baseUtcOffset.Minutes, 0);
            }

            if (!dstDisabled)
            {
                // only create the adjustment rule if DST is enabled
                TZif_GenerateAdjustmentRules(out _adjustmentRules, dts, typeOfLocalTime, transitionType, StandardTime, GmtTime);
            }

            ValidateTimeZoneInfo(_id, _baseUtcOffset, _adjustmentRules, out _supportsDaylightSavingTime);
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

            // The rules we use in Unix cares mostly about the start and end dates but doesn't fill the transition start and end info. 
            // as the rules now is public, we should fill it properly so the caller doesn't have to know how we use it internally 
            // and can use it as it is used in Windows

            AdjustmentRule[] rules = new AdjustmentRule[_adjustmentRules.Length];

            for (int i = 0; i < _adjustmentRules.Length; i++)
            {
                var rule = _adjustmentRules[i];
                var start = rule.DateStart.Kind == DateTimeKind.Utc ?
                            new DateTime(TimeZoneInfo.ConvertTime(rule.DateStart, this).Ticks, DateTimeKind.Unspecified) :
                            rule.DateStart;
                var end = rule.DateEnd.Kind == DateTimeKind.Utc ?
                            new DateTime(TimeZoneInfo.ConvertTime(rule.DateEnd, this).Ticks - 1, DateTimeKind.Unspecified) :
                            rule.DateEnd;

                var startTransition = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, start.Hour, start.Minute, start.Second), start.Month, start.Day);
                var endTransition = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, end.Hour, end.Minute, end.Second), end.Month, end.Day);

                rules[i] = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(start.Date, end.Date, rule.DaylightDelta, startTransition, endTransition);
            }

            return rules;
        }

        public static TimeZoneInfo FindSystemTimeZoneById(string id)
        {
            // UNIXTODO
            throw new NotImplementedException();
        }

        private static void PopulateAllSystemTimeZones(CachedData cachedData)
        {
            Debug.Assert(Monitor.IsEntered(cachedData));
            // UNIXTODO
            throw new NotImplementedException();
        }

        internal static Byte[] GetLocalTzFile()
        {
            // UNIXTODO
            throw new NotImplementedException();
        }

        private static TimeZoneInfo GetLocalTimeZone(CachedData cachedData)
        {
            Byte[] rawData = GetLocalTzFile();

            if (rawData != null)
            {
                try
                {
                    return new TimeZoneInfo(rawData, false); // create a TimeZoneInfo instance from the TZif data w/ DST support
                }
                catch (ArgumentException) { }
                catch (InvalidTimeZoneException) { }
                try
                {
                    return new TimeZoneInfo(rawData, true); // create a TimeZoneInfo instance from the TZif data w/o DST support
                }
                catch (ArgumentException) { }
                catch (InvalidTimeZoneException) { }
            }
            // the data returned from the PAL is completely bogus; return a dummy entry
            return CreateCustomTimeZone(c_localId, TimeSpan.Zero, c_localId, c_localId);
        }

        // TZFILE(5)                   BSD File Formats Manual                  TZFILE(5)
        // 
        // NAME
        //      tzfile -- timezone information
        // 
        // SYNOPSIS
        //      #include "/usr/src/lib/libc/stdtime/tzfile.h"
        // 
        // DESCRIPTION
        //      The time zone information files used by tzset(3) begin with the magic
        //      characters ``TZif'' to identify them as time zone information files, fol-
        //      lowed by sixteen bytes reserved for future use, followed by four four-
        //      byte values written in a ``standard'' byte order (the high-order byte of
        //      the value is written first).  These values are, in order:
        // 
        //      tzh_ttisgmtcnt  The number of UTC/local indicators stored in the file.
        //      tzh_ttisstdcnt  The number of standard/wall indicators stored in the
        //                      file.
        //      tzh_leapcnt     The number of leap seconds for which data is stored in
        //                      the file.
        //      tzh_timecnt     The number of ``transition times'' for which data is
        //                      stored in the file.
        //      tzh_typecnt     The number of ``local time types'' for which data is
        //                      stored in the file (must not be zero).
        //      tzh_charcnt     The number of characters of ``time zone abbreviation
        //                      strings'' stored in the file.
        // 
        //      The above header is followed by tzh_timecnt four-byte values of type
        //      long, sorted in ascending order.  These values are written in ``stan-
        //      dard'' byte order.  Each is used as a transition time (as returned by
        //      time(3)) at which the rules for computing local time change.  Next come
        //      tzh_timecnt one-byte values of type unsigned char; each one tells which
        //      of the different types of ``local time'' types described in the file is
        //      associated with the same-indexed transition time.  These values serve as
        //      indices into an array of ttinfo structures that appears next in the file;
        //      these structures are defined as follows:
        // 
        //            struct ttinfo {
        //                    long    tt_gmtoff;
        //                    int     tt_isdst;
        //                    unsigned int    tt_abbrind;
        //            };
        // 
        //      Each structure is written as a four-byte value for tt_gmtoff of type
        //      long, in a standard byte order, followed by a one-byte value for tt_isdst
        //      and a one-byte value for tt_abbrind.  In each structure, tt_gmtoff gives
        //      the number of seconds to be added to UTC, tt_isdst tells whether t_isdst
        //      should be set by localtime(3) and tt_abbrind serves as an index into the
        //      array of time zone abbreviation characters that follow the ttinfo struc-
        //      ture(s) in the file.
        // 
        //      Then there are tzh_leapcnt pairs of four-byte values, written in standard
        //      byte order; the first value of each pair gives the time (as returned by
        //      time(3)) at which a leap second occurs; the second gives the total number
        //      of leap seconds to be applied after the given time.  The pairs of values
        //      are sorted in ascending order by time.b
        // 
        //      Then there are tzh_ttisstdcnt standard/wall indicators, each stored as a
        //      one-byte value; they tell whether the transition times associated with
        //      local time types were specified as standard time or wall clock time, and
        //      are used when a time zone file is used in handling POSIX-style time zone
        //      environment variables.
        // 
        //      Finally there are tzh_ttisgmtcnt UTC/local indicators, each stored as a
        //      one-byte value; they tell whether the transition times associated with
        //      local time types were specified as UTC or local time, and are used when a
        //      time zone file is used in handling POSIX-style time zone environment
        //      variables.
        // 
        //      localtime uses the first standard-time ttinfo structure in the file (or
        //      simply the first ttinfo structure in the absence of a standard-time
        //      structure) if either tzh_timecnt is zero or the time argument is less
        //      than the first transition time recorded in the file.
        // 
        // SEE ALSO
        //      ctime(3), time2posix(3), zic(8)
        // 
        // BSD                           September 13, 1994                           BSD
        // 
        // 
        // 
        // TIME(3)                  BSD Library Functions Manual                  TIME(3)
        // 
        // NAME
        //      time -- get time of day
        // 
        // LIBRARY
        //      Standard C Library (libc, -lc)
        // 
        // SYNOPSIS
        //      #include <time.h>
        // 
        //      time_t
        //      time(time_t *tloc);
        // 
        // DESCRIPTION
        //      The time() function returns the value of time in seconds since 0 hours, 0
        //      minutes, 0 seconds, January 1, 1970, Coordinated Universal Time, without
        //      including leap seconds.  If an error occurs, time() returns the value
        //      (time_t)-1.
        // 
        //      The return value is also stored in *tloc, provided that tloc is non-null.
        // 
        // ERRORS
        //      The time() function may fail for any of the reasons described in
        //      gettimeofday(2).
        // 
        // SEE ALSO
        //      gettimeofday(2), ctime(3)
        // 
        // STANDARDS
        //      The time function conforms to IEEE Std 1003.1-2001 (``POSIX.1'').
        // 
        // BUGS
        //      Neither ISO/IEC 9899:1999 (``ISO C99'') nor IEEE Std 1003.1-2001
        //      (``POSIX.1'') requires time() to set errno on failure; thus, it is impos-
        //      sible for an application to distinguish the valid time value -1 (repre-
        //      senting the last UTC second of 1969) from the error return value.
        // 
        //      Systems conforming to earlier versions of the C and POSIX standards
        //      (including older versions of FreeBSD) did not set *tloc in the error
        //      case.
        // 
        // HISTORY
        //      A time() function appeared in Version 6 AT&T UNIX.
        // 
        // BSD                              July 18, 2003                             BSD
        // 
        // 

        //
        // TZif_CalculateTransitionTime -
        //
        // Example inputs:
        // ----------------- 
        // utc               =     1918-03-31T10:00:00.0000000Z
        // transitionType    =     {-08:00:00  DST=False,  Index 4}
        // standardTime      =     False
        // gmtTime           =     False
        //
        private static TransitionTime TZif_CalculateTransitionTime(DateTime utc, TimeSpan offset,
                                                                   TZifType transitionType, Boolean standardTime,
                                                                   Boolean gmtTime, out DateTime ruleDate)
        {
            // convert from UTC to local clock time
            Int64 ticks = utc.Ticks + offset.Ticks;
            if (ticks > DateTime.MaxValue.Ticks)
            {
                utc = DateTime.MaxValue;
            }
            else if (ticks < DateTime.MinValue.Ticks)
            {
                utc = DateTime.MinValue;
            }
            else
            {
                utc = new DateTime(ticks);
            }

            DateTime timeOfDay = new DateTime(1, 1, 1, utc.Hour, utc.Minute, utc.Second, utc.Millisecond);
            int month = utc.Month;
            int day = utc.Day;

            ruleDate = new DateTime(utc.Year, month, day);
            // FUTURE: take standardTime/gmtTime into account
            return TransitionTime.CreateFixedDateRule(timeOfDay, month, day);
        }

        private static void TZif_GenerateAdjustmentRules(out AdjustmentRule[] rules, DateTime[] dts, Byte[] typeOfLocalTime,
                                                         TZifType[] transitionType, Boolean[] StandardTime, Boolean[] GmtTime)
        {
            rules = null;

            int index = 0;
            List<AdjustmentRule> rulesList = new List<AdjustmentRule>(1);
            bool succeeded = true;

            while (succeeded && index < dts.Length)
            {
                succeeded = TZif_GenerateAdjustmentRule(ref index, ref rulesList, dts, typeOfLocalTime, transitionType, StandardTime, GmtTime);
            }

            rules = rulesList.ToArray();
            if (rules != null && rules.Length == 0)
            {
                rules = null;
            }
        }


        private static bool TZif_GenerateAdjustmentRule(ref int startIndex, ref List<AdjustmentRule> rulesList, DateTime[] dts, Byte[] typeOfLocalTime,
                                                                  TZifType[] transitionType, Boolean[] StandardTime, Boolean[] GmtTime)
        {
            int index = startIndex;
            bool Dst = false;
            int DstStartIndex = -1;
            int DstEndIndex = -1;
            DateTime startDate = DateTime.MinValue.Date;
            DateTime endDate = DateTime.MaxValue.Date;


            // find the next DST transition start time index
            while (!Dst && index < typeOfLocalTime.Length)
            {
                int typeIndex = typeOfLocalTime[index];
                if (typeIndex < transitionType.Length && transitionType[typeIndex].IsDst)
                {
                    // found the next DST transition start time
                    Dst = true;
                    DstStartIndex = index;
                }
                else
                {
                    index++;
                }
            }

            // find the next DST transition end time index
            while (Dst && index < typeOfLocalTime.Length)
            {
                int typeIndex = typeOfLocalTime[index];
                if (typeIndex < transitionType.Length && !transitionType[typeIndex].IsDst)
                {
                    // found the next DST transition end time
                    Dst = false;
                    DstEndIndex = index;
                }
                else
                {
                    index++;
                }
            }


            //
            // construct the adjustment rule from the two indices
            //
            if (DstStartIndex >= 0)
            {
                DateTime startTransitionDate = dts[DstStartIndex];
                DateTime endTransitionDate;


                if (DstEndIndex == -1)
                {
                    // we found a DST start but no DST end; in this case use the
                    // prior non-DST entry if it exists, else use the current entry for both start and end (e.g., zero daylightDelta)
                    if (DstStartIndex > 0)
                    {
                        DstEndIndex = DstStartIndex - 1;
                    }
                    else
                    {
                        DstEndIndex = DstStartIndex;
                    }
                    endTransitionDate = DateTime.MaxValue;
                }
                else
                {
                    endTransitionDate = dts[DstEndIndex];
                }

                int dstStartTypeIndex = typeOfLocalTime[DstStartIndex];
                int dstEndTypeIndex = typeOfLocalTime[DstEndIndex];

                TimeSpan daylightBias = transitionType[dstStartTypeIndex].UtcOffset - transitionType[dstEndTypeIndex].UtcOffset;
                // TZif supports seconds-level granularity with offsets but TimeZoneInfo only supports minutes since it aligns
                // with DateTimeOffset, SQL Server, and the W3C XML Specification
                if (daylightBias.Ticks % TimeSpan.TicksPerMinute != 0)
                {
                    daylightBias = new TimeSpan(daylightBias.Hours, daylightBias.Minutes, 0);
                }

                // 
                // the normal case is less than 12 months between transition times.  However places like America/Catamarca
                // have DST from 1946-1963 straight without a gap.  In that case we need to create a series of Adjustment
                // Rules to fudge the multi-year DST period
                //
                if ((endTransitionDate - startTransitionDate).Ticks <= TimeSpan.TicksPerDay * 364)
                {
                    TransitionTime dstStart;
                    TransitionTime dstEnd;
                    TimeSpan startTransitionOffset = (DstStartIndex > 0 ? transitionType[typeOfLocalTime[DstStartIndex - 1]].UtcOffset : transitionType[dstEndTypeIndex].UtcOffset);
                    TimeSpan endTransitionOffset = (DstEndIndex > 0 ? transitionType[typeOfLocalTime[DstEndIndex - 1]].UtcOffset : transitionType[dstStartTypeIndex].UtcOffset);


                    dstStart = TZif_CalculateTransitionTime(startTransitionDate,
                                                                           startTransitionOffset,
                                                                           transitionType[dstStartTypeIndex],
                                                                           StandardTime[dstStartTypeIndex],
                                                                           GmtTime[dstStartTypeIndex],
                                                                           out startDate);

                    dstEnd = TZif_CalculateTransitionTime(endTransitionDate,
                                                                           endTransitionOffset,
                                                                           transitionType[dstEndTypeIndex],
                                                                           StandardTime[dstEndTypeIndex],
                                                                           GmtTime[dstEndTypeIndex],
                                                                           out endDate);



                    // calculate the AdjustmentRule end date
                    if (DstStartIndex >= DstEndIndex)
                    {
                        // we found a DST start but no DST end
                        endDate = DateTime.MaxValue.Date;
                    }

                    AdjustmentRule r = AdjustmentRule.CreateAdjustmentRule(startDate, endDate, daylightBias, dstStart, dstEnd);
                    rulesList.Add(r);
                }
                else
                {
                    // create the multi-year DST rule series:
                    //
                    // For example America/Catamarca:
                    //     1946-10-01T04:00:00.0000000Z {-03:00:00 DST=True}
                    //     1963-10-01T03:00:00.0000000Z {-04:00:00 DST=False}
                    //
                    // gets converted into a series of overlapping 5/7month adjustment rules:
                    //
                    // [AdjustmentRule       #0] // start rule
                    // [1946/09/31 - 1947/06/15] // * starts 1 day prior to startTransitionDate
                    // [start: 10/01 @4:00     ] // * N months long, stopping at month 6 or 11
                    // [end  : 07/15           ] // notice how the _end_ is outside the range
                    //
                    // [AdjustmentRule       #1] // middle-year all-DST rule
                    // [1947/06/16 - 1947/11/15] // * starts 1 day after last day in previous rule
                    // [start: 05/15           ] // * 5 months long, stopping at month 6 or 11
                    // [end  : 12/15           ] // notice how the _start and end_ are outside the range
                    //
                    // [AdjustmentRule       #2] // middle-year all-DST rule
                    // [1947/11/16 - 1947/06/15] //  * starts 1 day after last day in previous rule
                    // [start: 10/01           ] //  * 7 months long, stopping at month 6 or 11
                    // [end  : 07/15           ] // notice how the _start and end_ are outside the range
                    //  
                    // .........................
                    //
                    // [AdjustmentRule       #N] // end rule
                    // [1963/06/16 - 1946/10/02] //   * starts 1 day after last day in previous rule
                    // [start: 05/15           ] //   * N months long, stopping 1 day after endTransitionDate
                    // [end  : 10/01           ] // notice how the _start_ is outside the range
                    //

                    // create the first rule from N to either 06/15 or 11/15
                    TZif_CreateFirstMultiYearRule(ref rulesList, daylightBias, startTransitionDate, DstStartIndex, dstStartTypeIndex, dstEndTypeIndex,
                                                                                            dts, transitionType, typeOfLocalTime, StandardTime, GmtTime);

                    // create the filler rules
                    TZif_CreateMiddleMultiYearRules(ref rulesList, daylightBias, endTransitionDate);

                    // create the last rule  
                    TZif_CreateLastMultiYearRule(ref rulesList, daylightBias, endTransitionDate, DstStartIndex, dstStartTypeIndex, DstEndIndex, dstEndTypeIndex,
                                                                                            dts, transitionType, typeOfLocalTime, StandardTime, GmtTime);
                }

                startIndex = index + 1;
                return true;
            }

            // setup the start values for the next call to TZif_GenerateAdjustmentRule(...)
            startIndex = index + 1;
            return false; // did not create a new AdjustmentRule
        }

        private static void TZif_CreateFirstMultiYearRule(ref List<AdjustmentRule> rulesList, TimeSpan daylightBias, DateTime startTransitionDate,
                                                          int DstStartIndex, int dstStartTypeIndex, int dstEndTypeIndex, DateTime[] dts, TZifType[] transitionType,
                                                          Byte[] typeOfLocalTime, bool[] StandardTime, bool[] GmtTime)
        {
            // [AdjustmentRule       #0] // start rule
            // [1946/09/31 - 1947/06/15] // * starts 1 day prior to startTransitionDate
            // [start: 10/01 @4:00     ] // * N months long, stopping at month 6 or 11
            // [end  : 07/15           ] // notice how the _end_ is outside the range

            DateTime startDate;
            DateTime endDate;
            TransitionTime dstStart;
            TransitionTime dstEnd;

            TimeSpan startTransitionOffset = (DstStartIndex > 0 ? transitionType[typeOfLocalTime[DstStartIndex - 1]].UtcOffset : transitionType[dstEndTypeIndex].UtcOffset);

            dstStart = TZif_CalculateTransitionTime(startTransitionDate,
                                                    startTransitionOffset,
                                                    transitionType[dstStartTypeIndex],
                                                    StandardTime[dstStartTypeIndex],
                                                    GmtTime[dstStartTypeIndex],
                                                    out startDate);

            //
            // Choosing the endDate based on the startDate:
            //
            // startTransitionDate.Month -> end
            // 1        4|5        8|9       12
            // [-> 06/15]|[-> 11/15]|[-> 06/15]
            //
            int startDateMonth = startDate.Month;
            int startDateYear = startDate.Year;

            if (startDateMonth <= 4)
            {
                endDate = new DateTime(startDateYear, 06, 15);
                dstEnd = s_transition7_15;
            }
            else if (startDateMonth <= 8)
            {
                endDate = new DateTime(startDateYear, 11, 15);
                dstEnd = s_transition12_15;
            }
            else if (startDateYear < 9999)
            {
                endDate = new DateTime(startDateYear + 1, 06, 15);
                dstEnd = s_transition7_15;
            }
            else
            {
                endDate = DateTime.MaxValue;
                dstEnd = s_transition7_15;
            }

            AdjustmentRule r = AdjustmentRule.CreateAdjustmentRule(startDate, endDate, daylightBias, dstStart, dstEnd);
            rulesList.Add(r);
        }


        private static void TZif_CreateLastMultiYearRule(ref List<AdjustmentRule> rulesList, TimeSpan daylightBias, DateTime endTransitionDate,
                                                          int DstStartIndex, int dstStartTypeIndex, int DstEndIndex, int dstEndTypeIndex, DateTime[] dts, TZifType[] transitionType,
                                                          Byte[] typeOfLocalTime, bool[] StandardTime, bool[] GmtTime)
        {
            // [AdjustmentRule       #N] // end rule
            // [1963/06/16 - 1946/10/02] //   * starts 1 day after last day in previous rule
            // [start: 05/15           ] //   * N months long, stopping 1 day after endTransitionDate
            // [end  : 10/01           ] // notice how the _start_ is outside the range

            DateTime endDate;
            TransitionTime dstEnd;

            TimeSpan endTransitionOffset = (DstEndIndex > 0 ? transitionType[typeOfLocalTime[DstEndIndex - 1]].UtcOffset : transitionType[dstStartTypeIndex].UtcOffset);


            dstEnd = TZif_CalculateTransitionTime(endTransitionDate,
                                                   endTransitionOffset,
                                                   transitionType[dstEndTypeIndex],
                                                   StandardTime[dstEndTypeIndex],
                                                   GmtTime[dstEndTypeIndex],
                                                   out endDate);

            if (DstStartIndex >= DstEndIndex)
            {
                // we found a DST start but no DST end
                endDate = DateTime.MaxValue.Date;
            }

            AdjustmentRule prevRule = rulesList[rulesList.Count - 1]; // grab the last element of the MultiYearRule sequence
            int y = prevRule.DateEnd.Year;
            if (prevRule.DateEnd.Month <= 6)
            {
                // create a rule from 06/16/YYYY to endDate
                AdjustmentRule r = AdjustmentRule.CreateAdjustmentRule(new DateTime(y, 06, 16), endDate, daylightBias, s_transition5_15, dstEnd);
                rulesList.Add(r);
            }
            else
            {
                // create a rule from 11/16/YYYY to endDate
                AdjustmentRule r = AdjustmentRule.CreateAdjustmentRule(new DateTime(y, 11, 16), endDate, daylightBias, s_transition10_15, dstEnd);
                rulesList.Add(r);
            }
        }


        private static void TZif_CreateMiddleMultiYearRules(ref List<AdjustmentRule> rulesList, TimeSpan daylightBias, DateTime endTransitionDate)
        {
            // 
            // [AdjustmentRule       #1] // middle-year all-DST rule
            // [1947/06/16 - 1947/11/15] // * starts 1 day after last day in previous rule
            // [start: 05/15           ] // * 5 months long, stopping at month 6 or 11
            // [end  : 12/15           ] // notice how the _start and end_ are outside the range
            //
            // [AdjustmentRule       #2] // middle-year all-DST rule
            // [1947/11/16 - 1947/06/15] //  * starts 1 day after last day in previous rule
            // [start: 10/01           ] //  * 7 months long, stopping at month 6 or 11
            // [end  : 07/15           ] // notice how the _start and end_ are outside the range
            //  
            // .........................

            AdjustmentRule prevRule = rulesList[rulesList.Count - 1]; // grab the first element of the MultiYearRule sequence
            DateTime endDate;

            //
            // Choosing the last endDate based on the endTransitionDate
            //
            // endTransitionDate.Month -> end
            // 1        4|5        8|9       12
            // [11/15 <-]|[11/15 <-]|[06/15 <-]
            //            
            if (endTransitionDate.Month <= 8)
            {
                // set the end date to 11/15/YYYY-1
                endDate = new DateTime(endTransitionDate.Year - 1, 11, 15);
            }
            else
            {
                // set the end date to 06/15/YYYY
                endDate = new DateTime(endTransitionDate.Year, 06, 15);
            }

            while (prevRule.DateEnd < endDate)
            {
                // the last endDate will be on either 06/15 or 11/15
                int y = prevRule.DateEnd.Year;
                if (prevRule.DateEnd.Month <= 6)
                {
                    // create a rule from 06/16/YYYY to 11/15/YYYY
                    AdjustmentRule r = AdjustmentRule.CreateAdjustmentRule(new DateTime(y, 06, 16), new DateTime(y, 11, 15),
                                                                           daylightBias, s_transition5_15, s_transition12_15);
                    prevRule = r;
                    rulesList.Add(r);
                }
                else
                {
                    // create a rule from 11/16/YYYY to 06/15/YYYY+1
                    AdjustmentRule r = AdjustmentRule.CreateAdjustmentRule(new DateTime(y, 11, 16), new DateTime(y + 1, 06, 15),
                                                                           daylightBias, s_transition10_15, s_transition7_15);
                    prevRule = r;
                    rulesList.Add(r);
                }
            }
        }


        // Returns the Substring from zoneAbbreviations starting at index and ending at '\0'
        // zoneAbbreviations is expected to be in the form: "PST\0PDT\0PWT\0\PPT"
        private static String TZif_GetZoneAbbreviation(String zoneAbbreviations, int index)
        {
            int lastIndex = zoneAbbreviations.IndexOf('\0', index);
            if (lastIndex > 0)
            {
                return zoneAbbreviations.Substring(index, lastIndex - index);
            }
            else
            {
                return zoneAbbreviations.Substring(index);
            }
        }

        // verify the 'index' is referenced from the typeOfLocalTime byte array.
        //
        private static Boolean TZif_ValidTransitionType(int index, Byte[] typeOfLocalTime)
        {
            Boolean result = false;

            if (typeOfLocalTime != null)
            {
                for (int i = 0; !result && i < typeOfLocalTime.Length; i++)
                {
                    if (index == typeOfLocalTime[i])
                    {
                        result = true;
                    }
                }
            }
            return result;
        }

        // Converts an array of bytes into an int - always using standard byte order (Big Endian)
        // per TZif file standard
        private static int TZif_ToInt32(byte[] value, int startIndex)
        {
            return (value[startIndex] << 24) | (value[startIndex + 1] << 16) | (value[startIndex + 2] << 8) | value[startIndex + 3];
        }

        private static void TZif_ParseRaw(Byte[] data, out TZifHead t, out DateTime[] dts, out Byte[] typeOfLocalTime, out TZifType[] transitionType,
                                          out String zoneAbbreviations, out Boolean[] StandardTime, out Boolean[] GmtTime)
        {
            // initialize the out parameters in case the TZifHead ctor throws
            dts = null;
            typeOfLocalTime = null;
            transitionType = null;
            zoneAbbreviations = String.Empty;
            StandardTime = null;
            GmtTime = null;

            // read in the 44-byte TZ header containing the count/length fields
            //
            t = new TZifHead(data, 0);
            int index = TZifHead.Length;

            // initialize the containers for the rest of the TZ data
            dts = new DateTime[t.TimeCount];
            typeOfLocalTime = new Byte[t.TimeCount];
            transitionType = new TZifType[t.TypeCount];
            zoneAbbreviations = String.Empty;
            StandardTime = new Boolean[t.TypeCount];
            GmtTime = new Boolean[t.TypeCount];


            // read in the 4-byte UTC transition points and convert them to Windows
            //
            for (int i = 0; i < t.TimeCount; i++)
            {
                int unixTime = TZif_ToInt32(data, index);
                dts[i] = TZif_UnixTimeToWindowsTime(unixTime);
                index += 4;
            }

            // read in the Type Indices; there is a 1:1 mapping of UTC transition points to Type Indices
            // these indices directly map to the array index in the transitionType array below
            //
            for (int i = 0; i < t.TimeCount; i++)
            {
                typeOfLocalTime[i] = data[index];
                index += 1;
            }

            // read in the Type table.  Each 6-byte entry represents
            // {UtcOffset, IsDst, AbbreviationIndex}
            // 
            // each AbbreviationIndex is a character index into the zoneAbbreviations string below
            //
            for (int i = 0; i < t.TypeCount; i++)
            {
                transitionType[i] = new TZifType(data, index);
                index += 6;
            }

            // read in the Abbreviation ASCII string.  This string will be in the form:
            // "PST\0PDT\0PWT\0\PPT"
            //
            System.Text.Encoding enc = new System.Text.UTF8Encoding();
            zoneAbbreviations = enc.GetString(data, index, (int)t.CharCount);
            index += (int)t.CharCount;

            // skip ahead of the Leap-Seconds Adjustment data.  In a future release, consider adding
            // support for Leap-Seconds
            //
            index += (int)(t.LeapCount * 8); // skip the leap second transition times

            // read in the Standard Time table.  There should be a 1:1 mapping between Type-Index and Standard
            // Time table entries.
            //
            // TRUE     =     transition time is standard time
            // FALSE    =     transition time is wall clock time
            // ABSENT   =     transition time is wall clock time
            //
            for (int i = 0; i < t.IsStdCount && i < t.TypeCount && index < data.Length; i++)
            {
                StandardTime[i] = (data[index++] != 0);
            }

            // read in the GMT Time table.  There should be a 1:1 mapping between Type-Index and GMT Time table
            // entries.
            //
            // TRUE     =     transition time is UTC
            // FALSE    =     transition time is local time
            // ABSENT   =     transition time is local time
            //
            for (int i = 0; i < t.IsGmtCount && i < t.TypeCount && index < data.Length; i++)
            {
                GmtTime[i] = (data[index++] != 0);
            }
        }


        // Windows NT time is specified as the number of 100 nanosecond intervals since January 1, 1601.
        // UNIX time is specified as the number of seconds since January 1, 1970. There are 134,774 days
        // (or 11,644,473,600 seconds) between these dates.
        //
        private static DateTime TZif_UnixTimeToWindowsTime(int unixTime)
        {
            // Add 11,644,473,600 and multiply by 10,000,000.
            Int64 ntTime = (((Int64)unixTime) + 11644473600) * 10000000;
            return DateTime.FromFileTimeUtc(ntTime);
        }

        private struct TZifType
        {
            private const int c_len = 6;
            public static int Length
            {
                get
                {
                    return c_len;
                }
            }

            public TimeSpan UtcOffset;
            public Boolean IsDst;
            public Byte AbbreviationIndex;

            public TZifType(Byte[] data, Int32 index)
            {
                if (data == null || data.Length < index + c_len)
                {
                    throw new ArgumentException(SR.Argument_TimeZoneInfoInvalidTZif, nameof(data));
                }
                UtcOffset = new TimeSpan(0, 0, TZif_ToInt32(data, index + 00));
                IsDst = (data[index + 4] != 0);
                AbbreviationIndex = data[index + 5];
            }
        }

        private struct TZifHead
        {
            private const int c_len = 44;
            public static int Length
            {
                get
                {
                    return c_len;
                }
            }

            public TZifHead(Byte[] data, Int32 index)
            {
                if (data == null || data.Length < c_len)
                {
                    throw new ArgumentException("bad data", nameof(data));
                }

                Magic = (uint)TZif_ToInt32(data, index + 00);

                if (Magic != 0x545A6966)
                {
                    // 0x545A6966 = {0x54, 0x5A, 0x69, 0x66} = "TZif"
                    throw new ArgumentException(SR.Argument_TimeZoneInfoBadTZif, nameof(data));
                }

                // don't use the BitConverter class which parses data
                // based on the Endianess of the machine architecture.
                // this data is expected to always be in "standard byte order",
                // regardless of the machine it is being processed on.

                IsGmtCount = (uint)TZif_ToInt32(data, index + 20);
                // skip the 16 byte reserved field
                IsStdCount = (uint)TZif_ToInt32(data, index + 24);
                LeapCount = (uint)TZif_ToInt32(data, index + 28);
                TimeCount = (uint)TZif_ToInt32(data, index + 32);
                TypeCount = (uint)TZif_ToInt32(data, index + 36);
                CharCount = (uint)TZif_ToInt32(data, index + 40);
            }

            public UInt32 Magic;       // TZ_MAGIC "TZif"
            // public  Byte[16] Reserved;    // reserved for future use
            public UInt32 IsGmtCount;  // number of transition time flags
            public UInt32 IsStdCount;  // number of transition time flags
            public UInt32 LeapCount;   // number of leap seconds
            public UInt32 TimeCount;   // number of transition times
            public UInt32 TypeCount;   // number of local time types
            public UInt32 CharCount;   // number of abbreviated characters
        }
    }
}
