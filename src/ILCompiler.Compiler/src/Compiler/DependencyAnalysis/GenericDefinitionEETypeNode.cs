// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class GenericDefinitionEETypeNode : EETypeNode
    {
        public GenericDefinitionEETypeNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
            Debug.Assert(type.IsGenericDefinition);
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return false;
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = null;

            // Ask the metadata manager if we have any dependencies due to reflectability.
            factory.MetadataManager.GetDependenciesDueToReflectability(ref dependencyList, factory, _type);

            return dependencyList;
        }

        protected internal override void ComputeOptionalEETypeFields(NodeFactory factory, bool relocsOnly)
        {
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder dataBuilder = new ObjectDataBuilder(factory, relocsOnly);

            dataBuilder.RequireInitialPointerAlignment();
            dataBuilder.AddSymbol(this);
            EETypeRareFlags rareFlags = 0;

            short flags = (short)EETypeKind.GenericTypeDefEEType;
            if (_type.IsValueType)
                flags |= (short)EETypeFlags.ValueTypeFlag;
            if (_type.IsInterface)
                flags |= (short)EETypeFlags.IsInterfaceFlag;
            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type))
                rareFlags = rareFlags | EETypeRareFlags.HasCctorFlag;
            if (_type.IsByRefLike)
                rareFlags |= EETypeRareFlags.IsByRefLikeFlag;

            if (rareFlags != 0)
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.RareFlags, (uint)rareFlags);

            if (HasOptionalFields)
                flags |= (short)EETypeFlags.OptionalFieldsFlag;

            dataBuilder.EmitShort((short)_type.Instantiation.Length);
            dataBuilder.EmitShort(flags);
            dataBuilder.EmitInt(0);         // Base size is always 0
            dataBuilder.EmitZeroPointer();  // No related type
            dataBuilder.EmitShort(0);       // No VTable
            dataBuilder.EmitShort(0);       // No interface map
            dataBuilder.EmitInt(_type.GetHashCode());
            dataBuilder.EmitPointerReloc(factory.TypeManagerIndirection);
            if (HasOptionalFields)
            {
                dataBuilder.EmitPointerReloc(_optionalFieldsNode);
            }

            return dataBuilder.ToObjectData();
        }

        public override int ClassCode => -160325006;
    }
}
