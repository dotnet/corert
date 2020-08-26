// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using Internal.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public sealed partial class MethodForRuntimeDeterminedType : MethodDesc
    {
        public override MethodNameAndSignature NameAndSignature
        {
            get
            {
                return _typicalMethodDef.NameAndSignature;
            }
        }
    }
}
