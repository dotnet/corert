// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Runtime.Augments;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    public partial class StartupCodeHelpers
    {
        internal static unsafe void InitializeCommandLineArgsW(int argc, char** argv)
        {
            string[] args = new string[argc];
            for (int i = 0; i < argc; ++i)
            {
                args[i] = new string(argv[i]);
            }
            EnvironmentAugments.SetCommandLineArgs(args);
        }

        internal static unsafe void InitializeCommandLineArgs(int argc, sbyte** argv)
        {
            string[] args = new string[argc];
            for (int i = 0; i < argc; ++i)
            {
                args[i] = new string(argv[i]);
            }
            EnvironmentAugments.SetCommandLineArgs(args);
        }

        private static string[] GetMainMethodArguments()
        {
            // GetCommandLineArgs includes the executable name, Main() arguments do not.
            string[] args = EnvironmentAugments.GetCommandLineArgs();

            Debug.Assert(args.Length > 0);

            string[] mainArgs = new string[args.Length - 1];
            Array.Copy(args, 1, mainArgs, 0, mainArgs.Length);

            return mainArgs;
        }

        private static void SetLatchedExitCode(int exitCode)
        {
            EnvironmentAugments.ExitCode = exitCode;
        }

        // Shuts down the class library and returns the process exit code.
        private static int Shutdown()
        {
            EnvironmentAugments.ShutdownCore();

            return EnvironmentAugments.ExitCode;
        }
    }
}
