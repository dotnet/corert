// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                    {
                        TypeDesc target = (TypeDesc)Target;
                        encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.ConstructedTypeSymbol(target));
                        encoder.EmitJMP(factory.ExternSymbol(JitHelper.GetNewObjectHelperForType(target)));
                    }
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
                    {
                        TypeDesc target = (TypeDesc)Target;
                        encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.NecessaryTypeSymbol(target));
                        encoder.EmitJMP(factory.ExternSymbol(JitHelper.GetCastingHelperNameForType(target, false)));
                    }
                    break;

                case ReadyToRunHelperId.CastClass:
                    {
                        TypeDesc target = (TypeDesc)Target;
                        encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.NecessaryTypeSymbol(target));
                        encoder.EmitJMP(factory.ExternSymbol(JitHelper.GetCastingHelperNameForType(target, true)));
                    }
                    break;

                case ReadyToRunHelperId.NewArr1:
                    {
                        TypeDesc target = (TypeDesc)Target;


                        // TODO: Swap argument order instead
                        // mov arg1, arg0
                        encoder.Builder.EmitByte(0x48);
                        encoder.Builder.EmitShort((short)((encoder.TargetRegister.Arg0 == Register.RCX) ? 0xD18B : 0xF78B));

                        encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.NecessaryTypeSymbol(target));
                        encoder.EmitJMP(factory.ExternSymbol(JitHelper.GetNewArrayHelperForType(target)));
                    }
                    break;

                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;
                        if (!target.HasStaticConstructor)
                        {
                            Debug.Assert(Id == ReadyToRunHelperId.GetNonGCStaticBase);
                            encoder.EmitLEAQ(encoder.TargetRegister.Result, factory.TypeNonGCStaticsSymbol(target));
                            encoder.EmitRET();
                        }
                        else
                        {
                            // We need to trigger the cctor before returning the base
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.TypeCctorContextSymbol(target));
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.TypeNonGCStaticsSymbol(target));
                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase));
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    encoder.EmitINT3();
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;
                        if (!target.HasStaticConstructor)
                        {
                            encoder.EmitLEAQ(encoder.TargetRegister.Result, factory.TypeGCStaticsSymbol(target));
                            AddrMode loadFromRax = new AddrMode(encoder.TargetRegister.Result, null, 0, 0, AddrModeSize.Int64);
                            encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromRax);
                            encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromRax);
                            encoder.EmitRET();
                        }
                        else
                        {
                            // We need to trigger the cctor before returning the base
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.TypeCctorContextSymbol(target));
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.TypeGCStaticsSymbol(target));
                            AddrMode loadFromRdx = new AddrMode(encoder.TargetRegister.Arg1, null, 0, 0, AddrModeSize.Int64);
                            encoder.EmitMOV(encoder.TargetRegister.Arg1, ref loadFromRdx);
                            encoder.EmitMOV(encoder.TargetRegister.Arg1, ref loadFromRdx);
                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnGCStaticBase));
                        }
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

                case ReadyToRunHelperId.InterfaceDispatch:
                    {
                        encoder.EmitLEAQ(Register.R10, factory.InterfaceDispatchCell((MethodDesc)Target));
                        AddrMode jmpAddrMode = new AddrMode(Register.R10, null, 0, 0, AddrModeSize.Int64);
                        encoder.EmitJmpToAddrMode(ref jmpAddrMode);
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
