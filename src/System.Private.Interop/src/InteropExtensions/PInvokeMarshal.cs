// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// This PInvokeMarshal class should provide full public Marshal 
    /// implementation for all things related to P/Invoke marshalling
    /// </summary>
    [CLSCompliant(false)]
    public partial class PInvokeMarshal
    {        
        [ThreadStatic]
        internal static int s_lastWin32Error;

        public static int GetLastWin32Error()
        {
            return s_lastWin32Error;
        }

        public static void SetLastWin32Error(int errorCode)
        {
            s_lastWin32Error = errorCode;
        }
        
        public static void SaveLastWin32Error()
        {
            s_lastWin32Error = Marshal.GetLastWin32Error();
        }

        public static IntPtr GetFunctionPointerForDelegate(Delegate del)
        {
            return IntPtr.Zero;
        }

        public static Delegate GetDelegateForFunctionPointer(IntPtr pStub, RuntimeTypeHandle delegateType)
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
            stringBuilder.UnsafeCopyTo((char*)destination);
        }

        public static unsafe void UnicodeStringToStringBuilder(ushort* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            stringBuilder.ReplaceBuffer((char*)newBuffer);
        }

        public static unsafe void StringBuilderToAnsiString(System.Text.StringBuilder stringBuilder, byte* pNative,
            bool bestFit, bool throwOnUnmappableChar)
        {
            int len;

            // Convert StringBuilder to UNICODE string
            // Optimize for the most common case. If there is only a single char[] in the StringBuilder,
            // get it and convert it to ANSI
            char[] buffer = stringBuilder.GetBuffer(out len);
            if (buffer != null)
            {
                fixed (char* pManaged = buffer)
                {
                    StringToAnsiString(pManaged, len, pNative, /*terminateWithNull=*/true, bestFit, throwOnUnmappableChar);
                }
            }
            else // Otherwise, convert StringBuilder to string and then convert to ANSI
            {
                string str = stringBuilder.ToString();
                
                // Convert UNICODE string to ANSI string
                fixed (char* pManaged = str)
                {
                    StringToAnsiString(pManaged, str.Length, pNative, /*terminateWithNull=*/true, bestFit, throwOnUnmappableChar);
                }
            }
        }

        public static unsafe void AnsiStringToStringBuilder(byte* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            if (newBuffer == null)
                throw new ArgumentNullException(nameof(newBuffer));

            int lenAnsi;
            int lenUnicode;
            CalculateStringLength(newBuffer, out lenAnsi, out lenUnicode);

            if (lenUnicode > 0)
            {
                char[] buffer = new char[lenUnicode];
                fixed (char* pTemp = &buffer[0])
                {
                    ConvertMultiByteToWideChar(newBuffer,
                                               lenAnsi,
                                               pTemp,
                                               lenUnicode);
                }
                stringBuilder.ReplaceBuffer(buffer);
            }
            else
            {
                stringBuilder.Clear();
            }
        }
        
        public static unsafe string AnsiStringToString(byte* pchBuffer)
        {
            if (pchBuffer == null)
            {
                return null;
            }

            int lenAnsi;
            int lenUnicode;
            CalculateStringLength(pchBuffer, out lenAnsi, out lenUnicode);

            string result = String.Empty;

            if (lenUnicode > 0)
            {
                result = new string(' ',lenUnicode);

                fixed (char* pTemp = result)
                {
                    ConvertMultiByteToWideChar(pchBuffer,
                                               lenAnsi,
                                               pTemp,
                                               lenUnicode);
                }
            }

            return result;
        }

        public static unsafe byte* StringToAnsiString(string str, bool bestFit, bool throwOnUnmappableChar)
        {
            if (str != null)
            {
                int lenUnicode = str.Length;

                fixed (char* pManaged = str)
                {
                    return StringToAnsiString(pManaged, lenUnicode, null, /*terminateWithNull=*/true, bestFit, throwOnUnmappableChar);
                }
            }

            return null;
        }

        public static unsafe void ByValWideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, int expectedCharCount,
            bool bestFit, bool throwOnUnmappableChar)
        {
            // Zero-init pNative if it is NULL
            if (managedArray == null)
            {
                // @TODO - Create a more efficient version of zero initialization
                for (int i = 0; i < expectedCharCount; i++)
                {
                    pNative[i] = 0;
                }
            }


            int lenUnicode = managedArray.Length;
            if (lenUnicode < expectedCharCount)

                throw new ArgumentException(SR.WrongSizeArrayInNStruct);

            fixed (char* pManaged = managedArray)
            {
                StringToAnsiString(pManaged, lenUnicode, pNative, /*terminateWithNull=*/false, bestFit, throwOnUnmappableChar);
            }
        }

        public static unsafe void ByValAnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            // This should never happen because it is a embedded array
            Debug.Assert(pNative != null);

            // This should never happen because the array is always allocated by the marshaller
            Debug.Assert(managedArray != null);

            // COMPAT: Use the managed array length as the maximum length of native buffer
            // This obviously doesn't make sense but desktop CLR does that
            int lenInBytes = managedArray.Length;
            fixed (char* pManaged = managedArray)
            {
                ConvertMultiByteToWideChar(pNative,
                                           lenInBytes,
                                           pManaged,
                                           lenInBytes);
            }
        }

        public static unsafe void WideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, bool bestFit, bool throwOnUnmappableChar)
        {
            // Do nothing if array is NULL. This matches desktop CLR behavior
            if (managedArray == null)
                return;

            // Desktop CLR crash (AV at runtime) - we can do better in .NET Native
            if (pNative == null)
                throw new ArgumentNullException(nameof(pNative));

            int lenUnicode = managedArray.Length;
            fixed (char* pManaged = managedArray)
            {
                StringToAnsiString(pManaged, lenUnicode, pNative, /*terminateWithNull=*/false, bestFit, throwOnUnmappableChar);
            }
        }

        public static unsafe void AnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            // Do nothing if native is NULL. This matches desktop CLR behavior
            if (pNative == null)
                return;

            // Desktop CLR crash (AV at runtime) - we can do better in .NET Native
            if (managedArray == null)
                throw new ArgumentNullException(nameof(managedArray));

            // COMPAT: Use the managed array length as the maximum length of native buffer
            // This obviously doesn't make sense but desktop CLR does that
            int lenInBytes = managedArray.Length;
            fixed (char* pManaged = managedArray)
            {
                ConvertMultiByteToWideChar(pNative,
                                           lenInBytes,
                                           pManaged,
                                           lenInBytes);
            }
        }

        public static unsafe byte WideCharToAnsiChar(char managedValue, bool bestFit, bool throwOnUnmappableChar)
        {
            // @TODO - we really shouldn't allocate one-byte arrays and then destroy it
            byte* nativeArray = StringToAnsiString(&managedValue, 1, null, /*terminateWithNull=*/false, bestFit, throwOnUnmappableChar);
            byte native = (*nativeArray);
            CoTaskMemFree(new IntPtr(nativeArray));
            return native;
        }

        public static unsafe char AnsiCharToWideChar(byte nativeValue)
        {
            char ch;
            ConvertMultiByteToWideChar(&nativeValue, 1, &ch, 1);
            return ch;
        }

        public static unsafe void StringToByValAnsiString(string str, byte* pNative, int charCount, bool bestFit, bool throwOnUnmappableChar, bool truncate = true)
        {
            if (pNative == null)
                throw new ArgumentNullException(nameof(pNative));

            if (str != null)
            {
                // Truncate the string if it is larger than specified by SizeConst
                int lenUnicode;

                if (truncate)
                {
                    lenUnicode = str.Length;
                    if (lenUnicode >= charCount)
                        lenUnicode = charCount - 1;
                }
                else
                {
                    lenUnicode = charCount;
                }

                fixed (char* pManaged = str)
                {
                    StringToAnsiString(pManaged, lenUnicode, pNative, /*terminateWithNull=*/true, bestFit, throwOnUnmappableChar);
                }
            }
            else
            {
                (*pNative) = (byte)'\0';
            }
        }

        public static unsafe string ByValAnsiStringToString(byte* pchBuffer, int charCount)
        {
            // Match desktop CLR behavior
            if (charCount == 0)
                throw new MarshalDirectiveException();

            int lenAnsi = GetAnsiStringLen(pchBuffer);
            int lenUnicode = charCount;

            string result = String.Empty;

            if (lenUnicode > 0)
            {
                char* unicodeBuf = stackalloc char[lenUnicode];
                int unicodeCharWritten = ConvertMultiByteToWideChar(pchBuffer,
                                                                    lenAnsi,
                                                                    unicodeBuf,
                                                                    lenUnicode);

                // If conversion failure, return empty string to match desktop CLR behavior
                if (unicodeCharWritten > 0)
                    result = new string(unicodeBuf, 0, unicodeCharWritten);
            }

            return result;
        }
        
        
        private static unsafe int GetAnsiStringLen(byte* pchBuffer)
        {
            byte* pchBufferOriginal = pchBuffer;
            while (*pchBuffer != 0)
            {
                pchBuffer++;
            }
            
            return (int)(pchBuffer - pchBufferOriginal);
        }

        // c# string (UTF-16) to UTF-8 encoded byte array
        private static unsafe byte* StringToAnsiString(char* pManaged, int lenUnicode, byte* pNative, bool terminateWithNull,
            bool bestFit, bool throwOnUnmappableChar)
        {
            bool allAscii = true;

            for (int i = 0; i < lenUnicode; i++)
            {
                if (pManaged[i] >= 128)
                {
                    allAscii = false;
                    break;
                }
            }

            int length;

            if (allAscii) // If all ASCII, map one UNICODE character to one ANSI char
            {
                length = lenUnicode;
            }
            else // otherwise, let OS count number of ANSI chars
            {
                length = GetByteCount(pManaged, lenUnicode);
            }

            if (pNative == null)
            {
                pNative = (byte*)CoTaskMemAlloc((System.UIntPtr)(length + 1));
            }
            if (allAscii) // ASCII conversion
            {
                byte* pDst = pNative;
                char* pSrc = pManaged;

                while (lenUnicode > 0)
                {
                    unchecked
                    {
                        *pDst++ = (byte)(*pSrc++);
                        lenUnicode--;
                    }
                }
            }
            else // Let OS convert
            {
                ConvertWideCharToMultiByte(pManaged,
                                           lenUnicode,
                                           pNative,
                                           length,
                                           bestFit,
                                           throwOnUnmappableChar);
            }

            // Zero terminate
            if (terminateWithNull)
                *(pNative + length) = 0;

            return pNative;
        }

        public static unsafe String PtrToStringUni(IntPtr ptr, int len)
        {
            return Marshal.PtrToStringUni(ptr, len);
        }

        public static unsafe String PtrToStringUni(IntPtr ptr)
        {
            return Marshal.PtrToStringUni(ptr);
        }
        
        public static unsafe String PtrToStringAnsi(IntPtr ptr)
        {
            return Marshal.PtrToStringAnsi(ptr);
        }
 
        public static unsafe String PtrToStringAnsi(IntPtr ptr, int len)
        {
            return Marshal.PtrToStringAnsi(ptr, len);
        }
        
        //====================================================================
        // Copy blocks from CLR arrays to native memory.
        //====================================================================
        public static unsafe void CopyToNative(Array source, int startIndex, IntPtr destination, int length)
        {  
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// This is a auxiliary function that counts the length of the ansi buffer and
        ///  estimate the length of the buffer in Unicode. It returns true if all bytes
        ///  in the buffer are ANSII.
        /// </summary>
        private static unsafe bool CalculateStringLength(byte* pchBuffer, out int ansiBufferLen, out int unicodeBufferLen)
        {
            ansiBufferLen = 0;

            bool allAscii = true;

            {
                byte* p = pchBuffer;
                byte b = *p++;

                while (b != 0)
                {
                    if (b >= 128)
                    {
                        allAscii = false;
                    }

                    ansiBufferLen++;

                    b = *p++;
                }
            }

            if (allAscii)
            {
                unicodeBufferLen = ansiBufferLen;
            }
            else // If non ASCII, let OS calculate number of characters
            {
                unicodeBufferLen = GetCharCount(pchBuffer, ansiBufferLen);
            }
            return allAscii;
        }        
    }
}
