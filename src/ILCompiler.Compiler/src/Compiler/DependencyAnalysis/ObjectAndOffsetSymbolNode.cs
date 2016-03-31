// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    internal class ObjectAndOffsetSymbolNode : DependencyNodeCore<NodeFactory>, ISymbolNode
    {
        private ObjectNode _object;
        private int _offset;
        private string _name;

        public ObjectAndOffsetSymbolNode(ObjectNode obj, int offset, string name)
        {
            _object = obj;
            _offset = offset;
            _name = name;
        }

        public override string GetName()
        {
            return "Symbol " + _name + " at offset " + _offset.ToStringInvariant();
        }

        public override bool HasConditionalStaticDependencies
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

        public override bool InterestingForDynamicDependencyAnalysis
        {
            get
            {
                return false;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return _name;
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return _offset;
            }
        }

        public void SetSymbolOffset(int offset)
        {
            _offset = offset;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            return null;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_object, "ObjectAndOffsetDependency") };
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory)
        {
            return null;
        }
    }
}
