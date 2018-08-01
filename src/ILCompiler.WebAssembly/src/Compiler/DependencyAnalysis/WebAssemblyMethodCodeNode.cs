// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class WebAssemblyMethodCodeNode : DependencyNodeCore<NodeFactory>, IMethodBodyNode
    {
        private MethodDesc _method;
        private IEnumerable<Object> _dependencies = Enumerable.Empty<Object>();

        public WebAssemblyMethodCodeNode(MethodDesc method)
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

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

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

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            var dependencies = new DependencyList();

            foreach (Object node in _dependencies)
                dependencies.Add(node, "Wasm code ");

            return dependencies;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        
        int ISortableNode.ClassCode => -1502960727;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((WebAssemblyMethodCodeNode)other)._method);
        }
    }
}
