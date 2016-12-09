// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

partial class Interop
{

    internal unsafe partial class WinRT
    {
        // Converts a ASCII (code<0x7f) string to byte array
        internal static byte[] AsciiStringToByteArray(string ascii)
        {
            byte[] ret = new byte[ascii.Length + 1];
            int index;

            for (index = 0; index < ascii.Length; index++)
            {
                ret[index] = (byte)ascii[index];
            }

            ret[index] = 0;

            return ret;
        }

        internal static unsafe void RoGetActivationFactory(string className, ref Guid iid, out IntPtr ppv)
        {
            fixed (char* unsafe_className = className)
            {
                void* hstring_typeName = null;

                HSTRING_HEADER hstringHeader;
                int hr =
                    WindowsCreateStringReference(
                        unsafe_className, (uint)className.Length, &hstringHeader, &hstring_typeName);

                if (hr < 0)
                    throw Marshal.GetExceptionForHR(hr);

                fixed (Guid* unsafe_iid = &iid)
                {
                    fixed (void* unsafe_ppv = &ppv)
                    {
                        hr = ExternalInterop.RoGetActivationFactory(
                            hstring_typeName,
                            unsafe_iid,
                            unsafe_ppv);

                        if (hr < 0)
                            throw Marshal.GetExceptionForHR(hr);
                    }
                }
            }
        }

        [DllImport(Interop.CORE_WINRT_STRING)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal static extern unsafe int WindowsCreateString(char* sourceString, uint length, void* hstring);

        [DllImport(Interop.CORE_WINRT_STRING)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal static extern unsafe int WindowsCreateStringReference(
            char* sourceString, uint length, HSTRING_HEADER* phstringHeader, void* hstring);

        [DllImport(Interop.CORE_WINRT_ERROR, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        public static extern int GetRestrictedErrorInfo(out System.IntPtr pRestrictedErrorInfo);

        [DllImport(Interop.CORE_WINRT_ERROR, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern int RoOriginateError(int hr, HSTRING hstring);

        [DllImport(Interop.CORE_WINRT_ERROR, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern int SetRestrictedErrorInfo(System.IntPtr pRestrictedErrorInfo);

        [DllImport(Interop.CORE_WINRT_ERROR1, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern int RoOriginateLanguageException(int hr, HSTRING message, IntPtr pLanguageException);

        [DllImport(Interop.CORE_WINRT_ERROR1, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern int RoReportUnhandledError(IntPtr pRestrictedErrorInfo);
    }

}
