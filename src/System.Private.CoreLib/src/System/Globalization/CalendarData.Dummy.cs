// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
