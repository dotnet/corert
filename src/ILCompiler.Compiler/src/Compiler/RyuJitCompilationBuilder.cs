// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;

using Internal.JitInterface;

namespace ILCompiler
{
    public sealed class RyuJitCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        private JitConfigProvider _jitConfig = new JitConfigProvider(Array.Empty<string>());

        public RyuJitCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(new RyuJitNodeFactory(context, group))
        {
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            _jitConfig = new JitConfigProvider(options);
            return this;
        }

        public override ICompilation ToCompilation()
        {
            return new RyuJitCompilation(CreateDependencyGraph(), _nodeFactory, _compilationRoots, _logger, _jitConfig);
        }
    }
}
