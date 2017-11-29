// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        public event Action<int, IExportableSymbolNode> ReportExportedItem;

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

            ISortableSymbolNode[] symbolNodes = new ISortableSymbolNode[_exportableSymbols.Count];
            _exportableSymbols.CopyTo(symbolNodes);
            Array.Sort(symbolNodes, new CompilerComparer());

            builder.EmitInt(1); // Export table version 1
            builder.EmitInt(symbolNodes.Length); // Count of exported symbols in this table

            int index = 1;
            foreach (ISortableSymbolNode symbol in symbolNodes)
            {
                builder.EmitReloc(symbol, RelocType.IMAGE_REL_BASED_REL32);
                ReportExportedItem?.Invoke(index, (IExportableSymbolNode)symbol);
                index++;
            }

            return builder.ToObjectData();
        }

        protected internal override int ClassCode => 40423846;

        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            Debug.Assert(Object.ReferenceEquals(other, this));
            return 0; // There should only ever be one of these per dependency graph
        }

        int ISortableSymbolNode.ClassCode => ClassCode;

        int ISortableSymbolNode.CompareToImpl(ISortableSymbolNode other, CompilerComparer comparer)
        {
            return CompareToImpl((ObjectNode)other, comparer);
        }
    }
}
