// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using Internal.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public partial class ArrayMethod : MethodDesc
    {
        public override MethodNameAndSignature NameAndSignature
        {
            get
            {
                // TODO Eventually implement via working with a RuntimeMethod that refers to the actual implementation.
                throw NotImplemented.ActiveIssue("https://github.com/dotnet/corert/issues/3772");
            }
        }
    }
}
