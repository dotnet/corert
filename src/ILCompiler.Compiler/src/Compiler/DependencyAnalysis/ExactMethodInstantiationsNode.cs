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
    /// Represents a map between reflection metadata and generated method bodies.
    /// </summary>
    internal sealed class ExactMethodInstantiationsNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        private NativeWriter _nativeWriter;
        private Section _nativeSection;
        private VertexHashtable _hashtable;

        private HashSet<MethodDesc> _exactMethodInsantiationsList;

        public ExactMethodInstantiationsNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__exact_method_instantiations_End", true);
            _externalReferences = externalReferences;

            _nativeWriter = new NativeWriter();
            _hashtable = new VertexHashtable();
            _nativeSection = _nativeWriter.NewSection();
            _nativeSection.Place(_hashtable);

            _exactMethodInsantiationsList = new HashSet<MethodDesc>();
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__exact_method_instantiations");
        }

        public ISymbolNode EndSymbol => _endSymbol;
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

            // Zero out the hashset so that we AV if someone tries to insert after we're done.
            _exactMethodInsantiationsList = null;

            MemoryStream stream = new MemoryStream();
            _nativeWriter.Save(stream);

            byte[] streamBytes = stream.ToArray();

            _endSymbol.SetSymbolOffset(streamBytes.Length);

            return new ObjectData(streamBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }

        public bool AddExactMethodInstantiationEntry(NodeFactory factory, MethodDesc method)
        {
            // Check if we already wrote this method to the hashtable
            if (!_exactMethodInsantiationsList.Add(method))
                return false;

            // Nothing to add for interface methods because they have no implementations of their own...
            if (method.OwningType.IsInterface)
                return false;

            if (method.IsRuntimeDeterminedExactMethod)
                return false;

            // This hashtable is only for method instantiations that don't use generic dictionaries,
            // so check if the given method is shared before proceeding
            if (method.IsSharedByGenericInstantiations || method.GetCanonMethodTarget(CanonicalFormKind.Specific) != method)
                return false;

            bool getUnboxingStub = (method.OwningType.IsValueType || method.OwningType.IsEnum) && !method.Signature.IsStatic;
            IMethodNode methodEntryPointNode = factory.MethodEntrypoint(method, getUnboxingStub);

            Vertex methodSignature = factory.MetadataManager.NativeLayoutInfo.GetNativeLayoutInfoSignatureForMethod(factory, method, _nativeWriter);
            Vertex methodPointer = _nativeWriter.GetUnsignedConstant(_externalReferences.GetIndex(methodEntryPointNode));

            // Make the generic method entry vertex

            Vertex entry = _nativeWriter.GetTuple(methodSignature, methodPointer);

            // Add to the hash table, hashed by the containing type's hashcode
            uint hashCode = (uint)method.OwningType.GetHashCode();
            _hashtable.Append(hashCode, _nativeSection.Place(entry));

            return true;
        }
    }
}