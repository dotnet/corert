// Licensed to the .NET Foundation under one or more agreements.
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

        internal WebAssemblyCodegenCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            Logger logger,
            WebAssemblyCodegenConfigProvider options)
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), logger)
        {
            LLVM.LoadLibrary_libLLVM("./libLLVM-x64.dll");
            Module = LLVM.ModuleCreateWithName("netscripten");
            LLVM.SetTarget(Module, "asmjs-unknown-emscripten");
            Options = options;
        }

        protected override bool GenerateDebugInfo
        {
            get
            {
                /// Some degree of control exposed by <see cref="WebAssemblyCodegenConfigProvider.NoLineNumbersString"/>.
                return true;
            }
        }

        private static IEnumerable<ICompilationRootProvider> GetCompilationRoots(IEnumerable<ICompilationRootProvider> existingRoots, NodeFactory factory)
        {
            yield return new CppCodegenCompilationRootProvider(factory.TypeSystemContext);

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
                /*RootWellKnownType(WellKnownType.Void, rootProvider);
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
                RootWellKnownType(WellKnownType.Double, rootProvider);*/
            }
        }
    }
}
