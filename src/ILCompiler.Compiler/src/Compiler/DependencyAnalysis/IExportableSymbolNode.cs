// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    public interface IExportableSymbolNode : ISymbolDefinitionNode
    {
        /// <summary>
        /// Set the return value of this property to non-ExportForm.None to indicate that this symbol
        /// is exported and will be referenced by external modules. The values of the enum indicate what form
        /// of export is to be used.
        /// </summary>
        ExportForm GetExportForm(NodeFactory factory);
    }
}
