// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    static class RuntimeAugments
    {
        [Intrinsic]
        public static object ConvertIntPtrToObjectReference(IntPtr pointerToObject)
        {
            return ConvertIntPtrToObjectReference(pointerToObject);
        }
    }
}
