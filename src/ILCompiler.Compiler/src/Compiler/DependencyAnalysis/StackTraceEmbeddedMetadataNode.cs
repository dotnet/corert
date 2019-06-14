// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// BlobIdStackTraceEmbeddedMetadata - holds the binary metadata graph
    /// </summary>
    public class StackTraceEmbeddedMetadataNode : ObjectNode, ISymbolDefinitionNode
    {
        public StackTraceEmbeddedMetadataNode()
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "_stacktrace_embedded_metadata_End", true);
        }

        private ObjectAndOffsetSymbolNode _endSymbol;
        public ISymbolDefinitionNode EndSymbol => _endSymbol;

        public override bool IsShareable => false;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.StackTraceEmbeddedMetadataNode;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("_stacktrace_embedded_metadata");
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node has no relocations.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            byte[] blob = ((PrecomputedMetadataManager)factory.MetadataManager).GetStackTraceBlob(factory);
            _endSymbol.SetSymbolOffset(blob.Length);

            return new ObjectData(
                blob,
                Array.Empty<Relocation>(),
                1,
                new ISymbolDefinitionNode[]
                {
                    this,
                    EndSymbol
                });
        }
    }
}
