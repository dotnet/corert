// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    public abstract class ObjectNode : DependencyNodeCore<NodeFactory>
    {
        public class ObjectData
        {
            public ObjectData(byte[] data, Relocation[] relocs, int alignment, ISymbolNode[] definedSymbols)
            {
                Data = data;
                Relocs = relocs;
                Alignment = alignment;
                DefinedSymbols = definedSymbols;
            }

            public readonly Relocation[] Relocs;
            public readonly byte[] Data;
            public readonly int Alignment;
            public readonly ISymbolNode[] DefinedSymbols;
        }

        public abstract ObjectData GetData(NodeFactory factory, bool relocsOnly = false);

        public abstract string Section
        {
            get;
        }

        /// <summary>
        /// Override this function to have a node which should be skipped when emitting
        /// to the object file. (For instance, if there are two nodes describing the same
        /// data structure, one of those nodes should return true here.)
        /// </summary>
        /// <param name="factory"></param>
        /// <returns></returns>
        public virtual bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return false;
        }

        public override bool HasConditionalStaticDependencies
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

        public override bool InterestingForDynamicDependencyAnalysis
        {
            get
            {
                return false;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return null;
        }

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            DependencyList dependencies = ComputeNonRelocationBasedDependencies(context);
            Relocation[] relocs = GetData(context, true).Relocs;

            if (relocs != null)
            {
                if (dependencies == null)
                    dependencies = new DependencyList();

                foreach (Relocation reloc in relocs)
                {
                    dependencies.Add(reloc.Target, "reloc");
                }
            }

            if (dependencies == null)
                return Array.Empty<DependencyListEntry>();
            else
                return dependencies;
        }

        protected virtual DependencyList ComputeNonRelocationBasedDependencies(NodeFactory context)
        {
            return null;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return null;
        }
    }
}
