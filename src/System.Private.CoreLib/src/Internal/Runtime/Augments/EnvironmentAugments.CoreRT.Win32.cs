// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;

namespace Internal.Runtime.Augments
{
    public static partial class EnvironmentAugments
    {
        private static void ExitRaw()
        {
            Interop.Kernel32.ExitProcess(s_latchedExitCode);
        }
    }
}
