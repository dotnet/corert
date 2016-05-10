// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

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
                return NodeFactory.NameMangler.CompilationUnitPrefix + "__embedded_metadata";
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.ReadOnlyDataSection;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

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

