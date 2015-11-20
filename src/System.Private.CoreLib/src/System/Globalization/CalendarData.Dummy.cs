// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;
using System.Collections.Generic;

namespace System.Globalization
{
    internal partial class CalendarData
    {
        private bool LoadCalendarDataFromSystem(String localeName, CalendarId calendarId)
        {
            return true;
        }

        internal static CalendarData GetCalendarData(CalendarId calendarId)
        {
            return CultureInfo.InvariantCulture.m_cultureData.GetCalendar(calendarId);
        }

        internal static int GetCalendars(String localeName, bool useUserOverride, CalendarId[] calendars)
        {
            return 1;
        }

        private static bool SystemSupportsTaiwaneseCalendar()
        {
            return false;
        }

        internal static int GetTwoDigitYearMax(CalendarId calendarId)
        {
            return 2029;
        }
    }
}
