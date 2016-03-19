// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Text;
using Internal.Runtime.Augments;

namespace System.Globalization
{

    internal partial class CultureData
    {
        private unsafe bool InitCultureData()
        {
            if (_sSpecificCulture == null)
            {
                _sSpecificCulture = "";
            }
            return true;
        }

        private string GetLocaleInfo(LocaleStringData type)
        {
            return "";
        }

        private string GetLocaleInfo(string localeName, LocaleStringData type)
        {
            return "";
        }

        private int GetLocaleInfo(LocaleNumberData type)
        {
            return 0;
        }

        private int[] GetLocaleInfo(LocaleGroupingData type)
        {
            return new int [] { 0 };
        }

        private static CultureInfo GetUserDefaultCulture()
        {
            return CultureInfo.InvariantCulture;
        }

        private static string GetLanguageDisplayName(string cultureName)
        {
            return "Invariant";
        }

        private static string GetRegionDisplayName(string isoCountryCode)
        {
            return "Invariant";
        }

        private static CultureData GetCultureDataFromRegionName(String regionName)
        {
            return CultureInfo.InvariantCulture.m_cultureData;
        }

        private string GetTimeFormatString()
        {
            return "h:mm tt";
        }

        private String[] GetShortTimeFormats()
        {
            return new string [] { "h:mm tt" };
        }

        private String[] GetTimeFormats()
        {
            return new string [] { "h:mm:ss tt", "h:mm tt" };
        }

        private static bool IsCustomCultureId(int cultureId)
        {
            return false;
        }

        private int GetFirstDayOfWeek()
        {
            return 0;
        }

    }
}
