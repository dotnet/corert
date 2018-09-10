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
        public TypesTableNode(TargetDetails target)
            : base(target) {}
        
        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunAvailableTypesTable");
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            NativeWriter writer = new NativeWriter();
            Section section = writer.NewSection();

            VertexHashtable typesHashtable = new VertexHashtable();
            section.Place(typesHashtable);

            foreach (TypeDesc type in ((ReadyToRunTableManager)factory.MetadataManager).GetTypesWithAvailableTypes())
            {
                int rid = 0;
                if (type.GetTypeDefinition() is EcmaType ecmaType)
                {
                    rid = MetadataTokens.GetToken(ecmaType.Handle) & 0x00FFFFFF;
                    Debug.Assert(rid != 0);

                    int hashCode = GetHashCode(ecmaType);
                    typesHashtable.Append(unchecked((uint)hashCode), section.Place(new UnsignedConstant((uint)rid << 1)));
                }
                else if (type.IsArray || type.IsMdArray)
                {
                    // TODO: arrays in type table - should we have a recursive descent into composite types here
                    // and e.g. add the element type to the type table in case of arrays?
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            MemoryStream writerContent = new MemoryStream();
            writer.Save(writerContent);

            return new ObjectData(
                data: writerContent.ToArray(),
                relocs: null,
                alignment: 8,
                definedSymbols: new ISymbolDefinitionNode[] { this });
        }

        private int GetHashCode(EcmaType type)
        {
            int hashCode = ComputeNameHashCode(type);
            while (type.ContainingType != null)
            {
                type = (EcmaType)type.ContainingType;
                hashCode ^= ComputeNameHashCode(type);
            }
            return hashCode;
        }
        private int ComputeNameHashCode(EcmaType type)
        {
            int hashCode = TypeHashingAlgorithms.ComputeNameHashCode(type.Name);
            if (!type.Namespace.Equals(""))
            {
                hashCode = TypeHashingAlgorithms.ComputeNameHashCode(type.Namespace) ^ hashCode;
            }
            return hashCode;
        }

        public override int ClassCode => -944318825;
    }
}
