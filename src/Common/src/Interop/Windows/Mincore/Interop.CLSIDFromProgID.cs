// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class mincore
    {
        [DllImport("api-ms-win-core-com-l1-1-0.dll", CharSet = CharSet.Unicode)]
        internal extern static int CLSIDFromProgID(string lpszProgID, out Guid clsid);
    }
}
