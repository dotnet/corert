// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Annotations used by debugger
    /// </summary>
    public static class DebugAnnotations
    {
        /// <summary>
        /// Informs debugger that previous line contains user code and debugger needs to dive deeper inside
        /// to find user code.
        /// </summary>
        [Conditional("DEBUG")]
        public static void PreviousCallContainsUserCode()
        {
            // This is a marker method and has no code in method body
        }
    }
}
