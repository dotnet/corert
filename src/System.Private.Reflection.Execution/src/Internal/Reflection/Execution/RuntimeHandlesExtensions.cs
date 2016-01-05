// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
