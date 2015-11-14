// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class ArrayOfEmbeddedDataNode : ObjectNode
    {
        HashSet<EmbeddedObjectNode> _nestedNodes = new HashSet<EmbeddedObjectNode>();
        List<EmbeddedObjectNode> _nestedNodesList = new List<EmbeddedObjectNode>();
        ObjectAndOffsetSymbolNode _startSymbol;
        ObjectAndOffsetSymbolNode _endSymbol;
        IComparer<EmbeddedObjectNode> _sorter;

        public ArrayOfEmbeddedDataNode(string startSymbolMangledName, string endSymbolMangledName, IComparer<EmbeddedObjectNode> nodeSorter)
        {
            _startSymbol = new ObjectAndOffsetSymbolNode(this, 0, startSymbolMangledName);
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, endSymbolMangledName);
            _sorter = nodeSorter;
        }

        public void AddEmbeddedObject(EmbeddedObjectNode symbol)
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
            foreach (EmbeddedObjectNode node in _nestedNodesList)
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
}
