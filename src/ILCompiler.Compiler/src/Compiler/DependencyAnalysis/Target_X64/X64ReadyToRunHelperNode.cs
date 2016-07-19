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
                    {
                        if (relocsOnly)
                            break;

                        AddrMode loadFromThisPtr = new AddrMode(encoder.TargetRegister.Arg0, null, 0, 0, AddrModeSize.Int64);
                        encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromThisPtr);
                    
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
                        encoder.EmitMOV(encoder.TargetRegister.Arg1, encoder.TargetRegister.Arg0);
                        encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.ConstructedTypeSymbol(target));
                        encoder.EmitJMP(factory.ExternSymbol(JitHelper.GetNewArrayHelperForType(target)));
                    }
                    break;

                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;
                        bool hasLazyStaticConstructor = factory.TypeSystemContext.HasLazyStaticConstructor(target);
                        encoder.EmitLEAQ(encoder.TargetRegister.Result, factory.TypeNonGCStaticsSymbol(target), hasLazyStaticConstructor ? NonGCStaticsNode.GetClassConstructorContextStorageSize(factory.Target, target) : 0);

                        if (!hasLazyStaticConstructor)
                        {
                            encoder.EmitRET();
                        }
                        else
                        {
                            // We need to trigger the cctor before returning the base. It is stored at the beginning of the non-GC statics region.
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.TypeNonGCStaticsSymbol(target));

                            AddrMode initialized = new AddrMode(encoder.TargetRegister.Arg0, null, factory.Target.PointerSize, 0, AddrModeSize.Int32);
                            encoder.EmitCMP(ref initialized, 1);
                            encoder.EmitRETIfEqual();

                            encoder.EmitMOV(encoder.TargetRegister.Arg1, encoder.TargetRegister.Result);
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

                        encoder.EmitLEAQ(encoder.TargetRegister.Result, factory.TypeGCStaticsSymbol(target));
                        AddrMode loadFromRax = new AddrMode(encoder.TargetRegister.Result, null, 0, 0, AddrModeSize.Int64);
                        encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromRax);
                        encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromRax);

                        if (!factory.TypeSystemContext.HasLazyStaticConstructor(target))
                        {
                            encoder.EmitRET();
                        }
                        else
                        {
                            // We need to trigger the cctor before returning the base. It is stored at the beginning of the non-GC statics region.
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg0, factory.TypeNonGCStaticsSymbol(target));

                            AddrMode initialized = new AddrMode(encoder.TargetRegister.Arg0, null, factory.Target.PointerSize, 0, AddrModeSize.Int32);
                            encoder.EmitCMP(ref initialized, 1);
                            encoder.EmitRETIfEqual();

                            encoder.EmitMOV(encoder.TargetRegister.Arg1, encoder.TargetRegister.Result);

                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnGCStaticBase));
                        }
                    }
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateCreationInfo target = (DelegateCreationInfo)Target;

                        encoder.EmitLEAQ(encoder.TargetRegister.Arg2, target.Target);

                        if (target.Thunk != null)
                        {
                            Debug.Assert(target.Constructor.Method.Signature.Length == 3);
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg3, target.Thunk);
                        }
                        else
                        {
                            Debug.Assert(target.Constructor.Method.Signature.Length == 2);
                        }

                        encoder.EmitJMP(target.Constructor);
                    }
                    break;

                case ReadyToRunHelperId.InterfaceDispatch:
                    {
                        encoder.EmitLEAQ(Register.R10, factory.InterfaceDispatchCell((MethodDesc)Target));
                        AddrMode jmpAddrMode = new AddrMode(Register.R10, null, 0, 0, AddrModeSize.Int64);
                        encoder.EmitJmpToAddrMode(ref jmpAddrMode);
                    }
                    break;

                case ReadyToRunHelperId.ResolveVirtualFunction:
                    {
                        MethodDesc targetMethod = (MethodDesc)Target;
                        if (targetMethod.OwningType.IsInterface)
                        {
                            encoder.EmitLEAQ(encoder.TargetRegister.Arg1, factory.InterfaceDispatchCell(targetMethod));
                            encoder.EmitJMP(factory.ExternSymbol("RhpResolveInterfaceMethod"));
                        }
                        else
                        {
                            if (relocsOnly)
                                break;

                            AddrMode loadFromThisPtr = new AddrMode(encoder.TargetRegister.Arg0, null, 0, 0, AddrModeSize.Int64);
                            encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromThisPtr);

                            int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, targetMethod);
                            Debug.Assert(slot != -1);
                            AddrMode loadFromSlot = new AddrMode(encoder.TargetRegister.Result, null, EETypeNode.GetVTableOffset(factory.Target.PointerSize) + (slot * factory.Target.PointerSize), 0, AddrModeSize.Int64);
                            encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromSlot);
                            encoder.EmitRET();
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
