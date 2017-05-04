// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler
{
    public sealed class ILScannerBuilder
    {
        private readonly CompilerTypeSystemContext _context;
        private readonly CompilationModuleGroup _compilationGroup;
        private readonly NameMangler _nameMangler;

        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        private Logger _logger = Logger.Null;
        private DependencyTrackingLevel _dependencyTrackingLevel = DependencyTrackingLevel.None;
        private IEnumerable<ICompilationRootProvider> _compilationRoots = Array.Empty<ICompilationRootProvider>();

        internal ILScannerBuilder(CompilerTypeSystemContext context, CompilationModuleGroup compilationGroup, NameMangler mangler)
        {
            _context = context;
            _compilationGroup = compilationGroup;
            _nameMangler = mangler;
        }

        public ILScannerBuilder UseDependencyTracking(DependencyTrackingLevel trackingLevel)
        {
            _dependencyTrackingLevel = trackingLevel;
            return this;
        }

        public ILScannerBuilder UseCompilationRoots(IEnumerable<ICompilationRootProvider> compilationRoots)
        {
            _compilationRoots = compilationRoots;
            return this;
        }

        private DependencyAnalyzerBase<NodeFactory> CreateDependencyGraph(NodeFactory factory)
        {
            // Choose which dependency graph implementation to use based on the amount of logging requested.
            switch (_dependencyTrackingLevel)
            {
                case DependencyTrackingLevel.None:
                    return new DependencyAnalyzer<NoLogStrategy<NodeFactory>, NodeFactory>(factory, null);

                case DependencyTrackingLevel.First:
                    return new DependencyAnalyzer<FirstMarkLogStrategy<NodeFactory>, NodeFactory>(factory, null);

                case DependencyTrackingLevel.All:
                    return new DependencyAnalyzer<FullGraphLogStrategy<NodeFactory>, NodeFactory>(factory, null);

                default:
                    throw new InvalidOperationException();
            }
        }

        public IILScanner ToILScanner()
        {
            // TODO: we will want different metadata managers depending on whether we're doing reflection analysis
            var metadataManager = new EmptyMetadataManager(_compilationGroup, _context);

            var nodeFactory = new ILScanNodeFactory(_context, _compilationGroup, metadataManager, _nameMangler);
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(nodeFactory);

            return new ILScanner(graph, nodeFactory, _compilationRoots, _logger);
        }
    }
}
