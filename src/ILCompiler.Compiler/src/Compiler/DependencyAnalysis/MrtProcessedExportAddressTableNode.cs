// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public class MrtProcessedExportAddressTableNode : ObjectNode, IExportableSymbolNode, ISortableSymbolNode
    {
        private readonly HashSet<ISortableSymbolNode> _exportableSymbols = new HashSet<ISortableSymbolNode>();
        private readonly string _symbolName;
        private readonly NodeFactory _factory;

        public MrtProcessedExportAddressTableNode(string symbolName, NodeFactory factory)
        {
            _symbolName = symbolName;
            _factory = factory;
        }

        public event Func<uint, IExportableSymbolNode, uint> ReportExportedItem;
        public event Func<uint> GetInitialExportOrdinal;

        public void AddExportableSymbol(IExportableSymbolNode exportableSymbol)
        {
            if (exportableSymbol.GetExportForm(_factory) == ExportForm.ByOrdinal)
            {
                if (exportableSymbol is EETypeNode)
                {
                    exportableSymbol = (IExportableSymbolNode)((EETypeNode)exportableSymbol).NodeForLinkage(_factory);
                }

                lock (_exportableSymbols)
                {
                    _exportableSymbols.Add((ISortableSymbolNode)exportableSymbol);
                }
            }
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(_symbolName);
        }

        public int Offset => 0;

        public virtual ExportForm GetExportForm(NodeFactory factory) => ExportForm.ByName;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;
        public override bool IsShareable => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            var builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();
            builder.AddSymbol(this);

            //
            // Entries in the export table need to be sorted by ordinals. When compiling using baseline TOC files, we reuse
            // the ordinals from the baseline for sorting, otherwise we start assigning new sequential ordinals. Export entries that do
            // not exist in the baseline will get new sequential ordinals, but for determinism, they are also pre-sorted using the
            // CompilerComparer logic
            //

            ISortableSymbolNode[] symbolNodes = new ISortableSymbolNode[_exportableSymbols.Count];
            _exportableSymbols.CopyTo(symbolNodes);
            Array.Sort(symbolNodes, new CompilerComparer());

            builder.EmitInt(1); // Export table version 1
            builder.EmitInt(symbolNodes.Length); // Count of exported symbols in this table

            uint index = GetInitialExportOrdinal == null ? 1 : GetInitialExportOrdinal();
            Dictionary<uint, ISortableSymbolNode> symbolsOridnalMap = new Dictionary<uint, ISortableSymbolNode>();
            foreach (ISortableSymbolNode symbol in symbolNodes)
            {
                uint indexUsed = ReportExportedItem.Invoke(index, (IExportableSymbolNode)symbol);
                symbolsOridnalMap.Add(indexUsed, symbol);
                index += (indexUsed == index ? (uint)1 : 0);
            }

            foreach (uint ordinal in symbolsOridnalMap.Keys.OrderBy(o => o))
            {
                builder.EmitReloc(symbolsOridnalMap[ordinal], RelocType.IMAGE_REL_BASED_REL32);
            }

            return builder.ToObjectData();
        }

        public override int ClassCode => 40423846;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            Debug.Assert(Object.ReferenceEquals(other, this));
            return 0; // There should only ever be one of these per dependency graph
        }
    }
}
