// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Security;

namespace System.Runtime.InteropServices
{
    public static partial class Marshal
    {
        public static int GetLastWin32Error()
        {
            return PInvokeMarshal.GetLastWin32Error();
        }

        internal static void SetLastWin32Error(int errorCode)
        {
            PInvokeMarshal.SetLastWin32Error(errorCode);
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

        public static IntPtr SecureStringToGlobalAllocAnsi(SecureString s)
        {
            return PInvokeMarshal.SecureStringToGlobalAllocAnsi(s);
        }

        public static IntPtr SecureStringToGlobalAllocUnicode(SecureString s)
        {
            return PInvokeMarshal.SecureStringToGlobalAllocUnicode(s);
        }

        public static IntPtr SecureStringToCoTaskMemAnsi(SecureString s)
        {
            return PInvokeMarshal.SecureStringToCoTaskMemAnsi(s);
        }

        public static IntPtr SecureStringToCoTaskMemUnicode(SecureString s)
        {
            return PInvokeMarshal.SecureStringToCoTaskMemUnicode(s);
        }

        public static IntPtr SecureStringToBSTR(SecureString s)
        {
            return PInvokeMarshal.SecureStringToBSTR(s);
        }

        public static int GetHRForException(Exception e)
        {
            return PInvokeMarshal.GetHRForException(e);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static Exception GetExceptionForHR(int errorCode)
        {
#if ENABLE_WINRT
            // In the default case, return a COM exception (same behavior in desktop CLR)
            return ExceptionHelpers.GetMappingExceptionForHR(
                errorCode,
                message: null,
                createCOMException : true,
                hasErrorInfo: false);
#else
            // TODO: Map HR to exeption even without COM interop support?
            return new COMException() {
                HResult = errorCode
            };
#endif
        }
    }
}
