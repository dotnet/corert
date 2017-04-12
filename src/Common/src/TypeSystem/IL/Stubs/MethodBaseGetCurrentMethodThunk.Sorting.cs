// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace Internal.IL.Stubs
{
    // Functionality related to deterministic ordering of types
    partial class MethodBaseGetCurrentMethodThunk
    {
        protected override int ClassCode => 1889524798;

        protected override int CompareToImpl(MethodDesc other, TypeSystemComparer comparer)
        {
            var otherMethod = (MethodBaseGetCurrentMethodThunk)other;
            return comparer.Compare(Method, otherMethod.Method);
        }
    }
}
