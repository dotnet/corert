// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Provides some basic access to some environment 
** functionality.
**
**
============================================================*/

using System.Runtime;
using System.Diagnostics;
using System.Globalization;
using System.Collections;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Runtime.CompilerServices;
using System.Threading;

using Internal.Runtime.Augments;

namespace System
{
    public enum EnvironmentVariableTarget
    {
        Process = 0,
        User = 1,
        Machine = 2,
    }

    internal static partial class Environment
    {
        /*==================================TickCount===================================
        **Action: Gets the number of ticks since the system was started.
        **Returns: The number of ticks since the system was started.
        **Arguments: None
        **Exceptions: None
        ==============================================================================*/
        public static int TickCount
        {
            get
            {
                return (int)TickCount64;
            }
        }

        public static string[] GetCommandLineArgs()
        {
            return EnvironmentAugments.GetCommandLineArgs();
        }

        //// Note: The CLR's Watson bucketization code looks at the caller of the FCALL method
        //// to assign blame for crashes.  Don't mess with this, such as by making it call 
        //// another managed helper method, unless you consult with some CLR Watson experts.


        public static void FailFast(String message)
        {
            RuntimeExceptionHelpers.FailFast(message);
        }

        public static void FailFast(String message, Exception exception)
        {
            RuntimeExceptionHelpers.FailFast(message, exception);
        }

        public static int CurrentManagedThreadId
        {
            get
            {
                return ManagedThreadId.Current;
            }
        }

        // The upper bits of t_executionIdCache are the executionId. The lower bits of
        // the t_executionIdCache are counting down to get it periodically refreshed.
        // TODO: Consider flushing the executionIdCache on Wait operations or similar 
        // actions that are likely to result in changing the executing core
        [ThreadStatic]
        static int t_executionIdCache;

        const int ExecutionIdCacheShift = 16;
        const int ExecutionIdCacheCountDownMask = (1 << ExecutionIdCacheShift) - 1;
        const int ExecutionIdRefreshRate = 5000;

        private static int RefreshExecutionId()
        {
            int executionId = ComputeExecutionId();

            Debug.Assert(ExecutionIdRefreshRate <= ExecutionIdCacheCountDownMask);

            // Mask with Int32.MaxValue to ensure the execution Id is not negative
            t_executionIdCache = ((executionId << ExecutionIdCacheShift) & Int32.MaxValue) + ExecutionIdRefreshRate;

            return executionId;
        }

        // Cached processor number used as a hint for which per-core stack to access. It is periodically
        // refreshed to trail the actual thread core affinity.
        internal static int CurrentExecutionId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int executionIdCache = t_executionIdCache--;
                if ((executionIdCache & ExecutionIdCacheCountDownMask) == 0)
                    return RefreshExecutionId();
                return (executionIdCache >> ExecutionIdCacheShift);
            }
        }

        public static bool HasShutdownStarted
        {
            get
            {
                // .NET Core does not have shutdown finalization
                return false;
            }
        }

        /*===================================NewLine====================================
        **Action: A property which returns the appropriate newline string for the given
        **        platform.
        **Returns: \r\n on Win32.
        **Arguments: None.
        **Exceptions: None.
        ==============================================================================*/
        public static String NewLine
        {
            get
            {
#if !PLATFORM_UNIX
                return "\r\n";
#else
                return "\n";
#endif // !PLATFORM_UNIX
            }
        }

        public static String StackTrace
        {
            // Disable inlining to have predictable stack frame that EnvironmentAugments can skip
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                return EnvironmentAugments.StackTrace;
            }
        }

        public static int ExitCode
        {
            get
            {
                return EnvironmentAugments.ExitCode;
            }
            set
            {
                EnvironmentAugments.ExitCode = value;
            }
        }

        public static void Exit(int exitCode) => EnvironmentAugments.Exit(exitCode);
    }
}
