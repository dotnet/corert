// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a map between EETypes and metadata records within the <see cref="MetadataNode"/>.
    /// </summary>
    internal sealed class TypeMetadataMapNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;

        public TypeMetadataMapNode()
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
                return "__type_to_metadata_map";
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
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

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            ObjectDataBuilder builder = new ObjectDataBuilder(factory);

            foreach (var mappingEntry in factory.MetadataManager.GetTypeDefinitionMapping())
            {
                var node = (EETypeNode)factory.ConstructedTypeSymbol(mappingEntry.Entity);
                if (node.Marked)
                {
                    // TODO: this format got very inefficient due to not being able to use RVAs
                    //       replace with a hash table

                    builder.EmitPointerReloc(node);
                    builder.EmitInt(mappingEntry.MetadataHandle);

                    if (factory.Target.PointerSize == 8)
                        builder.EmitInt(0); // Pad
                }
            }

            _endSymbol.SetSymbolOffset(builder.CountBytes);
            
            builder.DefinedSymbols.Add(this);
            builder.DefinedSymbols.Add(_endSymbol);

            return builder.ToObjectData();
        }
    }
}
