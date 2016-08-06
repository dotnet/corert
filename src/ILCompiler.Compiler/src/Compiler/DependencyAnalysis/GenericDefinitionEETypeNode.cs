// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using GenericVariance = Internal.Runtime.GenericVariance;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class GenericDefinitionEETypeNode : EETypeNode, ISymbolNode
    {
        public GenericDefinitionEETypeNode(TypeDesc type) : base(type)
        {
            Debug.Assert(type.IsGenericDefinition);
        }
        
        string ISymbolNode.MangledName
        {
            get
            {
                return "__GenericDefinitionEEType_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return false;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder dataBuilder = new ObjectDataBuilder(factory);
            dataBuilder.Alignment = 16;
            dataBuilder.DefinedSymbols.Add(this);

            short flags = (short)EETypeKind.GenericTypeDefEEType;
            if (_type.IsValueType)
                flags |= (short)EETypeFlags.ValueTypeFlag;
            if (_type.IsInterface)
                flags |= (short)EETypeFlags.IsInterfaceFlag;

            dataBuilder.EmitShort((short)_type.Instantiation.Length);
            dataBuilder.EmitShort(flags);
            dataBuilder.EmitInt(0);         // Base size is always 0
            dataBuilder.EmitZeroPointer();  // No related type
            dataBuilder.EmitShort(0);       // No VTable
            dataBuilder.EmitShort(0);       // No interface map
            dataBuilder.EmitInt(_type.GetHashCode());
            dataBuilder.EmitPointerReloc(factory.ModuleManagerIndirection);
            
            return dataBuilder.ToObjectData();
        }
    }
}
