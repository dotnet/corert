// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;

using Internal.StackTraceGenerator;

namespace Internal.DeveloperExperience
{
    internal sealed class DeveloperExperienceConsole : DeveloperExperience
    {
        public sealed override void WriteLine(String s)
        {
            Console.Error.WriteLine(s);
        }

        public sealed override String CreateStackTraceString(IntPtr ip, bool includeFileInfo)
        {
            String s = Internal.StackTraceGenerator.StackTraceGenerator.CreateStackTraceString(ip, includeFileInfo);
            if (s != null)
                return s;
            return base.CreateStackTraceString(ip, includeFileInfo);
        }

        public sealed override void TryGetSourceLineInfo(IntPtr ip, out string fileName, out int lineNumber, out int columnNumber)
        {
            Internal.StackTraceGenerator.StackTraceGenerator.TryGetSourceLineInfo(ip, out fileName, out lineNumber, out columnNumber);
            // we take whatever data StackTraceGenerator can get (none/partial/all). No reason to fall-back because the base-type
            // never finds anything.
        }

        public sealed override void TryGetILOffsetWithinMethod(IntPtr ip, out int ilOffset)
        {
            Internal.StackTraceGenerator.StackTraceGenerator.TryGetILOffsetWithinMethod(ip, out ilOffset);
        }
    }
}

