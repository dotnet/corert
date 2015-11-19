// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks
{
    //
    // Workaround for Task:730304 -	DR needs to provide a mechanism to retain individual methods in debug builds
    //
    // Currently, the dependency reducer doesn't have a way to mark specific methods for reduction-immunity so
    // as a workaround, we create this ridiculous looking class whose only purpose is to fool the DR into
    // thinking our debugger hooks are actually used.
    //
    [DependencyReductionRoot]
    internal static class DebugHelperRooter
    {
        public static void Rooter()
        {
            Task t = null;
            t.GetDelegateContinuationsForDebugger();
            TaskContinuation tc = null;
            tc.GetDelegateContinuationsForDebugger();
            AsyncMethodBuilderCore.TryGetStateMachineForDebugger(null);
            Task.GetActiveTaskFromId(0);
            return;
        }
    }
}
