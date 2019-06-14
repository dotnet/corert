// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    public class LoopHijackFlagNode : ObjectNode, ISymbolDefinitionNode
    {
        public LoopHijackFlagNode()
        {
        }

        public int Offset => 0;

        protected override string GetName(NodeFactory factory) => "LoopHijackFlag";

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("LoopHijackFlag");
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.DataSection;
            }
        }

        public override bool IsShareable => true;

        public override bool StaticDependenciesAreComputed => true;

        public override int ClassCode => -266743363;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // Emit a 4-byte integer flag with initial value of 0. 
            // TODO: define it as "comdat select any" when multiple object files present.
            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);
            objData.RequireInitialAlignment(4);
            objData.AddSymbol(this);
            objData.EmitInt(0);
            return objData.ToObjectData();
        }
    }
}
