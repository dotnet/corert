// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to throw exceptions from generated code.
    /// </summary>
    internal static class InteropHelpers
    {
        internal static unsafe byte* StringToAnsiString(string str, bool bestFit, bool throwOnUnmappableChar)
        {
            return PInvokeMarshal.StringToAnsiString(str, bestFit, throwOnUnmappableChar);
        }

        public static unsafe string AnsiStringToString(byte* buffer)
        {
            return PInvokeMarshal.AnsiStringToString(buffer);
        }

        internal static unsafe byte* StringToUTF8String(string str)
        {
            if (str == null)
                return null;

            fixed (char* charsPtr = str)
            {
                int length = Encoding.UTF8.GetByteCount(str) + 1;
                byte* bytesPtr = (byte*)PInvokeMarshal.CoTaskMemAlloc((System.UIntPtr)length);
                int bytes = Encoding.UTF8.GetBytes(charsPtr, str.Length, bytesPtr, length);
                Debug.Assert(bytes + 1 == length);
                bytesPtr[length - 1] = 0;
                return bytesPtr;
            }
        }

        public static unsafe string UTF8StringToString(byte* buffer)
        {
            if (buffer == null)
                return null;

            return Encoding.UTF8.GetString(buffer, string.strlen(buffer));
        }

        internal static unsafe void StringToByValAnsiString(string str, byte* pNative, int charCount, bool bestFit, bool throwOnUnmappableChar)
        {
            // In CoreRT charCount = Min(SizeConst, str.Length). So we don't need to truncate again.
            PInvokeMarshal.StringToByValAnsiString(str, pNative, charCount, bestFit, throwOnUnmappableChar, truncate: false);
        }

        public static unsafe string ByValAnsiStringToString(byte* buffer, int length)
        {
            return PInvokeMarshal.ByValAnsiStringToString(buffer, length);
        }

        internal static unsafe void StringToUnicodeFixedArray(string str, ushort* buffer, int length)
        {
            if (buffer == null)
                return;

            if (str == null)
            {
                buffer[0] = '\0';
                return;
            }

            Debug.Assert(str.Length >= length);

            fixed (char* pStr = str)
            {
                int size = length * sizeof(char);
                Buffer.MemoryCopy(pStr, buffer, size, size);
                *(buffer + length) = 0;
            }
        }

        internal static unsafe string UnicodeToStringFixedArray(ushort* buffer, int length)
        {
            if (buffer == null)
                return string.Empty;

            string result = string.Empty;

            if (length > 0)
            {
                result = new string(' ', length);

                fixed (char* pTemp = result)
                {
                    int size = length * sizeof(char);
                    Buffer.MemoryCopy(buffer, pTemp, size, size);
                }
            }
            return result;
        }

        internal static unsafe char* StringToUnicodeBuffer(string str)
        {
            if (str == null)
                return null;

            int stringLength = str.Length;

            char* buffer = (char*)PInvokeMarshal.CoTaskMemAlloc((UIntPtr)(sizeof(char) * (stringLength + 1))).ToPointer();

            fixed (char* pStr = str)
            {
                int size = stringLength * sizeof(char);
                Buffer.MemoryCopy(pStr, buffer, size, size);
                *(buffer + stringLength) = '\0';
            }
            return buffer;
        }

        public static unsafe string UnicodeBufferToString(char* buffer)
        {
            return new string(buffer);
        }

        public static unsafe byte* AllocMemoryForAnsiStringBuilder(StringBuilder sb)
        {
            if (sb == null)
            {
                return null;
            }
            return (byte *)CoTaskMemAllocAndZeroMemory(new IntPtr(checked((sb.Capacity + 2) * PInvokeMarshal.GetSystemMaxDBCSCharSize())));
        }

        public static unsafe char* AllocMemoryForUnicodeStringBuilder(StringBuilder sb)
        {
            if (sb == null)
            {
                return null;
            }
            return (char *)CoTaskMemAllocAndZeroMemory(new IntPtr(checked((sb.Capacity + 2) * 2)));
        }

        public static unsafe byte* AllocMemoryForAnsiCharArray(char[] chArray)
        {
            if (chArray == null)
            {
                return null;
            }
            return (byte*)CoTaskMemAllocAndZeroMemory(new IntPtr(checked((chArray.Length + 2) * PInvokeMarshal.GetSystemMaxDBCSCharSize())));
        }

        public static unsafe void AnsiStringToStringBuilder(byte* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
                return;

            PInvokeMarshal.AnsiStringToStringBuilder(newBuffer, stringBuilder);
        }

        public static unsafe void UnicodeStringToStringBuilder(ushort* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
                return;

            PInvokeMarshal.UnicodeStringToStringBuilder(newBuffer, stringBuilder);
        }

        public static unsafe void StringBuilderToAnsiString(System.Text.StringBuilder stringBuilder, byte* pNative,
            bool bestFit, bool throwOnUnmappableChar)
        {
            if (pNative == null)
                return;

            PInvokeMarshal.StringBuilderToAnsiString(stringBuilder, pNative, bestFit, throwOnUnmappableChar);
        }

        public static unsafe void StringBuilderToUnicodeString(System.Text.StringBuilder stringBuilder, ushort* destination)
        {
            if (destination == null)
                return;

            PInvokeMarshal.StringBuilderToUnicodeString(stringBuilder, destination);
        }

        public static unsafe void WideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, bool bestFit, bool throwOnUnmappableChar)
        {
            PInvokeMarshal.WideCharArrayToAnsiCharArray(managedArray, pNative, bestFit, throwOnUnmappableChar);
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
        public static unsafe void AnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            PInvokeMarshal.AnsiCharArrayToWideCharArray(pNative, managedArray);
        }

        /// <summary>
        /// Convert a single UNICODE wide char to a single ANSI byte.
        /// </summary>
        /// <param name="managedArray">single UNICODE wide char value</param>
        public static unsafe byte WideCharToAnsiChar(char managedValue, bool bestFit, bool throwOnUnmappableChar)
        {
            return PInvokeMarshal.WideCharToAnsiChar(managedValue, bestFit, throwOnUnmappableChar);
        }

        /// <summary>
        /// Convert a single ANSI byte value to a single UNICODE wide char value, best fit.
        /// </summary>
        /// <param name="nativeValue">Single ANSI byte value.</param>
        public static unsafe char AnsiCharToWideChar(byte nativeValue)
        {
            return PInvokeMarshal.AnsiCharToWideChar(nativeValue);
        }

        internal static unsafe IntPtr ResolvePInvoke(MethodFixupCell* pCell)
        {
            if (pCell->Target != IntPtr.Zero)
                return pCell->Target;

            return ResolvePInvokeSlow(pCell);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static unsafe IntPtr ResolvePInvokeSlow(MethodFixupCell* pCell)
        {
            ModuleFixupCell* pModuleCell = pCell->Module;
            IntPtr hModule = pModuleCell->Handle;
            if (hModule == IntPtr.Zero)
            {
                FixupModuleCell(pModuleCell);
                hModule = pModuleCell->Handle;
            }

            FixupMethodCell(hModule, pCell);
            return pCell->Target;
        }

        internal static unsafe IntPtr TryResolveModule(string moduleName)
        {
            IntPtr hModule = IntPtr.Zero;

            // Try original name first
            hModule = LoadLibrary(moduleName);
            if (hModule != IntPtr.Zero) return hModule;

#if PLATFORM_UNIX
            const string PAL_SHLIB_PREFIX = "lib";
#if PLATFORM_OSX
            const string PAL_SHLIB_SUFFIX = ".dylib";
#else
            const string PAL_SHLIB_SUFFIX = ".so";
#endif

             // Try prefix+name+suffix
            hModule = LoadLibrary(PAL_SHLIB_PREFIX + moduleName + PAL_SHLIB_SUFFIX);
            if (hModule != IntPtr.Zero) return hModule;

            // Try name+suffix
            hModule = LoadLibrary(moduleName + PAL_SHLIB_SUFFIX);
            if (hModule != IntPtr.Zero) return hModule;

            // Try prefix+name
            hModule = LoadLibrary(PAL_SHLIB_PREFIX + moduleName);
            if (hModule != IntPtr.Zero) return hModule;
#endif
            return IntPtr.Zero;
        }

        internal static unsafe IntPtr LoadLibrary(string moduleName)
        {
            IntPtr hModule;

#if !PLATFORM_UNIX
            hModule = Interop.mincore.LoadLibraryEx(moduleName, IntPtr.Zero, 0);
#else
            hModule = Interop.Sys.LoadLibrary(moduleName);
#endif

            return hModule;
        }

        internal static unsafe void FreeLibrary(IntPtr hModule)
        {
#if !PLATFORM_UNIX
            Interop.mincore.FreeLibrary(hModule);
#else
            Interop.Sys.FreeLibrary(hModule);
#endif
        }

        private static unsafe string GetModuleName(ModuleFixupCell* pCell)
        {
            byte* pModuleName = (byte*)pCell->ModuleName;
            return Encoding.UTF8.GetString(pModuleName, string.strlen(pModuleName));
        }

        internal static unsafe void FixupModuleCell(ModuleFixupCell* pCell)
        {
            string moduleName = GetModuleName(pCell);
            IntPtr hModule = TryResolveModule(moduleName);
            if (hModule != IntPtr.Zero)
            {
                var oldValue = Interlocked.CompareExchange(ref pCell->Handle, hModule, IntPtr.Zero);
                if (oldValue != IntPtr.Zero)
                {
                    // Some other thread won the race to fix it up.
                    FreeLibrary(hModule);
                }
            }
            else
            {
                throw new DllNotFoundException(SR.Format(SR.Arg_DllNotFoundExceptionParameterized, moduleName));
            }
        }

        internal static unsafe void FixupMethodCell(IntPtr hModule, MethodFixupCell* pCell)
        {
            byte* methodName = (byte*)pCell->MethodName;

#if PLATFORM_WINDOWS
            pCell->Target = GetProcAddress(hModule, methodName, pCell->CharSetMangling);
#else
            pCell->Target = Interop.Sys.GetProcAddress(hModule, methodName);
#endif
            if (pCell->Target == IntPtr.Zero)
            {
                string entryPointName = Encoding.UTF8.GetString(methodName, string.strlen(methodName));
                throw new EntryPointNotFoundException(SR.Format(SR.Arg_EntryPointNotFoundExceptionParameterized, entryPointName, GetModuleName(pCell->Module)));
            }
        }

#if PLATFORM_WINDOWS
        private static unsafe IntPtr GetProcAddress(IntPtr hModule, byte* methodName, CharSet charSetMangling)
        {
            // First look for the unmangled name.  If it is unicode function, we are going
            // to need to check for the 'W' API because it takes precedence over the
            // unmangled one (on NT some APIs have unmangled ANSI exports).
            
            var exactMatch = Interop.mincore.GetProcAddress(hModule, methodName);

            if ((charSetMangling == CharSet.Ansi && exactMatch != IntPtr.Zero) || charSetMangling == 0)
            {
                return exactMatch;
            }

            int nameLength = string.strlen(methodName);

            // We need to add an extra byte for the suffix, and an extra byte for the null terminator
            byte* probedMethodName = stackalloc byte[nameLength + 2];

            for (int i = 0; i < nameLength; i++)
            {
                probedMethodName[i] = methodName[i];
            }

            probedMethodName[nameLength + 1] = 0;

            probedMethodName[nameLength] = (charSetMangling == CharSet.Ansi) ? (byte)'A' : (byte)'W';

            IntPtr probedMethod = Interop.mincore.GetProcAddress(hModule, probedMethodName);
            if (probedMethod != IntPtr.Zero)
            {
                return probedMethod;
            }

            return exactMatch;
        }
#endif

        internal unsafe static void* CoTaskMemAllocAndZeroMemory(global::System.IntPtr size)
        {
            void* ptr;
            ptr = PInvokeMarshal.CoTaskMemAlloc((UIntPtr)(void*)size).ToPointer();

            // PInvokeMarshal.CoTaskMemAlloc will throw OOMException if out of memory
            Debug.Assert(ptr != null);

            Buffer.ZeroMemory((byte*)ptr, size.ToInt64());
            return ptr;
        }

        internal unsafe static void CoTaskMemFree(void* p)
        {
            PInvokeMarshal.CoTaskMemFree((IntPtr)p);
        }

        public static IntPtr GetFunctionPointerForDelegate(Delegate del)
        {
            return PInvokeMarshal.GetFunctionPointerForDelegate(del);
        }

        public static Delegate GetDelegateForFunctionPointer(IntPtr ptr, RuntimeTypeHandle delegateType)
        {
            return PInvokeMarshal.GetDelegateForFunctionPointer(ptr, delegateType);
        }

        /// <summary>
        /// Retrieves the function pointer for the current open static delegate that is being called
        /// </summary>
        public static IntPtr GetCurrentCalleeOpenStaticDelegateFunctionPointer()
        {
            return PInvokeMarshal.GetCurrentCalleeOpenStaticDelegateFunctionPointer();
        }

        /// <summary>
        /// Retrieves the current delegate that is being called
        /// </summary>
        public static T GetCurrentCalleeDelegate<T>() where T : class // constraint can't be System.Delegate
        {
            return PInvokeMarshal.GetCurrentCalleeDelegate<T>();
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct ModuleFixupCell
        {
            public IntPtr Handle;
            public IntPtr ModuleName;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct MethodFixupCell
        {
            public IntPtr Target;
            public IntPtr MethodName;
            public ModuleFixupCell* Module;
            public CharSet CharSetMangling;
        }
    }
}
