// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using WinRTInterop = Internal.Runtime.Augments.WinRTInterop;

namespace System.Threading.Tasks
{
    internal enum CausalitySource
    {
        Application = 0,
        Library = 1,
        System = 2,
    }

    internal enum CausalityTraceLevel
    {
        Required = 0,
        Important = 1,
        Verbose = 2,
    }

    //
    // WinRT-specific implementation of AsyncCausality events
    //
    internal static class AsyncCausalityTracer
    {
        public static void EnableToETW(bool enabled)
        {
        }

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
        public static void TraceOperationCreation(Task task, string operationName)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationCreation((int)CausalityTraceLevel.Required, (int)CausalitySource.Library, s_platformId, task.OperationId(), operationName, relatedContext:  0);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceOperationCompletion(Task task, AsyncCausalityStatus status)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationCompletion((int)CausalityTraceLevel.Required, (int)CausalitySource.Library, s_platformId, task.OperationId(), (int)status);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceOperationRelation(Task task, CausalityRelation relation)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationRelation((int)CausalityTraceLevel.Important, (int)CausalitySource.Library, s_platformId, task.OperationId(), (int)relation);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceSynchronousWorkStart(Task task, CausalitySynchronousWork work)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceSynchronousWorkStart((int)CausalityTraceLevel.Required, (int)CausalitySource.Library, s_platformId, task.OperationId(), (int)work);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceSynchronousWorkCompletion(CausalitySynchronousWork work)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceSynchronousWorkCompletion((int)CausalityTraceLevel.Required, (int)CausalitySource.Library, (int)work);
            }
        }

        private static ulong OperationId(this Task task)
        {
            // The contract with the debugger is that the operation ID contains the appdomain ID in the upper 32 bits and Task.Id in the lower 32 bits.
            // Project N does not have "appdomains" so we'll put a fixed "id" of 0x00000001 in the upper 32 bits.
            return 0x0000000100000000LU | (ulong)(uint)(task.Id);
        }

        static AsyncCausalityTracer()
        {
            WinRTInterop.Callbacks.InitTracingStatusChanged(loggingOn => s_loggingOn = loggingOn);
        }

        private static bool s_loggingOn /*= false*/;

        // {4B0171A6-F3D0-41A0-9B33-02550652B995} - Guid that marks our causality events as "Coming from the BCL."
        private static readonly Guid s_platformId = new Guid(0x4B0171A6, unchecked((short)0xF3D0), 0x41A0, 0x9B, 0x33, 0x02, 0x55, 0x06, 0x52, 0xB9, 0x95);
    }
}
