// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public interface IHasStartSymbol
    {
        ObjectAndOffsetSymbolNode StartSymbol { get; }
    }

    /// <summary>
    /// Represents an array of <typeparamref name="TEmbedded"/> nodes. The contents of this node will be emitted
    /// by placing a starting symbol, followed by contents of <typeparamref name="TEmbedded"/> nodes (optionally
    /// sorted using provided comparer), followed by ending symbol.
    /// </summary>
    public class ArrayOfEmbeddedDataNode<TEmbedded> : ObjectNode, IHasStartSymbol
        where TEmbedded : EmbeddedObjectNode
    {
        private HashSet<TEmbedded> _nestedNodes = new HashSet<TEmbedded>();
        private List<TEmbedded> _nestedNodesList = new List<TEmbedded>();
        private ObjectAndOffsetSymbolNode _startSymbol;
        private ObjectAndOffsetSymbolNode _endSymbol;
        private IComparer<TEmbedded> _sorter;

        public ArrayOfEmbeddedDataNode(string startSymbolMangledName, string endSymbolMangledName, IComparer<TEmbedded> nodeSorter)
        {
            _startSymbol = new ObjectAndOffsetSymbolNode(this, 0, startSymbolMangledName, true);
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, endSymbolMangledName, true);
            _sorter = nodeSorter;
        }

        public ObjectAndOffsetSymbolNode StartSymbol => _startSymbol;
        public ObjectAndOffsetSymbolNode EndSymbol => _endSymbol;

        public void AddEmbeddedObject(TEmbedded symbol)
        {
            lock (_nestedNodes)
            {
                if (_nestedNodes.Add(symbol))
                {
                    _nestedNodesList.Add(symbol);
                }
                symbol.ContainingNode = this;
            }
        }

        public int IndexOfEmbeddedObject(TEmbedded symbol)
        {
            Debug.Assert(_sorter == null);
            return _nestedNodesList.IndexOf(symbol);
        }

        protected override string GetName(NodeFactory factory) => $"Region {_startSymbol.GetMangledName(factory.NameMangler)}";

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        protected IEnumerable<TEmbedded> NodesList =>  _nestedNodesList;

        protected virtual void GetElementDataForNodes(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            foreach (TEmbedded node in NodesList)
            {
                if (!relocsOnly)
                    node.InitializeOffsetFromBeginningOfArray(builder.CountBytes);

                node.EncodeData(ref builder, factory, relocsOnly);
                if (node is ISymbolDefinitionNode)
                {
                    builder.AddSymbol((ISymbolDefinitionNode)node);
                }
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();

            if (_sorter != null)
                _nestedNodesList.Sort(_sorter);

            builder.AddSymbol(_startSymbol);

            GetElementDataForNodes(ref builder, factory, relocsOnly);

            _endSymbol.SetSymbolOffset(builder.CountBytes);
            builder.AddSymbol(_endSymbol);

            ObjectData objData = builder.ToObjectData();
            return objData;
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return _nestedNodesList.Count == 0;
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            dependencies.Add(StartSymbol, "StartSymbol");
            dependencies.Add(EndSymbol, "EndSymbol");

            return dependencies;
        }
    }

    // TODO: delete this once we review each use of this and put it on the generic plan with the
    //       right element type
    public class ArrayOfEmbeddedDataNode : ArrayOfEmbeddedDataNode<EmbeddedObjectNode>
    {
        public ArrayOfEmbeddedDataNode(string startSymbolMangledName, string endSymbolMangledName, IComparer<EmbeddedObjectNode> nodeSorter)
            : base(startSymbolMangledName, endSymbolMangledName, nodeSorter)
        {
        }
    }
}
