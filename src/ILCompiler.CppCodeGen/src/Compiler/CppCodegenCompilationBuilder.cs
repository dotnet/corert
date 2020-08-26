// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler
{
    public sealed class CppCodegenCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        CppCodegenConfigProvider _config = new CppCodegenConfigProvider(Array.Empty<string>());
        private ILProvider _ilProvider = new CoreRTILProvider();

        public CppCodegenCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(context, group, new CoreRTNameMangler(new CppNodeMangler(), true))
        {
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            _config = new CppCodegenConfigProvider(options);
            return this;
        }

        public override CompilationBuilder UseILProvider(ILProvider ilProvider)
        {
            _ilProvider = ilProvider;
            return this;
        }

        protected override ILProvider GetILProvider()
        {
            return _ilProvider;
        }

        public override ICompilation ToCompilation()
        {
            CppCodegenNodeFactory factory = new CppCodegenNodeFactory(_context, _compilationGroup, _metadataManager, _interopStubManager, _nameMangler, _vtableSliceProvider, _dictionaryLayoutProvider, GetPreinitializationManager());
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(factory);

            return new CppCodegenCompilation(graph, factory, _compilationRoots, _ilProvider, _debugInformationProvider, _logger, _config);
        }
    }

    internal class CppCodegenConfigProvider
    {
        private readonly HashSet<string> _options;
        
        public const string NoLineNumbersString = "NoLineNumbers";

        public CppCodegenConfigProvider(IEnumerable<string> options)
        {
            _options = new HashSet<string>(options, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasOption(string optionName)
        {
            return _options.Contains(optionName);
        }
    }
}
