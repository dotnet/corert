// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

using Internal.Runtime.Augments;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    partial class StartupCodeHelpers
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

        internal static unsafe void InitializeCommandLineArgs(int argc, byte** argv)
        {
            string[] args = new string[argc];
            for (int i = 0; i < argc; ++i)
            {
                byte* argval = argv[i];
                int len = CStrLen(argval);
                args[i] = Encoding.UTF8.GetString(argval, len);
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
    }
}
