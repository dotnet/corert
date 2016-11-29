// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

using InvokeTableFlags = Internal.Runtime.InvokeTableFlags;
using FatFunctionPointerConstants = Internal.Runtime.FatFunctionPointerConstants;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a map between reflection metadata and generated method bodies.
    /// </summary>
    internal sealed class ReflectionInvokeMapNode : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public ReflectionInvokeMapNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__method_to_entrypoint_map_End", true);
            _externalReferences = externalReferences;
        }

        public ISymbolNode EndSymbol
        {
            get
            {
                return _endSymbol;
            }
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__method_to_entrypoint_map");
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

            var writer = new NativeWriter();
            var typeMapHashTable = new VertexHashtable();

            Section hashTableSection = writer.NewSection();
            hashTableSection.Place(typeMapHashTable);

            // Get a list of all methods that have a method body and metadata from the metadata manager.
            foreach (var mappingEntry in factory.MetadataManager.GetMethodMapping())
            {
                MethodDesc method = mappingEntry.Entity;

                // The current format requires us to have an EEType for the owning type. We might want to lift this.
                if (!factory.MetadataManager.TypeGeneratesEEType(method.OwningType))
                    continue;

                // We have a method body, we have a metadata token, but we can't get an invoke stub. Bail.
                if (!factory.MetadataManager.HasReflectionInvokeStub(method))
                    continue;

                InvokeTableFlags flags = 0;

                if (method.HasInstantiation)
                    flags |= InvokeTableFlags.IsGenericMethod;

                if (method.GetCanonMethodTarget(CanonicalFormKind.Specific).RequiresInstArg())
                    flags |= InvokeTableFlags.RequiresInstArg;

                // TODO: better check for default public(!) constructor
                if (method.IsConstructor && method.Signature.Length == 0)
                    flags |= InvokeTableFlags.IsDefaultConstructor;

                // TODO: HasVirtualInvoke

                if (!method.IsAbstract)
                    flags |= InvokeTableFlags.HasEntrypoint;

                // Once we have a true multi module compilation story, we'll need to start emitting entries where this is not set.
                flags |= InvokeTableFlags.HasMetadataHandle;

                // TODO: native signature for P/Invokes and NativeCallable methods
                if (method.IsRawPInvoke() || method.IsNativeCallable)
                    continue;

                // Grammar of an entry in the hash table:
                // Flags + DeclaringType + MetadataHandle/NameAndSig + Entrypoint + DynamicInvokeMethod + [NumGenericArgs + GenericArgs]

                Vertex vertex = writer.GetUnsignedConstant((uint)flags);

                if ((flags & InvokeTableFlags.HasMetadataHandle) != 0)
                {
                    // Only store the offset portion of the metadata handle to get better integer compression
                    vertex = writer.GetTuple(vertex,
                        writer.GetUnsignedConstant((uint)(mappingEntry.MetadataHandle & MetadataGeneration.MetadataOffsetMask)));
                }
                else
                {
                    // TODO: no MD handle case
                }

                // Go with a necessary type symbol. It will be upgraded to a constructed one if a constructed was emitted.
                IEETypeNode owningTypeSymbol = factory.NecessaryTypeSymbol(method.OwningType);
                vertex = writer.GetTuple(vertex,
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(owningTypeSymbol)));

                if ((flags & InvokeTableFlags.HasEntrypoint) != 0)
                {
                    vertex = writer.GetTuple(vertex,
                        writer.GetUnsignedConstant(_externalReferences.GetIndex(
                            factory.MethodEntrypoint(method.GetCanonMethodTarget(CanonicalFormKind.Specific)))));
                }

                // TODO: data to generate the generic dictionary with the type loader
                MethodDesc invokeStubMethod = factory.MetadataManager.GetReflectionInvokeStub(method);
                MethodDesc canonInvokeStubMethod = invokeStubMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                if (invokeStubMethod != canonInvokeStubMethod)
                {
                    vertex = writer.GetTuple(vertex,
                        writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.FatFunctionPointer(invokeStubMethod), FatFunctionPointerConstants.Offset) << 1));
                }
                else
                {
                    vertex = writer.GetTuple(vertex,
                        writer.GetUnsignedConstant(_externalReferences.GetIndex(factory.MethodEntrypoint(invokeStubMethod)) << 1));
                }

                if ((flags & InvokeTableFlags.IsGenericMethod) != 0)
                {
                    if ((flags & InvokeTableFlags.RequiresInstArg) == 0 || (flags & InvokeTableFlags.HasEntrypoint) == 0)
                    {
                        VertexSequence args = new VertexSequence();
                        for (int i = 0; i < method.Instantiation.Length; i++)
                        {
                            uint argId = _externalReferences.GetIndex(factory.NecessaryTypeSymbol(method.Instantiation[i]));
                            args.Append(writer.GetUnsignedConstant(argId));
                        }
                        vertex = writer.GetTuple(vertex, args);
                    }
                    else
                    {
                        uint dictionaryId = _externalReferences.GetIndex(factory.MethodGenericDictionary(method));
                        vertex = writer.GetTuple(vertex, writer.GetUnsignedConstant(dictionaryId));
                    }
                }

                int hashCode = method.GetCanonMethodTarget(CanonicalFormKind.Specific).OwningType.GetHashCode();
                typeMapHashTable.Append((uint)hashCode, hashTableSection.Place(vertex));
            }

            MemoryStream ms = new MemoryStream();
            writer.Save(ms);
            byte[] hashTableBytes = ms.ToArray();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }
    }
}
