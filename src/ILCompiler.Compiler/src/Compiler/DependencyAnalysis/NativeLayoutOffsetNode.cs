// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an offset into the native layout info blob.
    /// </summary>
    internal sealed class NativeLayoutOffsetNode : ObjectNode, ISymbolNode
    {
        private static int s_counter = 0;

        private int _id;
        private Vertex _nativeVertex;

        public NativeLayoutOffsetNode(Vertex nativeVertex)
        {
            _nativeVertex = nativeVertex;
            _id = s_counter++;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__NativeLayoutInfoToken_" + _id);
        }

        public int Offset => 0;
        public override bool IsShareable => false;
        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            factory.MetadataManager.NativeLayoutInfo.SaveNativeLayoutInfoWriter();

            ObjectDataBuilder objData = new ObjectDataBuilder(factory);

            objData.Alignment = sizeof(uint);
            objData.DefinedSymbols.Add(this);

            objData.EmitInt(_nativeVertex.VertexOffset);

            return objData.ToObjectData();
        }
    }
}