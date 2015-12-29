// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    ///     This attribute is applied by the system C# compiler on generic type arguments that are using
    ///     the "unmanaged struct" constraint.
    /// </summary>
    internal sealed class UnmanagedValueTypeConstraintAttribute : Attribute
    {
    }
}
