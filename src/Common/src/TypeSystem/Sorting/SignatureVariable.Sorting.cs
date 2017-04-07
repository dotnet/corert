// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.TypeSystem
{
    // Functionality related to determinstic ordering of types
    partial class SignatureVariable
    {
        protected internal sealed override int CompareToImpl(TypeDesc other, TypeSystemComparer comparer)
        {
            // Who needs this and why?
            throw new NotSupportedException();
        }
    }
}
