// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysis.ARM;

namespace ILCompiler.DependencyAnalysis
{
    public partial class UnboxingStubsRegionNode
    {
        protected void EmitUnboxingStubsCode(NodeFactory factory, ref ARMEmitter encoder)
        {
            encoder.Builder.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
            encoder.Builder.AddSymbol(this);

            foreach (var unboxingStub in factory.MetadataManager.GetCompiledUnboxingStubs())
            {
                // Padding to ensure proper stub alignment
                while (encoder.Builder.CountBytes % factory.Target.MinimumFunctionAlignment != 0)
                    encoder.EmitDebugBreak();

                unboxingStub.SetSymbolDefinitionOffset(encoder.Builder.CountBytes);
                encoder.Builder.AddSymbol(unboxingStub);

                encoder.EmitADD(encoder.TargetRegister.Arg0, (byte)factory.Target.PointerSize); // add r0, sizeof(void*);         
                encoder.EmitJMP(factory.MethodEntrypoint(unboxingStub.Method)); // b methodEntryPoint
            }

            _endSymbol.SetSymbolOffset(encoder.Builder.CountBytes);
            encoder.Builder.AddSymbol(_endSymbol);
        }
    }
}
