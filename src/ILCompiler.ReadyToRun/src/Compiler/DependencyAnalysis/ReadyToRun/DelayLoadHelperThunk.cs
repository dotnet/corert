// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    /// <summary>
    /// This node emits a thunk calling DelayLoad_Helper with a given instance signature
    /// to populate its indirection cell.
    /// </summary>
    public class DelayLoadHelperThunk : AssemblyStubNode, ISymbolDefinitionNode
    {
        private readonly ISymbolNode _helperCell;

        private readonly Import _instanceCell;

        private readonly ISymbolNode _moduleImport;

        public DelayLoadHelperThunk(ReadyToRunHelper helperId, ReadyToRunCodegenNodeFactory factory, Import instanceCell)
        {
            _helperCell = factory.GetReadyToRunHelperCell(helperId);
            _instanceCell = instanceCell;
            _moduleImport = factory.ModuleImport;
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("DelayLoadHelper->");
            _instanceCell.AppendMangledName(nameMangler, sb);
        }

        protected override string GetName(NodeFactory factory)
        {
            return "DelayLoadHelper";
        }

        protected override void EmitCode(NodeFactory factory, ref X64.X64Emitter instructionEncoder, bool relocsOnly)
        {
            // lea rax, [pCell]
            instructionEncoder.EmitLEAQ(X64.Register.RAX, _instanceCell);
            if (!relocsOnly)
            {
                // push table index
                instructionEncoder.Builder.EmitByte(0x6A);
                instructionEncoder.Builder.EmitByte((byte)_instanceCell.Table.IndexFromBeginningOfArray);

                // push [module]
                instructionEncoder.Builder.EmitByte(0xFF);
                instructionEncoder.Builder.EmitByte(0x35);
            }
            instructionEncoder.Builder.EmitReloc(_moduleImport, RelocType.IMAGE_REL_BASED_REL32);
            
            if (!relocsOnly)
            {
                // TODO: additional tricks regarding UNIX AMD64 ABI

                // jmp [helper]
                instructionEncoder.Builder.EmitByte(0xFF);
                instructionEncoder.Builder.EmitByte(0x25);
            }
            instructionEncoder.Builder.EmitReloc(_helperCell, RelocType.IMAGE_REL_BASED_REL32);
        }

        protected override void EmitCode(NodeFactory factory, ref X86.X86Emitter instructionEncoder, bool relocsOnly)
        {
            throw new NotImplementedException();
        }

        protected override void EmitCode(NodeFactory factory, ref ARM.ARMEmitter instructionEncoder, bool relocsOnly)
        {
            throw new NotImplementedException();
        }

        protected override void EmitCode(NodeFactory factory, ref ARM64.ARM64Emitter instructionEncoder, bool relocsOnly)
        {
            throw new NotImplementedException();
        }

        protected override int ClassCode => 433266948;
    }

    public class DelayLoadHelperThunk_Obj : DelayLoadHelperThunk
    {
        public DelayLoadHelperThunk_Obj(ReadyToRunCodegenNodeFactory nodeFactory, Import instanceCell)
            : base(ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj, nodeFactory, instanceCell)
        {
        }
    }
}
