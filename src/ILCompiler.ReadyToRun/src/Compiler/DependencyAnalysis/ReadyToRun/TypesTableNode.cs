// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;

using Internal.NativeFormat;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class TypesTableNode : HeaderTableNode
    {
        List<(int Rid, EcmaType Node)> _typeNodes;
        
        public TypesTableNode(TargetDetails target)
            : base(target)
        {
            _typeNodes = new List<(int Rid, EcmaType Node)>();
        }
        
        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunAvailableTypesTable");
        }

        public int Add(EcmaType eeTypeNode)
        {
            int rid = MetadataTokens.GetToken(eeTypeNode.Handle) & 0x00FFFFFF;
            Debug.Assert(rid != 0);
            int eeTypeIndex = _typeNodes.Count;
            _typeNodes.Add((Rid: rid, Node: eeTypeNode));
            return eeTypeIndex;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            NativeWriter writer = new NativeWriter();
            Section section = writer.NewSection();

            VertexHashtable typesHashtable = new VertexHashtable();
            section.Place(typesHashtable);
            
            foreach ((int Rid, EcmaType Node) typeNode in _typeNodes)
            {
                int hashCode = TypeHashingAlgorithms.ComputeNameHashCode(typeNode.Node.Namespace) ^ TypeHashingAlgorithms.ComputeNameHashCode(typeNode.Node.Name);
                typesHashtable.Append(unchecked((uint)hashCode), section.Place(new UnsignedConstant((uint)typeNode.Rid << 1)));
            }

            MemoryStream writerContent = new MemoryStream();
            writer.Save(writerContent);

            return new ObjectData(
                data: writerContent.ToArray(),
                relocs: null,
                alignment: 8,
                definedSymbols: new ISymbolDefinitionNode[] { this });
        }

        public override int ClassCode => -944318825;
    }
}
