// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class mincore
    {
        internal struct TP_CALLBACK_ENVIRON
        {
            private uint Version;
            private IntPtr Pool;
            private IntPtr CleanupGroup;
            private IntPtr CleanupGroupCancelCallback;
            private IntPtr RaceDll;
            private IntPtr ActivationContext;
            private IntPtr FinalizationCallback;
            private uint Flags;

            internal void Initialize()
            {
                Version = 1;
            }

            internal void SetLongFunction()
            {
                Flags |= 1;
            }
        }

        [DllImport("api-ms-win-core-threadpool-l1-2-0.dll")]
        internal extern static IntPtr CreateThreadpoolWork(IntPtr pfnwk, IntPtr pv, IntPtr pcbe);

        [DllImport("api-ms-win-core-threadpool-l1-2-0.dll")]
        internal extern static IntPtr CloseThreadpoolWork(IntPtr pfnwk);

        [DllImport("api-ms-win-core-threadpool-l1-2-0.dll")]
        internal extern static IntPtr SubmitThreadpoolWork(IntPtr pwk);

        [DllImport("api-ms-win-core-threadpool-l1-2-0.dll")]
        internal extern static unsafe bool TrySubmitThreadpoolCallback(IntPtr pns, IntPtr pv, TP_CALLBACK_ENVIRON * pcbe);
    }
}
