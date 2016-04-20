// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    /// <summary>
    /// Represents the kinds of types that can occur within the type system.
    /// </summary>
    public enum TypeKind
    {
        DefType,
        ByRef,
        Pointer,
        SzArray,
        Array,
        GenericParameter,
    }
}
