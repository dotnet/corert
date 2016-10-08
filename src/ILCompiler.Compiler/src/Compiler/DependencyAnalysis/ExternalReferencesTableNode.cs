// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a node that points to various symbols and can be sequentially addressed.
    /// </summary>
    internal sealed class ExternalReferencesTableNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;

        private Dictionary<ISymbolNode, uint> _insertedSymbolsDictionary = new Dictionary<ISymbolNode, uint>();
        private List<ISymbolNode> _insertedSymbols = new List<ISymbolNode>();

        public ExternalReferencesTableNode()
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, ((ISymbolNode)this).MangledName + "End");
        }

        public ISymbolNode EndSymbol
        {
            get
            {
                return _endSymbol;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return NodeFactory.CompilationUnitPrefix + "__external_references";
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        /// <summary>
        /// Adds a new entry to the table. Thread safety: not thread safe. Expected to be called at the final
        /// object data emission phase from a single thread.
        /// </summary>
        public uint GetIndex(ISymbolNode symbol)
        {
            uint index;
            if (!_insertedSymbolsDictionary.TryGetValue(symbol, out index))
            {
                index = (uint)_insertedSymbols.Count;
                _insertedSymbolsDictionary[symbol] = index;
                _insertedSymbols.Add(symbol);
            }

            return index;
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.DataSection;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        protected override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            // Zero out the dictionary so that we AV if someone tries to insert after we're done.
            _insertedSymbolsDictionary = null;

            var builder = new ObjectDataBuilder(factory);

            foreach (ISymbolNode symbol in _insertedSymbols)
            {
                // TODO: set low bit if the linkage of the symbol is IAT_PVALUE.
                builder.EmitPointerReloc(symbol);
            }

            _endSymbol.SetSymbolOffset(builder.CountBytes);
            
            builder.DefinedSymbols.Add(this);
            builder.DefinedSymbols.Add(_endSymbol);

            return builder.ToObjectData();
        }
    }
}
