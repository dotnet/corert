// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        public static void Exit(int exitCode)
        {
            s_latchedExitCode = exitCode;

            ShutdownCore();

            RuntimeImports.RhpShutdown();

            ExitRaw();
        }

        private static string[] s_commandLineArgs;

        internal static void SetCommandLineArgs(string[] args)
        {
            s_commandLineArgs = args;
        }

        public static string[] GetCommandLineArgs()
        {
            return (string[])s_commandLineArgs.Clone();
        }
    }
}
