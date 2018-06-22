// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.JitInterface;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler
{
    public sealed class ReadyToRunCodegenCompilation : Compilation
    {
        private JitConfigProvider _jitConfigProvider;
        string _inputFilePath;

        public new ReadyToRunCodegenNodeFactory NodeFactory { get; }
        internal ReadyToRunCodegenCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            ReadyToRunCodegenNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            Logger logger,
            JitConfigProvider configProvider,
            string inputFilePath)
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), null, null, logger)
        {
            NodeFactory = nodeFactory;
            _jitConfigProvider = configProvider;
            _inputFilePath = inputFilePath;
        }

        private static IEnumerable<ICompilationRootProvider> GetCompilationRoots(IEnumerable<ICompilationRootProvider> existingRoots, NodeFactory factory)
        {
            // Todo: Eventually this should return an interesting set of roots, such as the ready-to-run header
            yield break;
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _dependencyGraph.ComputeMarkedNodes();

            var nodes = _dependencyGraph.MarkedNodeList;

            ReadyToRunObjectWriter.EmitObject(_inputFilePath, outputFile, nodes, NodeFactory);
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            // Prevent the CoreRT NodeFactory adding various tables that aren't needed for ready-to-run images.
            // Ie, module header, metadata table, various type system tables.
        }
    }
}
