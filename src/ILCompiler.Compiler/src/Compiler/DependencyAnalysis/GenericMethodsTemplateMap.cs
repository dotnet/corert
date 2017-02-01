﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Hashtable of all generic method templates used by the TypeLoader at runtime
    /// </summary>
    internal sealed class GenericMethodsTemplateMap : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public GenericMethodsTemplateMap(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__GenericMethodsTemplateMap_End", true);
            _externalReferences = externalReferences;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__GenericMethodsTemplateMap");
        }

        public ISymbolNode EndSymbol => _endSymbol;
        public int Offset => 0;
        public override bool IsShareable => false;
        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName() => this.GetMangledName();

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // Dependencies for this node are tracked by the method code nodes
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolNode[] { this });

            // Ensure the native layout data has been saved, in order to get valid Vertex offsets for the signature Vertices
            factory.MetadataManager.NativeLayoutInfo.SaveNativeLayoutInfoWriter(factory);

            NativeWriter nativeWriter = new NativeWriter();
            VertexHashtable hashtable = new VertexHashtable();
            Section nativeSection = nativeWriter.NewSection();
            nativeSection.Place(hashtable);


            foreach (MethodDesc method in factory.MetadataManager.GetCompiledMethods())
            {
                if (!IsEligibleToBeATemplate(method))
                    continue;

                // Method entry
                Vertex methodEntry = factory.NativeLayout.TemplateMethodEntry(method).SavedVertex;

                // Method's native layout info
                Vertex nativeLayout = factory.NativeLayout.TemplateMethodLayout(method).SavedVertex;

                // Hashtable Entry
                Vertex entry = nativeWriter.GetTuple(
                    nativeWriter.GetUnsignedConstant((uint)methodEntry.VertexOffset),
                    nativeWriter.GetUnsignedConstant((uint)nativeLayout.VertexOffset));

                // Add to the hash table, hashed by the containing type's hashcode
                uint hashCode = (uint)method.GetHashCode();
                hashtable.Append(hashCode, nativeSection.Place(entry));
            }

            MemoryStream stream = new MemoryStream();
            nativeWriter.Save(stream);

            byte[] streamBytes = stream.ToArray();

            _endSymbol.SetSymbolOffset(streamBytes.Length);

            return new ObjectData(streamBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }

        public static DependencyList GetTemplateMethodDependencies(NodeFactory factory, MethodDesc method)
        {
            if (!IsEligibleToBeATemplate(method))
                return null;

            DependencyList dependencies = new DependencyList();

            dependencies.Add(new DependencyListEntry(factory.NativeLayout.TemplateMethodEntry(method), "Template Method Entry"));
            dependencies.Add(new DependencyListEntry(factory.NativeLayout.TemplateMethodLayout(method), "Template Method Layout"));

            return dependencies;
        }

        private static bool IsEligibleToBeATemplate(MethodDesc method)
        {
            if (!method.HasInstantiation)
                return false;

            if (method.IsCanonicalMethod(CanonicalFormKind.Specific))
            {
                // Must be fully canonical
                Debug.Assert(method == method.GetCanonMethodTarget(CanonicalFormKind.Specific));
                return true;
            }
            else if (method.IsCanonicalMethod(CanonicalFormKind.Universal))
            {
                // Must be fully canonical
                if (method == method.GetCanonMethodTarget(CanonicalFormKind.Universal))
                    return true;
            }

            return false;
        }
    }
}