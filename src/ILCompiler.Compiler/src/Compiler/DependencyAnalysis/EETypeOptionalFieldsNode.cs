// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    internal class EETypeOptionalFieldsNode : ObjectNode, ISymbolNode
    {
        private EETypeNode _owner;

        public EETypeOptionalFieldsNode(EETypeNode owner)
        {
            _owner = owner;
        }

        public override ObjectNodeSection Section
        {
            get
            {
                if (_owner.Type.Context.Target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public override bool StaticDependenciesAreComputed => true;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__optionalfields_");
            _owner.AppendMangledName(nameMangler, sb);
        }
        public int Offset => 0;
        public override bool IsShareable => true;

        protected override string GetName() => this.GetMangledName();

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return _owner.ShouldSkipEmittingObjectNode(factory) || !_owner.HasOptionalFields;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.RequirePointerAlignment();
            objData.DefinedSymbols.Add(this);

            if (!relocsOnly)
            {
                objData.EmitBytes(_owner.GetOptionalFieldsData());
            }
            
            return objData.ToObjectData();
        }
    }
}
