// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: ITypeComp
**
**
** Purpose: ITypeComp interface definition.
**
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [Guid("00020403-0000-0000-C000-000000000046")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ITypeComp
    {
        void Bind([MarshalAs(UnmanagedType.LPWStr)] String szName, int lHashVal, Int16 wFlags, out ITypeInfo ppTInfo, out DESCKIND pDescKind, out BINDPTR pBindPtr);
        void BindType([MarshalAs(UnmanagedType.LPWStr)] String szName, int lHashVal, out ITypeInfo ppTInfo, out ITypeComp ppTComp);
    }
}
