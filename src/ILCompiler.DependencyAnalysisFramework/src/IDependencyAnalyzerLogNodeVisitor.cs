// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ILCompiler.DependencyAnalysisFramework
{
    public interface IDependencyAnalyzerLogNodeVisitor
    {
        void VisitCombinedNode(Tuple<DependencyNode, DependencyNode> node);
        void VisitNode(DependencyNode node);
        void VisitRootNode(string rootName);
    }
}
