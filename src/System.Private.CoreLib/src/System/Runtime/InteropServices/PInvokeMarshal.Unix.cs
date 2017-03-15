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
            s_lastWin32Error = Interop.Sys.GetErrNo();
        }

        internal static void ClearLastWin32Error()
        {
            Interop.Sys.ClearErrNo();
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

        internal static unsafe IntPtr MemAlloc(IntPtr cb)
        {
            return Interop.MemAlloc((UIntPtr)(void*)cb);
        }

        public static void MemFree(IntPtr hglobal)
        {
            Interop.MemFree(hglobal);
        }

        internal static IntPtr CoTaskMemAlloc(UIntPtr bytes)
        {
            return Interop.MemAlloc(bytes);
        }

        internal static void CoTaskMemFree(IntPtr allocatedMemory)
        {
            Interop.MemFree(allocatedMemory);
        }
    }
}
