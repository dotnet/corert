// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    public abstract class AssemblyStubNode : ObjectNode, ISymbolNode
    {
        public AssemblyStubNode()
        {
        }

        public override ObjectNodeSection Section => ObjectNodeSection.TextSection;

        public override bool StaticDependenciesAreComputed => true;

        public abstract void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb);
        public int Offset => 0;
        public override bool IsShareable => false;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            switch (factory.Target.Architecture)
            {
                case TargetArchitecture.X64:
                    X64.X64Emitter x64Emitter = new X64.X64Emitter(factory);
                    EmitCode(factory, ref x64Emitter, relocsOnly);
                    x64Emitter.Builder.Alignment = factory.Target.MinimumFunctionAlignment;
                    x64Emitter.Builder.DefinedSymbols.Add(this);
                    return x64Emitter.Builder.ToObjectData();
                default:
                    throw new NotImplementedException();
            }
        }

        protected abstract void EmitCode(NodeFactory factory, ref X64.X64Emitter instructionEncoder, bool relocsOnly);
    }
}
