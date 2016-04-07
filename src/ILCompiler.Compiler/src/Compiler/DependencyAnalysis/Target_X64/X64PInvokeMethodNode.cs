// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysis.X64;

namespace ILCompiler.DependencyAnalysis
{
    /// X64 specific portions of PInvokeMethodNode
    public partial class PInvokeMethodNode
    {
        protected override void EmitCode(NodeFactory factory, ref X64Emitter encoder, bool relocsOnly)
        {
            encoder.EmitJMP(factory.ExternSymbol(_target.GetPInvokeMethodMetadata().Name));
        }
    }
}
