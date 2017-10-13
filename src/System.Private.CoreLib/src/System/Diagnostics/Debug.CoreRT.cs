// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.DeveloperExperience;
using System.Runtime.CompilerServices;
using System.Security;

namespace System.Diagnostics
{
    // .NET Native-specific Debug implementation
    public static partial class Debug
    {
        [DebuggerHidden]
        [Intrinsic]
        internal static void DebugBreak()
        {
            // IMPORTANT: This call will let us detect if debug break is broken, and also
            // gives double chances.
            DebugBreak();
        }
    }
}
