// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal extern static unsafe int LCMapStringEx(
                    string lpLocaleName,
                    uint dwMapFlags,
                    char* lpSrcStr,
                    int cchSrc,
                    void* lpDestStr,
                    int cchDest,
                    void* lpVersionInformation,
                    void* lpReserved,
                    IntPtr sortHandle);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", EntryPoint = "FindNLSStringEx")]
        internal extern static unsafe int FindNLSStringEx(
                    char* lpLocaleName,
                    uint dwFindNLSStringFlags,
                    char* lpStringSource,
                    int cchSource,
                    char* lpStringValue,
                    int cchValue,
                    int* pcchFound,
                    void* lpVersionInformation,
                    void* lpReserved,
                    IntPtr sortHandle);

        [DllImport("api-ms-win-core-string-l1-1-0.dll", EntryPoint = "CompareStringEx")]
        internal extern static unsafe int CompareStringEx(
                    char* lpLocaleName,
                    uint dwCmpFlags,
                    char* lpString1,
                    int cchCount1,
                    char* lpString2,
                    int cchCount2,
                    void* lpVersionInformation,
                    void* lpReserved,
                    IntPtr lParam);

        [DllImport("api-ms-win-core-string-l1-1-0.dll", EntryPoint = "CompareStringOrdinal")]
        internal extern static unsafe int CompareStringOrdinal(
                    char* lpString1,
                    int cchCount1,
                    char* lpString2,
                    int cchCount2,
                    bool bIgnoreCase);

        [DllImport("api-ms-win-core-libraryloader-l1-1-0.dll", EntryPoint = "FindStringOrdinal")]
        internal extern static unsafe int FindStringOrdinal(
                    uint dwFindStringOrdinalFlags,
                    char* lpStringSource,
                    int cchSource,
                    char* lpStringValue,
                    int cchValue,
                    int bIgnoreCase);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetLocaleInfoEx(string lpLocaleName, uint LCType, IntPtr lpLCData, int cchData);

        [DllImport("api-ms-win-core-localization-l1-2-1.dll")]
        internal extern static bool EnumSystemLocalesEx(IntPtr lpLocaleEnumProcEx, uint dwFlags, IntPtr lParam, IntPtr lpReserved);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int ResolveLocaleName(string lpNameToResolve, char* lpLocaleName, int cchLocaleName);

        // Wrappers around the GetLocaleInfoEx APIs which handle marshalling the returned
        // data as either and Int or String.
        internal static unsafe String GetLocaleInfoEx(String localeName, uint field)
        {
            // REVIEW: Determine the maximum size for the buffer
            const int BUFFER_SIZE = 530;

            char* pBuffer = stackalloc char[BUFFER_SIZE];
            int resultCode = Interop.mincore.GetLocaleInfoEx(localeName, field, pBuffer, BUFFER_SIZE);
            if (resultCode > 0)
            {
                return new String(pBuffer);
            }

            return "";
        }

        internal static unsafe int GetLocaleInfoExInt(String localeName, uint field)
        {
            const uint LOCALE_RETURN_NUMBER = 0x20000000;
            const int BUFFER_SIZE = 2; // sizeof(int) / sizeof(char)

            field |= LOCALE_RETURN_NUMBER;

            char* pBuffer = stackalloc char[BUFFER_SIZE];
            Interop.mincore.GetLocaleInfoEx(localeName, field, pBuffer, BUFFER_SIZE);

            return *(int*)pBuffer;
        }

        internal static unsafe int GetLocaleInfoEx(string lpLocaleName, uint lcType, char* lpLCData, int cchData)
        {
            return GetLocaleInfoEx(lpLocaleName, lcType, (IntPtr)lpLCData, cchData);
        }

        [DllImport("api-ms-win-core-localization-l2-1-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int EnumTimeFormatsEx(IntPtr lpTimeFmtEnumProcEx, string lpLocaleName, uint dwFlags, IntPtr lParam);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int GetCalendarInfoEx(string lpLocaleName, uint Calendar, IntPtr lpReserved, uint CalType, IntPtr lpCalData, int cchData, out int lpValue);

        [DllImport("api-ms-win-core-localization-l1-2-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int GetCalendarInfoEx(string lpLocaleName, uint Calendar, IntPtr lpReserved, uint CalType, IntPtr lpCalData, int cchData, IntPtr lpValue);

        [DllImport("api-ms-win-core-localization-l2-1-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int EnumCalendarInfoExEx(IntPtr pCalInfoEnumProcExEx, string lpLocaleName, uint Calendar, string lpReserved, uint CalType, IntPtr lParam);
    }
}
