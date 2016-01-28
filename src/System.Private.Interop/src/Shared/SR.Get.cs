// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    internal static partial class SR
    {
        internal static string GetResourceString(string resourceKey, string defaultString)
        {
            return defaultString;
        }

        internal static string Format(string resourceId, params object[] par)
        {
            String reportStr = String.Empty;
            foreach (object item in par)
            {
                reportStr = String.Concat(reportStr, " | ", item.ToString());
            }
            reportStr = String.Concat(resourceId, reportStr);
            return reportStr;
        }
    }
}
