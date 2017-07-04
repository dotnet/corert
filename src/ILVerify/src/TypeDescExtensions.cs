// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Internal.TypeSystem
{
    public static class TypeDescExtensions
    {
        public static TypeDesc ResolveSignatureVariable(this TypeDesc t, MethodDesc method)
        {
            if (t.IsSignatureVariable)
            {
                SignatureVariable sigVar = t as SignatureVariable;
                if (sigVar.IsMethodSignatureVariable)
                    return method.Instantiation[sigVar.Index];
                else
                    return method.OwningType.Instantiation[sigVar.Index];
            }
            return t;
        }
    }
}
