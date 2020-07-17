// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Internal.StackTraceGenerator
{
    public static class StackTraceGenerator
    {
        //
        // Makes reasonable effort to construct one useful line of a stack trace. Returns null if it can't.
        //
        public static String CreateStackTraceString(IntPtr ip, bool includeFileInfo)
        {
            // CORERT-TODO: Implement StackTraceGenerator on Unix
            return null;
        }

        //
        // Makes reasonable effort to get source info. Returns null sourceFile and 0 lineNumber/columnNumber if it can't.
        //
        public static void TryGetSourceLineInfo(IntPtr ip, out string fileName, out int lineNumber, out int columnNumber)
        {
            // CORERT-TODO: Implement StackTraceGenerator on Unix
            fileName = null;
            lineNumber = 0;
            columnNumber = 0;
        }

        /// <summary>
        /// Makes reasonable effort to locate the IL offset within the current method.
        /// </summary>
        public static void TryGetILOffsetWithinMethod(IntPtr ip, out int ilOffset)
        {
            // CORERT-TODO: Implement StackTraceGenerator on Unix
            ilOffset = StackFrame.OFFSET_UNKNOWN;
        }
    }
}
