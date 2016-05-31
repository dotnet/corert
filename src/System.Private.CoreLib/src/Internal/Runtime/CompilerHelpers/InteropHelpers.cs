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

            fixed (char * pStr = str)
            {
                int stringLength = str.Length;
                int bufferLength = encoding.GetByteCount(pStr, stringLength);
                var buffer = new byte[bufferLength + 1];
                fixed (byte * pBuffer = buffer)
                {
                    encoding.GetBytes(pStr, stringLength, pBuffer, bufferLength);
                    return buffer;
                }
            }
        }

        internal static char[] GetEmptyStringBuilderBuffer(StringBuilder sb)
        {
            // CORERT-TODO: Reuse buffer from string builder where possible?
            return new char[sb.Capacity + 1];
        }

        internal static unsafe void ReplaceStringBuilderBuffer(StringBuilder sb, char[] buf)
        {
            // CORERT-TODO: Reuse buffer from string builder where possible?
            fixed (char* p = buf)
                sb.ReplaceBuffer(p);
        }

        internal unsafe static IntPtr ResolvePInvoke(MethodFixupCell* pCell)
        {
            if (pCell->Target != IntPtr.Zero)
                return pCell->Target;

            return ResolvePInvokeSlow(pCell);
        }

        internal unsafe static IntPtr ResolvePInvokeSlow(MethodFixupCell *pCell)
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

        internal unsafe static void FixupModuleCell(ModuleFixupCell *pCell)
        {
#if !PLATFORM_UNIX
            char* moduleName = (char*)pCell->ModuleName;

            IntPtr hModule = Interop.mincore.LoadLibraryEx(moduleName, IntPtr.Zero, 0);
            if (hModule != IntPtr.Zero)
            {
                var oldValue = Interlocked.CompareExchange(ref pCell->Handle, hModule, IntPtr.Zero);
                if (oldValue != IntPtr.Zero)
                {
                    // Some other thread won the race to fix it up.
                    Interop.mincore.FreeLibrary(hModule);
                }
            }
            else
            {
                // TODO: should be DllNotFoundException, but layering...
                throw new TypeLoadException(new string(moduleName));
            }
#else
            byte* moduleName = (byte*)pCell->ModuleName;

            IntPtr hModule = Interop.Sys.LoadLibrary(moduleName);
            if (hModule != IntPtr.Zero)
            {
                var oldValue = Interlocked.CompareExchange(ref pCell->Handle, hModule, IntPtr.Zero);
                if (oldValue != IntPtr.Zero)
                {
                    // Some other thread won the race to fix it up.
                    Interop.Sys.FreeLibrary(hModule);
                }
            }
            else
            {
                // TODO: should be DllNotFoundException, but layering...
                throw new TypeLoadException(Encoding.UTF8.GetString(moduleName, strlen(moduleName)));
            }
#endif
        }

        internal unsafe static void FixupMethodCell(IntPtr hModule, MethodFixupCell *pCell)
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

        internal unsafe static int strlen(byte* pString)
        {
            int length = 0;
            for (; *pString != 0; pString++)
                length++;
            return length;
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
