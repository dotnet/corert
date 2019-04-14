// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System
{
    public static partial class Environment
    {
        internal static int CurrentNativeThreadId => unchecked((int)Interop.Kernel32.GetCurrentThreadId());

        internal static long TickCount64 => (long)Interop.mincore.GetTickCount64();
    }
}
