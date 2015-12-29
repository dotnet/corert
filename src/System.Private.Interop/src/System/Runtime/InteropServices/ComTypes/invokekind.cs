// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
