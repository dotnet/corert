// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Diagnostics
{
    /// <summary>
    /// Annotations used by debugger
    /// </summary>
    [System.Runtime.CompilerServices.ReflectionBlocked]
    public static class DebugAnnotations
    {
        /// <summary>
        /// Informs debugger that previous line contains code that debugger needs to dive deeper inside.
        /// </summary>
        public static void PreviousCallContainsDebuggerStepInCode()
        {
            // This is a marker method and has no code in method body
        }
    }
}
