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

using Debug = System.Diagnostics.Debug;

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
            DebugInformationProvider debugInformationProvider,
            Logger logger)
            : base(dependencyGraph, nodeFactory, roots, debugInformationProvider, logger)
        {
        }

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

        ILScanResults IILScanner.Scan()
        {
            _nodeFactory.NameMangler.CompilationUnitPrefix = "";
            _dependencyGraph.ComputeMarkedNodes();

            return new ILScanResults(_dependencyGraph, _nodeFactory);
        }
    }

    public interface IILScanner
    {
        ILScanResults Scan();
    }

    public class ILScanResults : CompilationResults
    {
        internal ILScanResults(DependencyAnalyzerBase<NodeFactory> graph, NodeFactory factory)
            : base(graph, factory)
        {
        }

        public VTableSliceProvider GetVTableLayoutInfo()
        {
            return new ScannedVTableProvider(MarkedNodes);
        }

        public DictionaryLayoutProvider GetDictionaryLayoutInfo()
        {
            return new ScannedDictionaryLayoutProvider(MarkedNodes);
        }

        private class ScannedVTableProvider : VTableSliceProvider
        {
            private Dictionary<TypeDesc, IReadOnlyList<MethodDesc>> _vtableSlices = new Dictionary<TypeDesc, IReadOnlyList<MethodDesc>>();

            public ScannedVTableProvider(ImmutableArray<DependencyNodeCore<NodeFactory>> markedNodes)
            {
                foreach (var node in markedNodes)
                {
                    var vtableSliceNode = node as VTableSliceNode;
                    if (vtableSliceNode != null)
                    {
                        _vtableSlices.Add(vtableSliceNode.Type, vtableSliceNode.Slots);
                    }
                }
            }

            internal override VTableSliceNode GetSlice(TypeDesc type)
            {
                // TODO: move ownership of compiler-generated entities to CompilerTypeSystemContext.
                // https://github.com/dotnet/corert/issues/3873
                if (type.GetTypeDefinition() is Internal.TypeSystem.Ecma.EcmaType)
                    return new PrecomputedVTableSliceNode(type, _vtableSlices[type]);
                else
                    return new LazilyBuiltVTableSliceNode(type);
            }
        }

        private class ScannedDictionaryLayoutProvider : DictionaryLayoutProvider
        {
            private Dictionary<TypeSystemEntity, IEnumerable<GenericLookupResult>> _layouts = new Dictionary<TypeSystemEntity, IEnumerable<GenericLookupResult>>();

            public ScannedDictionaryLayoutProvider(ImmutableArray<DependencyNodeCore<NodeFactory>> markedNodes)
            {
                foreach (var node in markedNodes)
                {
                    var layoutNode = node as DictionaryLayoutNode;
                    if (layoutNode != null)
                    {
                        TypeSystemEntity owningMethodOrType = layoutNode.OwningMethodOrType;
                        _layouts.Add(owningMethodOrType, layoutNode.Entries);
                    }
                }
            }

            public override DictionaryLayoutNode GetLayout(TypeSystemEntity methodOrType)
            {
                if (methodOrType is TypeDesc type)
                {
                    // TODO: move ownership of compiler-generated entities to CompilerTypeSystemContext.
                    // https://github.com/dotnet/corert/issues/3873
                    if (type.GetTypeDefinition() is Internal.TypeSystem.Ecma.EcmaType)
                        return new PrecomputedDictionaryLayoutNode(type, _layouts[type]);
                    else
                        return new LazilyBuiltDictionaryLayoutNode(type);
                }
                else
                {
                    Debug.Assert(methodOrType is MethodDesc);
                    MethodDesc method = (MethodDesc)methodOrType;

                    // TODO: move ownership of compiler-generated entities to CompilerTypeSystemContext.
                    // https://github.com/dotnet/corert/issues/3873
                    if (method.GetTypicalMethodDefinition() is Internal.TypeSystem.Ecma.EcmaMethod)
                        return new PrecomputedDictionaryLayoutNode(method, _layouts[method]);
                    else
                        return new LazilyBuiltDictionaryLayoutNode(method);
                }
            }
        }
    }
}
