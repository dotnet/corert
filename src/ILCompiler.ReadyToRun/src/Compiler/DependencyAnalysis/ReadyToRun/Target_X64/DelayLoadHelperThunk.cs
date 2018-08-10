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
    public partial class DelayLoadHelperThunk
    {
        protected override void EmitCode(NodeFactory factory, ref X64.X64Emitter instructionEncoder, bool relocsOnly)
        {
            // lea rax, [pCell]
            instructionEncoder.EmitLEAQ(X64.Register.RAX, _instanceCell);
            if (!relocsOnly)
            {
                // push table index
                instructionEncoder.EmitPUSH((sbyte)_instanceCell.Table.IndexFromBeginningOfArray);
            }

            // push [module]
            instructionEncoder.EmitPUSH(_moduleImport);
            // TODO: additional tricks regarding UNIX AMD64 ABI
            instructionEncoder.EmitJMP(_helperCell);
        }
    }
}
