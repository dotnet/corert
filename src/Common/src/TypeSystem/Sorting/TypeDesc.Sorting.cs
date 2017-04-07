// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    // Functionality related to determinstic ordering of types and members
    partial class TypeDesc
    {
        // Note to implementers: the type of `other` is actually the same as the type of `this`.
        protected internal abstract int CompareToImpl(TypeDesc other, TypeSystemComparer comparer);
    }
}
