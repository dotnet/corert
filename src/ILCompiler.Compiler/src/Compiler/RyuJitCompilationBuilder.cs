// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.JitInterface;

namespace ILCompiler
{
    public sealed class RyuJitCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        private KeyValuePair<string, string>[] _ryujitOptions = Array.Empty<KeyValuePair<string, string>>();

        public RyuJitCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(context, group,
                  new CoreRTNameMangler(context.Target.IsWindows ? (NodeMangler)new WindowsNodeMangler() : (NodeMangler)new UnixNodeMangler(), false))
        {
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
            ArrayBuilder<CorJitFlag> jitFlagBuilder = new ArrayBuilder<CorJitFlag>();

            switch (_optimizationMode)
            {
                case OptimizationMode.None:
                    jitFlagBuilder.Add(CorJitFlag.CORJIT_FLAG_DEBUG_CODE);
                    break;

                case OptimizationMode.PreferSize:
                    jitFlagBuilder.Add(CorJitFlag.CORJIT_FLAG_SIZE_OPT);
                    break;

                case OptimizationMode.PreferSpeed:
                    jitFlagBuilder.Add(CorJitFlag.CORJIT_FLAG_SPEED_OPT);
                    break;

                default:
                    // Not setting a flag results in BLENDED_CODE.
                    break;
            }

            if (_generateDebugInfo)
                jitFlagBuilder.Add(CorJitFlag.CORJIT_FLAG_DEBUG_INFO);

            var factory = new RyuJitNodeFactory(_context, _compilationGroup, _metadataManager, _nameMangler);

            var jitConfig = new JitConfigProvider(jitFlagBuilder.ToArray(), _ryujitOptions);
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(factory);
            return new RyuJitCompilation(graph, factory, _compilationRoots, _logger, jitConfig);
        }
    }
}
