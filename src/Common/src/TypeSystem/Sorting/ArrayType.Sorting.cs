// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    // Functionality related to determinstic ordering of types
    partial class ArrayType
    {
        protected internal override int CompareToImpl(TypeDesc other, TypeSystemComparer comparer)
        {
            var otherType = (ArrayType)other;
            int result = _rank - otherType._rank;
            if (result != 0)
                return result;

            return comparer.Compare(ElementType, otherType.ElementType);
        }
    }
}
