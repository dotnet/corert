// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::Internal.Runtime.Augments;
using global::System.Threading.Tasks;

namespace Internal.Threading
{
    internal sealed class TaskTraceCallbacksImplementation : TaskTraceCallbacks
    {
        public sealed override bool Enabled
        {
            get
            {
                return TplEtwProvider.Log.IsEnabled();
            }
        }

        public sealed override void TaskWaitBegin_Asynchronous(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID)
        {
            TplEtwProvider.Log.TaskWaitBegin(
                OriginatingTaskSchedulerID,
                OriginatingTaskID,
                TaskID,
                TplEtwProvider.TaskWaitBehavior.Asynchronous);
        }

        public sealed override void TaskWaitBegin_Synchronous(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID)
        {
            TplEtwProvider.Log.TaskWaitBegin(
                OriginatingTaskSchedulerID,
                OriginatingTaskID,
                TaskID,
                TplEtwProvider.TaskWaitBehavior.Synchronous);
        }

        public sealed override void TaskWaitEnd(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID)
        {
            TplEtwProvider.Log.TaskWaitEnd(
                OriginatingTaskSchedulerID,
                OriginatingTaskID,
                TaskID);
        }

        public sealed override void TaskScheduled(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID,
            int CreatingTaskID,
            int TaskCreationOptions)
        {
            TplEtwProvider.Log.TaskScheduled(
                OriginatingTaskSchedulerID,
                OriginatingTaskID,
                TaskID,
                CreatingTaskID,
                TaskCreationOptions);
        }

        public sealed override void TaskStarted(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID)
        {
            TplEtwProvider.Log.TaskStarted(
                OriginatingTaskSchedulerID,
                OriginatingTaskID,
                TaskID);
        }

        public sealed override void TaskCompleted(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID,
            bool IsExceptional)
        {
            TplEtwProvider.Log.TaskCompleted(
                OriginatingTaskSchedulerID,
                OriginatingTaskID,
                TaskID,
                IsExceptional);
        }
    }
}