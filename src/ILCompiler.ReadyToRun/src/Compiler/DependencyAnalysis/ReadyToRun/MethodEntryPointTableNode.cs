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
    public class MethodEntryPointTableNode : HeaderTableNode
    {
        private struct EntryPoint
        {
            public static EntryPoint Null = new EntryPoint(-1, -1);
            
            public readonly int MethodIndex;
            public readonly int FixupIndex;

            public bool IsNull => (MethodIndex < 0);
            
            public EntryPoint(int methodIndex, int fixupIndex)
            {
                MethodIndex = methodIndex;
                FixupIndex = fixupIndex;
            }
        }

        List<EntryPoint> _ridToEntryPoint;

        List<byte[]> _uniqueFixups;
        Dictionary<byte[], int> _uniqueFixupIndex;
        
        public MethodEntryPointTableNode(TargetDetails target)
            : base(target)
        {
            _ridToEntryPoint = new List<EntryPoint>();

            _uniqueFixups = new List<byte[]>();
            _uniqueFixupIndex = new Dictionary<byte[], int>(ByteArrayComparer.Instance);
        }
        
        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunMethodEntryPointTable");
        }

        public void Add(MethodCodeNode methodNode, int methodIndex)
        {
            if (methodNode.Method is EcmaMethod ecmaMethod)
            {
                // Strip away the token type bits, keep just the low 24 bits RID
                int rid = MetadataTokens.GetToken(ecmaMethod.Handle) & 0x00FFFFFF;
                Debug.Assert(rid != 0);
                
                rid--;

                // TODO: how to synthesize method fixups blob?
                byte[] fixups = null;

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


                _ridToEntryPoint[rid] = new EntryPoint(methodIndex, fixupIndex);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
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
