// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

//

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    public enum VARKIND : int
    {
        VAR_PERINSTANCE = 0x0,
        VAR_STATIC = 0x1,
        VAR_CONST = 0x2,
        VAR_DISPATCH = 0x3
    }
}
