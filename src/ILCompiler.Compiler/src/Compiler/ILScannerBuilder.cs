﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;

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
        private MetadataManager _metadataManager;

        internal ILScannerBuilder(CompilerTypeSystemContext context, CompilationModuleGroup compilationGroup, NameMangler mangler)
        {
            _context = context;
            _compilationGroup = compilationGroup;
            _nameMangler = mangler;
            _metadataManager = new EmptyMetadataManager(context);
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

        public ILScannerBuilder UseMetadataManager(MetadataManager metadataManager)
        {
            _metadataManager = metadataManager;
            return this;
        }

        public IILScanner ToILScanner()
        {
            var interopStubManager = new CompilerGeneratedInteropStubManager(_compilationGroup, _context, new InteropStateManager(_compilationGroup.GeneratedAssembly));
            var nodeFactory = new ILScanNodeFactory(_context, _compilationGroup, _metadataManager, interopStubManager, _nameMangler);
            DependencyAnalyzerBase<NodeFactory> graph = _dependencyTrackingLevel.CreateDependencyGraph(nodeFactory);

            return new ILScanner(graph, nodeFactory, _compilationRoots, new NullDebugInformationProvider(), _logger);
        }
    }
}
