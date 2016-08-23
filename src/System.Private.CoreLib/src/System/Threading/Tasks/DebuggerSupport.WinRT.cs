// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using WinRTInterop = Internal.Runtime.Augments.WinRTInterop;
using AsyncStatus = Internal.Runtime.Augments.AsyncStatus;
using CausalityRelation = Internal.Runtime.Augments.CausalityRelation;
using CausalitySource = Internal.Runtime.Augments.CausalitySource;
using CausalityTraceLevel = Internal.Runtime.Augments.CausalityTraceLevel;
using CausalitySynchronousWork = Internal.Runtime.Augments.CausalitySynchronousWork;

namespace System.Threading.Tasks
{
    //
    // WinRT-specific implementation of AsyncCausality events
    //
    internal static partial class DebuggerSupport
    {
        //==============================================================================================================
        // This section of the class encapsulates the call to AsyncCausalityTracer for the async-aware callstacks.
        //==============================================================================================================
        public static bool LoggingOn
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return s_loggingOn;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceOperationCreation(CausalityTraceLevel traceLevel, Task task, String operationName, ulong relatedContext)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationCreation(traceLevel, CausalitySource.Library, s_platformId, task.OperationId(), operationName, relatedContext);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceOperationCompletion(CausalityTraceLevel traceLevel, Task task, AsyncStatus status)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationCompletion(traceLevel, CausalitySource.Library, s_platformId, task.OperationId(), status);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceOperationRelation(CausalityTraceLevel traceLevel, Task task, CausalityRelation relation)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationRelation(traceLevel, CausalitySource.Library, s_platformId, task.OperationId(), relation);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceSynchronousWorkStart(CausalityTraceLevel traceLevel, Task task, CausalitySynchronousWork work)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceSynchronousWorkStart(traceLevel, CausalitySource.Library, s_platformId, task.OperationId(), work);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceSynchronousWorkCompletion(CausalityTraceLevel traceLevel, CausalitySynchronousWork work)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceSynchronousWorkCompletion(traceLevel, CausalitySource.Library, work);
            }
        }

        private static ulong OperationId(this Task task)
        {
            // The contract with the debugger is that the operation ID contains the appdomain ID in the upper 32 bits and Task.Id in the lower 32 bits.
            // Project N does not have "appdomains" so we'll put a fixed "id" of 0x00000001 in the upper 32 bits.
            return 0x0000000100000000LU | (ulong)(uint)(task.Id);
        }

        static DebuggerSupport()
        {
            WinRTInterop.Callbacks.InitTracingStatusChanged(loggingOn => s_loggingOn = loggingOn);
        }

        private static bool s_loggingOn /*= false*/;

        // {4B0171A6-F3D0-41A0-9B33-02550652B995} - Guid that marks our causality events as "Coming from the BCL."
        private static readonly Guid s_platformId = new Guid(0x4B0171A6, unchecked((short)0xF3D0), 0x41A0, 0x9B, 0x33, 0x02, 0x55, 0x06, 0x52, 0xB9, 0x95);
    }
}
