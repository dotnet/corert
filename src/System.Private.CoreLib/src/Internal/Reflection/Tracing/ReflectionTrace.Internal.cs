// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;

using ReflectionTraceCallbacks = global::Internal.Runtime.Augments.ReflectionTraceCallbacks;

namespace Internal.Reflection.Tracing
{
    internal static partial class ReflectionTrace
    {
        private static ReflectionTraceCallbacks s_callbacks;
    }
}

