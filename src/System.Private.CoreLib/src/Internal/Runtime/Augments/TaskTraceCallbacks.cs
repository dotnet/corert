// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Runtime.Augments
{
    /// <summary>
    /// Callbacks to allow <see cref="System.Threading.Tasks.Task"/> and related types to report ETW
    /// events without having to put the relevant EventSource (and all the associated code) in this
    /// assembly.
    /// Implemented in System.Private.Threading.
    /// </summary>
    public abstract class TaskTraceCallbacks
    {
        public abstract bool Enabled { get; }

        public abstract void TaskWaitBegin_Asynchronous(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID);

        public abstract void TaskWaitBegin_Synchronous(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID);

        public abstract void TaskWaitEnd(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID);

        public abstract void TaskScheduled(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID,
            int CreatingTaskID,
            int TaskCreationOptions);

        public abstract void TaskStarted(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID);

        public abstract void TaskCompleted(
            int OriginatingTaskSchedulerID,
            int OriginatingTaskID,
            int TaskID,
            bool IsExceptional);
    }
}


