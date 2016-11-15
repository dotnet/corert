﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a blob of native metadata describing assemblies, the types in them, and their members.
    /// The data is used at runtime to e.g. support reflection.
    /// </summary>
    internal sealed class MetadataNode : ObjectNode, ISymbolNode
    {
        ObjectAndOffsetSymbolNode _endSymbol;

        public MetadataNode()
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, this.GetMangledName() + "End");
        }

        public ISymbolNode EndSymbol => _endSymbol;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(NodeFactory.CompilationUnitPrefix).Append("__embedded_metadata");
        }
        public int Offset => 0;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node has no relocations.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            byte[] blob = factory.MetadataManager.GetMetadataBlob();
            _endSymbol.SetSymbolOffset(blob.Length);

            return new ObjectData(
                blob,
                Array.Empty<Relocation>(),
                1,
                new ISymbolNode[]
                {
                    this,
                    _endSymbol
                });
        }
    }
}
