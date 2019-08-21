// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    class CopiedFieldRvaNode : ObjectNode, ISymbolDefinitionNode
    {
        private EcmaField _field;

        public CopiedFieldRvaNode(EcmaField field)
        {
            Debug.Assert(field.HasRva);

            _field = (EcmaField)field.GetTypicalFieldDefinition();
        }

        public override ObjectNodeSection Section => ObjectNodeSection.TextSection;

        public override bool IsShareable => false;

        public override int ClassCode => 223495;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();
            builder.AddSymbol(this);

            builder.EmitBytes(_field.GetFieldRvaData());

            return builder.ToObjectData();
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($"_FieldRva_{nameMangler.GetMangledFieldName(_field)}");
        }
    }
}
