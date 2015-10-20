// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// This attribute is added onto types which were injected by the toolchain
    /// as fake representations. This happens if the application code contains invalid references, in such case
    /// the toolchain injects these fake types to be able to compile the application. Hitting these at runtime
    /// causes runtime exceptions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class FakeElementAttribute : Attribute
    {
    }
}
