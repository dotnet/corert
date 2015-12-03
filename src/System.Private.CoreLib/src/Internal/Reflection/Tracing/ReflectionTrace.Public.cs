// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

#if DEBUG
        public static String GetTraceString(this Type type)
        {
            ReflectionTraceCallbacks callbacks = s_callbacks;
            if (callbacks == null)
                return null;
            return callbacks.GetTraceString(type);
        }
#endif

        public static void Initialize(ReflectionTraceCallbacks callbacks)
        {
            s_callbacks = callbacks;
            return;
        }
    }
}


