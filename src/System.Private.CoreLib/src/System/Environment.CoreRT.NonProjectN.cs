// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;

namespace System
{
    public static partial class Environment
    {
        public static void Exit(int exitCode)
        {
            s_latchedExitCode = exitCode;
            ShutdownCore();
            RuntimeImports.RhpShutdown();
            ExitRaw();
        }

        public static string[] GetCommandLineArgs() => (string[])s_commandLineArgs.Clone();
    }
}
