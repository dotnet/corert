// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using TaskTraceCallbacks = Internal.Runtime.Augments.TaskTraceCallbacks;

namespace Internal.Threading.Tasks.Tracing
{
    /// <summary>
    /// Helper class for reporting <see cref="System.Threading.Tasks.Task"/>-related events.
    /// Calls are forwarded to an instance of <see cref="TaskTraceCallbacks"/>, if one has been
    /// provided.
    /// </summary>
    public static class TaskTrace
    {
        private static TaskTraceCallbacks s_callbacks;

        public static bool Enabled
        {
            get
            {
                TaskTraceCallbacks callbacks = s_callbacks;
                if (callbacks == null)
                    return false;
                if (!callbacks.Enabled)
                    return false;
                return true;
            }
        }

        public static void Initialize(TaskTraceCallbacks callbacks)
        {
            s_callbacks = callbacks;
        }

        public static void TaskWaitBegin_Asynchronous(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID)
        {
            TaskTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.TaskWaitBegin_Asynchronous(OriginatingTaskSchedulerID, OriginatingTaskID, TaskID);
        }

        public static void TaskWaitBegin_Synchronous(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID)
        {
            TaskTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.TaskWaitBegin_Synchronous(OriginatingTaskSchedulerID, OriginatingTaskID, TaskID);
        }

        public static void TaskWaitEnd(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID)
        {
            TaskTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.TaskWaitEnd(OriginatingTaskSchedulerID, OriginatingTaskID, TaskID);
        }

        public static void TaskScheduled(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID,
            int CreatingTaskID,
            int TaskCreationOptions)
        {
            TaskTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.TaskScheduled(OriginatingTaskSchedulerID, OriginatingTaskID, TaskID, CreatingTaskID, TaskCreationOptions);
        }

        public static void TaskStarted(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID)
        {
            TaskTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.TaskStarted(OriginatingTaskSchedulerID, OriginatingTaskID, TaskID);
        }

        public static void TaskCompleted(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID,
            bool IsExceptional)
        {
            TaskTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return;
            callbacks.TaskCompleted(OriginatingTaskSchedulerID, OriginatingTaskID, TaskID, IsExceptional);
        }
    }
}