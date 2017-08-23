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
    public sealed class CppCodegenCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        CppCodegenConfigProvider _config = new CppCodegenConfigProvider(Array.Empty<string>());

        public CppCodegenCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(context, group, new CoreRTNameMangler(new CppNodeMangler(), true))
        {
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            _config = new CppCodegenConfigProvider(options);
            return this;
        }

        public override ICompilation ToCompilation()
        {
            var interopStubManager = new CompilerGeneratedInteropStubManager(_compilationGroup, _context, new InteropStateManager(_compilationGroup.GeneratedAssembly));
            CppCodegenNodeFactory factory = new CppCodegenNodeFactory(_context, _compilationGroup, _metadataManager, interopStubManager, _nameMangler, _vtableSliceProvider, _dictionaryLayoutProvider);
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(factory);

            return new CppCodegenCompilation(graph, factory, _compilationRoots, _debugInformationProvider, _logger, _config);
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
