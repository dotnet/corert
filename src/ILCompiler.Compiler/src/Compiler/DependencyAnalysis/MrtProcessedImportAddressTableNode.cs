// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    public class MrtProcessedImportAddressTableNode : EmbeddedDataContainerNode, IHasStartSymbol, ISortableSymbolNode, ISymbolDefinitionNode
    {
        private List<MrtImportNode> _importNodes = new List<MrtImportNode>();
        private bool _nodeListComplete;
        private int _pointerSize;
        private EmbeddedObjectNode _pointerFromImportTablesTable;

        public MrtProcessedImportAddressTableNode(string exportTableToImportSymbol, TypeSystemContext context) : base("_ImportTable_" + exportTableToImportSymbol, "_ImportTable_end_" + exportTableToImportSymbol)
        {
            ExportTableToImportSymbol = exportTableToImportSymbol;
            _pointerSize = context.Target.PointerSize;
        }

        public readonly string ExportTableToImportSymbol;

        protected override string GetName(NodeFactory factory) => $"Region {StartSymbol.GetMangledName(factory.NameMangler)}";

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public void AddNode(MrtImportNode node)
        {
            Debug.Assert(!_nodeListComplete);
            _importNodes.Add(node);
        }

        public void FinalizeOffsets()
        {
            if (_nodeListComplete)
                return;

            _nodeListComplete = true;
            _importNodes.Sort((import1, import2) => import1.Ordinal - import2.Ordinal);

            // Layout of importtable node
            //
            // Version Number - Always 1 (32bit int)
            // Count of nodes            (32bit int)
            // Symbol that points to imported EAT (this is done via traditional linker import/export tables) (Pointer sized)
            // Pointer sized indirection cell (1 per node, initial value is that of the index into the EAT)
            int offset = 4 + 4 + _pointerSize;
            foreach (MrtImportNode node in _importNodes)
            {
                node.InitializeOffsetFromBeginningOfArray(offset);
                offset += _pointerSize;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();

            builder.AddSymbol(this);
            builder.AddSymbol(StartSymbol);

            builder.EmitInt(1);
            builder.EmitInt(_importNodes.Count);
            builder.EmitPointerReloc(factory.ExternSymbol(ExportTableToImportSymbol));


            if (!relocsOnly)
            {
                FinalizeOffsets();

                foreach (MrtImportNode node in _importNodes)
                {
                    Debug.Assert(((ISymbolDefinitionNode)node).Offset == builder.CountBytes);
                    builder.AddSymbol(node);
                    builder.EmitNaturalInt(node.Ordinal);
                }
            }

            EndSymbol.SetSymbolOffset(builder.CountBytes);
            builder.AddSymbol(EndSymbol);

            ObjectData objData = builder.ToObjectData();
            return objData;
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            dependencies.Add(StartSymbol, "StartSymbol");
            dependencies.Add(EndSymbol, "EndSymbol");

            lock(this)
            {
                if (_pointerFromImportTablesTable == null)
                {
                    _pointerFromImportTablesTable = factory.ImportAddressTablesTable.NewNode(this);
                }
            }
            dependencies.Add(_pointerFromImportTablesTable, "Pointer from ImportTablesTableNode");

            return dependencies;
        }

        public override int ClassCode => -1145565068;

        void ISymbolNode.AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__");
            StartSymbol.AppendMangledName(nameMangler, sb);
        }

        int ISymbolNode.Offset => 0;
        int ISymbolDefinitionNode.Offset => 0;
    }
}
