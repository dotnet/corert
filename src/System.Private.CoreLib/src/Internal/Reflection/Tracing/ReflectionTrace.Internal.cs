// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using ReflectionTraceCallbacks = Internal.Runtime.Augments.ReflectionTraceCallbacks;

namespace Internal.Reflection.Tracing
{
    internal static partial class ReflectionTrace
    {
        private static ReflectionTraceCallbacks s_callbacks;
    }
}

