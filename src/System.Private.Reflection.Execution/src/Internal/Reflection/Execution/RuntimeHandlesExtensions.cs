// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Internal.Runtime.Augments;

namespace Internal.Reflection.Execution
{
    internal static class RuntimeHandlesExtensions
    {
        public static bool IsNull(this RuntimeTypeHandle rtth)
        {
            return RuntimeAugments.GetRuntimeTypeHandleRawValue(rtth) == IntPtr.Zero;
        }
    }
}
