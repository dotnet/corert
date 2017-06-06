// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

internal partial class Interop
{
    internal partial class Advapi32
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, BestFitMapping = false, EntryPoint = "RegSetValueExW")]
        internal static extern int RegSetValueEx(SafeRegistryHandle hKey, string lpValueName, int Reserved, RegistryValueKind dwType, string lpData, int cbData);
    }
}
