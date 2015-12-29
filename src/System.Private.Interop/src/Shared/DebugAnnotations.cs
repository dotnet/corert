// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
