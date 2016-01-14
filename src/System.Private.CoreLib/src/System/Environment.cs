// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using Internal.DeveloperExperience;

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

        internal static long TickCount64
        {
            get
            {
                return (long)Interop.mincore.GetTickCount64();
            }
        }

        public static string MachineName
        {
            get
            {
                const string ComputerName = "COMPUTERNAME";
                return Environment.GetEnvironmentVariable(ComputerName);
            }
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
                return RuntimeImports.RhHasShutdownStarted();
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

        static Environment()
        {
            RuntimeImports.RhEnableShutdownFinalization(0xffffffffu);
        }

        public static String StackTrace
        {
            get
            {
                // RhGetCurrentThreadStackTrace returns the number of frames(cFrames) added to input buffer.
                // It returns a negative value, -cFrames which is the required array size, if the buffer is too small.
                // Initial array length is deliberately chosen to be 0 so that we reallocate to exactly the right size
                // for StackFrameHelper.FormatStackTrace call. If we want to do this optimistically with one call change
                // FormatStackTrace to accept an explicit length.
                IntPtr[] frameIPs = new IntPtr[0];
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

                return Internal.Diagnostics.StackTraceHelper.FormatStackTrace(frameIPs, true);
            }
        }
    }
}
