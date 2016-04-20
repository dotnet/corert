// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Type that is logically equivalent to a type which is defined by a TypeDef
    /// record in an ECMA 335 metadata stream.
    /// </summary>
    public abstract partial class DefType : TypeDesc
    {
        public sealed override TypeKind Variety
        {
            get
            {
                return TypeKind.DefType;
            }
        }
    }
}
