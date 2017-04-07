// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler
{
    // Functionality related to determinstic ordering of types and members
    partial class CompilerTypeSystemContext
    {
        partial class BoxedValueType
        {
            protected override int CompareToImpl(TypeDesc other, TypeSystemComparer comparer)
            {
                return comparer.Compare(ValueTypeRepresented, ((BoxedValueType)other).ValueTypeRepresented);
            }
        }
    }
}
