// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
// ---------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text;
using System.Runtime;
using Internal.NativeFormat;
using Internal.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    [ComImport]
    [Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr pUnkOuter, Guid* riid, IntPtr* ppvObject);

        [PreserveSig]
        int LockServer (int fLock);
    }
}