// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Runtime.CompilerServices;
using WinRTInterop = global::Internal.Runtime.Augments.WinRTInterop;
using AsyncStatus = global::Internal.Runtime.Augments.AsyncStatus;
using CausalityRelation = global::Internal.Runtime.Augments.CausalityRelation;
using CausalitySource = global::Internal.Runtime.Augments.CausalitySource;
using CausalityTraceLevel = global::Internal.Runtime.Augments.CausalityTraceLevel;
using CausalitySynchronousWork = global::Internal.Runtime.Augments.CausalitySynchronousWork;

namespace System.Threading.Tasks
{
    //
    // This class encapsulates the infrastructure to emit ETW AsyncCausality events and Task-ID tracking for the use of the debugger.
    // Unfortunately, this infrastructure generates a lot of interop code for AsyncCausalityTracer. Thus, this code is structured
    // so that an ILTransform can optimize away this class on ship-builds.
    //
    // To keep the transform from becoming too coupled to the code, this class must obey some basic rules:
    //
    //    All non-private methods must be static and have a return type of "void", "bool" or a non-value-type and no [out] parameters.
    //    They must be written as if the body was of the form:
    //
    //      #if NO_DEBUGGER_SUPPORT_DESIRED
    //          return default(RETURNTYPE);
    //      #else
    //          <your-code>
    //      #endif
    //
    // The transform will also remove DebuggerSupport's class constructor. Assuming the class is written correctly, the DR and NUTC-inlining will
    // get rid of the rest after that.
    //
    // The method behaviors must be defined as such that with this transform, the Task code will work as if no debugger was attached.
    //
    internal static class DebuggerSupport
    {
        //==============================================================================================================
        // This section of the class tracks adds the ability to retrieve an active Task from its ID. The debugger
        // is only component that needs this tracking so we only enable it when the debugger specifically requests it
        // by setting Task.s_asyncDebuggingEnabled.
        //==============================================================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddToActiveTasks(Task task)
        {
            if (Task.s_asyncDebuggingEnabled)
                AddToActiveTasksNonInlined(task);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AddToActiveTasksNonInlined(Task task)
        {
            int id = task.Id;
            lock (_activeTasksLock)
            {
                _activeTasks[id] = task;
            }
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromActiveTasks(Task task)
        {
            if (Task.s_asyncDebuggingEnabled)
                RemoveFromActiveTasksNonInlined(task);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RemoveFromActiveTasksNonInlined(Task task)
        {
            int id = task.Id;
            lock (_activeTasksLock)
            {
                _activeTasks.Remove(id);
            }
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task GetActiveTaskFromId(int taskId)
        {
            Task task = null;
            _activeTasks.TryGetValue(taskId, out task);
            return task;
        }

        private static readonly Dictionary<int, Task> _activeTasks = new Dictionary<int, Task>();
        private static readonly Object _activeTasksLock = new Object();

        //==============================================================================================================
        // This section of the class encapsulates the call to AsyncCausalityTracer for the async-aware callstacks.
        //==============================================================================================================
        public static bool LoggingOn
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _loggingOn;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceOperationCreation(CausalityTraceLevel traceLevel, Task task, String operationName, ulong relatedContext)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationCreation(traceLevel, CausalitySource.Library, _platformId, task.OperationId(), operationName, relatedContext);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceOperationCompletion(CausalityTraceLevel traceLevel, Task task, AsyncStatus status)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationCompletion(traceLevel, CausalitySource.Library, _platformId, task.OperationId(), status);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceOperationRelation(CausalityTraceLevel traceLevel, Task task, CausalityRelation relation)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceOperationRelation(traceLevel, CausalitySource.Library, _platformId, task.OperationId(), relation);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TraceSynchronousWorkStart(CausalityTraceLevel traceLevel, Task task, CausalitySynchronousWork work)
        {
            if (LoggingOn)
            {
                WinRTInterop.Callbacks.TraceSynchronousWorkStart(traceLevel, CausalitySource.Library, _platformId, task.OperationId(), work);
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
            _loggingOn = false;
            WinRTInterop.Callbacks.InitTracingStatusChanged(
                delegate (bool loggingOn)
                {
                    _loggingOn = loggingOn;
                }
            );
        }

        private static bool _loggingOn;

        // {4B0171A6-F3D0-41A0-9B33-02550652B995} - Guid that marks our causality events as "Coming from the BCL."
        private static readonly Guid _platformId = new Guid(0x4B0171A6, unchecked((short)0xF3D0), 0x41A0, 0x9B, 0x33, 0x02, 0x55, 0x06, 0x52, 0xB9, 0x95);


        //==============================================================================================================
        // This section of the class wraps calls to get the lazy-created Task object for the purpose of reporting
        // async causality events to the debugger. We route them through here so that the build chain can remove
        // them on ship-builds, hence avoiding the allocation cost of the Task at runtime.
        //==============================================================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task GetTaskIfDebuggingEnabled(this AsyncVoidMethodBuilder builder)
        {
            if (LoggingOn || Task.s_asyncDebuggingEnabled)
                return builder.Task;
            else
                return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task GetTaskIfDebuggingEnabled(this AsyncTaskMethodBuilder builder)
        {
            if (LoggingOn || Task.s_asyncDebuggingEnabled)
                return builder.Task;
            else
                return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task GetTaskIfDebuggingEnabled<TResult>(this AsyncTaskMethodBuilder<TResult> builder)
        {
            if (LoggingOn || Task.s_asyncDebuggingEnabled)
                return builder.Task;
            else
                return null;
        }
    }
}
