// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ILCompiler.DependencyAnalysis.X64;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    partial class ReadyToRunGenericHelperNode
    {
        protected void EmitDictionaryLookup(NodeFactory factory, ref X64Emitter encoder, Register context, Register result, GenericLookupResult lookup, bool relocsOnly)
        {
            // INVARIANT: must not trash context register

            // Find the generic dictionary slot
            int dictionarySlot = 0;
            if (!relocsOnly)
            {
                // The concrete slot won't be known until we're emitting data - don't ask for it in relocsOnly.
                dictionarySlot = factory.GenericDictionaryLayout(_dictionaryOwner).GetSlotForEntry(lookup);
            }

            // Load the generic dictionary cell
            AddrMode loadEntry = new AddrMode(
                context, null, dictionarySlot * factory.Target.PointerSize, 0, AddrModeSize.Int64);
            encoder.EmitMOV(result, ref loadEntry);
        }

        protected sealed override void EmitCode(NodeFactory factory, ref X64Emitter encoder, bool relocsOnly)
        {
            // First load the generic context into Arg0.
            EmitLoadGenericContext(factory, ref encoder, relocsOnly);

            switch (_id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)_target;

                        if (!factory.TypeSystemContext.HasLazyStaticConstructor(target))
                        {
                            EmitDictionaryLookup(factory, ref encoder, encoder.TargetRegister.Arg0, encoder.TargetRegister.Result, _lookupSignature, relocsOnly);
                            encoder.EmitRET();
                        }
                        else
                        {
                            EmitDictionaryLookup(factory, ref encoder, encoder.TargetRegister.Arg0, encoder.TargetRegister.Arg0, _lookupSignature, relocsOnly);

                            // We need to trigger the cctor before returning the base. It is stored at the beginning of the non-GC statics region.
                            int cctorContextSize = NonGCStaticsNode.GetClassConstructorContextStorageSize(factory.Target, target);

                            AddrMode loadBase = new AddrMode(encoder.TargetRegister.Arg0, null, cctorContextSize, 0, AddrModeSize.Int64);
                            encoder.EmitLEA(encoder.TargetRegister.Result, ref loadBase);

                            AddrMode initialized = new AddrMode(encoder.TargetRegister.Arg0, null, factory.Target.PointerSize, 0, AddrModeSize.Int32);
                            encoder.EmitCMP(ref initialized, 1);
                            encoder.EmitRETIfEqual();

                            encoder.EmitMOV(encoder.TargetRegister.Arg1, encoder.TargetRegister.Result);
                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase));
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)_target;

                        EmitDictionaryLookup(factory, ref encoder, encoder.TargetRegister.Arg0, encoder.TargetRegister.Result, _lookupSignature, relocsOnly);

                        AddrMode loadFromResult = new AddrMode(encoder.TargetRegister.Result, null, 0, 0, AddrModeSize.Int64);
                        encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromResult);
                        encoder.EmitMOV(encoder.TargetRegister.Result, ref loadFromResult);

                        if (!factory.TypeSystemContext.HasLazyStaticConstructor(target))
                        {
                            encoder.EmitRET();
                        }
                        else
                        {
                            // We need to trigger the cctor before returning the base. It is stored at the beginning of the non-GC statics region.
                            GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            EmitDictionaryLookup(factory, ref encoder, encoder.TargetRegister.Arg0, encoder.TargetRegister.Arg0, nonGcRegionLookup, relocsOnly);

                            AddrMode initialized = new AddrMode(encoder.TargetRegister.Arg0, null, factory.Target.PointerSize, 0, AddrModeSize.Int32);
                            encoder.EmitCMP(ref initialized, 1);
                            encoder.EmitRETIfEqual();

                            encoder.EmitMOV(encoder.TargetRegister.Arg1, encoder.TargetRegister.Result);

                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnGCStaticBase));
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)_target;

                        // Look up the index cell
                        EmitDictionaryLookup(factory, ref encoder, encoder.TargetRegister.Arg0, encoder.TargetRegister.Arg1, _lookupSignature, relocsOnly);

                        ISymbolNode helperEntrypoint;
                        if (factory.TypeSystemContext.HasLazyStaticConstructor(target))
                        {
                            // There is a lazy class constructor. We need the non-GC static base because that's where the
                            // class constructor context lives.
                            GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            EmitDictionaryLookup(factory, ref encoder, encoder.TargetRegister.Arg0, encoder.TargetRegister.Arg2, nonGcRegionLookup, relocsOnly);

                            helperEntrypoint = factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnThreadStaticBase);
                        }
                        else
                        {
                            helperEntrypoint = factory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType);
                        }

                        // First arg: address of the TypeManager slot that provides the helper with
                        // information about module index and the type manager instance (which is used
                        // for initialization on first access).
                        AddrMode loadFromArg1 = new AddrMode(encoder.TargetRegister.Arg1, null, 0, 0, AddrModeSize.Int64);
                        encoder.EmitMOV(encoder.TargetRegister.Arg0, ref loadFromArg1);

                        // Second arg: index of the type in the ThreadStatic section of the modules
                        AddrMode loadFromArg1AndDelta = new AddrMode(encoder.TargetRegister.Arg1, null, factory.Target.PointerSize, 0, AddrModeSize.Int64);
                        encoder.EmitMOV(encoder.TargetRegister.Arg1, ref loadFromArg1AndDelta);

                        encoder.EmitJMP(helperEntrypoint);
                    }
                    break;

                // These are all simple: just get the thing from the dictionary and we're done
                case ReadyToRunHelperId.TypeHandle:
                case ReadyToRunHelperId.MethodDictionary:
                case ReadyToRunHelperId.VirtualCall:
                case ReadyToRunHelperId.ResolveVirtualFunction:
                case ReadyToRunHelperId.MethodEntry:
                    {
                        EmitDictionaryLookup(factory, ref encoder, encoder.TargetRegister.Arg0, encoder.TargetRegister.Result, _lookupSignature, relocsOnly);
                        encoder.EmitRET();
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        protected virtual void EmitLoadGenericContext(NodeFactory factory, ref X64Emitter encoder, bool relocsOnly)
        {
            // Assume generic context is already loaded in Arg0.
        }
    }

    partial class ReadyToRunGenericLookupFromTypeNode
    {
        protected override void EmitLoadGenericContext(NodeFactory factory, ref X64Emitter encoder, bool relocsOnly)
        {
            // We start with Arg0 pointing to the EEType

            // Locate the VTable slot that points to the dictionary
            int vtableSlot = 0;
            if (!relocsOnly)
            {
                // The concrete slot won't be known until we're emitting data - don't ask for it in relocsOnly.
                vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(factory, (TypeDesc)_dictionaryOwner);
            }

            int pointerSize = factory.Target.PointerSize;
            int slotOffset = EETypeNode.GetVTableOffset(pointerSize) + (vtableSlot * pointerSize);

            // Load the dictionary pointer from the VTable
            AddrMode loadDictionary = new AddrMode(encoder.TargetRegister.Arg0, null, slotOffset, 0, AddrModeSize.Int64);
            encoder.EmitMOV(encoder.TargetRegister.Arg0, ref loadDictionary);
        }
    }
}
