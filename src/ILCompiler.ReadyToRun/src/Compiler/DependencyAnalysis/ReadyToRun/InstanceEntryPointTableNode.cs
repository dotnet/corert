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
        public InstanceEntryPointTableNode(TargetDetails target)
            : base(target)
        {
        }
        
        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunInstanceEntryPointTable");
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            if (relocsOnly)
            {
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, Array.Empty<ISymbolDefinitionNode>());
            }

            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            NativeWriter hashtableWriter = new NativeWriter();

            Section hashtableSection = hashtableWriter.NewSection();
            VertexHashtable vertexHashtable = new VertexHashtable();
            hashtableSection.Place(vertexHashtable);

            Dictionary<byte[], BlobVertex> uniqueFixups = new Dictionary<byte[], BlobVertex>(ByteArrayComparer.Instance);
            Dictionary<byte[], BlobVertex> uniqueSignatures = new Dictionary<byte[], BlobVertex>(ByteArrayComparer.Instance);

            foreach (MethodWithGCInfo method in r2rFactory.EnumerateCompiledMethods())
            {
                if (method.Method.HasInstantiation || method.Method.OwningType.HasInstantiation)
                {
                    int methodIndex = r2rFactory.RuntimeFunctionsTable.GetIndex(method);

                    ArraySignatureBuilder signatureBuilder = new ArraySignatureBuilder();
                    signatureBuilder.EmitMethodSignature(
                        method.Method, 
                        constrainedType: null,
                        default(ModuleToken),
                        enforceDefEncoding: true,
                        method.SignatureContext,
                        isUnboxingStub: false, 
                        isInstantiatingStub: false);
                    byte[] signature = signatureBuilder.ToArray();
                    BlobVertex signatureBlob;
                    if (!uniqueSignatures.TryGetValue(signature, out signatureBlob))
                    {
                        signatureBlob = new BlobVertex(signature);
                        hashtableSection.Place(signatureBlob);
                        uniqueSignatures.Add(signature, signatureBlob);
                    }

                    byte[] fixup = method.GetFixupBlob(factory);
                    BlobVertex fixupBlob = null;
                    if (fixup != null && !uniqueFixups.TryGetValue(fixup, out fixupBlob))
                    {
                        fixupBlob = new BlobVertex(fixup);
                        hashtableSection.Place(fixupBlob);
                        uniqueFixups.Add(fixup, fixupBlob);
                    }

                    EntryPointVertex entryPointVertex = new EntryPointWithBlobVertex((uint)methodIndex, fixupBlob, signatureBlob);
                    hashtableSection.Place(entryPointVertex);
                    vertexHashtable.Append(unchecked((uint)ReadyToRunHashCode.MethodHashCode(method.Method)), entryPointVertex);
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

        public override int ClassCode => -348722540;
    }
}
