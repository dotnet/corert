using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILToNative.DependencyAnalysisFramework;

namespace ILToNative.DependencyAnalysis
{
    public abstract class EmbeddedObjectNode : DependencyNodeCore<NodeFactory>
    {
        public int Offset
        {
            get;
            set;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return null;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return null;
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
        public abstract void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly);
    }
}
