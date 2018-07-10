// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks
{
    //
    // This class encapsulates the infrastructure to emit AsyncCausality events and Task-ID tracking for the use of the debugger.
    //
    internal static partial class DebuggerSupport
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
            lock (s_activeTasksLock)
            {
                s_activeTasks[id] = task;
            }
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
            lock (s_activeTasksLock)
            {
                s_activeTasks.Remove(id);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task GetActiveTaskFromId(int taskId)
        {
            Task task = null;
            s_activeTasks.TryGetValue(taskId, out task);
            return task;
        }

        private static readonly LowLevelDictionary<int, Task> s_activeTasks = new LowLevelDictionary<int, Task>();
        private static readonly object s_activeTasksLock = new object();

        //==============================================================================================================
        // This section of the class wraps calls to get the lazy-created Task object for the purpose of reporting
        // async causality events to the debugger.
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
