// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


namespace System.Runtime.InteropServices
{
    /// <summary>
    /// This is an internal hacky implementation of Marshal
    /// The original implementation of Marshal resides in S.P.Interop
    /// </summary>
    internal class Marshal
    {
        public static unsafe String PtrToStringUni(IntPtr ptr, int len)
        {
            return PInvokeMarshal.PtrToStringUni(ptr, len);
        }

        public static unsafe String PtrToStringUni(IntPtr ptr)
        {
            return PInvokeMarshal.PtrToStringUni(ptr);
        }

        public static int GetLastWin32Error()
        {
            return PInvokeMarshal.GetLastWin32Error();
        }

        public static unsafe IntPtr AllocHGlobal(IntPtr cb)
        {
            return PInvokeMarshal.AllocHGlobal(cb);
        }

        public static unsafe IntPtr AllocHGlobal(int cb)
        {
            return PInvokeMarshal.AllocHGlobal(cb);
        }

        public static void FreeHGlobal(IntPtr hglobal)
        {
            PInvokeMarshal.FreeHGlobal(hglobal);
        }

        public static unsafe IntPtr AllocCoTaskMem(int cb)
        {
            return PInvokeMarshal.AllocCoTaskMem(cb);
        }

        public static void FreeCoTaskMem(IntPtr ptr)
        {
            PInvokeMarshal.FreeCoTaskMem(ptr);
        }

        public static void Copy(IntPtr source, byte[] destination, int startIndex, int length)
        {
            InteropExtensions.CopyToManaged(source, destination, startIndex, length);
        }

#if PLATFORM_UNIX
        public static unsafe String PtrToStringAnsi(IntPtr ptr)
        {
            return PInvokeMarshal.PtrToStringAnsi(ptr);
        }
#endif
    }
}
