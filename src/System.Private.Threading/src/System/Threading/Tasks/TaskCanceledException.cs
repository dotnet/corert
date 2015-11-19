// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// TaskCanceledException.cs
//

//
// An exception for task cancellations.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Runtime.InteropServices;

namespace System.Threading.Tasks
{
    /// <summary>
    /// Represents an exception used to communicate task cancellation.
    /// </summary>
    public class TaskCanceledException : OperationCanceledException
    {
        private Task m_canceledTask; // The task which has been canceled.

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Threading.Tasks.TaskCanceledException"/> class.
        /// </summary>
        public TaskCanceledException() : base(SR.TaskCanceledException_ctor_DefaultMessage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Threading.Tasks.TaskCanceledException"/>
        /// class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public TaskCanceledException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Threading.Tasks.TaskCanceledException"/>
        /// class with a specified error message and a reference to the inner exception that is the cause of
        /// this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public TaskCanceledException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Threading.Tasks.TaskCanceledException"/> class
        /// with a reference to the <see cref="T:System.Threading.Tasks.Task"/> that has been canceled.
        /// </summary>
        /// <param name="task">A task that has been canceled.</param>
        public TaskCanceledException(Task task) :
            base(SR.TaskCanceledException_ctor_DefaultMessage, task != null ? task.CancellationToken : new CancellationToken())
        {
            m_canceledTask = task;
        }

        /// <summary>
        /// Gets the task associated with this exception.
        /// </summary>
        /// <remarks>
        /// It is permissible for no Task to be associated with a 
        /// <see cref="T:System.Threading.Tasks.TaskCanceledException"/>, in which case
        /// this property will return null.
        /// </remarks>
        public Task Task
        {
            get { return m_canceledTask; }
        }
    }
}