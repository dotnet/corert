// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;

namespace ILCompiler.DependencyAnalysis
{
    internal class WindowsDebugTypeSignatureMapSection : ObjectNode, ISymbolDefinitionNode
    {
        UserDefinedTypeDescriptor _userDefinedTypeDescriptor;

        public WindowsDebugTypeSignatureMapSection(UserDefinedTypeDescriptor userDefinedTypeDescriptor)
        {
            _userDefinedTypeDescriptor = userDefinedTypeDescriptor;
        }

        private ObjectNodeSection _section = new ObjectNodeSection(".dbgtypesignaturemap", SectionType.ReadOnly);
        public override ObjectNodeSection Section => _section;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.WindowsDebugTypeSignatureMapSectionNode;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }


        // returns the DEBUG_S_TYPE_MDTOKEN_MAP subsection as a byte array
        // DEBUG_S_TYPE_MDTOKEN_MAP subsection contains type-index to mdToken mapping
        // 
        // contents of subsection:
        // offset 0,     4   bytes: count of entries in the map
        // offset 4,     8*N bytes: 4 byte type-index + 4 byte 'offset' relative to the start of 'type data'
        // offset 4+8*N, *   bytes: ECMA formatted type signature packed sequentially with no padding
        //
        // 'offset' optimization: for type signatures with size<= 4-bytes
        //                        we can store the signature in offset field such that
        //                        offset = (1 << 31) | (sig[0] << 24 | sig[1] << 16 | sig[2] << 8 | sig[3])
        //                        We chose this bit encoding because sig[0] holds the CorElementType whose
        //                        highest bit is always 0 and the highest bit of offset can be used as a flag
        //                        to indicate that it is not an offset but the signature itself.
        //
        // all entries are sorted by 'offset' field and so offset-based entries are arranged before other
        // (raw-signature) entries (since raw-signature entries are of the form 0x80000000 | signature, and will always be
        // numerically bigger than the offset)
        //
        private DebugInfoBlob GetDebugTypeIndexToTokenMap(ManagedBinaryEmitter pseudoAssembly, ICollection<KeyValuePair<TypeDesc, uint>> completeKnownTypes)
        {
            DebugInfoBlob typeDataBlob = new DebugInfoBlob();
            DebugInfoBlob typeIndexToTokenMapBlob = new DebugInfoBlob();
            List<KeyValuePair<uint, uint>> sigInOffsetEntries = new List<KeyValuePair<uint, uint>>();

            typeIndexToTokenMapBlob.WriteDWORD(checked((uint)completeKnownTypes.Count));
            BlobBuilder blobBuilder = new BlobBuilder();
            foreach (var entry in completeKnownTypes)
            {
                uint typeIndex = entry.Value;
                blobBuilder.Clear();
                pseudoAssembly.EncodeSignatureForType(entry.Key, blobBuilder);

                // if signature fits in 4-bytes, store it in sigInOffsetEntries
                // otherwise store it in the type-data blob
                if (blobBuilder.Count <= 4)
                {
                    uint sigInOffset = 0x80000000;
                    int i = 0;

                    // This is a slightly confusing approach, but this is how one iterates through the bytes in a blobBuilder without flattening it to a byte[]
                    foreach (Blob blob in blobBuilder.GetBlobs())
                    {
                        foreach (byte b in blob.GetBytes())
                        {
                            sigInOffset |= ((uint)b) << (8 * (3 - i));
                            i++;
                        }
                    }

                    // sigInOffsetEntries will be later sorted and appended to typeIndexToTokenMapBlob
                    sigInOffsetEntries.Add(new KeyValuePair<uint, uint>(typeIndex, sigInOffset));
                }
                else
                {
                    typeIndexToTokenMapBlob.WriteDWORD(typeIndex);
                    typeIndexToTokenMapBlob.WriteDWORD(typeDataBlob.Size());
                    typeDataBlob.WriteBuffer(blobBuilder);
                }
            }

            // sort sigInOffsetEntries based on sigInOffset
            sigInOffsetEntries.Sort((KeyValuePair<uint, uint> left, KeyValuePair<uint, uint> right) =>
            {
                if (left.Value < right.Value)
                    return -1;
                if (left.Value == right.Value)
                    return 0;
                return 1;
            });

            // write the sorted sigInOffsetEntries
            foreach (KeyValuePair<uint, uint> sigInOffsetEntry in sigInOffsetEntries)
            {
                typeIndexToTokenMapBlob.WriteDWORD(sigInOffsetEntry.Key);
                typeIndexToTokenMapBlob.WriteDWORD(sigInOffsetEntry.Value);
            }

            // add typeDataBlob to the end of m_typeIndexToTokenMapBlob
            typeIndexToTokenMapBlob.WriteBuffer(typeDataBlob.ToArray());

            return typeIndexToTokenMapBlob;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
            
            DebugInfoBlob debugData = GetDebugTypeIndexToTokenMap(factory.WindowsDebugData.DebugPseudoAssemblySection.PseudoAssembly, factory.WindowsDebugData.UserDefinedTypeDescriptor.CompleteKnownTypes);

            return new ObjectData(debugData.ToArray(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugTypeSignatureMapSection";
        }
    }
}
