// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

internal static partial class Interop
{
    internal static unsafe partial class mincore
    {
        internal const int MUI_LANGUAGE_ID = 0x4;
        internal const int MUI_LANGUAGE_NAME = 0x8;
        internal const int MUI_PREFERRED_UI_LANGUAGES = 0x10;
        internal const int MUI_INSTALLED_LANGUAGES = 0x20;
        internal const int MUI_ALL_LANGUAGES = 0x40;
        internal const int MUI_LANG_NEUTRAL_PE_FILE = 0x100;
        internal const int MUI_NON_LANG_NEUTRAL_FILE = 0x200;

        [DllImport("api-ms-win-core-localization-l1-2-1.dll", CharSet = CharSet.Unicode)]
        internal static extern bool GetFileMUIPath(int flags, String filePath, StringBuilder language, ref int languageLength, StringBuilder fileMuiPath, ref int fileMuiPathLength, ref Int64 enumerator);   
    }
}