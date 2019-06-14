// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal partial class WinRT
    {
        private const string CORE_WINRT = "api-ms-win-core-winrt-l1-1-0.dll";

        internal const uint RO_INIT_SINGLETHREADED = 0;
        internal const uint RO_INIT_MULTITHREADED = 1;

        [DllImport(CORE_WINRT, ExactSpelling = true)]
        internal static extern int RoInitialize(uint initType);

        [DllImport(CORE_WINRT, ExactSpelling = true)]
        internal static extern int RoUninitialize();
    }
}
