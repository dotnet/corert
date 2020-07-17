// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Text;

namespace Internal.Diagnostics
{
    public static class StackTraceHelper
    {
        public static string FormatStackTrace(IntPtr[] ips, bool includeFileInfo)
        {
            return FormatStackTrace(ips, 0, includeFileInfo);
        }

        public static string FormatStackTrace(IntPtr[] ips, int skipFrames, bool includeFileInfo)
        {
            return new StackTrace(ips, skipFrames, ips.Length, includeFileInfo).ToString(StackTrace.TraceFormat.Normal);
        }

        public static class SpecialIP
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible")]
            public static IntPtr EdiSeparator = (IntPtr)1;  // Marks a boundary where an ExceptionDispatchInfo rethrew an exception.
        }
    }
}
