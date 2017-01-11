﻿using Internal.NativeFormat;
using Internal.Text;
using System;
using System.IO;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a hash table of resources within the resource blob in the image.
    /// </summary>
    internal class ResourceIndexNode : ObjectNode, ISymbolNode
    {
        private ResourceDataNode _resourceDataNode;

        public ResourceIndexNode(ResourceDataNode resourceDataNode)
        {
            _resourceDataNode = resourceDataNode;
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__embedded_resourceindex_End", true);
        }


        private ObjectAndOffsetSymbolNode _endSymbol;

        public ISymbolNode EndSymbol => _endSymbol;

        public override bool IsShareable => false;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__embedded_resourceindex");
        }

        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node has no relocations.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            byte[] blob = GenerateIndexBlob();
            return new ObjectData(
                blob,
                Array.Empty<Relocation>(),
                1,
                new ISymbolNode[]
                {
                                this,
                                EndSymbol
                });
        }

        /// <summary>
        /// Builds a native hashtable containing data about each manifest resource
        /// </summary>
        /// <returns></returns>
        private byte[] GenerateIndexBlob()
        {
            NativeWriter nativeWriter = new NativeWriter();
            Section indexHashtableSection = nativeWriter.NewSection();
            VertexHashtable indexHashtable = new VertexHashtable();
            indexHashtableSection.Place(indexHashtable);

            // Build a table with a tuple of Assembly Full Name, Resource Name, Offset within the resource data blob, Length
            // for each resource. 
            // This generates a hashtable for the convenience of managed code since there's
            // a reader for VertexHashtable, but not for VertexSequence.

            foreach (ResourceIndexData indexData in _resourceDataNode.IndexData)
            {
                Vertex asmName = nativeWriter.GetStringConstant(indexData.AssemblyName);
                Vertex resourceName = nativeWriter.GetStringConstant(indexData.ResourceName);
                Vertex offsetVertex = nativeWriter.GetUnsignedConstant((uint)indexData.NativeOffset);
                Vertex lengthVertex = nativeWriter.GetUnsignedConstant((uint)indexData.Length);

                Vertex indexVertex = nativeWriter.GetTuple(asmName, resourceName);
                indexVertex = nativeWriter.GetTuple(indexVertex, offsetVertex);
                indexVertex = nativeWriter.GetTuple(indexVertex, lengthVertex);

                int hashCode = TypeHashingAlgorithms.ComputeNameHashCode(indexData.AssemblyName);
                indexHashtable.Append((uint)hashCode, indexHashtableSection.Place(indexVertex));
            }

            MemoryStream stream = new MemoryStream();
            nativeWriter.Save(stream);
            byte[] blob = stream.ToArray();
            _endSymbol.SetSymbolOffset(blob.Length);
            return blob;
        }
    }
}