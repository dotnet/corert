// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Reflection
{
    public abstract partial class TypeInfo : Type, IReflectableType
    {
        // TODO https://github.com/dotnet/corefx/issues/9805: These are inherited from Type and shouldn't need to be redeclared on TypeInfo but 
        //   they are a well-known methods to the reducer.

        public override abstract Assembly Assembly { get; }
        public override abstract Type BaseType { get; }
        public override Type MakeGenericType(params Type[] typeArguments) => base.MakeGenericType(typeArguments);
    }
}
