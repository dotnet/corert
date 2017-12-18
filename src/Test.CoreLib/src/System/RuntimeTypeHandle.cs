// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RuntimeTypeHandle
    {
        private EETypePtr _pEEType;

        internal RuntimeTypeHandle(EETypePtr pEEType)
        {
            _pEEType = pEEType;
        }

        [Intrinsic]
        internal static unsafe IntPtr GetValueInternal(RuntimeTypeHandle handle)
        {
            return (IntPtr)handle._pEEType.ToPointer();
        }
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    // Needed by the compiler to lower LDTOKEN
    internal static class LdTokenHelpers
    {
        private static RuntimeTypeHandle GetRuntimeTypeHandle(IntPtr pEEType)
        {
            return new RuntimeTypeHandle(new EETypePtr(pEEType));
        }
    }
}
