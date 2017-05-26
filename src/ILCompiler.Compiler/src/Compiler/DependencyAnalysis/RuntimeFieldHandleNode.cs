// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class RuntimeFieldHandleNode : ObjectNode, ISymbolDefinitionNode
    {
        private FieldDesc _targetField;

        public RuntimeFieldHandleNode(FieldDesc targetField)
        {
            Debug.Assert(!targetField.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any));
            Debug.Assert(!targetField.OwningType.IsRuntimeDeterminedSubtype);
            _targetField = targetField;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix)
              .Append("__RuntimeFieldHandle_")
              .Append(nameMangler.GetMangledFieldName(_targetField));
        }
        public int Offset => 0;
        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;

        private static Utf8String s_NativeLayoutSignaturePrefix = new Utf8String("__RFHSignature_");

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            // TODO: https://github.com/dotnet/corert/issues/3224
            // We should figure out reflectable fields when scanning for reflection
            FieldDesc fieldDefinition = _targetField.GetTypicalFieldDefinition();
            if (!factory.MetadataManager.CanGenerateMetadata(fieldDefinition))
            {
                return new DependencyList
                {
                    new DependencyListEntry(factory.FieldMetadata(fieldDefinition), "LDTOKEN")
                };
            }

            return null;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);

            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            NativeLayoutFieldLdTokenVertexNode ldtokenSigNode = factory.NativeLayout.FieldLdTokenVertex(_targetField);
            objData.EmitPointerReloc(factory.NativeLayout.NativeLayoutSignature(ldtokenSigNode, s_NativeLayoutSignaturePrefix, _targetField));

            return objData.ToObjectData();
        }
    }
}
