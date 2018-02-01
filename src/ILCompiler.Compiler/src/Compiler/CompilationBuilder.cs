// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler
{
    public abstract class CompilationBuilder
    {
        protected readonly CompilerTypeSystemContext _context;
        protected readonly CompilationModuleGroup _compilationGroup;
        protected readonly NameMangler _nameMangler;

        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        protected Logger _logger = Logger.Null;
        private DependencyTrackingLevel _dependencyTrackingLevel = DependencyTrackingLevel.None;
        protected IEnumerable<ICompilationRootProvider> _compilationRoots = Array.Empty<ICompilationRootProvider>();
        protected OptimizationMode _optimizationMode = OptimizationMode.None;
        protected MetadataManager _metadataManager;
        protected VTableSliceProvider _vtableSliceProvider = new LazyVTableSliceProvider();
        protected DictionaryLayoutProvider _dictionaryLayoutProvider = new LazyDictionaryLayoutProvider();
        protected DebugInformationProvider _debugInformationProvider = new DebugInformationProvider();
        protected DevirtualizationManager _devirtualizationManager = new DevirtualizationManager();

        public CompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup compilationGroup, NameMangler nameMangler)
        {
            _context = context;
            _compilationGroup = compilationGroup;
            _nameMangler = nameMangler;
            _metadataManager = new EmptyMetadataManager(context);
        }

        public CompilationBuilder UseLogger(Logger logger)
        {
            _logger = logger;
            return this;
        }

        public CompilationBuilder UseDependencyTracking(DependencyTrackingLevel trackingLevel)
        {
            _dependencyTrackingLevel = trackingLevel;
            return this;
        }

        public CompilationBuilder UseMetadataManager(MetadataManager metadataManager)
        {
            _metadataManager = metadataManager;
            return this;
        }

        public CompilationBuilder UseCompilationRoots(IEnumerable<ICompilationRootProvider> compilationRoots)
        {
            _compilationRoots = compilationRoots;
            return this;
        }

        public CompilationBuilder UseOptimizationMode(OptimizationMode mode)
        {
            _optimizationMode = mode;
            return this;
        }

        public CompilationBuilder UseVTableSliceProvider(VTableSliceProvider provider)
        {
            _vtableSliceProvider = provider;
            return this;
        }

        public CompilationBuilder UseGenericDictionaryLayoutProvider(DictionaryLayoutProvider provider)
        {
            _dictionaryLayoutProvider = provider;
            return this;
        }

        public CompilationBuilder UseDevirtualizationManager(DevirtualizationManager manager)
        {
            _devirtualizationManager = manager;
            return this;
        }

        public CompilationBuilder UseDebugInfoProvider(DebugInformationProvider provider)
        {
            _debugInformationProvider = provider;
            return this;
        }

        public abstract CompilationBuilder UseBackendOptions(IEnumerable<string> options);

        protected DependencyAnalyzerBase<NodeFactory> CreateDependencyGraph(NodeFactory factory, IComparer<DependencyNodeCore<NodeFactory>> comparer = null)
        {
            return _dependencyTrackingLevel.CreateDependencyGraph(factory, comparer);
        }

        public ILScannerBuilder GetILScannerBuilder(CompilationModuleGroup compilationGroup = null)
        {
            return new ILScannerBuilder(_context, compilationGroup ?? _compilationGroup, _nameMangler);
        }

        public abstract ICompilation ToCompilation();
    }

    /// <summary>
    /// Represents the level of optimizations performed by the compiler.
    /// </summary>
    public enum OptimizationMode
    {
        /// <summary>
        /// Do not optimize.
        /// </summary>
        None,

        /// <summary>
        /// Minimize code space.
        /// </summary>
        PreferSize,

        /// <summary>
        /// Generate blended code. (E.g. favor size for rarely executed code such as class constructors.)
        /// </summary>
        Blended,

        /// <summary>
        /// Maximize execution speed.
        /// </summary>
        PreferSpeed,
    }
}
