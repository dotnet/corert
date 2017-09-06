// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a reference to a symbol attributed with a funclet id.
    /// </summary>
    public interface ISymbolNodeWithFuncletId : ISymbolNode
    {
        ISymbolNode AssociatedMethodSymbol { get; }
        int FuncletId { get; }
    }
}
