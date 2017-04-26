// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Interlocked = System.Threading.Interlocked;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to throw exceptions from generated code.
    /// </summary>
    internal static class InteropHelpers
    {
        internal static unsafe byte* StringToAnsiString(String str, bool bestFit, bool throwOnUnmappableChar)
        {
            return PInvokeMarshal.StringToAnsiString(str, bestFit, throwOnUnmappableChar);
        }

        public static unsafe string AnsiStringToString(byte* buffer)
        {
            return PInvokeMarshal.AnsiStringToString(buffer);
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

        internal static unsafe void StringToUnicodeFixedArray(String str, UInt16* buffer, int length)
        {
            if (buffer == null)
                return;

            Debug.Assert(str.Length >= length);

            fixed (char* pStr = str)
            {
                int size = length * sizeof(char);
                Buffer.MemoryCopy(pStr, buffer, size, size);
                *(buffer + length) = 0;
            }
        }

        internal static unsafe string UnicodeToStringFixedArray(UInt16* buffer, int length)
        {
            if (buffer == null)
                return String.Empty;

            string result = String.Empty;

            if (length > 0)
            {
                result = new String(' ', length);

                fixed (char* pTemp = result)
                {
                    int size = length * sizeof(char);
                    Buffer.MemoryCopy(buffer, pTemp, size, size);
                }
            }
            return result;
        }

        internal static unsafe char* StringToUnicodeBuffer(String str)
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
            return new String(buffer);
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

        internal static unsafe IntPtr ResolvePInvoke(MethodFixupCell* pCell)
        {
            if (pCell->Target != IntPtr.Zero)
                return pCell->Target;

            return ResolvePInvokeSlow(pCell);
        }

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

        internal static unsafe void FixupModuleCell(ModuleFixupCell* pCell)
        {
            byte* pModuleName = (byte*)pCell->ModuleName;
            string moduleName = Encoding.UTF8.GetString(pModuleName, strlen(pModuleName));

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
                // TODO: should be DllNotFoundException, but layering...
                throw new TypeLoadException(moduleName);
            }
        }

        internal static unsafe void FixupMethodCell(IntPtr hModule, MethodFixupCell* pCell)
        {
            byte* methodName = (byte*)pCell->MethodName;

#if !PLATFORM_UNIX
            pCell->Target = Interop.mincore.GetProcAddress(hModule, methodName);
#else
            pCell->Target = Interop.Sys.GetProcAddress(hModule, methodName);
#endif
            if (pCell->Target == IntPtr.Zero)
            {
                // TODO: Shoud be EntryPointNotFoundException, but layering...
                throw new TypeLoadException(Encoding.UTF8.GetString(methodName, strlen(methodName)));
            }
        }

        internal static unsafe int strlen(byte* pString)
        {
            byte* p = pString;
            while (*p != 0) p++;
            return checked((int)(p - pString));
        }

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
        /// <summary>
        /// Returns the stub to the pinvoke marshalling stub
        /// </summary>
        public static IntPtr GetStubForPInvokeDelegate(Delegate del)
        {
            return PInvokeMarshal.GetStubForPInvokeDelegate(del);
        }

        /// <summary>
        /// Retrieve the corresponding P/invoke instance from the stub
        /// </summary>
        public static Delegate GetPInvokeDelegateForStub(IntPtr pStub, RuntimeTypeHandle delegateType)
        {
            return PInvokeMarshal.GetPInvokeDelegateForStub(pStub, delegateType);
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
        }
    }
}
