// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// This PInvokeMarshal class should provide full public Marshal 
    /// implementation for all things related to P/Invoke marshalling
    /// </summary>
    public partial class PInvokeMarshal
    {
        public static void SaveLastWin32Error()
        {
            s_lastWin32Error = Interop.mincore.GetLastError();
        }

        internal static void ClearLastWin32Error()
        {
            Interop.mincore.SetLastError(0);
        }
    }
}
