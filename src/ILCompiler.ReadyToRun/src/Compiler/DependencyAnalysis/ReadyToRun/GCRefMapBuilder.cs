﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;

// The GCRef map is used to encode GC type of arguments for callsites. Logically, it is sequence <pos, token> where pos is 
// position of the reference in the stack frame and token is type of GC reference (one of GCREFMAP_XXX values).
//
// - The encoding always starts at the byte boundary. The high order bit of each byte is used to signal end of the encoding 
// stream. The last byte has the high order bit zero. It means that there are 7 useful bits in each byte.
// - "pos" is always encoded as delta from previous pos.
// - The basic encoding unit is two bits. Values 0, 1 and 2 are the common constructs (skip single slot, GC reference, interior 
// pointer). Value 3 means that extended encoding follows. 
// - The extended information is integer encoded in one or more four bit blocks. The high order bit of the four bit block is 
// used to signal the end.
// - For x86, the encoding starts by size of the callee poped stack. The size is encoded using the same mechanism as above (two bit
// basic encoding, with extended encoding for large values).

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class GCRefMapBuilder
    {
        /// <summary>
        /// Node factory to use
        /// </summary>
        private readonly NodeFactory _factory;

        /// <summary>
        /// Pending value, not yet written out
        /// </summary>
        private int _pendingByte;

        /// <summary>
        /// Number of bits in pending byte. Note that the trailing zero bits are not written out, 
        /// so this can be more than 7.
        /// </summary>
        private int _bits;

        /// <summary>
        /// Current position
        /// </summary>
        private uint _pos;

        /// <summary>
        /// Builder for the generated data
        /// </summary>
        public ObjectDataBuilder Builder;

        public GCRefMapBuilder(NodeFactory factory, bool relocsOnly)
        {
            _factory = factory;
            _pendingByte = 0;
            _bits = 0;
            _pos = 0;
            Builder = new ObjectDataBuilder(factory, relocsOnly);
        }

        public void GetCallRefMap(MethodDesc method)
        {
            bool hasThis = (method.Signature.Flags & MethodSignatureFlags.Static) == 0;
            bool isVarArg = false;
            TypeHandle returnType = new TypeHandle(method.Signature.ReturnType);
            TypeHandle[] parameterTypes = new TypeHandle[method.Signature.Length];
            for (int parameterIndex = 0; parameterIndex < parameterTypes.Length; parameterIndex++)
            {
                parameterTypes[parameterIndex] = new TypeHandle(method.Signature[parameterIndex]);
            }
            CallingConventions callingConventions = (hasThis ? CallingConventions.ManagedInstance : CallingConventions.ManagedStatic);
            bool hasParamType = method.GetCanonMethodTarget(CanonicalFormKind.Specific).RequiresInstArg();
            bool extraFunctionPointerArg = false;
            bool[] forcedByRefParams = new bool[parameterTypes.Length];
            bool skipFirstArg = false;
            bool extraObjectFirstArg = false;
            ArgIteratorData argIteratorData = new ArgIteratorData(hasThis, isVarArg, parameterTypes, returnType);

            ArgIterator argit = new ArgIterator(
                method.Context,
                argIteratorData,
                callingConventions,
                hasParamType,
                extraFunctionPointerArg,
                forcedByRefParams,
                skipFirstArg,
                extraObjectFirstArg);

            int nStackBytes = argit.SizeOfFrameArgumentArray();

            // Allocate a fake stack
            CORCOMPILE_GCREFMAP_TOKENS[] fakeStack = new CORCOMPILE_GCREFMAP_TOKENS[TransitionBlock.Size + nStackBytes];

            // Fill it in
            FakeGcScanRoots(method, argit, fakeStack);

            // Encode the ref map
            uint nStackSlots;
            if (_factory.Target.Architecture == TargetArchitecture.X86)
            {
                uint cbStackPop = argit.CbStackPop();
                WriteStackPop(cbStackPop / (uint)_factory.Target.PointerSize);

                nStackSlots = (uint)(nStackBytes / _factory.Target.PointerSize + ArchitectureConstants.NUM_ARGUMENT_REGISTERS);
            }
            else
            {
                nStackSlots = (uint)((TransitionBlock.Size + nStackBytes - TransitionBlock.GetOffsetOfArgumentRegisters()) / _factory.Target.PointerSize);
            }

            for (uint pos = 0; pos < nStackSlots; pos++)
            {
                int ofs;

                if (_factory.Target.Architecture == TargetArchitecture.X86)
                {
                    ofs = (int)(pos < ArchitectureConstants.NUM_ARGUMENT_REGISTERS ?
                        TransitionBlock.GetOffsetOfArgumentRegisters() + ArchitectureConstants.ARGUMENTREGISTERS_SIZE - (pos + 1) * _factory.Target.PointerSize :
                        TransitionBlock.GetOffsetOfArgs() + (pos - ArchitectureConstants.NUM_ARGUMENT_REGISTERS) * _factory.Target.PointerSize);
                }
                else
                {
                    ofs = (int)(TransitionBlock.GetOffsetOfArgumentRegisters() + pos * _factory.Target.PointerSize);
                }

                CORCOMPILE_GCREFMAP_TOKENS token = fakeStack[ofs];

                if (token != CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_SKIP)
                {
                    WriteToken(pos, (byte)token);
                }
            }

            Flush();
        }

        /// <summary>
        /// Fill in the GC-relevant stack frame locations.
        /// </summary>
        private void FakeGcScanRoots(MethodDesc method, ArgIterator argit, CORCOMPILE_GCREFMAP_TOKENS[] frame)
        {
            // Encode generic instantiation arg
            if (argit.HasParamType())
            {
                if (method.RequiresInstMethodDescArg())
                {
                    frame[argit.GetParamTypeArgOffset()] = CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_METHOD_PARAM;
                }
                else if (method.RequiresInstMethodTableArg())
                {
                    frame[argit.GetParamTypeArgOffset()] = CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_TYPE_PARAM;
                }
            }

            // If the function has a this pointer, add it to the mask
            if (argit.HasThis())
            {
                bool isUnboxingStub = false; // TODO: is this correct?
                bool interior = method.OwningType.IsValueType && !isUnboxingStub;

                frame[ArgIterator.GetThisOffset()] = (interior ? CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_INTERIOR : CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_REF);
            }

            if (argit.IsVarArg())
            {
                frame[argit.GetVASigCookieOffset()] = CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_VASIG_COOKIE;

                // We are done for varargs - the remaining arguments are reported via vasig cookie
                return;
            }

            // Also if the method has a return buffer, then it is the first argument, and could be an interior ref,
            // so always promote it.
            if (argit.HasRetBuffArg())
            {
                frame[argit.GetRetBuffArgOffset()] = CORCOMPILE_GCREFMAP_TOKENS.GCREFMAP_INTERIOR;
            }

            //
            // Now iterate the arguments
            //

            // Cycle through the arguments, and call GcScanRoots for each
            int argIndex = 0;
            int argOffset;
            while ((argOffset = argit.GetNextOffset()) != TransitionBlock.InvalidOffset)
            {
                ArgLocDesc? argLocDescForStructInRegs = argit.GetArgLoc(argOffset);
                ArgDestination argDest = new ArgDestination(argOffset, argLocDescForStructInRegs);
                GcScanRoots(method.Signature[argIndex], in argDest, delta: 0, frame);
                argIndex++;
            }
        }

        /// <summary>
        /// Report GC locations for a single method parameter.
        /// </summary>
        /// <param name="type">Parameter type</param>
        /// <param name="argDest">Location of the parameter</param>
        /// <param name="frame">Frame map to update by marking GC locations</param>
        private void GcScanRoots(TypeDesc type, in ArgDestination argDest, int delta, CORCOMPILE_GCREFMAP_TOKENS[] frame)
        {
            switch (type.Category)
            {
                // TYPE_GC_NONE
                case TypeFlags.Void:
                case TypeFlags.Boolean:
                case TypeFlags.Char:
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                case TypeFlags.Single:
                case TypeFlags.Double:
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                case TypeFlags.Enum:
                    break;

                // TYPE_GC_REF
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    argDest.GcMark(frame, delta, interior: false);
                    break;

                // TYPE_GC_BYREF
                case TypeFlags.ByRef:
                    argDest.GcMark(frame, delta, interior: true);
                    break;

                // TYPE_GC_OTHER
                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    GcScanValueType(type, argDest, delta, frame);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private void GcScanValueType(TypeDesc type, ArgDestination argDest, int delta, CORCOMPILE_GCREFMAP_TOKENS[] frame)
        {
            if (ArgIterator.IsArgPassedByRef(new TypeHandle(type)))
            {
                argDest.GcMark(frame, delta, interior: true);
                return;
            }

#if UNIX_AMD64_ABI
            // ReportPointersFromValueTypeArg
            if (argDest.IsStructPassedInRegs)
            {
                // ReportPointersFromStructPassedInRegs
                throw new NotImplementedException();
            }
#endif
            // ReportPointersFromValueType
            if (type.IsByRefLike)
            {
                // TODO: FindByRefPointersInByRefLikeObject
                throw new NotImplementedException();
            }

            if (type is DefType defType)
            {
                FieldLayoutAlgorithm fieldLayoutAlgorithm = _factory.TypeSystemContext.GetLayoutAlgorithmForType(defType);
                ComputedInstanceFieldLayout instanceFieldLayout = fieldLayoutAlgorithm.ComputeInstanceLayout(defType, InstanceLayoutKind.TypeAndFields);
                foreach (FieldAndOffset fieldAndOffset in instanceFieldLayout.Offsets)
                {
                    FieldDesc field = fieldAndOffset.Field;
                    GcScanRoots(field.FieldType, argDest, fieldAndOffset.Offset.AsInt, frame);
                }
                return;
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Append single bit to the stream
        /// </summary>
        /// <param name="bit"></param>
        private void AppendBit(uint bit)
        {
            if (bit != 0)
            {
                while (_bits >= 7)
                {
                    Builder.EmitByte((byte)(_pendingByte | 0x80));
                    _pendingByte = 0;
                    _bits -= 7;
                }

                _pendingByte |= (1 << _bits);
            }

            _bits++;
        }

        private void AppendTwoBit(uint bits)
        {
            AppendBit(bits & 1);
            AppendBit(bits >> 1);
        }

        private void AppendInt(uint val)
        {
            do
            {
                AppendBit(val & 1);
                AppendBit((val >> 1) & 1);
                AppendBit((val >> 2) & 1);

                val >>= 3;

                AppendBit((val != 0) ? 1u : 0u);
            }
            while (val != 0);
        }

        /// <summary>
        /// Emit stack pop into the stream (X86 only).
        /// </summary>
        /// <param name="stackPop">Stack pop value</param>
        public void WriteStackPop(uint stackPop)
        {
            if (stackPop < 3)
            {
                AppendTwoBit(stackPop);
            }
            else
            {
                AppendTwoBit(3);
                AppendInt((uint)(stackPop - 3));
            }
        }

        public void WriteToken(uint pos, uint gcRefMapToken)
        {
            uint posDelta = pos - _pos;
            _pos = pos + 1;

            if (posDelta != 0)
            {
                if (posDelta < 4)
                {
                    // Skipping by one slot at a time for small deltas produces smaller encoding.
                    while (posDelta > 0)
                    {
                        AppendTwoBit(0);
                        posDelta--;
                    }
                }
                else
                {
                    AppendTwoBit(3);
                    AppendInt((posDelta - 4) << 1);
                }
            }

            if (gcRefMapToken < 3)
            {
                AppendTwoBit(gcRefMapToken);
            }
            else
            {
                AppendTwoBit(3);
                AppendInt(((gcRefMapToken - 3) << 1) | 1);
            }
        }

        public void Flush()
        {
            if ((_pendingByte & 0x7F) != 0 || _pos == 0)
                Builder.EmitByte((byte)(_pendingByte & 0x7F));

            _pendingByte = 0;
            _bits = 0;

            _pos = 0;
        }
    }
}
