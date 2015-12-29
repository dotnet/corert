// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: ICustomQueryInterface
**
**
** Purpose: This the interface that be implemented by class that want to
**          customize the behavior of QueryInterface.
**
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    //====================================================================
    // The interface for customizing IQueryInterface
    //====================================================================
    public interface ICustomQueryInterface
    {
        CustomQueryInterfaceResult GetInterface([In]ref Guid iid, out IntPtr ppv);
    }
}
