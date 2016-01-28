// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
//
// Marshalling helpers used by MCG
//
// NOTE:
//   These source code are being published to InternalAPIs and consumed by RH builds
//   Use PublishInteropAPI.bat to keep the InternalAPI copies in sync
// ----------------------------------------------------------------------------------

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Reflection;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Text;
    using System.Runtime;
    using System.Diagnostics.Contracts;
    using Internal.NativeFormat;
    using System.Runtime.CompilerServices;

#if RHTESTCL
    using OutputClass = System.Console;
#else
    using OutputClass = System.Diagnostics.Debug;
#endif

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Expose functionality from System.Private.CoreLib and forwards calls to InteropExtensions in System.Private.CoreLib
    /// </summary>
    [CLSCompliant(false)]
    public static partial class McgMarshal
    {
        [ThreadStatic]
        internal static int s_lastWin32Error;
        public static void SaveLastWin32Error()
        {
            s_lastWin32Error = ExternalInterop.GetLastWin32Error();
        }

        public static bool GuidEquals(ref Guid left, ref Guid right)
        {
            return InteropExtensions.GuidEquals(ref left, ref right);
        }

        public static bool ComparerEquals<T>(T left, T right)
        {
            return InteropExtensions.ComparerEquals<T>(left, right);
        }

        public static T CreateClass<T>() where T : class
        {
            return InteropExtensions.UncheckedCast<T>(InteropExtensions.RuntimeNewObject(typeof(T).TypeHandle));
        }

        public static bool IsEnum(object obj)
        {
#if RHTESTCL
            return false;
#else
            return InteropExtensions.IsEnum(obj.GetTypeHandle());
#endif
        }

        public static bool IsCOMObject(Type type)
        {
#if RHTESTCL
            return false;
#else
            return InteropExtensions.AreTypesAssignable(type.TypeHandle, typeof(__ComObject).TypeHandle);
#endif
        }

        public static T FastCast<T>(object value) where T : class
        {
            // We have an assert here, to verify that a "real" cast would have succeeded.
            // However, casting on weakly-typed RCWs modifies their state, by doing a QI and caching
            // the result.  This often makes things work which otherwise wouldn't work (especially variance).
            Debug.Assert(value == null || value is T);
            return InteropExtensions.UncheckedCast<T>(value);
        }

        /// <summary>
        /// Converts a managed DateTime to native OLE datetime
        /// Used by MCG marshalling code
        /// </summary>
        public static double ToNativeOleDate(DateTime dateTime)
        {
            return InteropExtensions.ToNativeOleDate(dateTime);
        }

        /// <summary>
        /// Converts native OLE datetime to managed DateTime
        /// Used by MCG marshalling code
        /// </summary>
        public static DateTime FromNativeOleDate(double nativeOleDate)
        {
            return InteropExtensions.FromNativeOleDate(nativeOleDate);
        }

        /// <summary>
        /// Used in Marshalling code
        /// Call safeHandle.InitializeHandle to set the internal _handle field
        /// </summary>
        public static void InitializeHandle(SafeHandle safeHandle, IntPtr win32Handle)
        {
            InteropExtensions.InitializeHandle(safeHandle, win32Handle);
        }

        /// <summary>
        /// Check if obj's type is the same as represented by normalized handle
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsOfType(object obj, RuntimeTypeHandle handle)
        {
            return obj.IsOfType(handle);
        }

#if ENABLE_WINRT
        public static unsafe void SetExceptionErrorCode(Exception exception, int errorCode)
        {
            InteropExtensions.SetExceptionErrorCode(exception, errorCode);
        }

        /// <summary>
        /// Used in Marshalling code
        /// Gets the handle of the CriticalHandle
        /// </summary>
        public static IntPtr GetHandle(CriticalHandle criticalHandle)
        {
            return criticalHandle.handle;
        }

        /// <summary>
        /// Used in Marshalling code
        /// Sets the handle of the CriticalHandle
        /// </summary>
        public static void SetHandle(CriticalHandle criticalHandle, IntPtr handle)
        {
            criticalHandle.handle = handle;
        }
#endif
    }

    /// <summary>
    /// McgMarshal helpers exposed to be used by MCG
    /// </summary>
    public static partial class McgMarshal
    {
        #region Type marshalling

        public static Type TypeNameToType(HSTRING nativeTypeName, int nativeTypeKind)
        {
#if ENABLE_WINRT
            return McgTypeHelpers.TypeNameToType(nativeTypeName, nativeTypeKind);
#else
            throw new NotSupportedException("TypeNameToType");
#endif
        }

        public unsafe static void TypeToTypeName(
            Type type,
            out HSTRING nativeTypeName,
            out int nativeTypeKind)
        {
#if ENABLE_WINRT
            McgTypeHelpers.TypeToTypeName(type, out nativeTypeName, out nativeTypeKind);
#else
            throw new NotSupportedException("TypeToTypeName");
#endif
        }

        #endregion

        #region String marshalling

        [CLSCompliant(false)]
        public static unsafe void StringBuilderToUnicodeString(System.Text.StringBuilder stringBuilder, ushort* destination)
        {
            stringBuilder.UnsafeCopyTo((char*)destination);
        }

        [CLSCompliant(false)]
        public static unsafe void UnicodeStringToStringBuilder(ushort* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            stringBuilder.ReplaceBuffer((char*)newBuffer);
        }

#if !RHTESTCL

        [CLSCompliant(false)]
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

        [CLSCompliant(false)]
        public static unsafe void AnsiStringToStringBuilder(byte* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            if (newBuffer == null)
                throw new ArgumentNullException();

            int lenAnsi;
            int lenUnicode;
            CalculateStringLength(newBuffer, out lenAnsi, out lenUnicode);

            if (lenUnicode > 0)
            {
                char[] buffer = new char[lenUnicode];
                fixed (char* pTemp = buffer)
                {
                    ExternalInterop.ConvertMultiByteToWideChar(new System.IntPtr(newBuffer),
                                                               lenAnsi,
                                                               new System.IntPtr(pTemp),
                                                               lenUnicode);
                }
                stringBuilder.ReplaceBuffer(buffer);
            }
            else
            {
                stringBuilder.Clear();
            }
        }

        /// <summary>
        /// Convert ANSI string to unicode string, with option to free native memory. Calls generated by MCG
        /// </summary>
        /// <remarks>Input assumed to be zero terminated. Generates String.Empty for zero length string.
        /// This version is more efficient than ConvertToUnicode in src\Interop\System\Runtime\InteropServices\Marshal.cs in that it can skip calling
        /// MultiByteToWideChar for ASCII string, and it does not need another char[] buffer</remarks>
        [CLSCompliant(false)]
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
                result = new String(' ', lenUnicode); // TODO: FastAllocate not accessible here

                fixed (char* pTemp = result)
                {
                    ExternalInterop.ConvertMultiByteToWideChar(new System.IntPtr(pchBuffer),
                                                               lenAnsi,
                                                               new System.IntPtr(pTemp),
                                                               lenUnicode);
                }
            }

            return result;
        }

        /// <summary>
        /// Convert UNICODE string to ANSI string.
        /// </summary>
        /// <remarks>This version is more efficient than StringToHGlobalAnsi in Interop\System\Runtime\InteropServices\Marshal.cs in that
        /// it could allocate single byte per character, instead of SystemMaxDBCSCharSize per char, and it can skip calling WideCharToMultiByte for ASCII string</remarks>
        [CLSCompliant(false)]
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

        /// <summary>
        /// Convert UNICODE wide char array to ANSI ByVal byte array.
        /// </summary>
        /// <remarks>
        /// * This version works with array instead string, it means that there will be NO NULL to terminate the array.
        /// * The buffer to store the byte array must be allocated by the caller and must fit managedArray.Length.
        /// </remarks>
        /// <param name="managedArray">UNICODE wide char array</param>
        /// <param name="pNative">Allocated buffer where the ansi characters must be placed. Could NOT be null. Buffer size must fit char[].Length.</param>
        [CLSCompliant(false)]
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

        [CLSCompliant(false)]
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
                ExternalInterop.ConvertMultiByteToWideChar(new System.IntPtr(pNative),
                                                           lenInBytes,
                                                           new System.IntPtr(pManaged),
                                                           lenInBytes);
            }
        }

        [CLSCompliant(false)]
        public static unsafe void WideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, bool bestFit, bool throwOnUnmappableChar)
        {
            // Do nothing if array is NULL. This matches desktop CLR behavior
            if (managedArray == null)
                return;

            // Desktop CLR crash (AV at runtime) - we can do better in .NET Native
            if (pNative == null)
                throw new ArgumentNullException();

            int lenUnicode = managedArray.Length;
            fixed (char* pManaged = managedArray)
            {
                StringToAnsiString(pManaged, lenUnicode, pNative, /*terminateWithNull=*/false, bestFit, throwOnUnmappableChar);
            }
        }

        /// <summary>
        /// Convert ANSI ByVal byte array to UNICODE wide char array, best fit
        /// </summary>
        /// <remarks>
        /// * This version works with array instead to string, it means that the len must be provided and there will be NO NULL to
        /// terminate the array.
        /// * The buffer to the UNICODE wide char array must be allocated by the caller.
        /// </remarks>
        /// <param name="pNative">Pointer to the ANSI byte array. Could NOT be null.</param>
        /// <param name="lenInBytes">Maximum buffer size.</param>
        /// <param name="managedArray">Wide char array that has already been allocated.</param>
        [CLSCompliant(false)]
        public static unsafe void AnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            // Do nothing if native is NULL. This matches desktop CLR behavior
            if (pNative == null)
                return;

            // Desktop CLR crash (AV at runtime) - we can do better in .NET Native
            if (managedArray == null)
                throw new ArgumentNullException();

            // COMPAT: Use the managed array length as the maximum length of native buffer
            // This obviously doesn't make sense but desktop CLR does that
            int lenInBytes = managedArray.Length;
            fixed (char* pManaged = managedArray)
            {
                ExternalInterop.ConvertMultiByteToWideChar(new System.IntPtr(pNative),
                                                           lenInBytes,
                                                           new System.IntPtr(pManaged),
                                                           lenInBytes);
            }
        }

        /// <summary>
        /// Convert a single UNICODE wide char to a single ANSI byte.
        /// </summary>
        /// <param name="managedArray">single UNICODE wide char value</param>
        public static unsafe byte WideCharToAnsiChar(char managedValue, bool bestFit, bool throwOnUnmappableChar)
        {
            // @TODO - we really shouldn't allocate one-byte arrays and then destroy it
            byte* nativeArray = StringToAnsiString(&managedValue, 1, null, /*terminateWithNull=*/false, bestFit, throwOnUnmappableChar);
            byte native = (*nativeArray);
            ExternalInterop.CoTaskMemFree(nativeArray);
            return native;
        }

        /// <summary>
        /// Convert a single ANSI byte value to a single UNICODE wide char value, best fit.
        /// </summary>
        /// <param name="nativeValue">Single ANSI byte value.</param>
        public static unsafe char AnsiCharToWideChar(byte nativeValue)
        {
            char[] buffer = new char[1];
            fixed (char* pTemp = buffer)
            {
                ExternalInterop.ConvertMultiByteToWideChar(new System.IntPtr(&nativeValue), 1, new System.IntPtr(pTemp), 1);
                return buffer[0];
            }
        }

        /// <summary>
        /// Convert UNICODE string to ANSI ByVal string.
        /// </summary>
        /// <remarks>This version is more efficient than StringToHGlobalAnsi in Interop\System\Runtime\InteropServices\Marshal.cs in that
        /// it could allocate single byte per character, instead of SystemMaxDBCSCharSize per char, and it can skip calling WideCharToMultiByte for ASCII string</remarks>
        /// <param name="str">Unicode string.</param>
        /// <param name="pNative"> Allocated buffer where the ansi string must be placed. Could NOT be null. Buffer size must fit str.Length.</param>
        [CLSCompliant(false)]
        public static unsafe void StringToByValAnsiString(string str, byte* pNative, int charCount, bool bestFit, bool throwOnUnmappableChar)
        {
            if (pNative == null)
                throw new ArgumentNullException();

            if (str != null)
            {
                // Truncate the string if it is larger than specified by SizeConst
                int lenUnicode = str.Length;
                if (lenUnicode >= charCount)
                    lenUnicode = charCount - 1;

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

        /// <summary>
        /// Convert ANSI string to unicode string, with option to free native memory. Calls generated by MCG
        /// </summary>
        /// <remarks>Input assumed to be zero terminated. Generates String.Empty for zero length string.
        /// This version is more efficient than ConvertToUnicode in src\Interop\System\Runtime\InteropServices\Marshal.cs in that it can skip calling
        /// MultiByteToWideChar for ASCII string, and it does not need another char[] buffer</remarks>
        [CLSCompliant(false)]
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
                int unicodeCharWritten = ExternalInterop.ConvertMultiByteToWideChar(new System.IntPtr(pchBuffer),
                                                                                    lenAnsi,
                                                                                    new System.IntPtr(unicodeBuf),
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
                length = ExternalInterop.GetByteCount(pManaged, lenUnicode);
            }

            if (pNative == null)
            {
                pNative = (byte*)ExternalInterop.CoTaskMemAlloc((System.IntPtr)(length + 1));
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
                uint flags = (bestFit ? 0 : ExternalInterop.Constants.WC_NO_BEST_FIT_CHARS);
                int defaultCharUsed = 0;
                ExternalInterop.ConvertWideCharToMultiByte(pManaged,
                                                           lenUnicode,
                                                           new System.IntPtr(pNative),
                                                           length,
                                                           flags,
                                                           throwOnUnmappableChar ? new System.IntPtr(&defaultCharUsed) : default(IntPtr)
                                                           );
                if (defaultCharUsed != 0)
                {
                    throw new ArgumentException(SR.Arg_InteropMarshalUnmappableChar);
                }
            }

            // Zero terminate
            if (terminateWithNull)
                *(pNative + length) = 0;

            return pNative;
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
                unicodeBufferLen = ExternalInterop.GetCharCount(new IntPtr(pchBuffer), ansiBufferLen);
            }
            return allAscii;
        }

#endif

#if ENABLE_WINRT
        /// <summary>
        /// Creates a temporary HSTRING on the staack
        /// NOTE: pchPinnedSourceString must be pinned before calling this function, making sure the pointer
        /// is valid during the entire interop call
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe void StringToHStringReference(
            char *pchPinnedSourceString,
            string sourceString,
            HSTRING_HEADER* pHeader,
            HSTRING* phString)
        {
            if (sourceString == null)
                throw new ArgumentNullException(SR.Null_HString);

            int hr = ExternalInterop.WindowsCreateStringReference(
                pchPinnedSourceString,
                (uint)sourceString.Length,
                pHeader,
                (void**)phString);

            if (hr < 0)
                throw Marshal.GetExceptionForHR(hr);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe HSTRING StringToHString(string sourceString)
        {
            if (sourceString == null)
                throw new ArgumentNullException(SR.Null_HString);

            return StringToHStringInternal(sourceString);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe HSTRING StringToHStringForField(string sourceString)
        {
#if !RHTESTCL
            if (sourceString == null)
                throw new MarshalDirectiveException(SR.BadMarshalField_Null_HString);
#endif
            return StringToHStringInternal(sourceString);
        }

        private static unsafe HSTRING StringToHStringInternal(string sourceString)
        {
            HSTRING ret;
            int hr = StringToHStringNoNullCheck(sourceString, &ret);
            if (hr < 0)
                throw Marshal.GetExceptionForHR(hr);

            return ret;
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal static unsafe int StringToHStringNoNullCheck(string sourceString, HSTRING* hstring)
        {
            fixed (char* pChars = sourceString)
            {
                int hr = ExternalInterop.WindowsCreateString(pChars, (uint)sourceString.Length, (void*)hstring);

                return hr;
            }
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal static unsafe string HStringToString(IntPtr hString)
        {
            HSTRING hstring = new HSTRING(hString);
            return HStringToString(hstring);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe string HStringToString(HSTRING pHString)
        {
            if (pHString.handle == IntPtr.Zero)
            {
                return String.Empty;
            }

            uint length = 0;
            char* pchBuffer = ExternalInterop.WindowsGetStringRawBuffer(pHString.handle.ToPointer(), &length);

            return new string(pchBuffer, 0, (int)length);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe void FreeHString(IntPtr pHString)
        {
            ExternalInterop.WindowsDeleteString(pHString.ToPointer());
        }
#endif //ENABLE_WINRT

        #endregion

        #region COM marshalling

        /// <summary>
        /// Explicit AddRef for RCWs
        /// You can't call IFoo.AddRef anymore as IFoo no longer derive from IUnknown
        /// You need to call McgMarshal.AddRef();
        /// </summary>
        /// <remarks>
        /// Used by prefast MCG plugin (mcgimportpft) only
        /// </remarks>
        [CLSCompliant(false)]
        public static int AddRef(__ComObject obj)
        {
            return obj.AddRef();
        }

        /// <summary>
        /// Explicit Release for RCWs
        /// You can't call IFoo.Release anymore as IFoo no longer derive from IUnknown
        /// You need to call McgMarshal.Release();
        /// </summary>
        /// <remarks>
        /// Used by prefast MCG plugin (mcgimportpft) only
        /// </remarks>
        [CLSCompliant(false)]
        public static int Release(__ComObject obj)
        {
            return obj.Release();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public unsafe static int ComAddRef(IntPtr pComItf)
        {
            return CalliIntrinsics.StdCall__AddRef(((__com_IUnknown*)(void*)pComItf)->pVtable->
                pfnAddRef, pComItf);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal unsafe static int ComRelease_StdCall(IntPtr pComItf)
        {
            return CalliIntrinsics.StdCall__Release(((__com_IUnknown*)(void*)pComItf)->pVtable->
                pfnRelease, pComItf);
        }

        /// <summary>
        /// Inline version of ComRelease
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)] //reduces MCG-generated code size
        public unsafe static int ComRelease(IntPtr pComItf)
        {
            IntPtr pRelease = ((__com_IUnknown*)(void*)pComItf)->pVtable->pfnRelease;

            // Check if the COM object is implemented by PN Interop code, for which we can call directly
            if (pRelease == AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release))
            {
                return __interface_ccw.DirectRelease(pComItf);
            }

            // Normal slow path, do not inline
            return ComRelease_StdCall(pComItf);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public unsafe static int ComSafeRelease(IntPtr pComItf)
        {
            if (pComItf != default(IntPtr))
            {
                return ComRelease(pComItf);
            }

            return 0;
        }

        /// <summary>
        /// Returns the cached WinRT factory RCW under the current context
        /// </summary>
        [CLSCompliant(false)]
        public static unsafe __ComObject GetActivationFactory(string className, McgTypeInfo factoryIntf)
        {
#if  ENABLE_WINRT
            return FactoryCache.Get().GetActivationFactory(className, factoryIntf);
#else
            throw new PlatformNotSupportedException("GetActivationFactory");
#endif
        }

        /// <summary>
        /// Used by CCW infrastructure code to return the target object from this pointer
        /// </summary>
        /// <returns>The target object pointed by this pointer</returns>
        public static object ThisPointerToTargetObject(IntPtr pUnk)
        {
            return ComCallableObject.GetTarget(pUnk);
        }

        [CLSCompliant(false)]
        public static object ComInterfaceToObject(
            IntPtr pComItf,
            McgTypeInfo interfaceTypeInfo)
        {
            return ComInterfaceToObject(pComItf, interfaceTypeInfo, McgClassInfo.Null);
        }

        [CLSCompliant(false)]
        public static object ComInterfaceToObject_NoUnboxing(
            IntPtr pComItf,
            McgTypeInfo interfaceTypeInfo)
        {
            return McgComHelpers.ComInterfaceToObjectInternal(pComItf, interfaceTypeInfo, McgClassInfo.Null, McgComHelpers.CreateComObjectFlags.SkipTypeResolutionAndUnboxing);
        }

        [CLSCompliant(false)]
        public static object ComInterfaceToObject(
            IntPtr pComItf,
            McgTypeInfo interfaceTypeInfo,
            McgClassInfo classInfoInSignature)
        {
            object result = McgComHelpers.ComInterfaceToObjectInternal(pComItf, interfaceTypeInfo, classInfoInSignature, McgComHelpers.CreateComObjectFlags.None);

            //
            // Make sure the type we returned is actually of the right type
            // NOTE: Don't pass null to IsInstanceOfClass as it'll return false
            //
            if (!classInfoInSignature.IsNull && result != null)
            {
                if (!InteropExtensions.IsInstanceOfClass(result, classInfoInSignature.ClassType))
                    throw new InvalidCastException();
            }

            return result;
        }

        public unsafe static IntPtr ComQueryInterfaceNoThrow(IntPtr pComItf, ref Guid iid)
        {
            int hr = 0;
            return ComQueryInterfaceNoThrow(pComItf, ref iid, out hr);
        }

        public unsafe static IntPtr ComQueryInterfaceNoThrow(IntPtr pComItf, ref Guid iid, out int hr)
        {
            IntPtr pComIUnk;
            hr = ComQueryInterfaceWithHR(pComItf, ref iid, out pComIUnk);

            return pComIUnk;
        }

        internal unsafe static int ComQueryInterfaceWithHR(IntPtr pComItf, ref Guid iid, out IntPtr ppv)
        {
            IntPtr pComIUnk;
            int hr;

            fixed (Guid* unsafe_iid = &iid)
            {
                hr = CalliIntrinsics.StdCall__QueryInterface(((__com_IUnknown*)(void*)pComItf)->pVtable->
                                pfnQueryInterface,
                                pComItf,
                                new IntPtr(unsafe_iid),
                                new IntPtr(&pComIUnk));
            }

            if (hr != 0)
            {
                ppv = default(IntPtr);
            }
            else
            {
                ppv = pComIUnk;
            }

            return hr;
        }

        /// <summary>
        /// Helper function to copy vTable to native heap on CoreCLR.
        /// </summary>
        /// <typeparam name="T">Vtbl type</typeparam>
        /// <param name="pVtbl">static v-table field , always a valid pointer</param>
        /// <param name="pNativeVtbl">Pointer to Vtable on native heap on CoreCLR , on N it's an alias for pVtbl</param>
        public static unsafe IntPtr GetCCWVTableCopy(void* pVtbl, ref IntPtr pNativeVtbl, int size)
        {
            if (pNativeVtbl == default(IntPtr))
            {
#if CORECLR
                // On CoreCLR copy vTable to native heap , on N VTable is frozen.
                IntPtr  pv = Marshal.AllocHGlobal(size);

                int* pSrc = (int*)pVtbl;
                int* pDest = (int*)pv.ToPointer();
                int pSize = sizeof(int);

                // this should never happen , if a CCW is discarded we never get here.
                Debug.Assert(size >= pSize);
                for (int i = 0; i < size; i += pSize)
                {
                    *pDest++ = *pSrc++;
                }
                if (Interlocked.CompareExchange(ref pNativeVtbl, pv, default(IntPtr)) != default(IntPtr))
                {
                    // Another thread sneaked-in and updated pNativeVtbl , just use the update from other thread
                    Marshal.FreeHGlobal(pv);
                }
#else  // .NET NATIVE
                // Wrap it in an IntPtr
                pNativeVtbl = (IntPtr)pVtbl;
#endif // CORECLR
            }
            return pNativeVtbl;
        }




        [CLSCompliant(false)]
        public static IntPtr ObjectToComInterface(Object obj, McgTypeInfo typeInfo)
        {
            return McgComHelpers.ObjectToComInterfaceInternal(obj, typeInfo);
        }

        public static IntPtr ObjectToIInspectable(Object obj)
        {
#if ENABLE_WINRT
            return ObjectToComInterface(obj, McgModuleManager.IInspectable);
#else
            throw new PlatformNotSupportedException("ObjectToIInspectable");
#endif
        }

#if ENABLE_WINRT
#if !RHTESTCL
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
#endif
        /// <summary>
        /// Get outer IInspectable for managed object deriving from native scenario
        /// At this point the inner is not created yet - you need the outer first and pass it to the factory
        /// to create the inner
        /// </summary>
        [CLSCompliant(false)]
        public static IntPtr GetOuterIInspectableForManagedObject(__ComObject managedObject)
        {
            ComCallableObject ccw = null;

            try
            {
                //
                // Create the CCW over the RCW
                // Note that they are actually both the same object
                // Base class = inner
                // Derived class = outer
                //
                ccw = new ComCallableObject(
                    managedObject,      // The target object              = managedObject
                    managedObject       // The inner RCW (as __ComObject) = managedObject
                );

                //
                // Retrieve the outer IInspectable
                // Pass skipInterfaceCheck = true to avoid redundant checks
                //
                return ccw.GetComInterfaceForTypeInfo_NoCheck(McgModuleManager.IInspectable);
            }
            finally
            {
                //
                // Free the extra ref count initialized by __native_ccw.Init (to protect the CCW from being collected)
                //
                if (ccw != null)
                    ccw.Release();
            }
        }
#endif

        [CLSCompliant(false)]
        public static unsafe IntPtr ManagedObjectToComInterface(Object obj, McgTypeInfo itfTypeInfo)
        {
            return McgComHelpers.ManagedObjectToComInterface(obj, itfTypeInfo);
        }

        public static unsafe object IInspectableToObject(IntPtr pComItf)
        {
            return ComInterfaceToObject(pComItf, McgModuleManager.IInspectable, McgClassInfo.Null);
        }

        public static unsafe IntPtr CreateInstanceFromApp(Guid clsid)
        {
#if ENABLE_WINRT
            Interop.COM.MULTI_QI results;
            IntPtr pResults = new IntPtr(&results);
            fixed (Guid* pIID = &Interop.COM.IID_IUnknown)
            {
                Guid* pClsid = &clsid;

                results.pIID = new IntPtr(pIID);
                results.pItf = IntPtr.Zero;
                results.hr = 0;
                int hr = ExternalInterop.CoCreateInstanceFromApp(pClsid, IntPtr.Zero, 0x15 /* (CLSCTX_SERVER) */, IntPtr.Zero, 1, pResults);
                if (hr < 0)
                {
                    throw McgMarshal.GetExceptionForHR(hr, /*isWinRTScenario = */ false);
                }
                if (results.hr < 0)
                {
                    throw McgMarshal.GetExceptionForHR(results.hr, /* isWinRTScenario = */ false);
                }
                return results.pItf;
            }
#else
            throw new PlatformNotSupportedException("CreateInstanceFromApp");
#endif
        }

#endregion

#region Testing

        /// <summary>
        /// Internal-only method to allow testing of apartment teardown code
        /// </summary>
        public static void ReleaseRCWsInCurrentApartment()
        {
            ContextEntry.RemoveCurrentContext();
        }

        /// <summary>
        /// Used by detecting leaks
        /// Used in prefast MCG only
        /// </summary>
        public static int GetTotalComObjectCount()
        {
            return ComObjectCache.s_comObjectMap.Count;
        }

        /// <summary>
        /// Used by detecting and dumping leaks
        /// Used in prefast MCG only
        /// </summary>
        public static IEnumerable<__ComObject> GetAllComObjects()
        {
            List<__ComObject> list = new List<__ComObject>();
            for (int i = 0; i < ComObjectCache.s_comObjectMap.GetMaxCount(); ++i)
            {
                IntPtr pHandle = default(IntPtr);
                if (ComObjectCache.s_comObjectMap.GetValue(i, ref pHandle) && (pHandle != default(IntPtr)))
                {
                    GCHandle handle = GCHandle.FromIntPtr(pHandle);
                    list.Add(InteropExtensions.UncheckedCast<__ComObject>(handle.Target));
                }
            }

            return list;
        }

#endregion

        /// <summary>
        /// This method returns HR for the exception being thrown.
        /// 1. On Windows8+, WinRT scenarios we do the following.
        ///      a. Check whether the exception has any IRestrictedErrorInfo associated with it.
        ///          If so, it means that this exception was actually caused by a native exception in which case we do simply use the same
        ///              message and stacktrace.
        ///      b.  If not, this is actually a managed exception and in this case we RoOriginateLanguageException with the msg, hresult and the IErrorInfo
        ///          aasociated with the managed exception. This helps us to retrieve the same exception in case it comes back to native.
        /// 2. On win8 and for classic COM scenarios.
        ///     a. We create IErrorInfo for the given Exception object and SetErrorInfo with the given IErrorInfo.
        /// </summary>
        /// <param name="ex"></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetHRForExceptionWinRT(Exception ex)
        {
#if ENABLE_WINRT
            return ExceptionHelpers.GetHRForExceptionWithErrorPropogationNoThrow(ex, true);
#else
            // TODO : ExceptionHelpers should be platform specific , move it to
            // seperate source files
            return 0;
            //return Marshal.GetHRForException(ex);
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetHRForException(Exception ex)
        {
#if ENABLE_WINRT
            return ExceptionHelpers.GetHRForExceptionWithErrorPropogationNoThrow(ex, false);
#else
            return ex.HResult;
#endif
        }

        /// <summary>
        /// This method returns a new Exception object given the HR value.
        /// </summary>
        /// <param name="hr"></param>
        /// <param name="isWinRTScenario"></param>
        public static Exception GetExceptionForHR(int hr, bool isWinRTScenario)
        {
#if ENABLE_WINRT
            return ExceptionHelpers.GetExceptionForHRInternalNoThrow(hr, isWinRTScenario, !isWinRTScenario);
#else
            return new COMException(hr.ToString());
#endif
        }

        #region Shared templates
#if ENABLE_WINRT
        public static void CleanupNative<T>(IntPtr pObject)
        {
            if (typeof(T) == typeof(string))
            {
                global::System.Runtime.InteropServices.McgMarshal.FreeHString(pObject);
            }
            else
            {
                global::System.Runtime.InteropServices.McgMarshal.ComSafeRelease(pObject);
            }
        }
#endif
        #endregion
    }
}
