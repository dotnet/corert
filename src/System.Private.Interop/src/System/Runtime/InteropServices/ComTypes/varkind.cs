// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
