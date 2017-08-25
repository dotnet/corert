// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysis.ARM;

namespace ILCompiler.DependencyAnalysis
{
    public partial class UnboxingStubNode
    {
        protected void EmitUnboxingStubCode(NodeFactory factory, ref ARMEmitter encoder)
        {
            encoder.Builder.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
            encoder.Builder.AddSymbol(this);

            encoder.EmitADD(encoder.TargetRegister.Arg0, (byte)factory.Target.PointerSize); // add r0, sizeof(void*);         
            encoder.EmitJMP(factory.MethodEntrypoint(Method)); // b methodEntryPoint
        }
    }
}
