// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a node that points to various symbols and can be sequentially addressed.
    /// </summary>
    internal sealed class ExternalReferencesTableNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;

        private Dictionary<SymbolAndDelta, uint> _insertedSymbolsDictionary = new Dictionary<SymbolAndDelta, uint>();
        private List<SymbolAndDelta> _insertedSymbols = new List<SymbolAndDelta>();

        public ExternalReferencesTableNode()
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__external_references_End", true);
        }

        public ISymbolNode EndSymbol => _endSymbol;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__external_references");
        }
        public int Offset => 0;
        public override bool IsShareable => false;

        /// <summary>
        /// Adds a new entry to the table. Thread safety: not thread safe. Expected to be called at the final
        /// object data emission phase from a single thread.
        /// </summary>
        public uint GetIndex(ISymbolNode symbol, int delta = 0)
        {
            SymbolAndDelta key = new SymbolAndDelta(symbol, delta);

            uint index;
            if (!_insertedSymbolsDictionary.TryGetValue(key, out index))
            {
                index = (uint)_insertedSymbols.Count;
                _insertedSymbolsDictionary[key] = index;
                _insertedSymbols.Add(key);
            }

            return index;
        }

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            // Zero out the dictionary so that we AV if someone tries to insert after we're done.
            _insertedSymbolsDictionary = null;

            var builder = new ObjectDataBuilder(factory);

            foreach (SymbolAndDelta symbolAndDelta in _insertedSymbols)
            {
                // TODO: set low bit if the linkage of the symbol is IAT_PVALUE.
                builder.EmitPointerReloc(symbolAndDelta.Symbol, symbolAndDelta.Delta);
            }

            _endSymbol.SetSymbolOffset(builder.CountBytes);
            
            builder.DefinedSymbols.Add(this);
            builder.DefinedSymbols.Add(_endSymbol);

            return builder.ToObjectData();
        }

        struct SymbolAndDelta : IEquatable<SymbolAndDelta>
        {
            public readonly ISymbolNode Symbol;
            public readonly int Delta;

            public SymbolAndDelta(ISymbolNode symbol, int delta)
            {
                Symbol = symbol;
                Delta = delta;
            }

            public bool Equals(SymbolAndDelta other)
            {
                return Symbol == other.Symbol && Delta == other.Delta;
            }

            public override bool Equals(object obj)
            {
                return Equals((SymbolAndDelta)obj);
            }

            public override int GetHashCode()
            {
                return Symbol.GetHashCode();
            }
        }
    }
}
