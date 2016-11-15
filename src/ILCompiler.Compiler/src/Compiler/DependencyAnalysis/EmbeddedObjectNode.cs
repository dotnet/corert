// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public abstract class EmbeddedObjectNode : DependencyNodeCore<NodeFactory>
    {
        private const int InvalidOffset = int.MinValue;

        private int _offset;

        public EmbeddedObjectNode()
        {
            _offset = InvalidOffset;
        }

        public virtual int Offset
        {
            get
            {
                Debug.Assert(_offset != InvalidOffset);
                return _offset;
            }
            set
            {
                Debug.Assert(_offset == InvalidOffset || _offset == value);
                _offset = value;
            }
        }

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        public abstract void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly);
    }
}
