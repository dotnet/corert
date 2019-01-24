// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// TplEtwProvider.cs
//

//
// EventSource for TPL.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Diagnostics.Tracing;
using System.Security;

namespace System.Threading.Tasks
{
    /// <summary>Provides an event source for tracing TPL information.</summary>
    [EventSource(
        Name = "System.Threading.Tasks.TplEventSource",
        Guid = "2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5"
        //TODO:Bug455853:Add support for reading localized string in the EventSource il2il transform
        //,LocalizationResources = "mscorlib"
        )]
    internal sealed class TplEtwProvider : EventSource
    {
        /// <summary>
        /// Defines the singleton instance for the TPL ETW provider.
        /// The TPL Event provider GUID is {2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5}.
        /// </summary>
        public static TplEtwProvider Log = new TplEtwProvider();
        /// <summary>Prevent external instantiation.  All logging should go through the Log instance.</summary>
        private TplEtwProvider() { }

        /// <summary>Configured behavior of a task wait operation.</summary>
        public enum TaskWaitBehavior : int
        {
            /// <summary>A synchronous wait.</summary>
            Synchronous = 1,
            /// <summary>An asynchronous await.</summary>
            Asynchronous = 2
        }

        /// <summary>ETW tasks that have start/stop events.</summary>
        public class Tasks // this name is important for EventSource
        {
            // Do not use 1, it was used for "Loop" in the past.
            // Do not use 2, it was used for "Invoke" in the past.

            /// <summary>Executing a Task.</summary>
            public const EventTask TaskExecute = (EventTask)3;
            /// <summary>Waiting on a Task.</summary>
            public const EventTask TaskWait = (EventTask)4;
            // Do not use 5, it was used for "ForkJoin" in the past.
        }

        /// <summary>Enabled for all keywords.</summary>
        private const EventKeywords ALL_KEYWORDS = (EventKeywords)(-1);

        //-----------------------------------------------------------------------------------
        //        
        // TPL Event IDs (must be unique)
        //

        /// <summary>A task is scheduled to a task scheduler.</summary>
        private const int TASKSCHEDULED_ID = 7;
        /// <summary>A task is about to execute.</summary>
        private const int TASKSTARTED_ID = 8;
        /// <summary>A task has finished executing.</summary>
        private const int TASKCOMPLETED_ID = 9;
        /// <summary>A wait on a task is beginning.</summary>
        private const int TASKWAITBEGIN_ID = 10;
        /// <summary>A wait on a task is ending.</summary>
        private const int TASKWAITEND_ID = 11;


        //-----------------------------------------------------------------------------------
        //        
        // Task Events
        //

        // These are all verbose events, so we need to call IsEnabled(EventLevel.Verbose, ALL_KEYWORDS) 
        // call. However since the IsEnabled(l,k) call is more expensive than IsEnabled(), we only want 
        // to incur this cost when instrumentation is enabled. So the Task codepaths that call these
        // event functions still do the check for IsEnabled()


        #region TaskScheduled
        /// <summary>
        /// Fired when a task is queued to a TaskScheduler.
        /// </summary>
        /// <param name="OriginatingTaskSchedulerID">The scheduler ID.</param>
        /// <param name="OriginatingTaskID">The task ID.</param>
        /// <param name="TaskID">The task ID.</param>
        /// <param name="CreatingTaskID">The task ID</param>
        /// <param name="TaskCreationOptions">The options used to create the task.</param>
        [Event(TASKSCHEDULED_ID, Level = EventLevel.Verbose)]
        public void TaskScheduled(
            int OriginatingTaskSchedulerID, int OriginatingTaskID,  // PFX_COMMON_EVENT_HEADER
            int TaskID, int CreatingTaskID, int TaskCreationOptions)
        {
            if (IsEnabled(EventLevel.Verbose, ALL_KEYWORDS))
            {
                // There is no explicit WriteEvent() overload matching this event's fields.
                // Therefore calling WriteEvent() would hit the "params" overload, which leads to an object allocation every time this event is fired.
                // To prevent that problem we will call WriteEventCore(), which works with a stack based EventData array populated with the event fields                
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[5];

                    eventPayload[0].Size = sizeof(int);
                    eventPayload[0].DataPointer = ((IntPtr)(&OriginatingTaskSchedulerID));
                    eventPayload[1].Size = sizeof(int);
                    eventPayload[1].DataPointer = ((IntPtr)(&OriginatingTaskID));
                    eventPayload[2].Size = sizeof(int);
                    eventPayload[2].DataPointer = ((IntPtr)(&TaskID));
                    eventPayload[3].Size = sizeof(int);
                    eventPayload[3].DataPointer = ((IntPtr)(&CreatingTaskID));
                    eventPayload[4].Size = sizeof(int);
                    eventPayload[4].DataPointer = ((IntPtr)(&TaskCreationOptions));

                    WriteEventCore(TASKSCHEDULED_ID, 5, eventPayload);
                }
            }
        }
        #endregion TaskScheduled

        #region TaskStarted
        /// <summary>
        /// Fired just before a task actually starts executing.
        /// </summary>
        /// <param name="OriginatingTaskSchedulerID">The scheduler ID.</param>
        /// <param name="OriginatingTaskID">The task ID.</param>
        /// <param name="TaskID">The task ID.</param>
        [Event(TASKSTARTED_ID, Level = EventLevel.Verbose, Task = TplEtwProvider.Tasks.TaskExecute, Opcode = EventOpcode.Start)]
        public void TaskStarted(
            int OriginatingTaskSchedulerID, int OriginatingTaskID,  // PFX_COMMON_EVENT_HEADER
            int TaskID)
        {
            if (IsEnabled(EventLevel.Verbose, ALL_KEYWORDS))
            {
                WriteEvent(TASKSTARTED_ID, OriginatingTaskSchedulerID, OriginatingTaskID, TaskID);
            }
        }
        #endregion TaskStarted

        #region TaskCompleted
        /// <summary>
        /// Fired right after a task finished executing.
        /// </summary>
        /// <param name="OriginatingTaskSchedulerID">The scheduler ID.</param>
        /// <param name="OriginatingTaskID">The task ID.</param>
        /// <param name="TaskID">The task ID.</param>
        /// <param name="IsExceptional">Whether the task completed due to an error.</param>
        [Event(TASKCOMPLETED_ID, Level = EventLevel.Verbose, Version = 1, Task = TplEtwProvider.Tasks.TaskExecute, Opcode = EventOpcode.Stop)]
        public void TaskCompleted(
            int OriginatingTaskSchedulerID, int OriginatingTaskID,  // PFX_COMMON_EVENT_HEADER
            int TaskID, bool IsExceptional)
        {
            if (IsEnabled(EventLevel.Verbose, ALL_KEYWORDS))
            {
                // There is no explicit WriteEvent() overload matching this event's fields.
                // Therefore calling WriteEvent() would hit the "params" overload, which leads to 
                // an object allocation every time this event is fired. To prevent that problem we will
                // call WriteEventCore(), which works with a stack based EventData array populated with the event fields
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[4];

                    Int32 isExceptionalInt = IsExceptional ? 1 : 0;

                    eventPayload[0].Size = sizeof(int);
                    eventPayload[0].DataPointer = ((IntPtr)(&OriginatingTaskSchedulerID));
                    eventPayload[1].Size = sizeof(int);
                    eventPayload[1].DataPointer = ((IntPtr)(&OriginatingTaskID));
                    eventPayload[2].Size = sizeof(int);
                    eventPayload[2].DataPointer = ((IntPtr)(&TaskID));
                    eventPayload[3].Size = sizeof(int);
                    eventPayload[3].DataPointer = ((IntPtr)(&isExceptionalInt));

                    WriteEventCore(TASKCOMPLETED_ID, 4, eventPayload);
                }
            }
        }
        #endregion TaskCompleted

        #region TaskWaitBegin
        /// <summary>
        /// Fired when starting to wait for a taks's completion explicitly or implicitly.
        /// </summary>
        /// <param name="OriginatingTaskSchedulerID">The scheduler ID.</param>
        /// <param name="OriginatingTaskID">The task ID.</param>
        /// <param name="TaskID">The task ID.</param>
        /// <param name="Behavior">Configured behavior for the wait.</param>
        [Event(TASKWAITBEGIN_ID, Level = EventLevel.Verbose, Version = 1, Task = TplEtwProvider.Tasks.TaskWait, Opcode = EventOpcode.Start)]
        public void TaskWaitBegin(
            int OriginatingTaskSchedulerID, int OriginatingTaskID,  // PFX_COMMON_EVENT_HEADER
            int TaskID, TaskWaitBehavior Behavior)
        {
            if (IsEnabled(EventLevel.Verbose, ALL_KEYWORDS))
            {
                // WriteEvent(TASKWAITBEGIN_ID, OriginatingTaskSchedulerID, OriginatingTaskID, TaskID, Behavior);

                // There is no explicit WriteEvent() overload matching this event's fields.
                // Therefore calling WriteEvent() would hit the "params" overload, which leads to 
                // an object allocation every time this event is fired. To prevent that problem we will
                // call WriteEventCore(), which works with a stack based EventData array populated with the event fields
                unsafe
                {
                    EventData* eventPayload = stackalloc EventData[4];

                    eventPayload[0].Size = sizeof(int);
                    eventPayload[0].DataPointer = ((IntPtr)(&OriginatingTaskSchedulerID));
                    eventPayload[1].Size = sizeof(int);
                    eventPayload[1].DataPointer = ((IntPtr)(&OriginatingTaskID));
                    eventPayload[2].Size = sizeof(int);
                    eventPayload[2].DataPointer = ((IntPtr)(&TaskID));
                    eventPayload[3].Size = sizeof(int);
                    eventPayload[3].DataPointer = ((IntPtr)(&Behavior));

                    WriteEventCore(TASKWAITBEGIN_ID, 4, eventPayload);
                }
            }
        }
        #endregion TaskWaitBegin

        #region TaskWaitEnd
        /// <summary>
        /// Fired when the wait for a tasks completion returns.
        /// </summary>
        /// <param name="OriginatingTaskSchedulerID">The scheduler ID.</param>
        /// <param name="OriginatingTaskID">The task ID.</param>
        /// <param name="TaskID">The task ID.</param>
        [Event(TASKWAITEND_ID, Level = EventLevel.Verbose, Task = TplEtwProvider.Tasks.TaskWait, Opcode = EventOpcode.Stop)]
        public void TaskWaitEnd(
            int OriginatingTaskSchedulerID, int OriginatingTaskID,  // PFX_COMMON_EVENT_HEADER
            int TaskID)
        {
            if (IsEnabled(EventLevel.Verbose, ALL_KEYWORDS))
            {
                WriteEvent(TASKWAITEND_ID, OriginatingTaskSchedulerID, OriginatingTaskID, TaskID);
            }
        }
        #endregion TaskWaitEnd

    }  // class TplEtwProvider
}  // namespace
