// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using ILToNative.DependencyAnalysis.X64;
using Internal.TypeSystem;

namespace ILToNative.DependencyAnalysis
{
    /// <summary>
    /// X64 specific portions of ReadyToRunHelperNode
    /// </summary>
    public partial class ReadyToRunHelperNode
    {
        protected override void EmitCode(NodeFactory factory, ref X64Emitter encoder, bool relocsOnly)
        {
            switch (Helper.Id)
            {
                case ReadyToRunHelperId.NewHelper:
                    encoder.EmitLEAQ(Register.RCX, factory.ConstructedTypeSymbol((TypeDesc)Helper.Target));
                    encoder.EmitJMP(factory.ExternSymbol("__allocate_object"));
                    break;

                case ReadyToRunHelperId.VirtualCall:
                    if (relocsOnly)
                        break;

                    AddrMode loadFromRcx = new AddrMode(Register.RCX, null, 0, 0, AddrModeSize.Int64);
                    encoder.EmitMOV(Register.RAX, ref loadFromRcx);

                    // TODO: More efficient lookup of the slot
                    {
                        MethodDesc method = (MethodDesc)Helper.Target;
                        TypeDesc owningType = method.OwningType;

                        int baseSlots = 0;
                        var baseType = owningType.BaseType;

                        while (baseType != null)
                        {
                            List<MethodDesc> baseVirtualSlots;
                            factory.VirtualSlots.TryGetValue(baseType, out baseVirtualSlots);

                            if (baseVirtualSlots != null)
                                baseSlots += baseVirtualSlots.Count;
                            baseType = baseType.BaseType;
                        }

                        List<MethodDesc> virtualSlots = factory.VirtualSlots[owningType];
                        int methodSlot = -1;
                        for (int slot = 0; slot < virtualSlots.Count; slot++)
                        {
                            if (virtualSlots[slot] == method)
                            {
                                methodSlot = slot;
                                break;
                            }
                        }

                        Debug.Assert(methodSlot != -1);
                        AddrMode jmpAddrMode = new AddrMode(Register.RAX, null, 16 + (baseSlots + methodSlot) * factory.Target.PointerSize, 0, AddrModeSize.Int64);
                        encoder.EmitJmpToAddrMode(ref jmpAddrMode);
                    }
                    break;

                case ReadyToRunHelperId.IsInstanceOf:
                    encoder.EmitLEAQ(Register.RDX, factory.NecessaryTypeSymbol((TypeDesc)Helper.Target));
                    encoder.EmitJMP(factory.ExternSymbol("__isinst_class"));
                    break;

                case ReadyToRunHelperId.CastClass:
                    encoder.EmitLEAQ(Register.RDX, factory.NecessaryTypeSymbol((TypeDesc)Helper.Target));
                    encoder.EmitJMP(factory.ExternSymbol("__castclass_class"));
                    break;

                case ReadyToRunHelperId.NewArr1:
                    encoder.EmitLEAQ(Register.RDX, factory.NecessaryTypeSymbol((TypeDesc)Helper.Target));
                    encoder.EmitJMP(factory.ExternSymbol("__allocate_array"));
                    break;

                case ReadyToRunHelperId.GetNonGCStaticBase:
                    encoder.EmitLEAQ(Register.RAX, factory.TypeNonGCStaticsSymbol((MetadataType)Helper.Target));
                    encoder.EmitRET();
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    encoder.EmitLEAQ(Register.RAX, factory.TypeGCStaticsSymbol((MetadataType)Helper.Target));
                    AddrMode loadFromRax = new AddrMode(Register.RAX, null, 0, 0, AddrModeSize.Int64);
                    encoder.EmitMOV(Register.RAX, ref loadFromRax);
                    encoder.EmitMOV(Register.RAX, ref loadFromRax);
                    encoder.EmitRET();
                    break;

                case ReadyToRunHelperId.CCtorTrigger:
                    encoder.EmitLEAQ(Register.RCX, factory.TypeCctorContextSymbol((MetadataType)Helper.Target));
                    encoder.EmitJMP(factory.WellKnownEntrypoint(WellKnownEntrypoint.EnsureClassConstructorRun));
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateInfo target = (DelegateInfo)Helper.Target;

                        if (target.ShuffleThunk != null)
                        {
                            // TBD: mscorlib doesn't have ctor for static delegate, but we can make use of the
                            // ctor for non-static delegate, by passing the delegate ref as the first argument
                            // and the shuffle thunk as the second argument.
                            Register rcxDirect = (Register)((int)Register.RCX + (int)Register.RegDirect);
                            AddrMode loadFromRcxDirect = new AddrMode(rcxDirect, null, 0, 0, AddrModeSize.Int64);
                            encoder.EmitMOV(Register.RDX, ref loadFromRcxDirect);
                            encoder.EmitLEAQ(Register.R8, factory.MethodEntrypoint(target.ShuffleThunk));
                        }
                        else
                        {
                            encoder.EmitLEAQ(Register.R8, factory.MethodEntrypoint(target.Target));
                        }
                        encoder.EmitJMP(factory.MethodEntrypoint(target.Ctor));
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
