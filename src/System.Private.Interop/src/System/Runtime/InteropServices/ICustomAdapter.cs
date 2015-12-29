// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: ICustomAdapter
**
**
** Purpose: This the base interface that custom adapters can chose to implement
**          when they want to expose the underlying object.
**
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    public interface ICustomAdapter
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object GetUnderlyingObject();
    }
}
