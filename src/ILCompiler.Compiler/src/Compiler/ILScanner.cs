// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.IL.Stubs;
using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// IL scan analyzer of programs - this class analyzes what methods, types and other runtime artifact
    /// will need to be generated during a compilation. The result of analysis is a conservative superset of
    /// what methods will be compiled by the actual codegen backend.
    /// </summary>
    internal sealed class ILScanner : Compilation, IILScanner
    {
        internal ILScanner(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            ILScanNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            Logger logger)
            : base(dependencyGraph, nodeFactory, roots, logger)
        {
        }

        protected override bool GenerateDebugInfo => false;

        // TODO: Does IsReadyToRun actually matter for ILScanner?
        public override bool IsReadyToRun => false;

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            // TODO: We should have a base class for compilation that doesn't implement ICompilation so that
            // we don't need this.
            throw new NotSupportedException();
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (DependencyNodeCore<NodeFactory> dependency in obj)
            {
                var methodCodeNodeNeedingCode = dependency as ScannedMethodNode;
                if (methodCodeNodeNeedingCode == null)
                {
                    // To compute dependencies of the shadow method that tracks dictionary
                    // dependencies we need to ensure there is code for the canonical method body.
                    var dependencyMethod = (ShadowConcreteMethodNode)dependency;
                    methodCodeNodeNeedingCode = (ScannedMethodNode)dependencyMethod.CanonicalMethodNode;
                }

                // We might have already compiled this method.
                if (methodCodeNodeNeedingCode.StaticDependenciesAreComputed)
                    continue;

                MethodDesc method = methodCodeNodeNeedingCode.Method;

                try
                {
                    var importer = new ILImporter(this, method);
                    methodCodeNodeNeedingCode.InitializeDependencies(_nodeFactory, importer.Import());
                }
                catch (TypeSystemException ex)
                {
                    // Try to compile the method again, but with a throwing method body this time.
                    MethodIL throwingIL = TypeSystemThrowingILEmitter.EmitIL(method, ex);
                    var importer = new ILImporter(this, method, throwingIL);
                    methodCodeNodeNeedingCode.InitializeDependencies(_nodeFactory, importer.Import());
                }
            }
        }

        public ILScanResults Scan()
        {
            return new ILScanResults(_dependencyGraph.MarkedNodeList);
        }
    }

    public interface IILScanner
    {
        ILScanResults Scan();
    }

    public class ILScanResults
    {
        private readonly ImmutableArray<DependencyNodeCore<NodeFactory>> _markedNodes;

        internal ILScanResults(ImmutableArray<DependencyNodeCore<NodeFactory>> markedNodes)
        {
            _markedNodes = markedNodes;
        }

        public IEnumerable<MethodDesc> CompiledMethods
        {
            get
            {
                foreach (var node in _markedNodes)
                {
                    var methodNode = node as ScannedMethodNode;
                    if (methodNode != null)
                        yield return methodNode.Method;
                }
            }
        }
    }
}
