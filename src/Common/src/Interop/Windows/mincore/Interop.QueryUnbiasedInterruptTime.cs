// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
