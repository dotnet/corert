// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using Internal.DeveloperExperience;
using System.Runtime;

namespace System
{
    public static partial class Environment
    {
        public static int CurrentManagedThreadId => ManagedThreadId.Current;

        private static int s_latchedExitCode;

        public static int ExitCode
        {
            get => s_latchedExitCode;
            set => s_latchedExitCode = value;
        }

        public static void Exit(int exitCode)
        {
            s_latchedExitCode = exitCode;
            ShutdownCore();
            RuntimeImports.RhpShutdown();
            ExitRaw();
        }

        // Note: The CLR's Watson bucketization code looks at the caller of the FCALL method
        // to assign blame for crashes.  Don't mess with this, such as by making it call 
        // another managed helper method, unless you consult with some CLR Watson experts.
        public static void FailFast(string message) =>
            RuntimeExceptionHelpers.FailFast(message);

        public static void FailFast(string message, Exception exception) =>
            RuntimeExceptionHelpers.FailFast(message, exception);

        internal static void FailFast(string message, Exception exception, string errorSource)
        {
            // TODO: errorSource originates from CoreCLR (See: https://github.com/dotnet/coreclr/pull/15895)
            // For now, we ignore errorSource on CoreRT but we should distinguish the way FailFast prints exception message using errorSource
            bool result = DeveloperExperience.Default.OnContractFailure(exception.StackTrace, ContractFailureKind.Assert, message, null, null, null);
            if (!result)
            {
                RuntimeExceptionHelpers.FailFast(message, exception);
            }
        }
        
        private static int GetProcessorCount() => Runtime.RuntimeImports.RhGetProcessCpuCount();

        internal static void ShutdownCore()
        {
            // TODO: shut down threading etc.

#if !TARGET_WASM // WASMTODO Be careful what happens here as if the code has called emscripten_set_main_loop then the main loop method will normally be called repeatedly after this method
            AppContext.OnProcessExit();
#endif
        }

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

                return Internal.Diagnostics.StackTraceHelper.FormatStackTrace(frameIPs, 0, true);
            }
        }

        public static int TickCount => (int)TickCount64;

        public static string[] GetCommandLineArgs() => (string[])s_commandLineArgs.Clone();
    }
}
