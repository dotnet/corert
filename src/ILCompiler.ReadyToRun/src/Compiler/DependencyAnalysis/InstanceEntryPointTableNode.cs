// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class InstanceEntryPointTableNode : HeaderTableNode
    {
        private struct EntryPoint
        {
            public static EntryPoint Null = new EntryPoint(-1, -1, -1, 0);
            
            public readonly int MethodIndex;
            public readonly int FixupIndex;
            public readonly int SignatureIndex;
            public readonly int MethodHashCode;

            public bool IsNull => (MethodIndex < 0);
            
            public EntryPoint(int methodIndex, int fixupIndex, int signatureIndex, int methodHashCode)
            {
                MethodIndex = methodIndex;
                FixupIndex = fixupIndex;
                SignatureIndex = signatureIndex;
                MethodHashCode = methodHashCode;
            }
        }

        List<EntryPoint> _ridToEntryPoint;

        List<byte[]> _uniqueFixups;
        Dictionary<byte[], int> _uniqueFixupIndex;

        List<byte[]> _uniqueSignatures;
        Dictionary<byte[], int> _uniqueSignatureIndex;
        
        public InstanceEntryPointTableNode(TargetDetails target)
            : base(target)
        {
            _ridToEntryPoint = new List<EntryPoint>();

            _uniqueFixups = new List<byte[]>();
            _uniqueFixupIndex = new Dictionary<byte[], int>(ByteArrayComparer.Instance);

            _uniqueSignatures = new List<byte[]>();
            _uniqueSignatureIndex = new Dictionary<byte[], int>(ByteArrayComparer.Instance);
        }
        
        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunInstanceEntryPointTable");
        }

        public void Add(MethodCodeNode methodNode, int methodIndex)
        {
            if (methodNode.Method is EcmaMethod ecmaMethod)
            {
                // Strip away the token type bits, keep just the low 24 bits RID
                int rid = MetadataTokens.GetToken(ecmaMethod.Handle) & 0x00FFFFFF;
                Debug.Assert(rid != 0);
                
                // TODO: how to synthesize method fixups blob?
                byte[] fixups = null;
                Add(rid - 1, methodIndex, fixups, signature: null, methodHashCode: 0);
            }
            else
            {
                throw new NotImplementedException();
            }

            // TODO: method instance table
        }

        private void Add(int rid, int methodIndex, byte[] fixups, byte[] signature, int methodHashCode)
        {
            while (_ridToEntryPoint.Count <= rid)
            {
                _ridToEntryPoint.Add(EntryPoint.Null);
            }

            int fixupIndex = -1;
            if (fixups != null)
            {
                if (!_uniqueFixupIndex.TryGetValue(fixups, out fixupIndex))
                {
                    fixupIndex = _uniqueFixups.Count;
                    _uniqueFixupIndex.Add(fixups, fixupIndex);
                    _uniqueFixups.Add(fixups);
                }
            }

            int signatureIndex = -1;
            if (signature != null)
            {
                if (!_uniqueSignatureIndex.TryGetValue(signature, out signatureIndex))
                {
                    signatureIndex = _uniqueSignatures.Count;
                    _uniqueSignatureIndex.Add(signature, signatureIndex);
                    _uniqueSignatures.Add(signature);
                }
            }

            _ridToEntryPoint[rid] = new EntryPoint(methodIndex, fixupIndex, signatureIndex, methodHashCode);
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            NativeWriter hashtableWriter = new NativeWriter();

            Section hashtableSection = hashtableWriter.NewSection();
            VertexHashtable vertexHashtable = new VertexHashtable();
            hashtableSection.Place(vertexHashtable);

            BlobVertex[] fixupBlobs = PlaceBlobs(hashtableSection, _uniqueFixups);
            BlobVertex[] signatureBlobs = PlaceBlobs(hashtableSection, _uniqueSignatures);

            for (int rid = 0; rid < _ridToEntryPoint.Count; rid++)
            {
                EntryPoint entryPoint = _ridToEntryPoint[rid];
                if (!entryPoint.IsNull)
                {
                    BlobVertex fixupBlobVertex = (entryPoint.FixupIndex >= 0 ? fixupBlobs[entryPoint.FixupIndex] : null);
                    BlobVertex signatureBlobVertex = (entryPoint.SignatureIndex >= 0 ? signatureBlobs[entryPoint.SignatureIndex] : null);
                    EntryPointVertex entryPointVertex = new EntryPointWithBlobVertex((uint)entryPoint.MethodIndex, fixupBlobVertex, signatureBlobVertex);
                    hashtableSection.Place(entryPointVertex);
                    vertexHashtable.Append(unchecked((uint)entryPoint.MethodHashCode), entryPointVertex);
                }
            }

            MemoryStream hashtableContent = new MemoryStream();
            hashtableWriter.Save(hashtableContent);
            return new ObjectData(
                data: hashtableContent.ToArray(),
                relocs: null,
                alignment: 8,
                definedSymbols: new ISymbolDefinitionNode[] { this });
        }
        
        private static BlobVertex[] PlaceBlobs(Section section, List<byte[]> blobs)
        {
            BlobVertex[] blobVertices = new BlobVertex[blobs.Count];
            for (int blobIndex = 0; blobIndex < blobs.Count; blobIndex++)
            {
                BlobVertex blobVertex = new BlobVertex(blobs[blobIndex]);
                section.Place(blobVertex);
                blobVertices[blobIndex] = blobVertex;
            }
            return blobVertices;
        }

        protected override int ClassCode => -348722540;
    }
}
