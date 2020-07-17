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
        private const long HIWORDMASK = unchecked((long)0xffffffffffff0000L);

        // Win32 has the concept of Atoms, where a pointer can either be a pointer
        // or an int.  If it's less than 64K, this is guaranteed to NOT be a
        // pointer since the bottom 64K bytes are reserved in a process' page table.
        // We should be careful about deallocating this stuff.  Extracted to
        // a function to avoid C# problems with lack of support for IntPtr.
        // We have 2 of these methods for slightly different semantics for NULL.
        private static bool IsWin32Atom(IntPtr ptr)
        {
            long lPtr = (long)ptr;
            return 0 == (lPtr & HIWORDMASK);
        }

        private static bool IsNotWin32Atom(IntPtr ptr)
        {
            long lPtr = (long)ptr;
            return 0 != (lPtr & HIWORDMASK);
        }

        public static void SaveLastWin32Error()
        {
            s_lastWin32Error = Interop.mincore.GetLastError();
        }

        public static void ClearLastWin32Error()
        {
            Interop.mincore.SetLastError(0);
        }

        public static unsafe IntPtr MemAlloc(IntPtr cb)
        {
            return Interop.MemAlloc((UIntPtr)(void*)cb);
        }

        public static void MemFree(IntPtr hglobal)
        {
            if (IsNotWin32Atom(hglobal))
            {
                Interop.MemFree(hglobal);
            }
        }

        public static unsafe IntPtr MemReAlloc(IntPtr pv, IntPtr cb)
        {
            return Interop.MemReAlloc(pv, new UIntPtr((void*)cb));
        }

        public static IntPtr CoTaskMemAlloc(UIntPtr bytes)
        {
            return Interop.mincore.CoTaskMemAlloc(bytes);
        }

        public static void CoTaskMemFree(IntPtr allocatedMemory)
        {
            if (IsNotWin32Atom(allocatedMemory))
            {
                Interop.mincore.CoTaskMemFree(allocatedMemory);
            }
        }

        public static IntPtr CoTaskMemReAlloc(IntPtr pv, IntPtr cb)
        {
            return Interop.mincore.CoTaskMemRealloc(pv, cb);
        }

        internal static IntPtr AllocBSTR(int length)
        {
            IntPtr bstr = Interop.OleAut32.SysAllocStringLen(null, length);
            if (bstr == IntPtr.Zero)
                throw new OutOfMemoryException();
            return bstr;
        }

        internal static void FreeBSTR(IntPtr ptr)
        {
            Interop.OleAut32.SysFreeString(ptr);
        }

        #region String marshalling

        public static unsafe int ConvertMultiByteToWideChar(byte* buffer, int ansiLength, char* pWChar, int uniLength)
        {
            return Interop.Kernel32.MultiByteToWideChar(Interop.Kernel32.CP_ACP, 0, buffer, ansiLength, pWChar, uniLength);
        }

        // Convert a UTF16 string to ANSI byte array
        public static unsafe int ConvertWideCharToMultiByte(char* wideCharStr, int wideCharLen, byte* multiByteStr, int multiByteLen)
        {
            return Interop.Kernel32.WideCharToMultiByte(Interop.Kernel32.CP_ACP,
                                                        0,
                                                        wideCharStr,
                                                        wideCharLen,
                                                        multiByteStr,
                                                        multiByteLen,
                                                        default(IntPtr),
                                                        default(IntPtr)
                                                        );
        }

        // Convert a UTF16 string to ANSI byte array using flags
        public static unsafe int ConvertWideCharToMultiByte(char* wideCharStr,
                                                            int wideCharLen,
                                                            byte* multiByteStr,
                                                            int multiByteLen,
                                                            bool bestFit,
                                                            bool throwOnUnmappableChar)
        {
            uint flags = (bestFit ? 0 : Interop.Kernel32.WC_NO_BEST_FIT_CHARS);
            int defaultCharUsed = 0;
            int ret = Interop.Kernel32.WideCharToMultiByte(Interop.Kernel32.CP_ACP,
                                                        flags,
                                                        wideCharStr,
                                                        wideCharLen,
                                                        multiByteStr,
                                                        multiByteLen,
                                                        default(IntPtr),
                                                        throwOnUnmappableChar ? new System.IntPtr(&defaultCharUsed) : default(IntPtr)
                                                        );
            if (defaultCharUsed != 0)
            {
                throw new ArgumentException(SR.Arg_InteropMarshalUnmappableChar);
            }

            return ret;
        }

        // Return size in bytes required to convert a UTF16 string to byte array.
        public static unsafe int GetByteCount(char* wStr, int wideStrLen)
        {
            return Interop.Kernel32.WideCharToMultiByte(Interop.Kernel32.CP_ACP,
                                                        0,
                                                        wStr,
                                                        wideStrLen,
                                                        default(byte*),
                                                        0,
                                                        default(IntPtr),
                                                        default(IntPtr)
                                                        );
        }

        // Return number of charaters encoded in native byte array lpMultiByteStr
        unsafe public static int GetCharCount(byte* multiByteStr, int multiByteLen)
        {
            return Interop.Kernel32.MultiByteToWideChar(Interop.Kernel32.CP_ACP, 0, multiByteStr, multiByteLen, default(char*), 0);
        }

        public static unsafe int GetSystemMaxDBCSCharSize()
        {
            Interop.Kernel32.CPINFO cpInfo;
            if (Interop.Kernel32.GetCPInfo(Interop.Kernel32.CP_ACP, &cpInfo) != 0)
            {
                return cpInfo.MaxCharSize;
            }
            else
            {
                return 2;
            }
        }
        #endregion
    }
}
