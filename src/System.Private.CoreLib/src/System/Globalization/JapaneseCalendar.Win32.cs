// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Globalization
{
    public partial class JapaneseCalendar : Calendar
    {
        public static int GetJapaneseEraCount()
        {
            return WinRTInterop.Callbacks.GetJapaneseEraCount();
        }

        public static bool GetJapaneseEraInfo(int era, out DateTimeOffset dateOffset, out string eraName, out string abbreviatedEraName)
        {
            return WinRTInterop.Callbacks.GetJapaneseEraInfo(era, out dateOffset, out eraName, out abbreviatedEraName);
        }
    }
}
