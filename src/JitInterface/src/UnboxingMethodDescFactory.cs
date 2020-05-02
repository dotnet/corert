// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.TypeSystem;

namespace Internal.JitInterface
{
    internal class UnboxingMethodDescFactory : Dictionary<MethodDesc, UnboxingMethodDesc>
    {
        public UnboxingMethodDesc GetUnboxingMethod(MethodDesc method)
        {
            if (!TryGetValue(method, out UnboxingMethodDesc result))
            {
                result = new UnboxingMethodDesc(method, this);
                Add(method, result);
            }

            return result;
        }
    }
}
