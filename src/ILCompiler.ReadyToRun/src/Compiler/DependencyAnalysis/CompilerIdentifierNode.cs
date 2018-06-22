// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class CompilerIdentifierNode : ObjectNode, ISymbolDefinitionNode
    {
        private static readonly string _compilerIdentifier = "CoreRT Ready-To-Run Compiler";
        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;
        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
        protected override int ClassCode => 230053202;
        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__ReadyToRunHeader_CompilerIdentifier");
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();
            builder.AddSymbol(this);
            builder.EmitBytes(Encoding.ASCII.GetBytes(_compilerIdentifier));
            return builder.ToObjectData();
        }
    }
}
