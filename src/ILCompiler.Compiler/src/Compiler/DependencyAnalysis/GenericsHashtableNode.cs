// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a hashtable of all compiled generic type instantiations
    /// </summary>
    internal sealed class GenericsHashtableNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        private NativeWriter _writer;
        private Section _tableSection;
        private VertexHashtable _hashtable;

        private HashSet<TypeDesc> _genericTypeInstantiations;

        public GenericsHashtableNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__generics_hashtable_End", true);
            _externalReferences = externalReferences;

            _writer = new NativeWriter();
            _hashtable = new VertexHashtable();
            _tableSection = _writer.NewSection();
            _tableSection.Place(_hashtable);

            _genericTypeInstantiations = new HashSet<TypeDesc>();
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__generics_hashtable");
        }

        public ISymbolNode EndSymbol => _endSymbol;
        public int Offset => 0;
        public override bool IsShareable => false;
        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName() => this.GetMangledName();

        public void AddInstantiatedTypeEntry(NodeFactory factory, TypeDesc type)
        {
            Debug.Assert(type.HasInstantiation && !type.IsGenericDefinition);

            if (!_genericTypeInstantiations.Add(type))
                return;

            var typeSymbol = factory.NecessaryTypeSymbol(type);
            uint instantiationId = _externalReferences.GetIndex(typeSymbol);
            Vertex hashtableEntry = _writer.GetUnsignedConstant(instantiationId);

            _hashtable.Append((uint)type.GetHashCode(), _tableSection.Place(hashtableEntry));
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            // Zero out the hashset so that we AV if someone tries to insert after we're done.
            _genericTypeInstantiations = null;

            MemoryStream stream = new MemoryStream();
            _writer.Save(stream);
            byte[] streamBytes = stream.ToArray();

            _endSymbol.SetSymbolOffset(streamBytes.Length);

            return new ObjectData(streamBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}