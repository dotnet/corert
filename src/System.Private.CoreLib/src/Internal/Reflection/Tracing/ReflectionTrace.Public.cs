// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using ReflectionTraceCallbacks = Internal.Runtime.Augments.ReflectionTraceCallbacks;

namespace Internal.Reflection.Tracing
{
    [DeveloperExperienceModeOnly]
    internal static partial class ReflectionTrace
    {
        public static bool Enabled
        {
            get
            {
                ReflectionTraceCallbacks callbacks = s_callbacks;
                if (callbacks == null)
                    return false;
                if (!callbacks.Enabled)
                    return false;
                return true;
            }
        }

        public static String GetTraceString(this Type type)
        {
            ReflectionTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return null;
            return callbacks.GetTraceString(type);
        }

        public static void Initialize(ReflectionTraceCallbacks callbacks)
        {
            s_callbacks = callbacks;
            return;
        }
    }
}


