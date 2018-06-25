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

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;

using Internal.Runtime.Augments;
using Internal.DeveloperExperience;

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

        //// Note: The CLR's Watson bucketization code looks at the caller of the FCALL method
        //// to assign blame for crashes.  Don't mess with this, such as by making it call 
        //// another managed helper method, unless you consult with some CLR Watson experts.


        public static void FailFast(string message)
        {
            RuntimeExceptionHelpers.FailFast(message);
        }

        public static void FailFast(string message, Exception exception)
        {
            RuntimeExceptionHelpers.FailFast(message, exception);
        }

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

        // Still needed by shared\System\Diagnostics\Debug.Unix.cs
        public static string GetEnvironmentVariable(string variable) => EnvironmentAugments.GetEnvironmentVariable(variable);

        public static int CurrentManagedThreadId
        {
            get
            {
                return ManagedThreadId.Current;
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
        public static string NewLine
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

        public static string StackTrace
        {
            // Disable inlining to have predictable stack frame that EnvironmentAugments can skip
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                return EnvironmentAugments.StackTrace;
            }
        }

        public static int ProcessorCount
        {
            get
            {
                return Runtime.RuntimeImports.RhGetProcessCpuCount();
            }
        }
    }
}
