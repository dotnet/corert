// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    public enum DESCKIND
    {
        DESCKIND_NONE = 0,
        DESCKIND_FUNCDESC = DESCKIND_NONE + 1,
        DESCKIND_VARDESC = DESCKIND_FUNCDESC + 1,
        DESCKIND_TYPECOMP = DESCKIND_VARDESC + 1,
        DESCKIND_IMPLICITAPPOBJ = DESCKIND_TYPECOMP + 1,
        DESCKIND_MAX = DESCKIND_IMPLICITAPPOBJ + 1
    }
}
