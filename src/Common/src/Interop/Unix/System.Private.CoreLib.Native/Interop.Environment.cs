// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_GetEnvironmentVariable")]
        internal static unsafe extern int GetEnvironmentVariable(string name, out IntPtr result);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_ExitProcess")]
        internal static extern void ExitProcess(int exitCode);

        [DllImport(Interop.Libraries.CoreLibNative, EntryPoint = "CoreLibNative_GetHostName", SetLastError = true)]
        private unsafe static extern int GetHostName(byte* name, int nameLength);

        internal static unsafe string GetHostName()
        {
            const int HOST_NAME_MAX = 255;
            const int ArrLength = HOST_NAME_MAX + 1;

            byte* name = stackalloc byte[ArrLength];
            int err = GetHostName(name, ArrLength);
            if (err != 0)
            {
                // This should never happen.  According to the man page,
                // the only possible errno for gethostname is ENAMETOOLONG,
                // which should only happen if the buffer we supply isn't big
                // enough, and we're using a buffer size that the man page
                // says is the max for POSIX (and larger than the max for Linux).
                Debug.Fail("gethostname failed");
                throw new InvalidOperationException(SR.Format(SR.InvalidOperation_GetHostName, err));
            }

            // If the hostname is truncated, it is unspecified whether the returned buffer includes a terminating null byte.
            int length = 0;
            while (length < ArrLength && name[length] != 0)
            {
                length++;
            }

            return Encoding.ASCII.GetString(name, length);
        }
    }
}
