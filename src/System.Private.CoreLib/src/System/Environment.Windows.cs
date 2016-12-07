// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    public static partial class Environment
    {
        internal static long TickCount64
        {
            get
            {
                return (long)Interop.mincore.GetTickCount64();
            }
        }

        public static int ProcessorCount
        {
            get
            {
                // @TODO: can we finally fix this to return the actual number of processors when there are >64?
                Interop.mincore.SYSTEM_INFO info;
                Interop.mincore.GetNativeSystemInfo(out info);
                return (int)info.dwNumberOfProcessors;
            }
        }
    }
}
