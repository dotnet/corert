// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    internal class CppMethodCodeNode : DependencyNodeCore<NodeFactory>, ISymbolNode
    {
        private MethodDesc _method;
        private string _methodCode;
        private IEnumerable<Object> _dependencies;

        public CppMethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            _method = method;
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
        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return _methodCode != null;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return NodeFactory.NameMangler.GetMangledMethodName(_method);
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public override bool InterestingForDynamicDependencyAnalysis
        {
            get
            {
                return false;
            }
        }

        public override bool HasDynamicDependencies
        {
            get
            {
                return false;
            }
        }

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                return false;
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            var dependencies = new DependencyList();

            foreach (Object node in _dependencies)
                dependencies.Add(node, "CPP code ");

            return dependencies;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return null;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return null;
        }
    }
}
