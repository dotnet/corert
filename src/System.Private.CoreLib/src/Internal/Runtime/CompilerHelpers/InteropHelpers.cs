// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;

using Internal.Runtime.Augments;

#pragma warning disable SA1121 // explicitly using type aliases instead of built-in types
#if TARGET_64BIT
using nint = System.Int64;
using nuint = System.UInt64;
#else
using nint = System.Int32;
using nuint = System.UInt32;
#endif

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

        internal static unsafe void FreeLibrary(IntPtr hModule)
        {
#if !TARGET_UNIX
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

            uint dllImportSearchPath = 0;
            bool hasDllImportSearchPath = (pCell->DllImportSearchPathAndCookie & InteropDataConstants.HasDllImportSearchPath) != 0;
            if (hasDllImportSearchPath)
            {
                dllImportSearchPath = pCell->DllImportSearchPathAndCookie & ~InteropDataConstants.HasDllImportSearchPath;
            }

            Assembly callingAssembly = RuntimeAugments.Callbacks.GetAssemblyForHandle(new RuntimeTypeHandle(pCell->CallingAssemblyType));

            // First check if there's a NativeLibrary callback and call it to attempt the resolution
            IntPtr hModule = NativeLibrary.LoadLibraryCallbackStub(moduleName, callingAssembly, hasDllImportSearchPath, dllImportSearchPath);
            if (hModule == IntPtr.Zero)
            {
                // NativeLibrary callback didn't resolve the library. Use built-in rules.
                NativeLibrary.LoadLibErrorTracker loadLibErrorTracker = default;

                hModule = NativeLibrary.LoadBySearch(
                    callingAssembly,
                    searchAssemblyDirectory: false,
                    dllImportSearchPathFlags: 0,
                    ref loadLibErrorTracker,
                    moduleName);

                if (hModule == IntPtr.Zero)
                {
                    // Built-in rules didn't resolve the library. Use AssemblyLoadContext as a last chance attempt.
                    AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(callingAssembly);
                    hModule = loadContext.GetResolvedUnmanagedDll(callingAssembly, moduleName);
                }

                if (hModule == IntPtr.Zero)
                {
                    // If the module is still unresolved, this is an error.
                    loadLibErrorTracker.Throw(moduleName);
                }
            }

            Debug.Assert(hModule != IntPtr.Zero);
            var oldValue = Interlocked.CompareExchange(ref pCell->Handle, hModule, IntPtr.Zero);
            if (oldValue != IntPtr.Zero)
            {
                // Some other thread won the race to fix it up.
                FreeLibrary(hModule);
            }
        }

        internal static unsafe void FixupMethodCell(IntPtr hModule, MethodFixupCell* pCell)
        {
            byte* methodName = (byte*)pCell->MethodName;
            IntPtr pTarget;

#if TARGET_WINDOWS
            CharSet charSetMangling = pCell->CharSetMangling;
            if (charSetMangling == 0)
            {
                // Look for the user-provided entry point name only
                pTarget = Interop.mincore.GetProcAddress(hModule, methodName);
            }
            else
            if (charSetMangling == CharSet.Ansi)
            {
                // For ANSI, look for the user-provided entry point name first.
                // If that does not exist, try the charset suffix.
                pTarget = Interop.mincore.GetProcAddress(hModule, methodName);
                if (pTarget == IntPtr.Zero)
                    pTarget = GetProcAddressWithSuffix(hModule, methodName, (byte)'A');
            }
            else
            {
                // For Unicode, look for the entry point name with the charset suffix first.
                // The 'W' API takes precedence over the undecorated one.
                pTarget = GetProcAddressWithSuffix(hModule, methodName, (byte)'W');
                if (pTarget == IntPtr.Zero)
                    pTarget = Interop.mincore.GetProcAddress(hModule, methodName);
            }
#else
            pTarget = Interop.Sys.GetProcAddress(hModule, methodName);
#endif
            if (pTarget == IntPtr.Zero)
            {
                string entryPointName = Encoding.UTF8.GetString(methodName, string.strlen(methodName));
                throw new EntryPointNotFoundException(SR.Format(SR.Arg_EntryPointNotFoundExceptionParameterized, entryPointName, GetModuleName(pCell->Module)));
            }

            pCell->Target = pTarget;
        }

#if TARGET_WINDOWS
        private static unsafe IntPtr GetProcAddressWithSuffix(IntPtr hModule, byte* methodName, byte suffix)
        {
            int nameLength = string.strlen(methodName);

            // We need to add an extra byte for the suffix, and an extra byte for the null terminator
            byte* probedMethodName = stackalloc byte[nameLength + 2];

            for (int i = 0; i < nameLength; i++)
            {
                probedMethodName[i] = methodName[i];
            }

            probedMethodName[nameLength + 1] = 0;

            probedMethodName[nameLength] = suffix;

            return Interop.mincore.GetProcAddress(hModule, probedMethodName);
        }
#endif

        internal unsafe static void* CoTaskMemAllocAndZeroMemory(global::System.IntPtr size)
        {
            void* ptr;
            ptr = PInvokeMarshal.CoTaskMemAlloc((UIntPtr)(void*)size).ToPointer();

            // PInvokeMarshal.CoTaskMemAlloc will throw OOMException if out of memory
            Debug.Assert(ptr != null);

            Buffer.ZeroMemory((byte*)ptr, (nuint)(nint)size);
            return ptr;
        }

        internal unsafe static void CoTaskMemFree(void* p)
        {
            PInvokeMarshal.CoTaskMemFree((IntPtr)p);
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

        public static IntPtr ConvertManagedComInterfaceToNative(object pUnk)
        {
            if (pUnk == null)
            {
                return IntPtr.Zero;
            }

            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object ConvertNativeComInterfaceToManaged(IntPtr pUnk)
        {
            if (pUnk == IntPtr.Zero)
            {
                return null;
            }

            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        internal static int AsAnyGetNativeSize(object o)
        {
            // Array, string and StringBuilder are not implemented.
            if (o.EETypePtr.IsArray ||
                o is string ||
                o is StringBuilder)
            {
                throw new PlatformNotSupportedException();
            }

            // Assume that this is a type with layout.
            return Marshal.SizeOf(o.GetType());
        }

        internal static void AsAnyMarshalManagedToNative(object o, IntPtr address)
        {
            // Array, string and StringBuilder are not implemented.
            if (o.EETypePtr.IsArray ||
                o is string ||
                o is StringBuilder)
            {
                throw new PlatformNotSupportedException();
            }

            Marshal.StructureToPtr(o, address, fDeleteOld: false);
        }

        internal static void AsAnyMarshalNativeToManaged(IntPtr address, object o)
        {
            // Array, string and StringBuilder are not implemented.
            if (o.EETypePtr.IsArray ||
                o is string ||
                o is StringBuilder)
            {
                throw new PlatformNotSupportedException();
            }

            Marshal.PtrToStructureImpl(address, o);
        }

        internal static void AsAnyCleanupNative(IntPtr address, object o)
        {
            // Array, string and StringBuilder are not implemented.
            if (o.EETypePtr.IsArray ||
                o is string ||
                o is StringBuilder)
            {
                throw new PlatformNotSupportedException();
            }

            Marshal.DestroyStructure(address, o.GetType());
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct ModuleFixupCell
        {
            public IntPtr Handle;
            public IntPtr ModuleName;
            public EETypePtr CallingAssemblyType;
            public uint DllImportSearchPathAndCookie;
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
