// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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

#if !PROJECTN
        // This does not work on ProjectN (with no fallback) because Runtime.Base doesn't have enough infrastructure
        // to let us express typeof(T).TypeHandle.ToEETypePtr().
        [Intrinsic]
        internal static EETypePtr EETypePtrOf<T>()
        {
            throw new NotImplementedException();
        }
#endif

        internal unsafe uint BaseSize
        {
            get
            {
                return ToPointer()->BaseSize;
            }
        }
    }
}


