// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// A dependency analysis node that represents a runtime type data structure.
    /// </summary>
    public interface IEETypeNode : ISortableSymbolNode
    {
        TypeDesc Type { get; }
    }
}
