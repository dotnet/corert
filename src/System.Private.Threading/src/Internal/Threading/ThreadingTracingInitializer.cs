// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::Internal.Threading.Tasks.Tracing;
using global::Internal.Threading.Tracing;
using global::System.Runtime.CompilerServices;

namespace Internal.Threading
{
    [EagerOrderedStaticConstructor(EagerStaticConstructorOrder.Threading)]
    public static class ThreadingTracingInitializer
    {
        static ThreadingTracingInitializer()
        {
            TaskTrace.Initialize(new TaskTraceCallbacksImplementation());
            SpinLockTrace.Initialize(new SpinLockTraceCallbacksImplementation());
        }
    }
}