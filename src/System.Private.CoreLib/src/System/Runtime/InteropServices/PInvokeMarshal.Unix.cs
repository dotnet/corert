// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Security;

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

        public static void ClearLastWin32Error()
        {
            Interop.Sys.ClearErrNo();
        }

        private static bool IsWin32Atom(IntPtr ptr)
        {
            return false;
        }

        public static unsafe String PtrToStringAnsi(IntPtr ptr)
        {
            if (IntPtr.Zero == ptr)
            {
                return null;
            }

            int len = string.strlen((byte*)ptr);
            if (len == 0)
            {
                return string.Empty;
            }

            return System.Text.Encoding.UTF8.GetString((byte*)ptr, len);
        }

        public static unsafe String PtrToStringAnsi(IntPtr ptr, int len)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));
            if (len < 0)
                throw new ArgumentException(nameof(len));

            return System.Text.Encoding.UTF8.GetString((byte*)ptr, len);
        }

        public static unsafe IntPtr MemAlloc(IntPtr cb)
        {
            return Interop.MemAlloc((UIntPtr)(void*)cb);
        }

        public static void MemFree(IntPtr hglobal)
        {
            Interop.MemFree(hglobal);
        }

        public static unsafe IntPtr MemReAlloc(IntPtr pv, IntPtr cb)
        {
            return Interop.MemReAlloc(pv, new UIntPtr((void*)cb));
        }

        public static IntPtr CoTaskMemAlloc(UIntPtr bytes)
        {
            return Interop.MemAlloc(bytes);
        }

        public static void CoTaskMemFree(IntPtr allocatedMemory)
        {
            Interop.MemFree(allocatedMemory);
        }

        public static unsafe IntPtr CoTaskMemReAlloc(IntPtr pv, IntPtr cb)
        {
            return Interop.MemReAlloc(pv, new UIntPtr((void*)cb));
        }

        // In CoreRT on Unix, there is not yet a BSTR implementation. On Windows, we would use SysAllocStringLen from OleAut32.dll.
        internal static IntPtr AllocBSTR(int length)
        {
            throw new PlatformNotSupportedException();
        }

        internal static void FreeBSTR(IntPtr ptr)
        {
            throw new PlatformNotSupportedException();
        }

        #region String marshalling

        public static unsafe int ConvertMultiByteToWideChar(byte* multiByteStr,
                                                            int multiByteLen,
                                                            char* wideCharStr,
                                                            int wideCharLen)
        {
            return System.Text.Encoding.UTF8.GetChars(multiByteStr, multiByteLen, wideCharStr, wideCharLen);
        }

        public static unsafe int ConvertWideCharToMultiByte(char* wideCharStr,
                                                            int wideCharLen,
                                                            byte* multiByteStr,
                                                            int multiByteLen,
                                                            bool bestFit,
                                                            bool throwOnUnmappableChar)
        {
            return System.Text.Encoding.UTF8.GetBytes(wideCharStr, wideCharLen, multiByteStr, multiByteLen);
        }

        public static unsafe int ConvertWideCharToMultiByte(char* wideCharStr,
                                                            int wideCharLen,
                                                            byte* multiByteStr,
                                                            int multiByteLen)
        {
            return System.Text.Encoding.UTF8.GetBytes(wideCharStr, wideCharLen, multiByteStr, multiByteLen);
        }

        public static unsafe int GetByteCount(char* wideCharStr, int wideCharLen)
        {
            return System.Text.Encoding.UTF8.GetByteCount(wideCharStr, wideCharLen);
        }

        public static unsafe int GetCharCount(byte* multiByteStr, int multiByteLen)
        {
            return System.Text.Encoding.UTF8.GetCharCount(multiByteStr, multiByteLen);
        }

        public static unsafe int GetSystemMaxDBCSCharSize()
        {
            return 3;
        }
        #endregion
    }
}
