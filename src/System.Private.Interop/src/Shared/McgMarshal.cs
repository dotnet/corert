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
#elif CORECLR
            return type.GetTypeInfo().IsSubclassOf(typeof(__ComObject));
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

#if  ENABLE_MIN_WINRT
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
                throw new ArgumentNullException("newBuffer");

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
                throw new ArgumentNullException("pNative");

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
                throw new ArgumentNullException("managedArray");

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
                throw new ArgumentNullException("pNative");

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
       
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe HSTRING StringToHString(string sourceString)
        {
            if (sourceString == null)
                throw new ArgumentNullException("sourceString", SR.Null_HString);

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

        public static int FinalReleaseComObject(object o)
        {
            if (o == null)
                throw new ArgumentNullException("o");

            __ComObject co = null;

            // Make sure the obj is an __ComObject.
            try
            {
                co = (__ComObject)o;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(SR.Argument_ObjNotComObject, "o");
            }
            co.FinalReleaseSelf();
            return 0;
        }


        /// <summary>
        /// Returns the cached WinRT factory RCW under the current context
        /// </summary>
        [CLSCompliant(false)]
        public static unsafe __ComObject GetActivationFactory(string className, RuntimeTypeHandle factoryIntf)
        {
#if  ENABLE_MIN_WINRT
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
        public static object ComInterfaceToObject_NoUnboxing(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType)
        {
            return McgComHelpers.ComInterfaceToObjectInternal(
                pComItf, 
                interfaceType, 
                default(RuntimeTypeHandle), 
                McgComHelpers.CreateComObjectFlags.SkipTypeResolutionAndUnboxing
            );
        }

        /// <summary>
        /// Shared CCW Interface To Object
        /// </summary>
        /// <param name="pComItf"></param>
        /// <param name="interfaceType"></param>
        /// <param name="classTypeInSignature"></param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public static object ComInterfaceToObject(
            System.IntPtr pComItf,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSignature)
        {
#if ENABLE_MIN_WINRT
            if (interfaceType.Equals(typeof(object).TypeHandle))
            {
                return McgMarshal.IInspectableToObject(pComItf);
            }

            if (interfaceType.Equals(typeof(System.String).TypeHandle))
            {
                return McgMarshal.HStringToString(pComItf);
            }

            if (interfaceType.IsComClass())
            {
                RuntimeTypeHandle defaultInterface = interfaceType.GetDefaultInterface();
                Debug.Assert(!defaultInterface.IsNull());
                return ComInterfaceToObjectInternal(pComItf, defaultInterface, interfaceType);
            }
#endif
            return ComInterfaceToObjectInternal(
                pComItf,
                interfaceType,
                classTypeInSignature
            );
        }

        [CLSCompliant(false)]
        public static object ComInterfaceToObject(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType)
        {
            return ComInterfaceToObject(pComItf, interfaceType, default(RuntimeTypeHandle));
        }


        private static object ComInterfaceToObjectInternal(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSignature)
        {
            object result = McgComHelpers.ComInterfaceToObjectInternal(pComItf, interfaceType, classTypeInSignature, McgComHelpers.CreateComObjectFlags.None);

            //
            // Make sure the type we returned is actually of the right type
            // NOTE: Don't pass null to IsInstanceOfClass as it'll return false
            //
            if (!classTypeInSignature.IsNull() && result != null)
            {
                if (!InteropExtensions.IsInstanceOfClass(result, classTypeInSignature))
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
        public static IntPtr ObjectToComInterface(
            object obj,
            RuntimeTypeHandle typeHnd)
        {
#if ENABLE_MIN_WINRT
            if (typeHnd.Equals(typeof(object).TypeHandle))
            {
                return McgMarshal.ObjectToIInspectable(obj);
            }

            if (typeHnd.Equals(typeof(System.String).TypeHandle))
            {
                return McgMarshal.StringToHString((string)obj).handle;
            }

            if (typeHnd.IsComClass())
            {
                Debug.Assert(obj == null || obj is __ComObject);
                ///
                /// This code path should be executed only for WinRT classes
                ///
                typeHnd = typeHnd.GetDefaultInterface();
                Debug.Assert(!typeHnd.IsNull());
            }
#endif
            return McgComHelpers.ObjectToComInterfaceInternal(
                obj,
                typeHnd
            );
        }

        public static IntPtr ObjectToIInspectable(Object obj)
        {
#if ENABLE_MIN_WINRT
            return ObjectToComInterface(obj, InternalTypes.IInspectable);
#else
            throw new PlatformNotSupportedException("ObjectToIInspectable");
#endif
        }

        // This is not a safe function to use for any funtion pointers that do not point
        // at a static function. This is due to the behavior of shared generics,
        // where instance function entry points may share the exact same address
        // but static functions are always represented in delegates with customized
        // stubs.
        private static bool DelegateTargetMethodEquals(Delegate del, IntPtr pfn)
        {
            RuntimeTypeHandle thDummy;
            return del.GetFunctionPointer(out thDummy) == pfn;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr DelegateToComInterface(Delegate del, RuntimeTypeHandle typeHnd)
        {
            if (del == null)
                return default(IntPtr);

            IntPtr stubFunctionAddr = typeHnd.GetDelegateInvokeStub();

            object targetObj;

            //
            // If the delegate points to the forward stub for the native delegate,
            // then we want the RCW associated with the native interface.  Otherwise,
            // this is a managed delegate, and we want the CCW associated with it.
            //
            if (DelegateTargetMethodEquals(del, stubFunctionAddr))
                targetObj = del.Target;
            else
                targetObj = del;

            return McgMarshal.ObjectToComInterface(targetObj, typeHnd);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Delegate ComInterfaceToDelegate(IntPtr pComItf, RuntimeTypeHandle typeHnd)
        {
            if (pComItf == default(IntPtr))
                return null;

            object obj = ComInterfaceToObject(pComItf, typeHnd, /* classIndexInSignature */ default(RuntimeTypeHandle));

            //
            // If the object we got back was a managed delegate, then we're good.  Otherwise,
            // the object is an RCW for a native delegate, so we need to wrap it with a managed
            // delegate that invokes the correct stub.
            //
            Delegate del = obj as Delegate;
            if (del == null)
            {
                Debug.Assert(obj is __ComObject);
                IntPtr stubFunctionAddr = typeHnd.GetDelegateInvokeStub();

                del = InteropExtensions.CreateDelegate(
                    typeHnd,
                    stubFunctionAddr,
                    obj,
                    /*isStatic:*/ true,
                    /*isVirtual:*/ false,
                    /*isOpen:*/ false);
            }

            return del;
        }

        /// <summary>
        /// Marshal array of objects
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        unsafe public static void ObjectArrayToComInterfaceArray(uint len, System.IntPtr* dst, object[] src, RuntimeTypeHandle typeHnd)
        {
            for (uint i = 0; i < len; i++)
            {
                dst[i] = McgMarshal.ObjectToComInterface(src[i], typeHnd);
            }
        }

        /// <summary>
        /// Allocate native memory, and then marshal array of objects
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        unsafe public static System.IntPtr* ObjectArrayToComInterfaceArrayAlloc(object[] src, RuntimeTypeHandle typeHnd, out uint len)
        {
            System.IntPtr* dst = null;

            len = 0;

            if (src != null)
            {
                len = (uint)src.Length;

                dst = (System.IntPtr*)ExternalInterop.CoTaskMemAlloc((System.IntPtr)(len * (sizeof(System.IntPtr))));

                for (uint i = 0; i < len; i++)
                {
                    dst[i] = McgMarshal.ObjectToComInterface(src[i], typeHnd);
                }
            }

            return dst;
        }

        /// <summary>
        /// Get outer IInspectable for managed object deriving from native scenario
        /// At this point the inner is not created yet - you need the outer first and pass it to the factory
        /// to create the inner
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
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
                return ccw.GetComInterfaceForType_NoCheck(InternalTypes.IInspectable, ref Interop.COM.IID_IInspectable);
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

        [CLSCompliant(false)]
        public static unsafe IntPtr ManagedObjectToComInterface(Object obj, RuntimeTypeHandle interfaceType)
        {
            return McgComHelpers.ManagedObjectToComInterface(obj, interfaceType);
        }

        public static unsafe object IInspectableToObject(IntPtr pComItf)
        {
#if ENABLE_WINRT
            return ComInterfaceToObject(pComItf, InternalTypes.IInspectable);
#else
            throw new PlatformNotSupportedException("IInspectableToObject");
#endif
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowOnExternalCallFailed(int hr, System.RuntimeTypeHandle typeHnd)
        {
            bool isWinRTScenario
#if ENABLE_WINRT
            = typeHnd.IsWinRTInterface();
#else
            = false;
#endif
            throw McgMarshal.GetExceptionForHR(hr, isWinRTScenario);
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
#elif CORECLR
            return Marshal.GetExceptionForHR(hr);
#else
            return new COMException(hr.ToString(),hr);
#endif
        }

        #region Shared templates
#if  ENABLE_MIN_WINRT
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

#if ENABLE_MIN_WINRT
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static unsafe IntPtr ActivateInstance(string typeName)
        {
            __ComObject target = McgMarshal.GetActivationFactory(
                typeName,
                InternalTypes.IActivationFactoryInternal
            );

            IntPtr pIActivationFactoryInternalItf = target.QueryInterface_NoAddRef_Internal(
                InternalTypes.IActivationFactoryInternal,
                /* cacheOnly= */ false,
                /* throwOnQueryInterfaceFailure= */ true
            );

            __com_IActivationFactoryInternal* pIActivationFactoryInternal = (__com_IActivationFactoryInternal*)pIActivationFactoryInternalItf;

            IntPtr pResult = default(IntPtr);

            int hr = CalliIntrinsics.StdCall<int>(
                pIActivationFactoryInternal->pVtable->pfnActivateInstance,
                pIActivationFactoryInternal,
                &pResult
            );

            GC.KeepAlive(target);

            if (hr < 0)
            {
                throw McgMarshal.GetExceptionForHR(hr, /* isWinRTScenario = */ true);
            }

            return pResult;
        }
#endif

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr GetInterface(
            __ComObject obj,
            RuntimeTypeHandle typeHnd)
        {
            return obj.QueryInterface_NoAddRef_Internal(
                typeHnd);
        }     

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static object GetDynamicAdapter(__ComObject obj, RuntimeTypeHandle requestedType, RuntimeTypeHandle existingType)
        {
            return obj.GetDynamicAdapter(requestedType, existingType);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static object GetDynamicAdapter(__ComObject obj, RuntimeTypeHandle requestedType)
        {
            return obj.GetDynamicAdapter(requestedType,default(RuntimeTypeHandle));
        }
        
        #region "PInvoke Delegate"

#if !CORECLR
        private static class AsmCode
        {
            private const MethodImplOptions InternalCall = (MethodImplOptions)0x1000;

            [MethodImplAttribute(InternalCall)]
            [RuntimeImport("*", "InteropNative_GetCurrentThunkContext")]
            public static extern IntPtr GetCurrentInteropThunkContext();

            [MethodImplAttribute(InternalCall)]

            [RuntimeImport("*", "InteropNative_GetCommonStubAddress")]

            public static extern IntPtr GetInteropCommonStubAddress();
        }
#endif

        public static IntPtr GetStubForPInvokeDelegate(RuntimeTypeHandle delegateType, Delegate dele)
        {
            return GetStubForPInvokeDelegate(dele);
        }

        /// <summary>
        /// Return the stub to the pinvoke marshalling stub
        /// </summary>
        /// <param name="del">The delegate</param>
        static internal IntPtr GetStubForPInvokeDelegate(Delegate del)
        {
            if (del == null)
                return IntPtr.Zero;

            NativeFunctionPointerWrapper fpWrapper = del.Target as NativeFunctionPointerWrapper;
            if (fpWrapper != null)
            {
                //
                // Marshalling a delegate created from native function pointer back into function pointer
                // This is easy - just return the 'wrapped' native function pointer
                //
                return fpWrapper.NativeFunctionPointer;
            }
            else
            {
                //
                // Marshalling a managed delegate created from managed code into a native function pointer
                //
                return GetOrAllocateThunk(del);
            }
        }
        /// <summary>
        /// Used to lookup whether a delegate already has an entry
        /// </summary>
        private static System.Collections.Generic.Internal.HashSet<EquatablePInvokeDelegateThunk> s_pInvokeDelegateThunkHashSet;

        static Collections.Generic.Internal.HashSet<EquatablePInvokeDelegateThunk> GetDelegateThunkHashSet()
        {
            //
            // Create the hashset on-demand to avoid the dependency in the McgModule.ctor
            // Otherwise NUTC will complain that McgModule being eager ctor depends on a deferred
            // ctor type
            //
            if (s_pInvokeDelegateThunkHashSet == null)
            {
                const int DefaultSize = 101; // small prime number to avoid resizing in start up code

                Interlocked.CompareExchange(
                    ref s_pInvokeDelegateThunkHashSet,
                    new System.Collections.Generic.Internal.HashSet<EquatablePInvokeDelegateThunk>(DefaultSize, true),
                    null
                );
            }

            return s_pInvokeDelegateThunkHashSet;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal unsafe struct ThunkContextData
        {
            public GCHandle Handle;        //  A weak GCHandle to the delegate
            public IntPtr FunctionPtr;     // Function pointer for open static delegates
        };

        internal class EquatablePInvokeDelegateThunk : IEquatable<EquatablePInvokeDelegateThunk>
        {
            internal IntPtr Thunk;       //  Thunk pointer
            internal GCHandle Handle;    //  A weak GCHandle to the delegate
            internal IntPtr ContextData;     //   ThunkContextData pointer which will be stored in the context slot of the thunk
            internal int HashCode;
            internal EquatablePInvokeDelegateThunk(Delegate del, IntPtr pThunk)
            {
                // if it is an open static delegate get the function pointer
                IntPtr functionPtr  = del.GetRawFunctionPointerForOpenStaticDelegate();

                //
                // Allocate a weak GC handle pointing to the delegate
                // Whenever the delegate dies, we'll know next time when we recycle thunks
                //
                Handle = GCHandle.Alloc(del, GCHandleType.Weak);
                Thunk = pThunk;
                HashCode = GetHashCodeOfDelegate(del);

                ThunkContextData context;
                context.Handle = Handle;
                context.FunctionPtr = functionPtr;

                //
                // Allocate unmanaged memory for GCHandle of delegate and function pointer of open static delegate
                // We will store this pointer on the context slot of thunk data
                //
                ContextData = Marshal.AllocHGlobal(2*IntPtr.Size);
                unsafe
                {
                    ThunkContextData* thunkData = (ThunkContextData*)ContextData;

                    (*thunkData).Handle = context.Handle;
                    (*thunkData).FunctionPtr = context.FunctionPtr;
                }
            }

            ~EquatablePInvokeDelegateThunk()
            {
                Handle.Free();
                Marshal.FreeHGlobal(ContextData);
            }

            public static int GetHashCodeOfDelegate(Delegate del)
            {
                return RuntimeHelpers.GetHashCode(del);
            }

            public bool Equals(EquatablePInvokeDelegateThunk other)
            {
                return Thunk == other.Thunk;
            }

            public bool Equals(Delegate del)
            {
                return (Object.ReferenceEquals(del, Handle.Target));
            }

            public override int GetHashCode()
            {
                return HashCode;
            }

            public override bool Equals(Object obj)
            {
                // If parameter is null return false.
                if (obj == null)
                {
                    return false;
                }

                // If parameter cannot be cast to EquatablePInvokeDelegateThunk return false.
                EquatablePInvokeDelegateThunk other = obj as EquatablePInvokeDelegateThunk;
                if ((Object)other == null)
                {
                    return false;
                }

                // Return true if the thunks match
                return Thunk == other.Thunk;
            }

        }

        const int THUNK_RECYCLING_FREQUENCY = 200;                                                  // Every 200 thunks that we allocate, do a round of cleanup

#if ENABLE_WINRT
        private static int s_numInteropThunksAllocatedSinceLastCleanup = 0;
#endif

        static private IntPtr GetOrAllocateThunk(Delegate del)
        {
#if ENABLE_WINRT
            System.Collections.Generic.Internal.HashSet<EquatablePInvokeDelegateThunk> delegateHashSet = GetDelegateThunkHashSet();
            try
            {
                delegateHashSet.LockAcquire();

                EquatablePInvokeDelegateThunk key = null;
                int hashCode = EquatablePInvokeDelegateThunk.GetHashCodeOfDelegate(del);
                for (int entry = delegateHashSet.FindFirstKey(ref key, hashCode); entry >= 0; entry = delegateHashSet.FindNextKey(ref key, entry))
                {
                    if (key.Equals(del))
                        return key.Thunk;
                }
                
                IntPtr commonStubAddress = AsmCode.GetInteropCommonStubAddress();
                //
                // Keep allocating thunks until we reach the recycling frequency - we have a virtually unlimited
                // number of thunks that we can allocate (until we run out of virtual address space), but we
                // still need to cleanup thunks that are no longer being used, to avoid leaking memory.
                // This is helpful to detect bugs where user are calling into thunks whose delegate are already
                // collected. In desktop CLR, they'll simple AV, while in .NET Native, there is a good chance we'll
                // detect the delegate instance is NULL (by looking at the GCHandle in the map) and throw out a
                // good exception
                //
                if (s_numInteropThunksAllocatedSinceLastCleanup == THUNK_RECYCLING_FREQUENCY)
                {
                    //
                    // Cleanup the thunks that were previously allocated and are no longer in use to avoid memory leaks
                    //

                    GC.Collect();

                    foreach (EquatablePInvokeDelegateThunk delegateThunk in delegateHashSet.Keys)
                    {
                        // if the delegate has already been collected free the thunk and remove the entry from the hashset
                        if (delegateThunk.Handle.Target == null)
                        {
                            ThunkPool.FreeThunk(commonStubAddress, delegateThunk.Thunk);
                            bool removed = delegateHashSet.Remove(delegateThunk, delegateThunk.HashCode);
                            if (!removed)
                                Environment.FailFast("Inconsistency in delegate map");
                        }
                    }
                    s_numInteropThunksAllocatedSinceLastCleanup = 0;
                }


                IntPtr pThunk = ThunkPool.AllocateThunk(commonStubAddress);

                if (pThunk == IntPtr.Zero)
                {
                    // We've either run out of memory, or failed to allocate a new thunk due to some other bug. Now we should fail fast
                    Environment.FailFast("Insufficient number of thunks.");
                    return IntPtr.Zero;
                }
                else
                {
                    McgPInvokeDelegateData pinvokeDelegateData;
                    McgModuleManager.GetPInvokeDelegateData(del.GetTypeHandle(), out pinvokeDelegateData);

                    s_numInteropThunksAllocatedSinceLastCleanup++;

                    
                    EquatablePInvokeDelegateThunk delegateThunk = new EquatablePInvokeDelegateThunk(del, pThunk);
                    
                    delegateHashSet.Add(delegateThunk , delegateThunk.HashCode);
                    
                    //
                    //  For open static delegates set target to ReverseOpenStaticDelegateStub which calls the static function pointer directly
                    //
                    IntPtr pTarget =  del.GetRawFunctionPointerForOpenStaticDelegate()  == IntPtr.Zero  ?  pinvokeDelegateData.ReverseStub : pinvokeDelegateData.ReverseOpenStaticDelegateStub;
                    
                    ThunkPool.SetThunkData(pThunk, delegateThunk.ContextData, pTarget);

                    return pThunk;
                }
            }
            finally
            {
                delegateHashSet.LockRelease();
            }
#else
            throw new PlatformNotSupportedException("GetOrAllocateThunk");
#endif
        }

        /// <summary>
        /// Retrieve the corresponding P/invoke instance from the stub
        /// </summary>
        static public Delegate GetPInvokeDelegateForStub(IntPtr pStub, RuntimeTypeHandle delegateType)
        {
            if (pStub == IntPtr.Zero)
                return null;
#if ENABLE_WINRT
            //
            // First try to see if this is one of the thunks we've allocated when we marshal a managed
            // delegate to native code
            //
            IntPtr pContext;
            IntPtr pTarget;
            if (ThunkPool.TryGetThunkData(AsmCode.GetInteropCommonStubAddress(), pStub, out pContext, out pTarget))
            {
                GCHandle handle;
                unsafe
                {
                    // Pull out Handle from context
                    handle = (*((ThunkContextData*)pContext)).Handle;
                }
                Delegate target = InteropExtensions.UncheckedCast<Delegate>(handle.Target);

                //
                // The delegate might already been garbage collected
                // User should use GC.KeepAlive or whatever ways necessary to keep the delegate alive
                // until they are done with the native function pointer
                //
                if (target == null)
                {
                    Environment.FailFast(
                        "The corresponding delegate has been garbage collected. " +
                        "Please make sure the delegate is still referenced by managed code when you are using the marshalled native function pointer."
                    );
                }

                return target;
            }
#endif
            //
            // Otherwise, the stub must be a pure native function pointer
            // We need to create the delegate that points to the invoke method of a
            // NativeFunctionPointerWrapper derived class
            //
            McgPInvokeDelegateData pInvokeDelegateData;
            if (!McgModuleManager.GetPInvokeDelegateData(delegateType, out pInvokeDelegateData))
            {
                return null;
            }

            return CalliIntrinsics.Call__Delegate(
                pInvokeDelegateData.ForwardDelegateCreationStub,
                pStub
            );
        }

        /// <summary>
        /// Retrieves the function pointer for the current open static delegate that is being called
        /// </summary>
        static public IntPtr GetCurrentCalleeOpenStaticDelegateFunctionPointer()
        {
#if RHTESTCL || CORECLR
            throw new NotSupportedException();
#else
            //
            // RH keeps track of the current thunk that is being called through a secret argument / thread
            // statics. No matter how that's implemented, we get the current thunk which we can use for
            // look up later
            //
            IntPtr pContext = AsmCode.GetCurrentInteropThunkContext();
            Debug.Assert(pContext != null);

            IntPtr fnPtr;
            unsafe
            {
                // Pull out function pointer for open static delegate
                fnPtr = (*((ThunkContextData*)pContext)).FunctionPtr;
            }
            Debug.Assert(fnPtr != null);

            return fnPtr;
#endif
        }

        /// <summary>
        /// Retrieves the current delegate that is being called
        /// </summary>
        static public T GetCurrentCalleeDelegate<T>() where T : class // constraint can't be System.Delegate
        {
#if RHTESTCL || CORECLR
            throw new NotSupportedException();
#else
            //
            // RH keeps track of the current thunk that is being called through a secret argument / thread
            // statics. No matter how that's implemented, we get the current thunk which we can use for
            // look up later
            //
            IntPtr pContext = AsmCode.GetCurrentInteropThunkContext();

            Debug.Assert(pContext != null);

            GCHandle handle;
            unsafe
            {
                // Pull out Handle from context
                handle = (*((ThunkContextData*)pContext)).Handle;

            }

            T target = InteropExtensions.UncheckedCast<T>(handle.Target);

            //
            // The delegate might already been garbage collected
            // User should use GC.KeepAlive or whatever ways necessary to keep the delegate alive
            // until they are done with the native function pointer
            //
            if (target == null)
            {
                Environment.FailFast(
                    "The corresponding delegate has been garbage collected. " +
                    "Please make sure the delegate is still referenced by managed code when you are using the marshalled native function pointer."
                );
            }
            return target;

#endif
        }
#endregion
    }

    /// <summary>
    /// McgMarshal helpers exposed to be used by MCG
    /// </summary>
    public static partial class McgMarshal
    {
        public static object UnboxIfBoxed(object target)
        {
            return UnboxIfBoxed(target, null);
        }

        public static object UnboxIfBoxed(object target, string className)
        {
            //
            // If it is a managed wrapper, unbox it
            //
            object unboxedObj = McgComHelpers.UnboxManagedWrapperIfBoxed(target);
            if (unboxedObj != target)
                return unboxedObj;

            if (className == null)
                className = System.Runtime.InteropServices.McgComHelpers.GetRuntimeClassName(target);

            if (!String.IsNullOrEmpty(className))
            {
                IntPtr unboxingStub;
                if (McgModuleManager.TryGetUnboxingStub(className, out unboxingStub))
                {
                    object ret = CalliIntrinsics.Call<object>(unboxingStub, target);

                    if (ret != null)
                        return ret;
                }
            }
            return null;
        }

        internal static object BoxIfBoxable(object target)
        {
            return BoxIfBoxable(target, default(RuntimeTypeHandle));
        }

        /// <summary>
        /// Given a boxed value type, return a wrapper supports the IReference interface
        /// </summary>
        /// <param name="typeHandleOverride">
        /// You might want to specify how to box this. For example, any object[] derived array could
        /// potentially boxed as object[] if everything else fails
        /// </param>
        internal static object BoxIfBoxable(object target, RuntimeTypeHandle typeHandleOverride)
        {
            RuntimeTypeHandle expectedTypeHandle = typeHandleOverride;
            if (expectedTypeHandle.Equals(default(RuntimeTypeHandle)))
                expectedTypeHandle = target.GetTypeHandle();

            RuntimeTypeHandle boxingWrapperType;
            IntPtr boxingStub;
            int boxingPropertyType;
            if (McgModuleManager.TryGetBoxingWrapperType(expectedTypeHandle, target is Type, out boxingWrapperType, out boxingPropertyType,out boxingStub))
            {
                if(!boxingWrapperType.IsInvalid())
                {
                    //
                    // IReference<T> / IReferenceArray<T> / IKeyValuePair<K, V>
                    // All these scenarios require a managed wrapper
                    //

                    // Allocate the object
                    object refImplType = InteropExtensions.RuntimeNewObject(boxingWrapperType);

                    if (boxingPropertyType >= 0)
                    {
                        Debug.Assert(refImplType is BoxedValue);

                        BoxedValue boxed = InteropExtensions.UncheckedCast<BoxedValue>(refImplType);

                        // Call ReferenceImpl<T>.Initialize(obj, type);
                        boxed.Initialize(target, boxingPropertyType);
                    }
                    else
                    {
                        Debug.Assert(refImplType is BoxedKeyValuePair);

                        BoxedKeyValuePair boxed = InteropExtensions.UncheckedCast<BoxedKeyValuePair>(refImplType);

                        // IKeyValuePair<,>,   call CLRIKeyValuePairImpl<K,V>.Initialize(object obj);
                        // IKeyValuePair<,>[], call CLRIKeyValuePairArrayImpl<K,V>.Initialize(object obj);
                        refImplType = boxed.Initialize(target);
                    }

                    return refImplType;
                }
                else
                {
                    //
                    // General boxing for projected types, such as System.Uri
                    //
                    return CalliIntrinsics.Call<object>(boxingStub, target);
                }
            }

            return null;
        }
    }
}
