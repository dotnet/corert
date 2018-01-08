// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.NativeFormat;
using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using TypeFlags = Internal.TypeSystem.TypeFlags;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a hashtable that describes instance field layout of select types.
    /// Instance field layout information includes information about offsets and types of instance fields.
    /// </summary>
    internal sealed class InstanceFieldLayoutNode : ObjectNode, ISymbolDefinitionNode
    {
        private readonly ObjectAndOffsetSymbolNode _endSymbol;
        private readonly ExternalReferencesTableNode _externalReferences;

        public InstanceFieldLayoutNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__instanceFieldLayout_End", true);
            _externalReferences = externalReferences;
        }

        public ISymbolNode EndSymbol => _endSymbol;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__instanceFieldLayout");
        }
        public int Offset => 0;
        public override bool IsShareable => false;

        public override ObjectNodeSection Section => _externalReferences.Section;

        public override bool StaticDependenciesAreComputed => true;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public static void AddDependenciesDueToEETypePresence(ref DependencyList dependencyList, NodeFactory factory, TypeDesc type)
        {
            if (factory.Target.Abi == TargetAbi.ProjectN)
                return;

            // The hash table will reference the types of all instance field if this type qualifies
            if (NeedsFieldLayoutInformation(type))
            {
                dependencyList = dependencyList ?? new DependencyList();

                foreach (FieldDesc field in type.GetFields())
                {
                    if (field.IsStatic)
                        continue;

                    TypeDesc fieldType = NormalizeFieldType(field.FieldType);                    
                    dependencyList.Add(factory.ConstructedTypeSymbol(fieldType), "Instance field layout");
                }
            }
        }

        public static bool NeedsFieldLayoutInformation(TypeDesc type)
        {
            // We need field layout information for boxable valuetypes that cannot be compared
            // bit-by-bit.
            //
            // One might think that the presence of Equals/GetHashCode overrides on the type
            // would mean we no longer need this information, but we have seen customer code
            // that does:
            //
            // public override bool Equals(object obj) => base.Equals(obj);
            //
            // to shut up analyzer warnings (demonstrating their lack of understanding why the
            // warning shows up in the first place). We need this information in case the customer
            // code does that.
            return type.IsValueType &&
                !type.IsByRefLike &&
                !CanCompareBits((MetadataType)type);
        }
        
        public static TypeDesc NormalizeFieldType(TypeDesc fieldType)
        {
            TypeSystemContext context = fieldType.Context;

            // We don't care about the exact type for reference types. This lets us save some size on disk.
            if (fieldType.IsGCPointer)
                fieldType = context.GetWellKnownType(WellKnownType.Object);

            // We need something boxable
            if (fieldType.IsPointer || fieldType.IsFunctionPointer)
                fieldType = context.GetWellKnownType(WellKnownType.IntPtr);

            return fieldType;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            var writer = new NativeWriter();
            var typeMapHashTable = new VertexHashtable();

            Section hashTableSection = writer.NewSection();
            hashTableSection.Place(typeMapHashTable);

            foreach (var type in factory.MetadataManager.GetTypesWithConstructedEETypes())
            {
                if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                    continue;

                if (!NeedsFieldLayoutInformation(type))
                    continue;

                VertexSequence fieldsSequence = new VertexSequence();
                foreach (FieldDesc field in type.GetFields())
                {
                    if (field.IsStatic)
                        continue;

                    TypeDesc fieldType = NormalizeFieldType(field.FieldType);
                    IEETypeNode fieldTypeSymbol = factory.ConstructedTypeSymbol(fieldType);

                    fieldsSequence.Append(writer.GetTuple(
                        writer.GetUnsignedConstant(_externalReferences.GetIndex(fieldTypeSymbol)),
                        writer.GetUnsignedConstant((uint)field.Offset.AsInt)));
                }

                IEETypeNode typeSymbol = factory.ConstructedTypeSymbol(type);

                Vertex vertex = writer.GetTuple(
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(typeSymbol)),
                    fieldsSequence
                    );

                int hashCode = type.GetHashCode();
                typeMapHashTable.Append((uint)hashCode, hashTableSection.Place(vertex));
            }

            byte[] hashTableBytes = writer.Save();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this, _endSymbol });
        }

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;

        protected internal override int ClassCode => (int)ObjectNodeOrder.InstanceFieldLayoutNode;

        private static bool CanCompareBits(MetadataType type)
        {
            Debug.Assert(type.IsValueType);

            if (type.ContainsGCPointers)
                return false;

            // TODO: what we're shooting for is overlapping fields
            // or gaps between fields
            if (type.IsExplicitLayout || type.GetClassLayout().Size != 0)
                return false;

            bool result = true;
            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType.IsPointer || fieldType.IsFunctionPointer)
                {
                    TypeFlags category = fieldType.Category;
                    if (category == TypeFlags.Single || category == TypeFlags.Double)
                    {
                        // Double/Single have weird behaviors around NaN
                        result = false;
                        break;
                    }
                }
                else
                {
                    // Would be a suprise if this wasn't a valuetype. We checked ContainsGCPointers above.
                    Debug.Assert(fieldType.IsValueType);

                    // TODO: might want to cache the Equals/GetHashCode MethodDesc in a central location.
                    MethodDesc equalsMethod = type.Context.GetWellKnownType(WellKnownType.Object).GetMethod("Equals", null);
                    MethodDesc hashCodeMethod = type.Context.GetWellKnownType(WellKnownType.Object).GetMethod("GetHashCode", null);
                    if (fieldType.FindVirtualFunctionTargetMethodOnObjectType(equalsMethod) != null ||
                        fieldType.FindVirtualFunctionTargetMethodOnObjectType(hashCodeMethod) != null)
                    {
                        result = false;
                        break;
                    }

                    if (!CanCompareBits((MetadataType)fieldType))
                    {
                        result = false;
                        break;
                    }
                }
            }

            return result;
        }
    }
}
