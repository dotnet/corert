// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// TaskScheduler.cs
//

//
// This file contains the primary interface and management of tasks and queues.  
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Security;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.Text;
using Internal.Threading.Tasks.Tracing;

namespace System.Threading.Tasks
{
    /// <summary>
    /// An implementation of TaskScheduler that uses the ThreadPool scheduler
    /// </summary>
    internal sealed class ThreadPoolTaskScheduler : TaskScheduler
    {
        /// <summary>
        /// Constructs a new ThreadPool task scheduler object
        /// </summary>
        internal ThreadPoolTaskScheduler()
        {
        }

        /// <summary>
        /// Schedules a task to the ThreadPool.
        /// </summary>
        /// <param name="task">The task to schedule.</param>
        protected internal override void QueueTask(Task task)
        {
            if (TaskTrace.Enabled)
            {
                Task currentTask = Task.InternalCurrent;
                Task creatingTask = task.m_parent;

                TaskTrace.TaskScheduled(this.Id, currentTask == null ? 0 : currentTask.Id,
                                                 task.Id, creatingTask == null ? 0 : creatingTask.Id,
                                                 (int)task.Options);
            }

            if ((task.Options & TaskCreationOptions.LongRunning) != 0)
            {
                ThreadPool.QueueLongRunningWork(() => task.ExecuteEntry(false));
            }
            else
            {
                // Normal handling for non-LongRunning tasks.
                bool forceToGlobalQueue = ((task.Options & TaskCreationOptions.PreferFairness) != 0);
                ThreadPool.UnsafeQueueCustomWorkItem(task, forceToGlobalQueue);
            }
        }

        /// <summary>
        /// This internal function will do this:
        ///   (1) If the task had previously been queued, attempt to pop it and return false if that fails.
        ///   (2) Propagate the return value from Task.ExecuteEntry() back to the caller.
        /// 
        /// IMPORTANT NOTE: TryExecuteTaskInline will NOT throw task exceptions itself. Any wait code path using this function needs
        /// to account for exceptions that need to be propagated, and throw themselves accordingly.
        /// </summary>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If the task was previously scheduled, and we can't pop it, then return false.
            if (taskWasPreviouslyQueued && !ThreadPool.TryPopCustomWorkItem(task))
                return false;

            // Propagate the return value of Task.ExecuteEntry()
            bool rval = false;
            try
            {
                rval = task.ExecuteEntry(false); // handles switching Task.Current etc.
            }
            finally
            {
                //   Only call NWIP() if task was previously queued
                if (taskWasPreviouslyQueued) NotifyWorkItemProgress();
            }

            return rval;
        }

        protected internal override bool TryDequeue(Task task)
        {
            // just delegate to TP
            return ThreadPool.TryPopCustomWorkItem(task);
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return FilterTasksFromWorkItems(ThreadPool.GetQueuedWorkItems());
        }

        private IEnumerable<Task> FilterTasksFromWorkItems(IEnumerable<IThreadPoolWorkItem> tpwItems)
        {
            foreach (IThreadPoolWorkItem tpwi in tpwItems)
            {
                if (tpwi is Task)
                {
                    yield return (Task)tpwi;
                }
            }
        }

        /// <summary>
        /// Notifies the scheduler that work is progressing (no-op).
        /// </summary>
        internal override void NotifyWorkItemProgress()
        {
            ThreadPool.NotifyWorkItemProgress();
        }

        /// <summary>
        /// This is the only scheduler that returns false for this property, indicating that the task entry codepath is unsafe (CAS free)
        /// since we know that the underlying scheduler already takes care of atomic transitions from queued to non-queued.
        /// </summary>
        internal override bool RequiresAtomicStartTransition
        {
            get { return false; }
        }
    }
}
