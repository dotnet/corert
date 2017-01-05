// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

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

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static int GetHRForException(Exception e)
        {
            if (e == null)
            {
                return Interop.COM.S_OK;
            }

            // @TODO: Setup IErrorInfo
            return e.HResult;
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
            return new Exception(errorCode.ToString());
#endif
        }
    }
}
