// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// System.Type and System.RuntimeTypeHandle are defined here as the C# compiler requires them
// In the redhawk runtime these are not used. In the class library there is an implementation that support typeof

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    internal class Type
    {
        public RuntimeTypeHandle TypeHandle { get { return default(RuntimeTypeHandle); } }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RuntimeTypeHandle
    {
        private EETypePtr _pEEType;

#if CORERT
        [Intrinsic]
        internal unsafe static IntPtr GetValueInternal(RuntimeTypeHandle handle)
        {
            return (IntPtr)handle._pEEType.ToPointer();
        }
#endif
    }
}
