// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Internal.DeveloperExperience;

namespace Internal.Diagnostics
{
    /* Right now lists of IPs represent the data model for stack traces. You might wonder why we don't encapsulate this with an OO API such as StackTrace/StackFrame.
       Sadly the public definition StackFrame on desktop/contract includes a reference to MethodBase, which can't be referenced in corefx.dll
       If we wanted to, we could create a LowLevelStackTrace type which could be encapsulated or extended by the public contract
     */

    public static class StackTraceHelper
    {
        public static string FormatStackTrace(IntPtr[] ips, bool includeFileInfo)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ips.Length; i++)
            {
                if (i != 0)
                    sb.AppendLine();

                IntPtr ip = ips[i];
                if (ip == SpecialIP.EdiSeparator)
                {
                    sb.Append("--- End of stack trace from previous location where exception was thrown ---");
                }
                else
                {
                    sb.Append("   at ");
                    sb.Append(FormatStackFrame(ip, includeFileInfo));
                }
            }

            return sb.ToString();
        }

        public static string FormatStackFrame(IntPtr ip, bool includeFileInfo)
        {
            return Internal.DeveloperExperience.DeveloperExperience.Default.CreateStackTraceString(ip, includeFileInfo);
        }

        public static void TryGetSourceLineInfo(IntPtr ip, out string fileName, out int lineNumber, out int columnNumber)
        {
            Internal.DeveloperExperience.DeveloperExperience.Default.TryGetSourceLineInfo(ip, out fileName, out lineNumber, out columnNumber);
        }

        public static class SpecialIP
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible")]            
            public static IntPtr EdiSeparator = (IntPtr)1;  // Marks a boundary where an ExceptionDispatchInfo rethrew an exception.
        }
    }
}
