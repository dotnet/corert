// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.DependencyAnalysisFramework
{
    public interface IDependencyAnalyzerLogEdgeVisitor
    {
        void VisitEdge(DependencyNode nodeDepender, DependencyNode nodeDependedOn, string reason);
        void VisitEdge(string root, DependencyNode dependedOn);
        void VisitEdge(DependencyNode nodeDepender, DependencyNode nodeDependerOther, DependencyNode nodeDependedOn, string reason);
    }
}
