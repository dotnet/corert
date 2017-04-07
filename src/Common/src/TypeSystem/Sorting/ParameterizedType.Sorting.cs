// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // Functionality related to determinstic ordering of types
    partial class ParameterizedType
    {
        protected internal override int CompareToImpl(TypeDesc other, TypeSystemComparer comparer)
        {
            Debug.Assert(GetType() == other.GetType());
            Debug.Assert(!IsArray);
            return comparer.Compare(ParameterType, ((ParameterizedType)other).ParameterType);
        }
    }
}
