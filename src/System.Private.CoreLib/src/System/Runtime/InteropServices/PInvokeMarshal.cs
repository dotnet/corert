// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;
using System.Security;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// This PInvokeMarshal class should provide full public Marshal 
    /// implementation for all things related to P/Invoke marshalling
    /// </summary>
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

        public static unsafe IntPtr AllocHGlobal(IntPtr cb)
        {
            return MemAlloc(cb);
        }

        public static unsafe IntPtr AllocHGlobal(int cb)
        {
            return AllocHGlobal((IntPtr)cb);
        }

        public static void FreeHGlobal(IntPtr hglobal)
        {
            MemFree(hglobal);
        }

        public static unsafe IntPtr AllocCoTaskMem(int cb)
        {
            IntPtr allocatedMemory = CoTaskMemAlloc(new UIntPtr(unchecked((uint)cb)));
            if (allocatedMemory == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }
            return allocatedMemory;
        }

        public static void FreeCoTaskMem(IntPtr ptr)
        {
            CoTaskMemFree(ptr);
        }

        public static IntPtr SecureStringToGlobalAllocAnsi(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            Contract.EndContractBlock();

            return s.MarshalToString(globalAlloc: true, unicode: false);
        }

        public static IntPtr SecureStringToGlobalAllocUnicode(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            Contract.EndContractBlock();

            return s.MarshalToString(globalAlloc: true, unicode: true); ;
        }

        public static IntPtr SecureStringToCoTaskMemAnsi(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            Contract.EndContractBlock();

            return s.MarshalToString(globalAlloc: false, unicode: false);
        }

        public static IntPtr SecureStringToCoTaskMemUnicode(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            Contract.EndContractBlock();

            return s.MarshalToString(globalAlloc: false, unicode: true);
        }
    }
}
