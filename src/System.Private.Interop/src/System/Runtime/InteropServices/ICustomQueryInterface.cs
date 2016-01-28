// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
