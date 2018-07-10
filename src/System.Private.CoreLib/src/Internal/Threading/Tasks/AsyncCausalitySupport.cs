// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AsyncStatus = Internal.Runtime.Augments.AsyncStatus;
using CausalityRelation = Internal.Runtime.Augments.CausalityRelation;
using CausalitySource = Internal.Runtime.Augments.CausalitySource;
using CausalityTraceLevel = Internal.Runtime.Augments.CausalityTraceLevel;
using CausalitySynchronousWork = Internal.Runtime.Augments.CausalitySynchronousWork;

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
        public static void TraceOperationCreation(Task task, string operationName)
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

