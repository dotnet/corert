// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// This is an internal hacky implementation of Marshal
    /// The original implementation of Marshal resides in S.P.Interop
    /// </summary>
    internal class Marshal
    {
        public static unsafe string PtrToStringUni(IntPtr ptr, int len)
        {
            return PInvokeMarshal.PtrToStringUni(ptr, len);
        }

        public static unsafe string PtrToStringUni(IntPtr ptr)
        {
            return PInvokeMarshal.PtrToStringUni(ptr);
        }

        public static int GetLastWin32Error()
        {
            return PInvokeMarshal.GetLastWin32Error();
        }

        internal static void SetLastWin32Error(int errorCode)
        {
            PInvokeMarshal.SetLastWin32Error(errorCode);
        }

        public static int GetHRForLastWin32Error()
        {
            return PInvokeMarshal.GetHRForLastWin32Error();
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
            PInvokeMarshal.CopyToManaged(source, destination, startIndex, length);
        }

#if PLATFORM_UNIX
        public static unsafe String PtrToStringAnsi(IntPtr ptr)
        {
            return PInvokeMarshal.PtrToStringAnsi(ptr);
        }

        public static unsafe String PtrToStringAnsi(IntPtr ptr, int len)
        {
            return PInvokeMarshal.PtrToStringAnsi(ptr, len);
        }
#endif

        public static void ThrowExceptionForHR(int errorCode)
        {
            if (errorCode < 0)
                throw RuntimeAugments.Callbacks.GetExceptionForHR(errorCode);
        }

        internal static IntPtr AllocBSTR(int length)
        {
            return PInvokeMarshal.AllocBSTR(length);
        }

        public static void FreeBSTR(IntPtr ptr)
        {
            PInvokeMarshal.FreeBSTR(ptr);
        }

        public static int GetHRForException(Exception e)
        {
            return PInvokeMarshal.GetHRForException(e);
        }

        // Used by DispatchWrapper
        internal static IntPtr GetIDispatchForObject(object o) => throw new PlatformNotSupportedException();
        internal static int Release(IntPtr pUnk) => throw new PlatformNotSupportedException();

        internal static unsafe uint SysStringByteLen(IntPtr s)
        {
            return *(((uint*)s) - 1);
        }

        internal static bool IsPinnable(object o)
        {
            return (o == null) || o.EETypePtr.MightBeBlittable();
        }
    }
}
