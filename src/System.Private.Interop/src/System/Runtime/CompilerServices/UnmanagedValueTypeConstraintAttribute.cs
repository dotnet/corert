// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
