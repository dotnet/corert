// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.IL;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using LLVMSharp;
using ILCompiler.WebAssembly;

namespace ILCompiler
{
    public sealed class WebAssemblyCodegenCompilation : Compilation
    {
        internal WebAssemblyCodegenConfigProvider Options { get; }
        internal LLVMModuleRef Module { get; }
        public new WebAssemblyCodegenNodeFactory NodeFactory { get; }
        internal LLVMDIBuilderRef DIBuilder { get; }
        internal Dictionary<string, DebugMetadata> DebugMetadataMap { get; }
        internal WebAssemblyCodegenCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            WebAssemblyCodegenNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            Logger logger,
            WebAssemblyCodegenConfigProvider options)
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), ilProvider, debugInformationProvider, null, logger)
        {
            NodeFactory = nodeFactory;
            Module = LLVM.ModuleCreateWithName("netscripten");
            LLVM.SetTarget(Module, "asmjs-unknown-emscripten");
            Options = options;
            DIBuilder = LLVMPInvokes.LLVMCreateDIBuilder(Module);
            DebugMetadataMap = new Dictionary<string, DebugMetadata>();
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
            foreach (var dependency in obj)
            {
                var methodCodeNodeNeedingCode = dependency as WebAssemblyMethodCodeNode;
                if (methodCodeNodeNeedingCode == null)
                {
                    // To compute dependencies of the shadow method that tracks dictionary
                    // dependencies we need to ensure there is code for the canonical method body.
                    var dependencyMethod = (ShadowConcreteMethodNode)dependency;
                    methodCodeNodeNeedingCode = (WebAssemblyMethodCodeNode)dependencyMethod.CanonicalMethodNode;
                }

                // We might have already compiled this method.
                if (methodCodeNodeNeedingCode.StaticDependenciesAreComputed)
                    continue;

                ILImporter.CompileMethod(this, methodCodeNodeNeedingCode);
            }
        }

        public TypeDesc ConvertToCanonFormIfNecessary(TypeDesc type, CanonicalFormKind policy)
        {
            if (!type.IsCanonicalSubtype(CanonicalFormKind.Any))
                return type;

            if (type.IsPointer || type.IsByRef)
            {
                ParameterizedType parameterizedType = (ParameterizedType)type;
                TypeDesc paramTypeConverted = ConvertToCanonFormIfNecessary(parameterizedType.ParameterType, policy);
                if (paramTypeConverted == parameterizedType.ParameterType)
                    return type;

                if (type.IsPointer)
                    return TypeSystemContext.GetPointerType(paramTypeConverted);

                if (type.IsByRef)
                    return TypeSystemContext.GetByRefType(paramTypeConverted);
            }

            return type.ConvertToCanonForm(policy);
        }
    }
}
