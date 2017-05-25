// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace System
{
    internal static partial class Environment
    {
        internal static int CurrentNativeThreadId => ManagedThreadId.Current;

        internal static long TickCount64
        {
            get
            {
                return (long)Interop.Sys.GetTickCount64();
            }
        }

        public static int ProcessorCount => (int)Interop.Sys.SysConf(Interop.Sys.SysConfName._SC_NPROCESSORS_ONLN);

        private static int ComputeExecutionId()
        {
            int executionId = Interop.Sys.SchedGetCpu();

            // sched_getcpu doesn't exist on all platforms. On those it doesn't exist on, the shim
            // returns -1.  As a fallback in that case and to spread the threads across the buckets
            // by default, we use the current managed thread ID as a proxy.
            if (executionId < 0) executionId = Environment.CurrentManagedThreadId;

            return executionId;
        }
    }
}
