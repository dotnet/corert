// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace System
{
    public static partial class Environment
    {
        internal static int CurrentNativeThreadId => ManagedThreadId.Current;

        internal static long TickCount64
        {
            get
            {
                return (long)Interop.Sys.GetTickCount64();
            }
        }

        public static unsafe String ExpandEnvironmentVariables(String name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
            {
                return name;
            }

            int currentSize = 100;
            StringBuilder blob = new StringBuilder(currentSize); // A somewhat reasonable default size

            int lastPos = 0, pos;
            while (lastPos < name.Length && (pos = name.IndexOf('%', lastPos + 1)) >= 0)
            {
                if (name[lastPos] == '%')
                {
                    string key = name.Substring(lastPos + 1, pos - lastPos - 1);
                    string value = Environment.GetEnvironmentVariable(key);
                    if (value != null)
                    {
                        blob.Append(value);
                        lastPos = pos + 1;
                        continue;
                    }
                }
                blob.Append(name.Substring(lastPos, pos - lastPos));
                lastPos = pos;
            }
            blob.Append(name.Substring(lastPos));

            return blob.ToString();
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

        public static unsafe String GetEnvironmentVariable(String variable)
        {
            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            return Marshal.PtrToStringAnsi(Interop.Sys.GetEnv(variable));
        }
    }
}
