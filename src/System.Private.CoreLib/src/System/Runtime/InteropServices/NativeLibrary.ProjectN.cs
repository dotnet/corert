// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

namespace System.Runtime.InteropServices
{
    public static partial class NativeLibrary
    {
        internal static IntPtr LoadLibraryByName(string libraryName, Assembly assembly, DllImportSearchPath? searchPath, bool throwOnError)
        {
            throw new PlatformNotSupportedException();
        }

        internal static IntPtr LoadFromPath(string libraryName, bool throwOnError)
        {
            throw new PlatformNotSupportedException();
        }

        internal static IntPtr LoadByName(string libraryName, Assembly callingAssembly,
                                          bool hasDllImportSearchPathFlag, uint dllImportSearchPathFlag, 
                                          bool throwOnError)
        {
            throw new PlatformNotSupportedException();
        }

        internal static void FreeLib(IntPtr handle)
        {
            throw new PlatformNotSupportedException();
        }

        internal static IntPtr GetSymbol(IntPtr handle, string symbolName, bool throwOnError)
        {
            throw new PlatformNotSupportedException();
        }
    }
}
