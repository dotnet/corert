// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
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

            ReadyToRunTableManager r2rManager = (ReadyToRunTableManager)factory.MetadataManager;

            foreach (DefinedTypeInfo definedTypeInfo in r2rManager.GetDefinedTypes())
            {
                TypeDefinitionHandle typeDefHandle = definedTypeInfo.Handle;
                int hashCode = 0;
                for (; ; )
                {
                    TypeDefinition typeDef = definedTypeInfo.Module.MetadataReader.GetTypeDefinition(typeDefHandle);
                    string namespaceName = definedTypeInfo.Module.MetadataReader.GetString(typeDef.Namespace);
                    string typeName = definedTypeInfo.Module.MetadataReader.GetString(typeDef.Name);
                    hashCode ^= ReadyToRunHashCode.NameHashCode(namespaceName, typeName);
                    if (!typeDef.Attributes.IsNested())
                    {
                        break;
                    }
                    typeDefHandle = typeDef.GetDeclaringType();
                }
                typesHashtable.Append(unchecked((uint)hashCode), section.Place(new UnsignedConstant(((uint)MetadataTokens.GetRowNumber(definedTypeInfo.Handle) << 1) | 0)));
            }

            foreach (ExportedTypeInfo exportedTypeInfo in r2rManager.GetExportedTypes())
            {
                ExportedType exportedType = exportedTypeInfo.Module.MetadataReader.GetExportedType(exportedTypeInfo.Handle);
                string namespaceName = exportedTypeInfo.Module.MetadataReader.GetString(exportedType.Namespace);
                string typeName = exportedTypeInfo.Module.MetadataReader.GetString(exportedType.Name);
                int hashCode = ReadyToRunHashCode.NameHashCode(namespaceName, typeName);
                typesHashtable.Append(unchecked((uint)hashCode), section.Place(new UnsignedConstant(((uint)MetadataTokens.GetRowNumber(exportedTypeInfo.Handle) << 1) | 1)));
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
