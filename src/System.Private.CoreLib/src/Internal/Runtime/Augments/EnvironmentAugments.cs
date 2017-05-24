// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        public static int CurrentManagedThreadId => System.Threading.ManagedThreadId.Current;
        public static void FailFast(string message, Exception error) => RuntimeExceptionHelpers.FailFast(message, error);

        public static void Exit(int exitCode)
        {
#if CORERT
            s_latchedExitCode = exitCode;

            ShutdownCore();

            RuntimeImports.RhpShutdown();

            Interop.ExitProcess(s_latchedExitCode);
#else
            // This needs to be implemented for ProjectN.
            throw new PlatformNotSupportedException();
#endif
        }

        internal static void ShutdownCore()
        {
            // Here we'll handle AppDomain.ProcessExit, shut down threading etc.
        }

        private static int s_latchedExitCode;
        public static int ExitCode
        {
            get
            {
                return s_latchedExitCode;
            }
            set
            {
                s_latchedExitCode = value;
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
