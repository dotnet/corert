// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.DependencyAnalysisFramework
{
    /// <summary>
    /// Very memory efficient, and potentially faster mark strategy that eschews keeping track of what caused what to exist
    /// </summary>
    /// <typeparam name="DependencyContextType"></typeparam>
    public struct NoLogStrategy<DependencyContextType> : IDependencyAnalysisMarkStrategy<DependencyContextType>
    {
        private static object s_singleton = new object();

        bool IDependencyAnalysisMarkStrategy<DependencyContextType>.MarkNode(
            DependencyNodeCore<DependencyContextType> node,
            DependencyNodeCore<DependencyContextType> reasonNode,
            DependencyNodeCore<DependencyContextType> reasonNode2,
            string reason)
        {
            if (node.Marked)
                return false;

            node.SetMark(s_singleton);
            return true;
        }

        void IDependencyAnalysisMarkStrategy<DependencyContextType>.VisitLogEdges(IEnumerable<DependencyNodeCore<DependencyContextType>> nodeList, IDependencyAnalyzerLogEdgeVisitor logEdgeVisitor)
        {
            // This marker does not permit logging.
            return;
        }

        void IDependencyAnalysisMarkStrategy<DependencyContextType>.VisitLogNodes(IEnumerable<DependencyNodeCore<DependencyContextType>> nodeList, IDependencyAnalyzerLogNodeVisitor logNodeVisitor)
        {
            // This marker does not permit logging.
            return;
        }
    }
}
