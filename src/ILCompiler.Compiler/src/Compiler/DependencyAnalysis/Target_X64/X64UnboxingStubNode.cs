﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysis.X64;

namespace ILCompiler.DependencyAnalysis
{
    public partial class UnboxingStubNode
    {
        protected override void EmitCode(NodeFactory factory, ref X64Emitter encoder, bool relocsOnly)
        {
            AddrMode thisPtr = new AddrMode(
                Register.RegDirect | encoder.TargetRegister.Arg0, null, 0, 0, AddrModeSize.Int64);
            encoder.EmitADD(ref thisPtr, (sbyte)factory.Target.PointerSize);
            encoder.MarkDebuggerStepInPoint();
            encoder.EmitJMP(factory.MethodEntrypoint(Method));
        }
    }
}
