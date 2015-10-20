// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Diagnostics
{
    /// <summary>
    /// Annotations used by debugger
    /// </summary>
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
