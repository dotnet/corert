// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a native layout signature. A signature is a pair where the first item is a pointer
    /// to the TypeManager that contains the native layout info blob of interest, and the second item
    /// is an offset into that native layout info blob
    /// </summary>
    class NativeLayoutSignatureNode : ObjectNode, ISymbolNode
    {
        private static int s_counter = 0;

        private int _id;
        private Vertex _nativeSignature;

        public NativeLayoutSignatureNode(Vertex nativeSignature)
        {
            _nativeSignature = nativeSignature;
            _id = s_counter++;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__NativeLayoutInfoSignature_" + _id);
        }
        public int Offset => 0;

        protected override string GetName() => this.GetMangledName();

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            factory.MetadataManager.NativeLayoutInfo.SaveNativeLayoutInfoWriter();

            ObjectDataBuilder objData = new ObjectDataBuilder(factory);

            objData.Alignment = objData.TargetPointerSize;
            objData.DefinedSymbols.Add(this);

            objData.EmitPointerReloc(factory.TypeManagerIndirection);
            objData.EmitNaturalInt(_nativeSignature.VertexOffset);

            return objData.ToObjectData();
        }
    }
}