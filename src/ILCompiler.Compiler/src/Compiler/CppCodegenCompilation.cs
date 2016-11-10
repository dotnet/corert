// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.CppCodeGen;

namespace ILCompiler
{
    public sealed class CppCodegenCompilation : Compilation
    {
        private CppWriter _cppWriter = null;

        internal CppCodegenConfigProvider Options { get; }

        internal CppCodegenCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<CompilationRootProvider> roots,
            Logger logger,
            CppCodegenConfigProvider options)
            : base(dependencyGraph, nodeFactory, roots, new NameMangler(true), logger)
        {
            Options = options;
        }

        protected override void CompileInternal(string outputFile)
        {
            _cppWriter = new CppWriter(this, outputFile);

            var nodes = _dependencyGraph.MarkedNodeList;

            _cppWriter.OutputCode(nodes, NodeFactory);
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (CppMethodCodeNode methodCodeNodeNeedingCode in obj)
            {
                _cppWriter.CompileMethod(methodCodeNodeNeedingCode);
            }
        }
    }
}
