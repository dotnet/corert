// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class CoreCLRReadyToRunEntryPointTableNode : ObjectNode, ISymbolDefinitionNode
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

        TargetDetails _target;
        bool _instanceEntryPoints;
        
        List<EntryPoint> _ridToEntryPoint;

        List<byte[]> _uniqueFixups;
        Dictionary<byte[], int> _uniqueFixupIndex;

        List<byte[]> _uniqueSignatures;
        Dictionary<byte[], int> _uniqueSignatureIndex;
        
        public CoreCLRReadyToRunEntryPointTableNode(TargetDetails target, bool instanceEntryPoints)
        {
            _target = target;
            _instanceEntryPoints = instanceEntryPoints;

            _ridToEntryPoint = new List<EntryPoint>();

            _uniqueFixups = new List<byte[]>();
            _uniqueFixupIndex = new Dictionary<byte[], int>(ByteArrayComparer.Instance);

            _uniqueSignatures = new List<byte[]>();
            _uniqueSignatureIndex = new Dictionary<byte[], int>(ByteArrayComparer.Instance);
        }
        
        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append(_instanceEntryPoints ? "__ReadyToRunInstanceEntryPointTable" : "__ReadyToRunMethodEntryPointTable");
        }

        public int Offset => 0;

        public override bool IsShareable => false;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section
        {
            get
            {
                if (_target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public void Add(int rid, int methodIndex, byte[] fixups, byte[] signature, int methodHashCode)
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
            return _instanceEntryPoints ? GetInstanceTableData(factory, relocsOnly) : GetMethodTableData(factory, relocsOnly);
        }

        private ObjectData GetMethodTableData(NodeFactory factory, bool relocOnly)
        {
            NativeWriter arrayWriter = new NativeWriter();

            Section arraySection = arrayWriter.NewSection();
            VertexArray vertexArray = new VertexArray(arraySection);
            arraySection.Place(vertexArray);
            BlobVertex[] fixupBlobs = PlaceBlobs(arraySection, _uniqueFixups);

            for (int rid = 0; rid < _ridToEntryPoint.Count; rid++)
            {
                EntryPoint entryPoint = _ridToEntryPoint[rid];
                if (!entryPoint.IsNull)
                {
                    BlobVertex fixupBlobVertex = (entryPoint.FixupIndex >= 0 ? fixupBlobs[entryPoint.FixupIndex] : null);
                    EntryPointVertex entryPointVertex = new EntryPointVertex((uint)entryPoint.MethodIndex, fixupBlobVertex);
                    vertexArray.Set(rid, entryPointVertex);
                }
            }

            vertexArray.ExpandLayout();

            MemoryStream arrayContent = new MemoryStream();
            arrayWriter.Save(arrayContent);
            return new ObjectData(
                data: arrayContent.ToArray(),
                relocs: null,
                alignment: 8,
                definedSymbols: new ISymbolDefinitionNode[] { this });
        }

        private ObjectData GetInstanceTableData(NodeFactory factory, bool relocOnly)
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

        protected override int ClassCode => 787556329;
    }
}
