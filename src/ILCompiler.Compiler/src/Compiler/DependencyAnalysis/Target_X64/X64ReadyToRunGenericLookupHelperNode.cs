// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysis.X64;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    partial class ReadyToRunGenericLookupHelperNode
    {
        protected override void EmitCode(NodeFactory factory, ref X64Emitter encoder, bool relocsOnly)
        {
            if (_contextKind == GenericContextKind.ThisObj)
            {
                //Debug.Assert(false, "Emit code to get dictionary from 'this'");
                encoder.EmitINT3();
            }

            int dictionarySlot = 0;
            if (!relocsOnly)
            {
                // The concrete slot won't be known until we're emitting data.
                dictionarySlot = factory.GenericDictionaryLayout(_typeOrMethodContext).GetSlotForEntry(_target);
            }

            AddrMode loadEntry = new AddrMode(
                encoder.TargetRegister.Arg0, null, dictionarySlot * factory.Target.PointerSize, 0, AddrModeSize.Int64);
            encoder.EmitMOV(encoder.TargetRegister.Arg0, ref loadEntry);
            encoder.EmitRET();
        }
    }
}
