// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    public interface IExportableSymbolNode : ISymbolDefinitionNode
    {
        /// <summary>
        /// Set the return value of this property to true to indicate that this symbol
        /// is exported and will be referenced by external modules.
        /// </summary>
        bool IsExported(NodeFactory factory);
    }
}
