// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;

namespace System
{
    internal static partial class Environment
    {
        internal static int CurrentNativeThreadId => unchecked((int)Interop.mincore.GetCurrentThreadId());

        internal static long TickCount64 => (long)Interop.mincore.GetTickCount64();

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

        private static int ComputeExecutionId() => (int)Interop.mincore.GetCurrentProcessorNumber();

        public static string ExpandEnvironmentVariables(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            int currentSize = 128;
            for (;;)
            {
                char[] buffer = ArrayPool<char>.Shared.Rent(currentSize);

                int actualSize = Interop.Kernel32.ExpandEnvironmentStrings(name, buffer, buffer.Length);
                if (actualSize <= buffer.Length)
                {
                    string result = (actualSize != 0) ? new string(buffer, 0, actualSize) : null;
                    ArrayPool<char>.Shared.Return(buffer);
                    return result;
                }

                ArrayPool<char>.Shared.Return(buffer);
                currentSize = actualSize;
            }
        }
    }
}
