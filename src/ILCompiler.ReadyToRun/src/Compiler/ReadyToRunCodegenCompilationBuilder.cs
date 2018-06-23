// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.JitInterface;
using Internal.TypeSystem;

namespace ILCompiler
{
    public sealed class ReadyToRunCodegenCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        private KeyValuePair<string, string>[] _ryujitOptions = Array.Empty<KeyValuePair<string, string>>();
        string _inputFilePath;

        public ReadyToRunCodegenCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group, string inputFilePath)
            : base(context, group, new CoreRTNameMangler(new ReadyToRunNodeMangler(), false))
        {
            _inputFilePath = inputFilePath;
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            var builder = new ArrayBuilder<KeyValuePair<string, string>>();

            foreach (string param in options)
            {
                int indexOfEquals = param.IndexOf('=');

                // We're skipping bad parameters without reporting.
                // This is not a mainstream feature that would need to be friendly.
                // Besides, to really validate this, we would also need to check that the config name is known.
                if (indexOfEquals < 1)
                    continue;

                string name = param.Substring(0, indexOfEquals);
                string value = param.Substring(indexOfEquals + 1);

                builder.Add(new KeyValuePair<string, string>(name, value));
            }

            _ryujitOptions = builder.ToArray();

            return this;
        }

        public override ICompilation ToCompilation()
        {
            var interopStubManager = new CompilerGeneratedInteropStubManager(_compilationGroup, _context, new InteropStateManager(_context.GeneratedAssembly));
            ReadyToRunCodegenNodeFactory factory = new ReadyToRunCodegenNodeFactory(_context, _compilationGroup, _metadataManager, interopStubManager, _nameMangler, _vtableSliceProvider, _dictionaryLayoutProvider);
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(factory);
            var jitConfig = new JitConfigProvider(Enumerable.Empty<CorJitFlag>(), _ryujitOptions);

            return new ReadyToRunCodegenCompilation(graph, factory, _compilationRoots, _debugInformationProvider, _logger, _devirtualizationManager, jitConfig, _inputFilePath);
        }
    }
}
