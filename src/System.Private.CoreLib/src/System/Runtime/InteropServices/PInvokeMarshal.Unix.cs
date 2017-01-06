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
            s_lastWin32Error = Interop.Sys.GetLastErrNo();
        }

        internal static void ClearLastWin32Error()
        {
            Interop.Sys.SetLastErrNo(0);
        }

        public static unsafe String PtrToStringAnsi(IntPtr ptr)
        {
            if (IntPtr.Zero == ptr)
            {
                return null;
            }

            int len = Internal.Runtime.CompilerHelpers.InteropHelpers.strlen((byte*)ptr);
            if (len == 0)
            {
                return string.Empty;
            }

            return System.Text.Encoding.UTF8.GetString((byte*)ptr, len);
        }
    }
}
