using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.DependencyAnalysisFramework
{
    public interface IDependencyNode
    {
        bool Marked
        {
            get;
        }
    }

    public interface IDependencyNode<DependencyContextType> : IDependencyNode
    {
        bool InterestingForDynamicDependencyAnalysis
        {
            get;
        }

        bool HasDynamicDependencies
        {
            get;
        }

        bool HasConditionalStaticDependencies
        {
            get;
        }

        bool StaticDependenciesAreComputed
        {
            get;
        }

        IEnumerable<DependencyNodeCore<DependencyContextType>.DependencyListEntry> GetStaticDependencies(DependencyContextType context);
    
        IEnumerable<DependencyNodeCore<DependencyContextType>.CombinedDependencyListEntry> GetConditionalStaticDependencies(DependencyContextType context);

        IEnumerable<DependencyNodeCore<DependencyContextType>.CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<DependencyContextType>> markedNodes, int firstNode, DependencyContextType context);
    }
}
