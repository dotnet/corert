// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ILToNative.DependencyAnalysisFramework
{
    public abstract class ComputedStaticDependencyNode<DependencyContextType> : DependencyNodeCore<DependencyContextType>
    {
        private IEnumerable<DependencyListEntry> _dependencies;
        private IEnumerable<CombinedDependencyListEntry> _conditionalDependencies;

        public void SetStaticDependencies(IEnumerable<DependencyListEntry> dependencies,
                                          IEnumerable<CombinedDependencyListEntry> conditionalDependencies)
        {
            Debug.Assert(_dependencies == null);
            Debug.Assert(_conditionalDependencies == null);
            Debug.Assert(dependencies != null);

            _dependencies = dependencies;
            _conditionalDependencies = conditionalDependencies;
        }

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                return _conditionalDependencies != null;
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
                return true;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return _dependencies != null;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(DependencyContextType context)
        {
            return _conditionalDependencies;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(DependencyContextType context)
        {
            return _dependencies;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<DependencyContextType>> markedNodes, int firstNode, DependencyContextType context)
        {
            return Array.Empty<CombinedDependencyListEntry>();
        }
    }
}
