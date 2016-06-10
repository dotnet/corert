// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
/*============================================================
**
** Class:  EETypePtr
**
**
** Purpose: Pointer Type to a EEType in the runtime.
**
** 
===========================================================*/

namespace System
{
    // This type does not implement GetHashCode but implements Equals
#pragma warning disable 0659

    [StructLayout(LayoutKind.Sequential)]
    public struct EETypePtr
    {
        private IntPtr _value;

        internal EETypePtr(IntPtr value)
        {
            _value = value;
        }

        internal bool Equals(EETypePtr p)
        {
            return (_value == p._value);
        }

        internal unsafe Internal.Runtime.EEType* ToPointer()
        {
            return (Internal.Runtime.EEType*)(void*)_value;
        }
    }
}


