// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using ILCompiler.DependencyAnalysis.X64;
using Internal.TypeSystem;
using ILCompiler;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// X64 specific portions of ReadyToRunHelperNode
    /// </summary>
    public partial class ReadyToRunHelperNode
    {
        protected override void EmitCode(NodeFactory factory, ref X64Emitter encoder, bool relocsOnly)
        {
            switch (Id)
            {
                case ReadyToRunHelperId.NewHelper:
                    encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.ConstructedTypeSymbol((TypeDesc)Target));
                    encoder.EmitJMP(factory.ExternSymbol("__allocate_object"));
                    break;

                case ReadyToRunHelperId.VirtualCall:
                    if (relocsOnly)
                        break;

                    AddrMode loadFromThisPtr = new AddrMode(encoder.TargetRegister.Arg0, null, 0, 0, AddrModeSize.Int64);
                    encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromThisPtr);

                    {
                        int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, (MethodDesc)Target);
                        Debug.Assert(slot != -1);
                        AddrMode jmpAddrMode = new AddrMode(encoder.TargetRegister.Result, null, EETypeNode.GetVTableOffset(factory.Target.PointerSize) + (slot * factory.Target.PointerSize), 0, AddrModeSize.Int64);
                        encoder.EmitJmpToAddrMode(ref jmpAddrMode);
                    }
                    break;

                case ReadyToRunHelperId.IsInstanceOf:
                    encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.NecessaryTypeSymbol((TypeDesc)Target));
                    encoder.EmitJMP(factory.ExternSymbol("__isinst_class"));
                    break;

                case ReadyToRunHelperId.CastClass:
                    encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.NecessaryTypeSymbol((TypeDesc)Target));
                    encoder.EmitJMP(factory.ExternSymbol("__castclass_class"));
                    break;

                case ReadyToRunHelperId.NewArr1:
                    encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.NecessaryTypeSymbol((TypeDesc)Target));
                    encoder.EmitJMP(factory.ExternSymbol("__allocate_array"));
                    break;

                case ReadyToRunHelperId.GetNonGCStaticBase:
                    if (!((MetadataType)Target).HasStaticConstructor)
                    {
                        Debug.Assert(Id == ReadyToRunHelperId.GetNonGCStaticBase);
                        encoder.EmitLEAQ(encoder.TargetRegister.Result, factory.TypeNonGCStaticsSymbol((MetadataType)Target));
                        encoder.EmitRET();
                    }
                    else
                    {
                        // We need to trigger the cctor before returning the base
                        encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.TypeCctorContextSymbol((MetadataType)Target));
                        encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.TypeNonGCStaticsSymbol((MetadataType)Target));
                        encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase));
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    encoder.EmitINT3();
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    if (!((MetadataType)Target).HasStaticConstructor)
                    {
                        encoder.EmitLEAQ(encoder.TargetRegister.Result, factory.TypeGCStaticsSymbol((MetadataType)Target));
                        AddrMode loadFromRax = new AddrMode(encoder.TargetRegister.Result, null, 0, 0, AddrModeSize.Int64);
                        encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromRax);
                        encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromRax);
                        encoder.EmitRET();
                    }
                    else
                    {
                        // We need to trigger the cctor before returning the base
                        encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.TypeCctorContextSymbol((MetadataType)Target));
                        encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.TypeGCStaticsSymbol((MetadataType)Target));
                        AddrMode loadFromRdx = new AddrMode(encoder.TargetRegister.Arg1, null, 0, 0, AddrModeSize.Int64);
                        encoder.EmitMOV(encoder.TargetRegister.Arg1, ref loadFromRdx);
                        encoder.EmitMOV(encoder.TargetRegister.Arg1, ref loadFromRdx);
                        encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnGCStaticBase));
                    }
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateInfo target = (DelegateInfo)Target;

                        encoder.EmitLEAQ(encoder.TargetRegister.Arg2, factory.MethodEntrypoint(target.Target));
                        if (target.ShuffleThunk != null)
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg3, factory.MethodEntrypoint(target.ShuffleThunk));

                        encoder.EmitJMP(factory.MethodEntrypoint(target.Ctor));
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
