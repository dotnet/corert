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
    internal sealed class GenericVirtualMethodTableNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        private Dictionary<MethodDesc, Dictionary<TypeDesc, MethodDesc>> _gvmImplemenations;
        private Dictionary<MethodDesc, NativeLayoutInfoTokenNode> _methodDescToNativeLayoutInfoToken;

        public GenericVirtualMethodTableNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__gvm_table_End", true);
            _externalReferences = externalReferences;

            _gvmImplemenations = new Dictionary<MethodDesc, Dictionary<TypeDesc, MethodDesc>>();
            _methodDescToNativeLayoutInfoToken = new Dictionary<MethodDesc, NativeLayoutInfoTokenNode>();
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__gvm_table");
        }

        public ISymbolNode EndSymbol => _endSymbol;
        public int Offset => 0;
        public override bool IsShareable => false;
        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName() => this.GetMangledName();

        public DependencyList AddGenericVirtualMethodImplementation(NodeFactory factory, MethodDesc callingMethod, TypeDesc implementationType, MethodDesc implementationMethod)
        {
            DependencyList dependencyNodes = null;

            if (callingMethod.OwningType.IsInterface)
                return dependencyNodes;

            dependencyNodes = new DependencyList();

            // Compute the open method signatures
            MethodDesc openCallingMethod = callingMethod.GetTypicalMethodDefinition();
            MethodDesc openImplementationMethod = implementationMethod.GetTypicalMethodDefinition();
            TypeDesc openImplementationType = implementationType.GetTypeDefinition();

            Vertex openCallingMethodSignature = factory.MetadataManager.GetNativeLayoutInfoNode().GetNativeLayoutInfoSignatureForPlacedNameAndSignature(
                factory,
                openCallingMethod.Name,
                factory.MetadataManager.GetNativeLayoutInfoNode().GetNativeLayoutInfoSignatureForMethodSignature(factory, openCallingMethod));

            Vertex openImplementationMethodSignature = factory.MetadataManager.GetNativeLayoutInfoNode().GetNativeLayoutInfoSignatureForPlacedNameAndSignature(
                factory,
                openImplementationMethod.Name,
                factory.MetadataManager.GetNativeLayoutInfoNode().GetNativeLayoutInfoSignatureForMethodSignature(factory, openImplementationMethod));

            _methodDescToNativeLayoutInfoToken[openCallingMethod] = factory.NativeLayoutInfoToken(openCallingMethodSignature);
            _methodDescToNativeLayoutInfoToken[openImplementationMethod] = factory.NativeLayoutInfoToken(openImplementationMethodSignature);

            dependencyNodes.Add(new DependencyListEntry(factory.NativeLayoutInfoToken(openCallingMethodSignature), "gvm table needed signature"));
            dependencyNodes.Add(new DependencyListEntry(factory.NativeLayoutInfoToken(openImplementationMethodSignature), "gvm table needed signature"));

            // Insert open method signatures into the GVM map
            if (!_gvmImplemenations.ContainsKey(openCallingMethod))
                _gvmImplemenations[openCallingMethod] = new Dictionary<TypeDesc, MethodDesc>();

            _gvmImplemenations[openCallingMethod][openImplementationMethod.OwningType] = openImplementationMethod;

            return dependencyNodes;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            NativeWriter nativeFormatWriter = new NativeWriter();
            VertexHashtable gvmHashtable = new VertexHashtable();

            Section gvmHashtableSection = nativeFormatWriter.NewSection();
            gvmHashtableSection.Place(gvmHashtable);

            // Emit the GVM target information entries
            foreach (var gvm in _gvmImplemenations)
            {
                Debug.Assert(!gvm.Key.OwningType.IsInterface);

                foreach (var targetMethod in gvm.Value)
                {
                    uint callingTypeId = _externalReferences.GetIndex(factory.NecessaryTypeSymbol(gvm.Key.OwningType));
                    Vertex vertex = nativeFormatWriter.GetUnsignedConstant(callingTypeId);

                    uint targetTypeId = _externalReferences.GetIndex(factory.NecessaryTypeSymbol(targetMethod.Key));
                    vertex = nativeFormatWriter.GetTuple(vertex, nativeFormatWriter.GetUnsignedConstant(targetTypeId));

                    Debug.Assert(_methodDescToNativeLayoutInfoToken.ContainsKey(gvm.Key));
                    Debug.Assert(_methodDescToNativeLayoutInfoToken.ContainsKey(targetMethod.Value));

                    uint callingNameAndSigId = _externalReferences.GetIndex(_methodDescToNativeLayoutInfoToken[gvm.Key]);
                    vertex = nativeFormatWriter.GetTuple(vertex, nativeFormatWriter.GetUnsignedConstant(callingNameAndSigId));

                    uint targetNameAndSigId = _externalReferences.GetIndex(_methodDescToNativeLayoutInfoToken[targetMethod.Value]);
                    vertex = nativeFormatWriter.GetTuple(vertex, nativeFormatWriter.GetUnsignedConstant(targetNameAndSigId));

                    int hashCode = gvm.Key.OwningType.GetHashCode();
                    hashCode = ((hashCode << 13) ^ hashCode) ^ targetMethod.Key.GetHashCode();

                    gvmHashtable.Append((uint)hashCode, gvmHashtableSection.Place(vertex));
                }
            }

            MemoryStream stream = new MemoryStream();
            nativeFormatWriter.Save(stream);
            byte[] streamBytes = stream.ToArray();

            _endSymbol.SetSymbolOffset(streamBytes.Length);

            return new ObjectData(streamBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}