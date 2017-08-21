// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler.DependencyAnalysis
{
    public interface IMethodBodyNodeWithFuncletSymbols : IMethodBodyNode
    {
        /// <summary>
        /// Symbols of any funclets associated with this method.
        /// </summary>
        ISymbolNode[] FuncletSymbols { get; }
    }
}
