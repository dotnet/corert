// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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
            return new int[] { 0 };
        }

        private static CultureInfo GetUserDefaultCulture()
        {
            return CultureInfo.InvariantCulture;
        }

        private string GetLanguageDisplayName(string cultureName)
        {
            return "Invariant";
        }

        private string GetRegionDisplayName(string isoCountryCode)
        {
            return "Invariant";
        }

        private static CultureData GetCultureDataFromRegionName(String regionName)
        {
            return CultureInfo.InvariantCulture._cultureData;
        }

        private string GetTimeFormatString()
        {
            return "h:mm tt";
        }

        private String[] GetShortTimeFormats()
        {
            return new string[] { "h:mm tt" };
        }

        private String[] GetTimeFormats()
        {
            return new string[] { "h:mm:ss tt", "h:mm tt" };
        }

        private int GetFirstDayOfWeek()
        {
            return 0;
        }

        private int LocaleNameToLCID(string cultureName)
        {
            throw new NotImplementedException();
        }

        private static string LCIDToLocaleName(int culture)
        {
            throw new NotImplementedException();
        }

        private int GetAnsiCodePage(string cultureName)
        {
            throw new NotImplementedException();
        }

        private int GetOemCodePage(string cultureName)
        {
            throw new NotImplementedException();
        }

        private int GetMacCodePage(string cultureName)
        {
            throw new NotImplementedException();
        }

        private int GetEbcdicCodePage(string cultureName)
        {
            throw new NotImplementedException();
        }

        private int GetGeoId(string cultureName)
        {
            throw new NotImplementedException();
        }

        private int GetDigitSubstitution(string cultureName)
        {
            throw new NotImplementedException();
        }

        private string GetThreeLetterWindowsLanguageName(string cultureName)
        {
            throw new NotImplementedException();
        }

        private static CultureInfo[] EnumCultures(CultureTypes types)
        {
            throw new NotImplementedException();
        }

        private string GetConsoleFallbackName(string cultureName)
        {
            throw new NotImplementedException();
        }

        internal bool IsFramework
        {
            get { throw new NotImplementedException(); }
        }

        internal bool IsWin32Installed
        {
            get { throw new NotImplementedException(); }
        }

        internal bool IsReplacementCulture
        {
            get { throw new NotImplementedException(); }
        }
    }
}
