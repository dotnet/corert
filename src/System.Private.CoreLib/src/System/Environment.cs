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
    // Environment is marked as Eager to allow Lock to read the current
    // thread ID, since Lock is used in ClassConstructorRunner.Cctor.GetCctor
    [EagerOrderedStaticConstructor(EagerStaticConstructorOrder.SystemEnvironment)]
    public static partial class Environment
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
