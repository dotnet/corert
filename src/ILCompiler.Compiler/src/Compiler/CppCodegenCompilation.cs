// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.CppCodeGen;

namespace ILCompiler
{
    public class CppCodegenCompilation : Compilation
    {
        private CppWriter _cppWriter = null;

        public CppCodegenCompilation(CompilerTypeSystemContext context, CompilationModuleGroup compilationGroup)
            : base(new CppCodegenNodeFactory(context, compilationGroup), new NameMangler(true))
        {
        }

        public override Compilation UseBackendOptions(IEnumerable<string> options)
        {
            // TODO: NoLineNumbers

            return this;
        }

        protected override void CompileInternal(string outputFile)
        {
            _cppWriter = new CppWriter(this, outputFile);

            var nodes = _dependencyGraph.MarkedNodeList;

            _cppWriter.OutputCode(nodes, NodeFactory.CompilationModuleGroup.StartupCodeMain, NodeFactory);
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (CppMethodCodeNode methodCodeNodeNeedingCode in obj)
            {
                _cppWriter.CompileMethod(methodCodeNodeNeedingCode);
            }
        }
    }
}
