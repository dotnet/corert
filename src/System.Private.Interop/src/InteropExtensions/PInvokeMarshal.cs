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
    [CLSCompliant(false)]
    public sealed class PInvokeMarshal
    {
        public static void SaveLastWin32Error()
        {
            // nop
        }

        public static void ClearLastWin32Error()
        {
            // nop
        }

        public static int GetLastWin32Error()
        {
            return 0;
        }

        public static void SetLastWin32Error(int errorCode)
        {
            // nop
        }

        public static IntPtr GetStubForPInvokeDelegate(Delegate del)
        {
            return IntPtr.Zero;
        }

        public static Delegate GetPInvokeDelegateForStub(IntPtr pStub, RuntimeTypeHandle delegateType)
        {
            return default(Delegate);
        }

        public static IntPtr GetCurrentCalleeOpenStaticDelegateFunctionPointer()
        {
            return IntPtr.Zero;
        }

        public static unsafe IntPtr MemAlloc(IntPtr cb)
        {
            return Marshal.AllocHGlobal(cb);
        }

        public static void MemFree(IntPtr hglobal)
        {
            Marshal.FreeHGlobal(hglobal);
        }

        public static unsafe IntPtr MemReAlloc(IntPtr pv, IntPtr cb)
        {
            return Marshal.ReAllocHGlobal(pv, cb);
        }

        public static IntPtr CoTaskMemAlloc(UIntPtr bytes)
        {
            return Marshal.AllocCoTaskMem((int)bytes);
        }

        public static void CoTaskMemFree(IntPtr allocatedMemory)
        {
            Marshal.FreeCoTaskMem(allocatedMemory);
        }

        public static IntPtr CoTaskMemReAlloc(IntPtr pv, IntPtr cb)
        {
            return Marshal.ReAllocCoTaskMem(pv, (int)cb);
        }

        public static T GetCurrentCalleeDelegate<T>() where T : class // constraint can't be System.Delegate
        {
            return default(T);
        }

        public static unsafe void StringBuilderToUnicodeString(System.Text.StringBuilder stringBuilder, ushort* destination)
        {
            // nop
        }

        public static unsafe void UnicodeStringToStringBuilder(ushort* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            // nop
        }

        public static unsafe void StringBuilderToAnsiString(System.Text.StringBuilder stringBuilder, byte* pNative,
            bool bestFit, bool throwOnUnmappableChar)
        {
            // nop
        }

        public static unsafe void AnsiStringToStringBuilder(byte* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            // nop
        }

        public static unsafe string AnsiStringToString(byte* pchBuffer)
        {
            return default(string);
        }

        public static unsafe byte* StringToAnsiString(string str, bool bestFit, bool throwOnUnmappableChar)
        {
            return default(byte*);
        }

        public static unsafe void ByValWideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, int expectedCharCount,
            bool bestFit, bool throwOnUnmappableChar)
        {
            // nop
        }

        public static unsafe void ByValAnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            // nop
        }

        public static unsafe void WideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, bool bestFit, bool throwOnUnmappableChar)
        {
            // nop
        }

        public static unsafe void AnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            // nop
        }

        public static unsafe byte WideCharToAnsiChar(char managedValue, bool bestFit, bool throwOnUnmappableChar)
        {
            return default(byte);
        }

        public static unsafe char AnsiCharToWideChar(byte nativeValue)
        {
            return default(char);
        }

        public static unsafe void StringToByValAnsiString(string str, byte* pNative, int charCount, bool bestFit, bool throwOnUnmappableChar, bool truncate = true)
        {
            // nop
        }

        public static unsafe string ByValAnsiStringToString(byte* pchBuffer, int charCount)
        {
            return default(string);
        }

        public static unsafe int ConvertMultiByteToWideChar(byte* buffer, int ansiLength, char* pWChar, int uniLength)
        {
            return default(int);
        }

        public static unsafe int ConvertWideCharToMultiByte(char* wideCharStr, int wideCharLen, byte* multiByteStr, int multiByteLen)
        {
            return default(int);
        }

        public static unsafe int ConvertWideCharToMultiByte(char* wideCharStr,
                                                            int wideCharLen,
                                                            byte* multiByteStr,
                                                            int multiByteLen,
                                                            uint flags,
                                                            IntPtr usedDefaultChar)
        {
            return default(int);
        }

        public static unsafe int GetByteCount(char* wStr, int wideStrLen)
        {
            return default(int);
        }

        unsafe public static int GetCharCount(byte* multiByteStr, int multiByteLen)
        {
            return default(int);
        }

        public static unsafe int GetSystemMaxDBCSCharSize()
        {
            return default(int);
        }

        public static unsafe String PtrToStringUni(IntPtr ptr, int len)
        {
            return default(String);
        }

        public static unsafe String PtrToStringUni(IntPtr ptr)
        {
            return default(String);
        }

        public static unsafe void CopyToNative(Array source, int startIndex, IntPtr destination, int length)
        {  
            // nop
        }  
    }
}
