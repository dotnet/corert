// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.Reflection.Execution
{
    using global::System;
    using global::System.Reflection;
    using global::System.Collections.Generic;
    using global::System.Diagnostics;

    internal static class ReflectionExecutionLogger
    {
        [Conditional("REFLECTION_EXECUTION_TRACE")]
        public static void WriteLine(string message)
        {
            Debug.WriteLine(message);
        }

        [Conditional("REFLECTION_EXECUTION_TRACE")]
        public static void WriteLine(string format, params object[] args)
        {
            Debug.WriteLine(format, args);
        }
    }
}
