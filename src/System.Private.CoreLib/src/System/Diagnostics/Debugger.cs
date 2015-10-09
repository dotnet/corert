// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace System.Diagnostics
{
    public static class Debugger
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerHidden] // this helps VS appear to stop on the source line calling Debugger.Break() instead of inside it
        public static void Break()
        {
            // IsAttached is always true when IsDebuggerPresent is true, so no need to check for it
            if (Interop.mincore.IsDebuggerPresent())
                Debug.DebugBreak();
        }

        public static bool IsAttached
        {
            get
            {
                return _isDebuggerAttached;
            }
        }

        public static bool Launch()
        {
            throw new PlatformNotSupportedException();
        }

        internal static void NotifyOfCrossThreadDependency()
        {
            // nothing to do...yet
        }

#pragma warning disable 649  // Suppress compiler warning about _isDebuggerAttached never being assigned to.
        // _isDebuggerAttached: Do not remove: This field is known to the debugger and modified directly by the debugger. 
        private static bool _isDebuggerAttached;
#pragma warning restore 649 
    }
}

