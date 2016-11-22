// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static class EnvironmentAugments
    {
        public static int CurrentManagedThreadId => System.Threading.ManagedThreadId.Current;
        public static void FailFast(string message, Exception error) => RuntimeExceptionHelpers.FailFast(message, error);

        public static void Exit(int exitCode) => Environment.Exit(exitCode);

        private static int s_latchedExitCode;
        public static int ExitCode
        {
            get
            {
                return s_latchedExitCode;
            }
            set
            {
#if CORERT
                s_latchedExitCode = value;
#else
                // This needs to be hooked up into the compiler to do anything. Project N is not hooked up.
                throw new PlatformNotSupportedException();
#endif
            }
        }

        private static string[] s_commandLineArgs;

        internal static void SetCommandLineArgs(string[] args)
        {
            s_commandLineArgs = args;
        }

        public static string[] GetCommandLineArgs()
        {
            return (string[])s_commandLineArgs?.Clone();
        }

        public static bool HasShutdownStarted => false; // .NET Core does not have shutdown finalization

        public static string StackTrace
        {
            // Disable inlining to have predictable stack frame to skip
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                // RhGetCurrentThreadStackTrace returns the number of frames(cFrames) added to input buffer.
                // It returns a negative value, -cFrames which is the required array size, if the buffer is too small.
                // Initial array length is deliberately chosen to be 0 so that we reallocate to exactly the right size
                // for StackFrameHelper.FormatStackTrace call. If we want to do this optimistically with one call change
                // FormatStackTrace to accept an explicit length.
                IntPtr[] frameIPs = Array.Empty<IntPtr>();
                int cFrames = RuntimeImports.RhGetCurrentThreadStackTrace(frameIPs);
                if (cFrames < 0)
                {
                    frameIPs = new IntPtr[-cFrames];
                    cFrames = RuntimeImports.RhGetCurrentThreadStackTrace(frameIPs);
                    if (cFrames < 0)
                    {
                        return "";
                    }
                }

                return Internal.Diagnostics.StackTraceHelper.FormatStackTrace(frameIPs, 1, true);
            }
        }

        public static int TickCount => Environment.TickCount;
    }
}
