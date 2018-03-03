// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace System
{
    internal static partial class Environment
    {
        internal static int CurrentNativeThreadId => ManagedThreadId.Current;

        internal static long TickCount64
        {
            get
            {
                return (long)Interop.Sys.GetTickCount64();
            }
        }

#if DEBUG
        [Obsolete("ExpandEnvironmentVariables() only called on Windows so not implemented on Unix.")]
        public static string ExpandEnvironmentVariables(string name)
        {
            throw new PlatformNotSupportedException("ExpandEnvironmentVariables() only called on Windows so not implemented on Unix.");
        }
#endif
    }
}
