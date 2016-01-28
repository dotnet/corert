// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

