// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;

namespace ILCompiler
{
    public sealed class WebAssemblyCodegenCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        WebAssemblyCodegenConfigProvider _config = new WebAssemblyCodegenConfigProvider(Array.Empty<string>());

        public WebAssemblyCodegenCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(context, group, new CoreRTNameMangler(new WebAssemblyNodeMangler(), false))
        {
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            _config = new WebAssemblyCodegenConfigProvider(options);
            return this;
        }

        public override ICompilation ToCompilation()
        {
            var interopStubManager = new CompilerGeneratedInteropStubManager(_compilationGroup, _context, new InteropStateManager(_context.GeneratedAssembly));
            WebAssemblyCodegenNodeFactory factory = new WebAssemblyCodegenNodeFactory(_context, _compilationGroup, _metadataManager, interopStubManager, _nameMangler, _vtableSliceProvider, _dictionaryLayoutProvider);
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(factory, new ObjectNode.ObjectNodeComparer(new CompilerComparer()));
            return new WebAssemblyCodegenCompilation(graph, factory, _compilationRoots, _debugInformationProvider, _logger, _config);
        }
    }

    internal class WebAssemblyCodegenConfigProvider
    {
        private readonly HashSet<string> _options;
        
        public const string NoLineNumbersString = "NoLineNumbers";

        public WebAssemblyCodegenConfigProvider(IEnumerable<string> options)
        {
            _options = new HashSet<string>(options, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasOption(string optionName)
        {
            return _options.Contains(optionName);
        }
    }
}
