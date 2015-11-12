// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;
using global::System.Threading.Tasks;
using global::System.Collections.Generic;
using global::System.Runtime.CompilerServices;
using AsyncStatus = global::Internal.Runtime.Augments.AsyncStatus;
using CausalityRelation = global::Internal.Runtime.Augments.CausalityRelation;
using CausalitySource = global::Internal.Runtime.Augments.CausalitySource;
using CausalityTraceLevel = global::Internal.Runtime.Augments.CausalityTraceLevel;
using CausalitySynchronousWork = global::Internal.Runtime.Augments.CausalitySynchronousWork;

namespace Internal.Threading.Tasks
{
    //
    // An internal contract that exposes just enough async debugger support needed by the AsTask() extension methods in the WindowsRuntimeSystemExtensions class.
    //
    public static class AsyncCausalitySupport
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddToActiveTasks(Task task)
        {
            DebuggerSupport.AddToActiveTasks(task);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromActiveTasks(Task task)
        {
            DebuggerSupport.RemoveFromActiveTasks(task);
        }

        public static bool LoggingOn
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return DebuggerSupport.LoggingOn;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TraceOperationCreation(Task task, String operationName)
        {
            DebuggerSupport.TraceOperationCreation(CausalityTraceLevel.Required, task, operationName, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TraceOperationCompletedSuccess(Task task)
        {
            DebuggerSupport.TraceOperationCompletion(CausalityTraceLevel.Required, task, AsyncStatus.Completed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TraceOperationCompletedError(Task task)
        {
            DebuggerSupport.TraceOperationCompletion(CausalityTraceLevel.Required, task, AsyncStatus.Error);
        }
    }
}

