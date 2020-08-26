// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using ReflectionEventSource = System.Reflection.Runtime.Tracing.ReflectionEventSource;

namespace Internal.Reflection.Tracing
{
    [DeveloperExperienceModeOnly]
    public static partial class ReflectionTrace
    {
        public static bool Enabled
        {
            get
            {
                return ReflectionEventSource.IsInitialized && ReflectionEventSource.Log.IsEnabled();
            }
        }

        public static String GetTraceString(this TypeInfo typeInfo)
        {
            return typeInfo.NameString();
        }
    }
}


