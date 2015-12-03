// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Internal.StackTraceGenerator;

namespace Internal.DeveloperExperience
{
    internal sealed class DeveloperExperienceConsole : DeveloperExperience
    {
        public sealed override void WriteLine(String s)
        {
            ConsolePal.WriteError(s);
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
    }
}

