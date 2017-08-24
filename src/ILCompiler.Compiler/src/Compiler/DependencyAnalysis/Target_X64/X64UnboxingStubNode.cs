// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysis.X64;

namespace ILCompiler.DependencyAnalysis
{
    public partial class UnboxingStubsRegionNode
    {
        protected void EmitUnboxingStubsCode(NodeFactory factory, ref X64Emitter encoder)
        {
            encoder.Builder.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
            encoder.Builder.AddSymbol(this);

            foreach (var unboxingStub in factory.MetadataManager.GetCompiledUnboxingStubs())
            {
                // Padding to ensure proper stub alignment
                while (encoder.Builder.CountBytes % factory.Target.MinimumFunctionAlignment != 0)
                    encoder.EmitINT3();

                unboxingStub.SetSymbolDefinitionOffset(encoder.Builder.CountBytes);
                encoder.Builder.AddSymbol(unboxingStub);

                AddrMode thisPtr = new AddrMode(
                    Register.RegDirect | encoder.TargetRegister.Arg0, null, 0, 0, AddrModeSize.Int64);
                encoder.EmitADD(ref thisPtr, (sbyte)factory.Target.PointerSize);
                encoder.EmitJMP(factory.MethodEntrypoint(unboxingStub.Method));
            }

            _endSymbol.SetSymbolOffset(encoder.Builder.CountBytes);
            encoder.Builder.AddSymbol(_endSymbol);
        }
    }
}
