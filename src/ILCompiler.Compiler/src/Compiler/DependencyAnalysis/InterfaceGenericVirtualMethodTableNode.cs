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
    internal sealed class InterfaceGenericVirtualMethodTableNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        private Dictionary<MethodDesc, HashSet<MethodDesc>> _interfaceGvmSlots;
        private Dictionary<MethodDesc, Dictionary<TypeDesc, HashSet<int>>> _interfaceImpls;

        private Dictionary<MethodDesc, NativeLayoutInfoTokenNode> _methodDescToNativeLayoutInfoToken;
        private Dictionary<TypeDesc, NativeLayoutInfoTokenNode> _typeDescToNativeLayoutInfoToken;

        public InterfaceGenericVirtualMethodTableNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__interface_gvm_table_End", true);
            _externalReferences = externalReferences;

            _interfaceGvmSlots = new Dictionary<MethodDesc, HashSet<MethodDesc>>();
            _interfaceImpls = new Dictionary<MethodDesc, Dictionary<TypeDesc, HashSet<int>>>();
            _methodDescToNativeLayoutInfoToken = new Dictionary<MethodDesc, NativeLayoutInfoTokenNode>();
            _typeDescToNativeLayoutInfoToken = new Dictionary<TypeDesc, NativeLayoutInfoTokenNode>();
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__interface_gvm_table");
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

            if (!callingMethod.OwningType.IsInterface)
                return dependencyNodes;

            dependencyNodes = new DependencyList();

            // Compute the open method signatures
            MethodDesc openCallingMethod = callingMethod.GetTypicalMethodDefinition();
            MethodDesc openImplementationMethod = implementationMethod.GetTypicalMethodDefinition();
            TypeDesc openImplementationType = implementationType.GetTypeDefinition();

            Vertex openCallingMethodSignature = factory.MetadataManager.NativeLayoutInfo.GetNativeLayoutInfoSignatureForPlacedNameAndSignature(
                factory,
                openCallingMethod.Name,
                factory.MetadataManager.NativeLayoutInfo.GetNativeLayoutInfoSignatureForMethodSignature(factory, openCallingMethod));
            
            Vertex openImplementationMethodSignature = factory.MetadataManager.NativeLayoutInfo.GetNativeLayoutInfoSignatureForPlacedNameAndSignature(
                factory,
                openImplementationMethod.Name,
                factory.MetadataManager.NativeLayoutInfo.GetNativeLayoutInfoSignatureForMethodSignature(factory, openImplementationMethod));

            _methodDescToNativeLayoutInfoToken[openCallingMethod] = factory.NativeLayoutInfoToken(openCallingMethodSignature);
            _methodDescToNativeLayoutInfoToken[openImplementationMethod] = factory.NativeLayoutInfoToken(openImplementationMethodSignature);

            dependencyNodes.Add(new DependencyListEntry(factory.NativeLayoutInfoToken(openCallingMethodSignature), "gvm table needed signature"));
            dependencyNodes.Add(new DependencyListEntry(factory.NativeLayoutInfoToken(openImplementationMethodSignature), "gvm table needed signature"));

            // Add the entry to the interface GVM slots mapping table
            if (!_interfaceGvmSlots.ContainsKey(openCallingMethod))
                _interfaceGvmSlots[openCallingMethod] = new HashSet<MethodDesc>();
            _interfaceGvmSlots[openCallingMethod].Add(openImplementationMethod);

            // If the implementation method is implementing some interface method, compute which
            // interface explicitly implemented on the type that the current method implements an interface method for.
            // We need this because at runtime, the interfaces explicitly implemented on the type will have 
            // runtime-determined signatures that we can use to make generic substitutions and check for interface matching.
            if (!openImplementationType.IsInterface)
            {
                if (!_interfaceImpls.ContainsKey(openImplementationMethod))
                    _interfaceImpls[openImplementationMethod] = new Dictionary<TypeDesc, HashSet<int>>();
                if (!_interfaceImpls[openImplementationMethod].ContainsKey(openImplementationType))
                    _interfaceImpls[openImplementationMethod][openImplementationType] = new HashSet<int>();
                
                int index = 0;
                int numIfacesAdded = 0;
                foreach (var implementedInterfaces in implementationType.RuntimeInterfaces)
                {
                    if (implementedInterfaces == callingMethod.OwningType)
                    {
                        _interfaceImpls[openImplementationMethod][openImplementationType].Add(index);
                        numIfacesAdded++;

                        TypeDesc currentInterface = openImplementationType.RuntimeInterfaces[index];

                        Vertex currentInterfaceSignature = factory.MetadataManager.NativeLayoutInfo.GetNativeLayoutInfoSignatureForPlacedTypeSignature(
                            factory, 
                            currentInterface);

                        _typeDescToNativeLayoutInfoToken[currentInterface] = factory.NativeLayoutInfoToken(currentInterfaceSignature);

                        dependencyNodes.Add(new DependencyListEntry(factory.NativeLayoutInfoToken(currentInterfaceSignature), "gvm table needed signature"));
                    }

                    index++;
                }

                Debug.Assert(numIfacesAdded > 0);
            }

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

            // Emit the interface slot resolution entries
            foreach (var gvm in _interfaceGvmSlots)
            {
                Debug.Assert(gvm.Key.OwningType.IsInterface);

                // Emit the method signature and containing type of the current interface method
                uint typeId = _externalReferences.GetIndex(factory.NecessaryTypeSymbol(gvm.Key.OwningType));
                uint nameAndSigId = _externalReferences.GetIndex(_methodDescToNativeLayoutInfoToken[gvm.Key]);

                Vertex vertex = nativeFormatWriter.GetTuple(
                    nativeFormatWriter.GetUnsignedConstant(typeId),
                    nativeFormatWriter.GetUnsignedConstant(nameAndSigId));

                // Emit the method name / sig and containing type of each GVM target method for the current interface method entry
                vertex = nativeFormatWriter.GetTuple(vertex, nativeFormatWriter.GetUnsignedConstant((uint)gvm.Value.Count));
                foreach (var targetSlot in gvm.Value)
                {
                    nameAndSigId = _externalReferences.GetIndex(_methodDescToNativeLayoutInfoToken[targetSlot]);
                    typeId = _externalReferences.GetIndex(factory.NecessaryTypeSymbol(targetSlot.OwningType));
                    vertex = nativeFormatWriter.GetTuple(
                        vertex,
                        nativeFormatWriter.GetUnsignedConstant(nameAndSigId),
                        nativeFormatWriter.GetUnsignedConstant(typeId));

                    // Emit the interface GVM slot details for each type that implements the interface methods
                    {
                        Debug.Assert(_interfaceImpls.ContainsKey(targetSlot));

                        var ifaceImpls = _interfaceImpls[targetSlot];
                    
                        // First, emit how many types have method implementations for this interface method entry
                        vertex = nativeFormatWriter.GetTuple(vertex, nativeFormatWriter.GetUnsignedConstant((uint)ifaceImpls.Count));
                    
                        // Emit each type that implements the interface method, and the interface signatures for the interfaces implemented by the type
                        foreach (var currentImpl in ifaceImpls)
                        {
                            typeId = _externalReferences.GetIndex(factory.NecessaryTypeSymbol(currentImpl.Key));
                            vertex = nativeFormatWriter.GetTuple(vertex, nativeFormatWriter.GetUnsignedConstant(typeId));
                    
                            // Emit information on which interfaces the current method entry provides implementations for
                            vertex = nativeFormatWriter.GetTuple(vertex, nativeFormatWriter.GetUnsignedConstant((uint)currentImpl.Value.Count));
                            foreach (var ifaceId in currentImpl.Value)
                            {
                                // Emit the signature of the current interface implemented by the method
                                Debug.Assert(((uint)ifaceId) < currentImpl.Key.RuntimeInterfaces.Length);
                                TypeDesc currentInterface = currentImpl.Key.RuntimeInterfaces[ifaceId];
                                uint sigId = _externalReferences.GetIndex(_typeDescToNativeLayoutInfoToken[currentInterface]);
                                vertex = nativeFormatWriter.GetTuple(vertex, nativeFormatWriter.GetUnsignedConstant(sigId));
                            }
                        }
                    }
                }

                int hashCode = gvm.Key.OwningType.GetHashCode();
                gvmHashtable.Append((uint)hashCode, gvmHashtableSection.Place(vertex));
            }

            // Zero out the dictionary so that we AV if someone tries to insert after we're done.
            _interfaceGvmSlots = null;

            MemoryStream stream = new MemoryStream();
            nativeFormatWriter.Save(stream);
            byte[] streamBytes = stream.ToArray();

            _endSymbol.SetSymbolOffset(streamBytes.Length);

            return new ObjectData(streamBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}