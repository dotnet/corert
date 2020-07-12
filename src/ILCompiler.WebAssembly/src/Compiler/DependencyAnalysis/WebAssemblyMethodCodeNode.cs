// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal abstract class WebAssemblyMethodCodeNode : DependencyNodeCore<NodeFactory>
    {
        protected readonly MethodDesc _method;
        protected IEnumerable<Object> _dependencies = Enumerable.Empty<Object>();

        protected WebAssemblyMethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            _method = method;
        }

        public void SetDependencies(IEnumerable<Object> dependencies)
        {
            Debug.Assert(dependencies != null);
            _dependencies = dependencies;
        }
        
        public MethodDesc Method
        {
            get
            {
                return _method;
            }
        }

        public override bool StaticDependenciesAreComputed => CompilationCompleted;

        public bool CompilationCompleted { get; set; }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }
        public int Offset => 0;
        public bool RepresentsIndirectionCell => false;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
    }

    internal class WebAssemblyMethodBodyNode : WebAssemblyMethodCodeNode, IMethodBodyNode
    {
        public WebAssemblyMethodBodyNode(MethodDesc method)
            : base(method)
        {
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            var dependencies = new DependencyList();

            foreach (Object node in _dependencies)
                dependencies.Add(node, "Wasm code ");

            CodeBasedDependencyAlgorithm.AddDependenciesDueToMethodCodePresence(ref dependencies, factory, _method);

            return dependencies;
        }

        int ISortableNode.ClassCode => -1502960727;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((WebAssemblyMethodBodyNode)other)._method);
        }
    }
}
