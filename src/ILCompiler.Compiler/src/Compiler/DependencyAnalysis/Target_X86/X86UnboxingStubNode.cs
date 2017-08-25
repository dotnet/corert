// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysis.X86;

namespace ILCompiler.DependencyAnalysis
{
    public partial class UnboxingStubNode
    {
        protected void EmitUnboxingStubCode(NodeFactory factory, ref X86Emitter encoder)
        {
            encoder.Builder.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
            encoder.Builder.AddSymbol(this);

            AddrMode thisPtr = new AddrMode(
                Register.RegDirect | encoder.TargetRegister.Arg0, null, 0, 0, AddrModeSize.Int32);
            encoder.EmitADD(ref thisPtr, (sbyte)factory.Target.PointerSize);
            encoder.EmitJMP(factory.MethodEntrypoint(Method));
        }
    }
}
