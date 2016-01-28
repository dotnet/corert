// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [Flags]
    public enum INVOKEKIND : int
    {
        INVOKE_FUNC = 0x1,
        INVOKE_PROPERTYGET = 0x2,
        INVOKE_PROPERTYPUT = 0x4,
        INVOKE_PROPERTYPUTREF = 0x8
    }
}
