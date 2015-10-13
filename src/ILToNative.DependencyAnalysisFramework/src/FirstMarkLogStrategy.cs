// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace ILToNative.DependencyAnalysisFramework
{
    public struct FirstMarkLogStrategy<DependencyContextType> : IDependencyAnalysisMarkStrategy<DependencyContextType>
    {
        private class MarkData
        {
            public MarkData(string reason, DependencyNodeCore<DependencyContextType> reason1, DependencyNodeCore<DependencyContextType> reason2)
            {
                Reason = reason;
                Reason1 = reason1;
                Reason2 = reason2;
            }

            public string Reason
            {
                get;
                private set;
            }

            public DependencyNodeCore<DependencyContextType> Reason1
            {
                get;
                private set;
            }

            public DependencyNodeCore<DependencyContextType> Reason2
            {
                get;
                private set;
            }
        }

        private HashSet<string> _reasonStringOnlyNodes;

        bool IDependencyAnalysisMarkStrategy<DependencyContextType>.MarkNode(
            DependencyNodeCore<DependencyContextType> node, 
            DependencyNodeCore<DependencyContextType> reasonNode, 
            DependencyNodeCore<DependencyContextType> reasonNode2, 
            string reason)
        {
            if (node.Marked)
                return false;

            if ((reasonNode == null) && (reasonNode2 == null))
            {
                Debug.Assert(reason != null);
                if (_reasonStringOnlyNodes == null)
                    _reasonStringOnlyNodes = new HashSet<string>();

                _reasonStringOnlyNodes.Add(reason);
            }

            node.SetMark(new MarkData(reason, reasonNode, reasonNode2));
            return true;
        }

        void IDependencyAnalysisMarkStrategy<DependencyContextType>.VisitLogNodes(IEnumerable<DependencyNodeCore<DependencyContextType>> nodeList, IDependencyAnalyzerLogNodeVisitor logNodeVisitor)
        {
            HashSet<Tuple<DependencyNode, DependencyNode>> combinedNodesReported = new HashSet<Tuple<DependencyNode, DependencyNode>>();

            if (_reasonStringOnlyNodes != null)
            {
                foreach (string reasonOnly in _reasonStringOnlyNodes)
                {
                    logNodeVisitor.VisitRootNode(reasonOnly);
                }
            }

            foreach (DependencyNodeCore<DependencyContextType> node in nodeList)
            {
                if (node.Marked)
                {
                    MarkData markData = (MarkData)node.GetMark();

                    if (markData.Reason2 != null)
                    {
                        Tuple<DependencyNode, DependencyNode> combinedNode = new Tuple<DependencyNode, DependencyNode>(markData.Reason1, markData.Reason2);

                        if (!combinedNodesReported.Contains(combinedNode))
                        {
                            logNodeVisitor.VisitCombinedNode(combinedNode);
                        }
                    }
                }
            }
        }

        void IDependencyAnalysisMarkStrategy<DependencyContextType>.VisitLogEdges(IEnumerable<DependencyNodeCore<DependencyContextType>> nodeList, IDependencyAnalyzerLogEdgeVisitor logEdgeVisitor)
        {
            foreach (DependencyNodeCore<DependencyContextType> node in nodeList)
            {
                if (node.Marked)
                {
                    MarkData markData = (MarkData)node.GetMark();

                    if (markData.Reason2 != null)
                    {
                        Debug.Assert(markData.Reason1 != null);
                        logEdgeVisitor.VisitEdge(markData.Reason1, markData.Reason2, node, markData.Reason);
                    }
                    else if (markData.Reason1 != null)
                    {
                        logEdgeVisitor.VisitEdge(markData.Reason1, node, markData.Reason);
                    }
                    else
                    {
                        Debug.Assert(markData.Reason != null);
                        logEdgeVisitor.VisitEdge(markData.Reason, node);
                    }
                }
            }
        }
    }
}
