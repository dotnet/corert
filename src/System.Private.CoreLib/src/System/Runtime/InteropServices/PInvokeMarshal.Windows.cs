// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        internal static void ClearLastWin32Error()
        {
            Interop.mincore.SetLastError(0);
        }

        internal static unsafe IntPtr MemAlloc(IntPtr cb)
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

        internal static IntPtr CoTaskMemAlloc(UIntPtr bytes)
        {
            return Interop.mincore.CoTaskMemAlloc(bytes);
        }

        internal static void CoTaskMemFree(IntPtr allocatedMemory)
        {
            if (IsNotWin32Atom(allocatedMemory))
            {
                Interop.mincore.CoTaskMemFree(allocatedMemory);
            }
        }

        public static IntPtr SecureStringToBSTR(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            return s.MarshalToBSTR();
        }
    }
}
