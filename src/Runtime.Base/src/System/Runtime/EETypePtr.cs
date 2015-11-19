// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        internal unsafe System.Runtime.EEType* ToPointer()
        {
            return (System.Runtime.EEType*)(void*)_value;
        }
    }
}


