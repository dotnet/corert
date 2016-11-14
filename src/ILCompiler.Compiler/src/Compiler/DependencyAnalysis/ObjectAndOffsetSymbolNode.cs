// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    internal class ObjectAndOffsetSymbolNode : DependencyNodeCore<NodeFactory>, ISymbolNode
    {
        private ObjectNode _object;
        private int _offset;
        private Utf8String _name;

        public ObjectAndOffsetSymbolNode(ObjectNode obj, int offset, Utf8String name)
        {
            _object = obj;
            _offset = offset;
            _name = name;
        }

        protected override string GetName() => $"Symbol {_name.ToString()} at offset {_offset.ToStringInvariant()}";

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(_name);
        }
        public int Offset => _offset;

        public void SetSymbolOffset(int offset)
        {
            _offset = offset;
        }

        public ObjectNode Target => _object;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_object, "ObjectAndOffsetDependency") };
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
    }
}
