// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an array of <typeparamref name="TEmbedded"/> nodes. The contents of this node will be emitted
    /// by placing a starting symbol, followed by contents of <typeparamref name="TEmbedded"/> nodes (optionally
    /// sorted using provided comparer), followed by ending symbol.
    /// </summary>
    public class ArrayOfEmbeddedDataNode<TEmbedded> : ObjectNode
        where TEmbedded : EmbeddedObjectNode
    {
        private HashSet<TEmbedded> _nestedNodes = new HashSet<TEmbedded>();
        private List<TEmbedded> _nestedNodesList = new List<TEmbedded>();
        private ObjectAndOffsetSymbolNode _startSymbol;
        private ObjectAndOffsetSymbolNode _endSymbol;
        private IComparer<TEmbedded> _sorter;

        public ArrayOfEmbeddedDataNode(string startSymbolMangledName, string endSymbolMangledName, IComparer<TEmbedded> nodeSorter)
        {
            _startSymbol = new ObjectAndOffsetSymbolNode(this, 0, startSymbolMangledName);
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, endSymbolMangledName);
            _sorter = nodeSorter;
        }

        public void AddEmbeddedObject(TEmbedded symbol)
        {
            if (_nestedNodes.Add(symbol))
            {
                _nestedNodesList.Add(symbol);
            }
        }

        public override string GetName()
        {
            return "Region " + ((ISymbolNode)_startSymbol).MangledName;
        }

        public override string Section
        {
            get
            {
                return "data";
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);
            builder.Alignment = factory.Target.PointerSize;

            if (_sorter != null)
                _nestedNodesList.Sort(_sorter);

            builder.DefinedSymbols.Add(_startSymbol);
            foreach (TEmbedded node in _nestedNodesList)
            {
                if (!relocsOnly)
                    node.Offset = builder.CountBytes;

                node.EncodeData(ref builder, factory, relocsOnly);
                if (node is ISymbolNode)
                {
                    builder.DefinedSymbols.Add((ISymbolNode)node);
                }
            }
            _endSymbol.SetSymbolOffset(builder.CountBytes);
            builder.DefinedSymbols.Add(_endSymbol);

            ObjectData objData = builder.ToObjectData();
            return objData;
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
