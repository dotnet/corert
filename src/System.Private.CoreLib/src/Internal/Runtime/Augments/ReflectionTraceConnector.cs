// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

using Internal.Reflection.Tracing;

namespace Internal.Runtime.Augments
{
    [DeveloperExperienceModeOnly]
    public static class ReflectionTraceConnector
    {
        public static void Initialize(ReflectionTraceCallbacks callbacks)
        {
            ReflectionTrace.Initialize(callbacks);
            return;
        }
    }
}

