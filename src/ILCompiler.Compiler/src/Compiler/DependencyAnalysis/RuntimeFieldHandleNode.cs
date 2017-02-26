// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    class RuntimeFieldHandleNode : ObjectNode, ISymbolNode
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
        protected override string GetName() => this.GetMangledName();
        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);

            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            NativeLayoutFieldLdTokenVertexNode ldtokenSigNode = factory.NativeLayout.FieldLdTokenVertex(_targetField);
            objData.EmitPointerReloc(factory.NativeLayout.NativeLayoutSignature(ldtokenSigNode));

            return objData.ToObjectData();
        }
    }
}
