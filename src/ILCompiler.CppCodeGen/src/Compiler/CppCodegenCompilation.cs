// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Internal.IL;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.CppCodeGen;

namespace ILCompiler
{
    public sealed class CppCodegenCompilation : Compilation
    {
        private CppWriter _cppWriter = null;

        internal CppCodegenConfigProvider Options { get; }

        internal CppCodegenCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            Logger logger,
            CppCodegenConfigProvider options)
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), ilProvider, debugInformationProvider, null, logger)
        {
            Options = options;
        }

        private static IEnumerable<ICompilationRootProvider> GetCompilationRoots(IEnumerable<ICompilationRootProvider> existingRoots, NodeFactory factory)
        {
            yield return new CppCodegenCompilationRootProvider(factory.TypeSystemContext);

            foreach (var existingRoot in existingRoots)
                yield return existingRoot;
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _cppWriter = new CppWriter(this, outputFile);

            _dependencyGraph.ComputeMarkedNodes();

            var nodes = _dependencyGraph.MarkedNodeList;

            _cppWriter.OutputCode(nodes, NodeFactory);
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (var dependency in obj)
            {
                var methodCodeNodeNeedingCode = dependency as CppMethodCodeNode;
                if (methodCodeNodeNeedingCode == null)
                {
                    // To compute dependencies of the shadow method that tracks dictionary
                    // dependencies we need to ensure there is code for the canonical method body.
                    var dependencyMethod = (ShadowConcreteMethodNode)dependency;
                    methodCodeNodeNeedingCode = (CppMethodCodeNode)dependencyMethod.CanonicalMethodNode;
                }

                // We might have already compiled this method.
                if (methodCodeNodeNeedingCode.StaticDependenciesAreComputed)
                    continue;

                _cppWriter.CompileMethod((CppMethodCodeNode)methodCodeNodeNeedingCode);
            }
        }

        private class CppCodegenCompilationRootProvider : ICompilationRootProvider
        {
            private TypeSystemContext _context;

            public CppCodegenCompilationRootProvider(TypeSystemContext context)
            {
                _context = context;
            }

            private void RootWellKnownType(WellKnownType wellKnownType, IRootingServiceProvider rootProvider)
            {
                var type = _context.GetWellKnownType(wellKnownType);
                rootProvider.AddCompilationRoot(type, "Enables CPP codegen");
            }

            public void AddCompilationRoots(IRootingServiceProvider rootProvider)
            {
                RootWellKnownType(WellKnownType.Void, rootProvider);
                RootWellKnownType(WellKnownType.Boolean, rootProvider);
                RootWellKnownType(WellKnownType.Char, rootProvider);
                RootWellKnownType(WellKnownType.SByte, rootProvider);
                RootWellKnownType(WellKnownType.Byte, rootProvider);
                RootWellKnownType(WellKnownType.Int16, rootProvider);
                RootWellKnownType(WellKnownType.UInt16, rootProvider);
                RootWellKnownType(WellKnownType.Int32, rootProvider);
                RootWellKnownType(WellKnownType.UInt32, rootProvider);
                RootWellKnownType(WellKnownType.Int64, rootProvider);
                RootWellKnownType(WellKnownType.UInt64, rootProvider);
                RootWellKnownType(WellKnownType.IntPtr, rootProvider);
                RootWellKnownType(WellKnownType.UIntPtr, rootProvider);
                RootWellKnownType(WellKnownType.Single, rootProvider);
                RootWellKnownType(WellKnownType.Double, rootProvider);
            }
        }
    }
}
