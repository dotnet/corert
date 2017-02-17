// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Runtime.InteropServices;

using Interlocked = System.Threading.Interlocked;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to throw exceptions from generated code.
    /// </summary>
    internal static class InteropHelpers
    {
        internal static unsafe byte[] StringToAnsi(String str)
        {
            if (str == null)
                return null;

            // CORERT-TODO: Use same encoding as the rest of the interop
            var encoding = Encoding.UTF8;

            fixed (char* pStr = str)
            {
                int stringLength = str.Length;
                int bufferLength = encoding.GetByteCount(pStr, stringLength);
                var buffer = new byte[bufferLength + 1];
                fixed (byte* pBuffer = &buffer[0])
                {
                    encoding.GetBytes(pStr, stringLength, pBuffer, bufferLength);
                    return buffer;
                }
            }
        }

        public static unsafe string AnsiStringToString(byte* buffer)
        {
            if (buffer == null)
                return null;

            int length = strlen(buffer);
            string result = String.Empty;

            if (length > 0)
            {
                result = new String(' ', length);

                fixed (char* pTemp = result)
                {
                    // TODO: support ansi semantics in windows
                    Encoding.UTF8.GetChars(buffer, length, pTemp, length);
                }
            }
            return result;
        }

        internal static char[] GetEmptyStringBuilderBuffer(StringBuilder sb)
        {
            // CORERT-TODO: Reuse buffer from string builder where possible?
            return new char[sb.Capacity + 1];
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
            byte *pModuleName = (byte *)pCell->ModuleName;
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
            System.Diagnostics.Debug.Assert(ptr != null);

            Buffer.ZeroMemory((byte*)ptr, size.ToInt64());
            return ptr;
        }

        internal unsafe static void CoTaskMemFree(void* p)
        {
            PInvokeMarshal.CoTaskMemFree((IntPtr)p);
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
