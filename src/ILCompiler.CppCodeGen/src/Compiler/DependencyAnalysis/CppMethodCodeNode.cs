// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class CppMethodCodeNode : DependencyNodeCore<NodeFactory>, IMethodBodyNode
    {
        private MethodDesc _method;
        private string _methodCode;
        private IEnumerable<Object> _dependencies;

        public CppMethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
//            if (method.ToString().Contains("StackDelegate"))
//            {
//
//            }
            _method = method;
        }

        public override bool Matched()
        {
            return _method.ToString()
                .Contains(
                    "[S.P.TypeLoader]System.Collections.Generic.ArrayBuilder`1<System.__Canon>.__GetFieldHelper(int32,EETypePtr&)");
        }

        public void SetCode(string methodCode, IEnumerable<Object> dependencies)
        {
            Debug.Assert(_methodCode == null);
            _methodCode = methodCode;
            _dependencies = dependencies;
        }

        public string CppCode
        {
            get
            {
                return _methodCode;
            }
        }

        public MethodDesc Method
        {
            get
            {
                return _method;
            }
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override bool StaticDependenciesAreComputed => _methodCode != null;

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
                dependencies.Add(node, "CPP code ");

            foreach (TypeDesc type in _method.OwningType.Instantiation)
            {
                if (type is RuntimeDeterminedType)
                {

                }
            }
            // Raw p/invoke methods are special - these wouldn't show up as method bodies for other codegens
            // and the rest of the system doesn't expect to see them here.
            if (!_method.IsRawPInvoke())
                CodeBasedDependencyAlgorithm.AddDependenciesDueToMethodCodePresence(ref dependencies, factory, _method);

            return dependencies;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        int ISortableNode.ClassCode => 1643555522;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((CppMethodCodeNode)other)._method);
        }
    }
}
