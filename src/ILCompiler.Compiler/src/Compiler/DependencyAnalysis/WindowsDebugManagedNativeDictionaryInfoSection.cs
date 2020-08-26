// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;

namespace ILCompiler.DependencyAnalysis
{
    internal class WindowsDebugManagedNativeDictionaryInfoSection : ObjectNode, ISymbolDefinitionNode
    {
        public WindowsDebugManagedNativeDictionaryInfoSection()
        {
        }

        private ObjectNodeSection _section = new ObjectNodeSection(".dbgmanagednativedictionaryinfo", SectionType.ReadOnly);
        public override ObjectNodeSection Section => _section;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.WindowsDebugManagedNativeDictionaryInfoSectionNode;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }
        
        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            ObjectDataBuilder objDataBuilder = new ObjectDataBuilder(factory, relocsOnly);

            // Emit number of dictionaries in table
            objDataBuilder.AddSymbol(this);
            IReadOnlyCollection<GenericDictionaryNode> dictionariesEmitted = factory.MetadataManager.GetCompiledGenericDictionaries();
            objDataBuilder.EmitInt(dictionariesEmitted.Count);
            DebugInfoBlob signatureData = new DebugInfoBlob();

            BlobBuilder signatureBlobBuilder = new BlobBuilder();
            BlobBuilder signatureLenBuilder = new BlobBuilder();
            ManagedBinaryEmitter pseudoAssembly = factory.WindowsDebugData.DebugPseudoAssemblySection.PseudoAssembly;

            foreach (GenericDictionaryNode dictionary in dictionariesEmitted)
            {
                objDataBuilder.EmitReloc(dictionary, RelocType.IMAGE_REL_BASED_ADDR32NB);
                objDataBuilder.EmitUInt(signatureData.Size());

                signatureBlobBuilder.Clear();

                int typeDictLen = dictionary.TypeInstantiation.IsNull ? 0 : dictionary.TypeInstantiation.Length;
                int methodDictLen = dictionary.MethodInstantiation.IsNull ? 0 : dictionary.MethodInstantiation.Length;
                signatureBlobBuilder.WriteCompressedInteger(typeDictLen + methodDictLen);

                if (typeDictLen != 0)
                {
                    foreach (TypeDesc type in dictionary.TypeInstantiation)
                    {
                        pseudoAssembly.EncodeSignatureForType(type, signatureBlobBuilder);
                    }
                }

                if (methodDictLen != 0)
                {
                    foreach (TypeDesc type in dictionary.MethodInstantiation)
                    {
                        pseudoAssembly.EncodeSignatureForType(type, signatureBlobBuilder);
                    }
                }

                int blobSize = signatureBlobBuilder.Count;

                signatureLenBuilder.Clear();
                signatureLenBuilder.WriteCompressedInteger(blobSize);

                // Prepend the signature data with a length
                signatureData.WriteBuffer(signatureLenBuilder);
                // And then attach the actual signature data
                signatureData.WriteBuffer(signatureBlobBuilder);
            }

            // Attach signature information to end after all of the rva/offset pairs
            objDataBuilder.EmitBytes(signatureData.ToArray());

            return objDataBuilder.ToObjectData();
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugManagedNativeDictionaryInfoSection";
        }
    }
}
