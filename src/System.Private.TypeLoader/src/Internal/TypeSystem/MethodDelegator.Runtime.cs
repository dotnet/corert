// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    partial class MethodDelegator
    {
        public override MethodNameAndSignature NameAndSignature => _wrappedMethod.NameAndSignature;

        public override bool IsNonSharableMethod => _wrappedMethod.IsNonSharableMethod;

        public override bool UnboxingStub => _wrappedMethod.UnboxingStub;
    }
}
