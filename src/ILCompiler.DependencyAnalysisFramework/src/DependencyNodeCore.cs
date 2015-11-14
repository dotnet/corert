// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.DependencyAnalysisFramework
{
    public abstract class DependencyNodeCore<DependencyContextType> : DependencyNode
    {
        public struct DependencyListEntry
        {
            public DependencyListEntry(DependencyNodeCore<DependencyContextType> node,
                                       string reason)
            {
                Node = node;
                Reason = reason;
            }

            public DependencyListEntry(object node,
                                       string reason)
            {
                Node = (DependencyNodeCore<DependencyContextType>)node;
                Reason = reason;
            }

            public DependencyNodeCore<DependencyContextType> Node;
            public string Reason;
        }

        public class DependencyList : List<DependencyListEntry>
        {
            public void Add(DependencyNodeCore<DependencyContextType> node,
                                       string reason)
            {
                this.Add(new DependencyListEntry(node, reason));
            }

            public void Add(object node, string reason)
            {
                this.Add(new DependencyListEntry((DependencyNodeCore<DependencyContextType>)node, reason));
            }
        }

        public struct CombinedDependencyListEntry
        {
            public CombinedDependencyListEntry(DependencyNodeCore<DependencyContextType> node,
                                               DependencyNodeCore<DependencyContextType> otherReasonNode,
                                               string reason)
            {
                Node = node;
                OtherReasonNode = otherReasonNode;
                Reason = reason;
            }

            public CombinedDependencyListEntry(object node,
                                               object otherReasonNode,
                                               string reason)
            {
                Node = (DependencyNodeCore<DependencyContextType>)node;
                OtherReasonNode = (DependencyNodeCore<DependencyContextType>)otherReasonNode;
                Reason = reason;
            }

            // Used by HashSet, so must have good Equals/GetHashCode
            public DependencyNodeCore<DependencyContextType> Node;
            public DependencyNodeCore<DependencyContextType> OtherReasonNode;
            public string Reason;
        }

        public abstract bool InterestingForDynamicDependencyAnalysis
        {
            get;
        }

        public abstract bool HasDynamicDependencies
        {
            get;
        }

        public abstract bool HasConditionalStaticDependencies
        {
            get;
        }

        public abstract bool StaticDependenciesAreComputed
        {
            get;
        }

        public abstract IEnumerable<DependencyListEntry> GetStaticDependencies(DependencyContextType context);

        public abstract IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(DependencyContextType context);

        public abstract IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<DependencyContextType>> markedNodes, int firstNode, DependencyContextType context);

        internal void CallOnMarked(DependencyContextType context)
        {
            OnMarked(context);
        }

        /// <summary>
        /// Overrides of this method allow a node to perform actions when said node becomes
        /// marked.
        /// </summary>
        /// <param name="context"></param>
        protected virtual void OnMarked(DependencyContextType context)
        {
            // Do nothing by default
        }
    }
}
