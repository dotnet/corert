// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ILCompiler.DependencyAnalysisFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally and statically linked to the output obj file.
    /// </summary>
    public class ExternSymbolNode : DependencyNodeCore<NodeFactory>, ISymbolNode
    {
        string _name;

        public ExternSymbolNode(string name)
        {
            _name = name;
        }

        public override string GetName()
        {
            return "ExternSymbol " + _name;
        }

        public int Offset
        {
            get
            {
                return 0;
            }
        }

        public string MangledName
        {
            get
            {
                return _name;
            }
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

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return null;
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return null;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return null;
        }
    }
}
