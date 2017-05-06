// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a statically initialized array with data coming from RVA static field
    /// </summary>
    public class FrozenStaticFieldArrayNode : EmbeddedObjectNode, ISymbolDefinitionNode
    {
        private FieldDesc _arrayField;
        private int _pointerSize; 

        public FrozenStaticFieldArrayNode(FieldDesc arrayField, TargetDetails target)
        {
            _arrayField = arrayField;
            _pointerSize = target.PointerSize;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__FrozenArr_").Append(nameMangler.GetMangledFieldName(_arrayField));
        }

        public override bool StaticDependenciesAreComputed => true;

        int ISymbolNode.Offset => 0;

        int ISymbolDefinitionNode.Offset
        {
            get
            {
                // The frozen string symbol points at the EEType portion of the object, skipping over the sync block
                return OffsetFromBeginningOfArray + _pointerSize;
            }
        }

        private IEETypeNode GetEETypeNode(NodeFactory factory)
        {
            return factory.GetLocalTypeSymbol(_arrayField.PreInitDataField.FieldType);
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {           
            // Sync Block
            dataBuilder.EmitZeroPointer();

            // EEType
            dataBuilder.EmitPointerReloc(GetEETypeNode(factory));
          
            var arrType = _arrayField.FieldType as ArrayType;
            if (arrType == null || !arrType.IsSzArray)
            {
                throw new NotImplementedException();
            }

            FieldDesc arrayDataField = _arrayField.PreInitDataField;
            if (!arrayDataField.HasRva)
            {
                throw new BadImageFormatException();
            }

            var ecmaDataField = arrayDataField as EcmaField;
            if (ecmaDataField == null)
            {
                throw new NotImplementedException();
            }

            var rvaData = ecmaDataField.GetFieldRvaData();
            int elementSize = arrType.GetElementSize().AsInt;
            if (rvaData.Length % elementSize != 0)
            {
                throw new BadImageFormatException();
            }

            int length = rvaData.Length / elementSize;

            // numComponents
            dataBuilder.EmitInt(length);

            Debug.Assert(_pointerSize == 8 || _pointerSize == 4);

            if (_pointerSize == 8)
            {
                // padding numComponents in 64-bit
                dataBuilder.EmitInt(0);
            }

            // byte contents
            dataBuilder.EmitBytes(rvaData);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            // @TODO - Do we need to add the field / datafield here? I'd think they'll be implicitly included
            return new DependencyListEntry[]
            {
                new DependencyListEntry(GetEETypeNode(factory), "Frozen preinitialized array"),
            };
        }

        protected override void OnMarked(NodeFactory factory)
        {
            factory.FrozenSegmentRegion.AddEmbeddedObject(this);
        }
    }
}
