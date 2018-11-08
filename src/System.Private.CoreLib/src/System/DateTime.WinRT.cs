// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    public readonly partial struct DateTime
    {
        private static unsafe bool SystemSupportsLeapSeconds()
        {
            long l = 0;

            return Interop.Kernel32.GetProcessInformation(
                Interop.mincore.GetCurrentProcess(),
                Interop.Kernel32.ProcessLeapSecondInfo,
                ref l,
                sizeof(long));
        }
    }
}
