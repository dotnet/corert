// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a native layout signature. A signature is a pair where the first item is a pointer
    /// to the TypeManager that contains the native layout info blob of interest, and the second item
    /// is an offset into that native layout info blob
    /// </summary>
    class NativeLayoutSignatureNode : ObjectNode, ISymbolNode
    {
        private TypeSystemEntity _identity;
        private Utf8String _identityPrefix;
        private NativeLayoutSavedVertexNode _nativeSignature;

        public NativeLayoutSignatureNode(NativeLayoutSavedVertexNode nativeSignature, TypeSystemEntity identity, Utf8String identityPrefix)
        {
            _nativeSignature = nativeSignature;
            _identity = identity;
            _identityPrefix = identityPrefix;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            Utf8String identityString;
            if (_identity is MethodDesc)
            {
                identityString = nameMangler.GetMangledMethodName((MethodDesc)_identity);
            }
            else if (_identity is TypeDesc)
            {
                identityString = nameMangler.GetMangledTypeName((TypeDesc)_identity);
            }
            else if (_identity is FieldDesc)
            {
                identityString = nameMangler.GetMangledFieldName((FieldDesc)_identity);
            }
            else
            {
                Debug.Assert(false);
                identityString = new Utf8String("unknown");
            }

            sb.Append(nameMangler.CompilationUnitPrefix).Append(_identityPrefix).Append(identityString);
        }

        public int Offset => 0;
        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            dependencies.Add(new DependencyListEntry(_nativeSignature, "NativeLayoutSignatureNode target vertex"));
            return dependencies;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            // Ensure native layout is saved to get valid Vertex offsets
            factory.MetadataManager.NativeLayoutInfo.SaveNativeLayoutInfoWriter(factory);

            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);

            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            objData.EmitPointerReloc(factory.TypeManagerIndirection);
            objData.EmitNaturalInt(_nativeSignature.SavedVertex.VertexOffset);

            return objData.ToObjectData();
        }
    }
}