// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem.Interop
{
    // Functionality related to determinstic ordering of types
    partial class InlineArrayType
    {
        protected internal override int CompareToImpl(TypeDesc other, TypeSystemComparer comparer)
        {
            var otherType = (InlineArrayType)other;
            int result = (int)Length - (int)otherType.Length;
            if (result != 0)
                return result;

            return comparer.Compare(ElementType, otherType.ElementType);
        }
    }
}
