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
        protected NodeFactory _nodeFactory;

        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        protected Logger _logger = Logger.Null;
        protected DependencyAnalyzerBase<NodeFactory> _dependencyGraph;

        public CompilationBuilder(NodeFactory nodeFactory)
        {
            _nodeFactory = nodeFactory;
            _dependencyGraph = new DependencyAnalyzer<NoLogStrategy<NodeFactory>, NodeFactory>(nodeFactory, null);
        }

        public CompilationBuilder UseLogger(Logger logger)
        {
            _logger = logger;
            return this;
        }

        public CompilationBuilder ConfigureDependencyGraph(Func<NodeFactory, DependencyAnalyzerBase<NodeFactory>> creator)
        {
            _dependencyGraph = creator(_nodeFactory);
            return this;
        }

        public abstract CompilationBuilder UseBackendOptions(IEnumerable<string> options);

        public abstract ICompilation ToCompilation();
    }
}
