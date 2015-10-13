// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace ILToNative.DependencyAnalysisFramework
{
    public interface IDependencyAnalyzerLogNodeVisitor
    {
        void VisitCombinedNode(Tuple<DependencyNode, DependencyNode> node);
        void VisitNode(DependencyNode node);
        void VisitRootNode(string rootName);
    }
}
