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
    /// Hashtable of all generic type templates used by the TypeLoader at runtime
    /// </summary>
    public sealed class GenericTypesTemplateMap : ObjectNode, ISymbolNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public GenericTypesTemplateMap(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__GenericTypesTemplateMap_End", true);
            _externalReferences = externalReferences;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__GenericTypesTemplateMap");
        }

        public ISymbolNode EndSymbol => _endSymbol;
        public int Offset => 0;
        public override bool IsShareable => false;
        public override ObjectNodeSection Section => _externalReferences.Section;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

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

            foreach (TypeDesc type in factory.MetadataManager.GetTypesWithConstructedEETypes())
            {
                if (!IsEligibleToHaveATemplate(type))
                    continue;

                if (factory.Target.Abi == TargetAbi.ProjectN)
                {
                    // If the type does not have fully constructed type, don't emit it.
                    // TODO: Remove the workaround once we stop using the STS dependency analysis.
                    if (!factory.ConstructedTypeSymbol(type).Marked)
                        continue;
                }

                // Type's native layout info
                DefType defType = GetActualTemplateTypeForType(factory, type);
                NativeLayoutTemplateTypeLayoutVertexNode templateNode = factory.NativeLayout.TemplateTypeLayout(defType);

                // If this template isn't considered necessary, don't emit it.
                if (!templateNode.Marked)
                    continue;
                Vertex nativeLayout = templateNode.SavedVertex;

                // Hashtable Entry
                Vertex entry = nativeWriter.GetTuple(
                    nativeWriter.GetUnsignedConstant(_externalReferences.GetIndex(factory.NecessaryTypeSymbol(type))),
                    nativeWriter.GetUnsignedConstant((uint)nativeLayout.VertexOffset));

                // Add to the hash table, hashed by the containing type's hashcode
                uint hashCode = (uint)type.GetHashCode();
                hashtable.Append(hashCode, nativeSection.Place(entry));
            }

            byte[] streamBytes = nativeWriter.Save();

            _endSymbol.SetSymbolOffset(streamBytes.Length);

            return new ObjectData(streamBytes, Array.Empty<Relocation>(), 1, new ISymbolNode[] { this, _endSymbol });
        }

        public static DefType GetActualTemplateTypeForType(NodeFactory factory, TypeDesc type)
        {
            DefType defType = type as DefType;
            if (defType == null)
            {
                Debug.Assert(IsArrayTypeEligibleForTemplate(type));
                ArrayType arrayType = (ArrayType)type;
                defType = factory.ArrayOfTClass.MakeInstantiatedType(arrayType.ElementType);
            }

            return defType;
        }

        public static DependencyList GetTemplateTypeDependencies(NodeFactory factory, TypeDesc type)
        {
            if (!IsEligibleToHaveATemplate(type))
                return null;

            if (factory.Target.Abi == TargetAbi.ProjectN)
            {
                // If the type does not have fully constructed type, don't track its dependencies.
                // TODO: Remove the workaround once we stop using the STS dependency analysis.
                if (!factory.ConstructedTypeSymbol(type).Marked)
                    return null;
            }

            DependencyList dependencies = new DependencyList();

            dependencies.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(type), "Template type"));
            dependencies.Add(new DependencyListEntry(factory.NativeLayout.TemplateTypeLayout(GetActualTemplateTypeForType(factory, type)), "Template Type Layout"));

            return dependencies;
        }

        public static bool IsArrayTypeEligibleForTemplate(TypeDesc type)
        {
            if (!type.IsSzArray)
                return false;

            // Unmanaged Pointer and Function pointer arrays can't use the array template
            ArrayType arrayType = (ArrayType)type;
            TypeDesc elementType = arrayType.ElementType;

            return !elementType.IsPointer && !elementType.IsFunctionPointer;
        }

        public static bool IsEligibleToHaveATemplate(TypeDesc type)
        {
            if (!type.HasInstantiation && !IsArrayTypeEligibleForTemplate(type))
                return false;

            if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
            {
                // Must be fully canonical
                Debug.Assert(type == type.ConvertToCanonForm(CanonicalFormKind.Specific));
                return true;
            }

            return false;
        }
    }
}