// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class mincore
    {
        [DllImport(Interop.Libraries.RealTime, EntryPoint = "QueryUnbiasedInterruptTime")]
        private extern static int PInvoke_QueryUnbiasedInterruptTime(out ulong UnbiasedTime);

        internal static bool QueryUnbiasedInterruptTime(out ulong UnbiasedTime)
        {
            int result = PInvoke_QueryUnbiasedInterruptTime(out UnbiasedTime);
            return (result != 0);
        }
    }
}
