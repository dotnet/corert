﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using LLVMSharp;

namespace ILCompiler
{
    public sealed class WebAssemblyCodegenCompilation : Compilation
    {
        internal WebAssemblyCodegenConfigProvider Options { get; }
        internal LLVMModuleRef Module { get; }
        public new WebAssemblyCodegenNodeFactory NodeFactory { get; }
        internal WebAssemblyCodegenCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            WebAssemblyCodegenNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            Logger logger,
            WebAssemblyCodegenConfigProvider options)
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), null, null, logger)
        {
            NodeFactory = nodeFactory;
            Module = LLVM.ModuleCreateWithName("netscripten");
            LLVM.SetTarget(Module, "asmjs-unknown-emscripten");
            Options = options;
        }

        private static IEnumerable<ICompilationRootProvider> GetCompilationRoots(IEnumerable<ICompilationRootProvider> existingRoots, NodeFactory factory)
        {
            foreach (var existingRoot in existingRoots)
                yield return existingRoot;
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _dependencyGraph.ComputeMarkedNodes();

            var nodes = _dependencyGraph.MarkedNodeList;

            WebAssemblyObjectWriter.EmitObject(outputFile, nodes, NodeFactory, this, dumper);
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (WebAssemblyMethodCodeNode methodCodeNodeNeedingCode in obj)
            {
                Internal.IL.ILImporter.CompileMethod(this, methodCodeNodeNeedingCode);
            }
        }
    }
}
