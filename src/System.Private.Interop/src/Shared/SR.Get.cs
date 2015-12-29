// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
