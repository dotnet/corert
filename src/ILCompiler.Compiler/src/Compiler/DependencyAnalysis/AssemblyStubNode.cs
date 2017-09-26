// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    public abstract class AssemblyStubNode : ObjectNode, ISymbolDefinitionNode
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
                    X64.X64Emitter x64Emitter = new X64.X64Emitter(factory, relocsOnly);
                    EmitCode(factory, ref x64Emitter, relocsOnly);
                    x64Emitter.Builder.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
                    x64Emitter.Builder.AddSymbol(this);
                    return x64Emitter.Builder.ToObjectData();

                case TargetArchitecture.X86:
                    X86.X86Emitter x86Emitter = new X86.X86Emitter(factory, relocsOnly);
                    EmitCode(factory, ref x86Emitter, relocsOnly);
                    x86Emitter.Builder.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
                    x86Emitter.Builder.AddSymbol(this);
                    return x86Emitter.Builder.ToObjectData();

                case TargetArchitecture.ARM:
                case TargetArchitecture.ARMEL:
                    ARM.ARMEmitter armEmitter = new ARM.ARMEmitter(factory, relocsOnly);
                    EmitCode(factory, ref armEmitter, relocsOnly);
                    armEmitter.Builder.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
                    armEmitter.Builder.AddSymbol(this);
                    return armEmitter.Builder.ToObjectData();

                case TargetArchitecture.ARM64:
                    ARM64.ARM64Emitter arm64Emitter = new ARM64.ARM64Emitter(factory, relocsOnly);
                    EmitCode(factory, ref arm64Emitter, relocsOnly);
                    arm64Emitter.Builder.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
                    arm64Emitter.Builder.AddSymbol(this);
                    return arm64Emitter.Builder.ToObjectData();

                default:
                    throw new NotImplementedException();
            }
        }

        protected abstract void EmitCode(NodeFactory factory, ref X64.X64Emitter instructionEncoder, bool relocsOnly);
        protected abstract void EmitCode(NodeFactory factory, ref X86.X86Emitter instructionEncoder, bool relocsOnly);
        protected abstract void EmitCode(NodeFactory factory, ref ARM.ARMEmitter instructionEncoder, bool relocsOnly);
        protected abstract void EmitCode(NodeFactory factory, ref ARM64.ARM64Emitter instructionEncoder, bool relocsOnly);
    }
}
