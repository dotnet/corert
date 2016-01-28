// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    public enum TYPEKIND
    {
        TKIND_ENUM = 0,
        TKIND_RECORD = TKIND_ENUM + 1,
        TKIND_MODULE = TKIND_RECORD + 1,
        TKIND_INTERFACE = TKIND_MODULE + 1,
        TKIND_DISPATCH = TKIND_INTERFACE + 1,
        TKIND_COCLASS = TKIND_DISPATCH + 1,
        TKIND_ALIAS = TKIND_COCLASS + 1,
        TKIND_UNION = TKIND_ALIAS + 1,
        TKIND_MAX = TKIND_UNION + 1
    }
}
