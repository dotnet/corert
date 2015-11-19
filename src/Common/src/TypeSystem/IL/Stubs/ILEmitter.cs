// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.IL;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    public class ILCodeStream
    {
        private struct LabelAndOffset
        {
            public readonly ILCodeLabel Label;
            public readonly int Offset;
            public LabelAndOffset(ILCodeLabel label, int offset)
            {
                Label = label;
                Offset = offset;
            }
        }

        internal byte[] _instructions;
        internal int _length;
        internal int _startOffsetForLinking;

        private ArrayBuilder<LabelAndOffset> _offsetsNeedingPatching;

        internal ILCodeStream()
        {
            _instructions = Array.Empty<byte>();
            _startOffsetForLinking = -1;
        }

        private void EmitByte(byte b)
        {
            if (_instructions.Length == _length)
                Array.Resize<byte>(ref _instructions, 2 * _instructions.Length + 10);
            _instructions[_length++] = b;
        }

        private void EmitUInt16(ushort value)
        {
            EmitByte((byte)value);
            EmitByte((byte)(value >> 8));
        }

        private void EmitUInt32(int value)
        {
            EmitByte((byte)value);
            EmitByte((byte)(value >> 8));
            EmitByte((byte)(value >> 16));
            EmitByte((byte)(value >> 24));
        }

        public void Emit(ILOpcode opcode)
        {
            if ((int)opcode > 0x100)
                EmitByte((byte)ILOpcode.prefix1);
            EmitByte((byte)opcode);
        }

        public void Emit(ILOpcode opcode, int token)
        {
            Emit(opcode);
            EmitUInt32(token);
        }

        public void EmitLdc(int value)
        {
            if (-1 <= value && value <= 8)
            {
                Emit((ILOpcode)(ILOpcode.ldc_i4_0 + value));
            }
            else if (value == (sbyte)value)
            {
                Emit(ILOpcode.ldc_i4_s);
                EmitByte((byte)value);
            }
            else
            {
                Emit(ILOpcode.ldc_i4);
                EmitUInt32(value);
            }
        }

        public void EmitLdArg(int index)
        {
            if (index < 4)
            {
                Emit((ILOpcode)(ILOpcode.ldarg_0 + index));
            }
            else
            {
                Emit(ILOpcode.ldarg);
                EmitUInt16((ushort)index);
            }
        }

        public void EmitLdLoc(int index)
        {
            if (index < 4)
            {
                Emit((ILOpcode)(ILOpcode.ldloc_0 + index));
            }
            else if (index < 0x100)
            {
                Emit(ILOpcode.ldloc_s);
                EmitByte((byte)index);
            }
            else
            {
                Emit(ILOpcode.ldloc);
                EmitUInt16((ushort)index);
            }
        }

        public void EmitLdLoca(int index)
        {
            if (index < 0x100)
            {
                Emit(ILOpcode.ldloca_s);
                EmitByte((byte)index);
            }
            else
            {
                Emit(ILOpcode.ldloca);
                EmitUInt16((ushort)index);
            }
        }

        public void EmitStLoc(int index)
        {
            if (index < 4)
            {
                Emit((ILOpcode)(ILOpcode.stloc_0 + index));
            }
            else if (index < 0x100)
            {
                Emit(ILOpcode.stloc_s);
                EmitByte((byte)index);
            }
            else
            {
                Emit(ILOpcode.stloc);
                EmitUInt16((ushort)index);
            }
        }

        public void Emit(ILOpcode opcode, ILCodeLabel label)
        {
            Debug.Assert(opcode == ILOpcode.br || opcode == ILOpcode.brfalse ||
                opcode == ILOpcode.brtrue || opcode == ILOpcode.beq ||
                opcode == ILOpcode.bge || opcode == ILOpcode.bgt ||
                opcode == ILOpcode.ble || opcode == ILOpcode.blt ||
                opcode == ILOpcode.bne_un || opcode == ILOpcode.bge_un ||
                opcode == ILOpcode.bgt_un || opcode == ILOpcode.ble_un ||
                opcode == ILOpcode.blt_un || opcode == ILOpcode.leave);

            Emit(opcode);
            _offsetsNeedingPatching.Add(new LabelAndOffset(label, _length));
            EmitUInt32(0);
        }

        public void EmitLabel(ILCodeLabel label)
        {
            label.Place(this, _length);
        }

        internal void PatchLabels()
        {
            for (int i = 0; i < _offsetsNeedingPatching.Count; i++)
            {
                LabelAndOffset patch = _offsetsNeedingPatching[i];

                Debug.Assert(patch.Label.IsPlaced);
                Debug.Assert(_startOffsetForLinking > -1);

                int value = patch.Label.AbsoluteOffset - _startOffsetForLinking - patch.Offset - 4;
                int offset = patch.Offset;
                _instructions[offset] = (byte)value;
                _instructions[offset + 1] = (byte)(value >> 8);
                _instructions[offset + 2] = (byte)(value >> 16);
                _instructions[offset + 3] = (byte)(value >> 24);
            }
        }
    }

    class ILStubMethodIL : MethodIL
    {
        byte[] _ilBytes;
        LocalVariableDefinition[] _locals;
        Object[] _tokens;

        public ILStubMethodIL(byte[] ilBytes, LocalVariableDefinition[] locals, Object[] tokens)
        {
            _ilBytes = ilBytes;
            _locals = locals;
            _tokens = tokens;
        }
        public override byte[] GetILBytes()
        {
            return _ilBytes;
        }
        public override int GetMaxStack()
        {
            // Conservative estimate...
            return _ilBytes.Length;
        }
        public override ILExceptionRegion[] GetExceptionRegions()
        {
            return Array.Empty<ILExceptionRegion>();
        }
        public override bool GetInitLocals()
        {
            return true;
        }
        public override LocalVariableDefinition[] GetLocals()
        {
            return _locals;
        }
        public override Object GetObject(int token)
        {
            return _tokens[(token & 0xFFFFFF) - 1];
        }
    }

    public class ILCodeLabel
    {
        private ILCodeStream _codeStream;
        private int _offsetWithinCodeStream;

        internal bool IsPlaced
        {
            get
            {
                return _codeStream != null;
            }
        }

        internal int AbsoluteOffset
        {
            get
            {
                Debug.Assert(IsPlaced);
                Debug.Assert(_codeStream._startOffsetForLinking >= 0);
                return _codeStream._startOffsetForLinking + _offsetWithinCodeStream;
            }
        }

        internal ILCodeLabel()
        {
        }

        internal void Place(ILCodeStream codeStream, int offsetWithinCodeStream)
        {
            Debug.Assert(!IsPlaced);
            _codeStream = codeStream;
            _offsetWithinCodeStream = offsetWithinCodeStream;
        }
    }

    public class ILEmitter
    {
        ArrayBuilder<ILCodeStream> _codeStreams;
        ArrayBuilder<LocalVariableDefinition> _locals;
        ArrayBuilder<Object> _tokens;

        public ILEmitter()
        {
        }

        public ILCodeStream NewCodeStream()
        {
            ILCodeStream stream = new ILCodeStream();
            _codeStreams.Add(stream);
            return stream;
        }

        private int NewToken(Object value, int tokenType)
        {
            _tokens.Add(value);
            return _tokens.Count | tokenType;
        }

        public int NewToken(TypeDesc value)
        {
            return NewToken(value, 0x01000000);
        }

        public int NewToken(MethodDesc value)
        {
            return NewToken(value, 0x0a000000);
        }

        public int NewToken(FieldDesc value)
        {
            return NewToken(value, 0x0a000000);
        }

        public int NewToken(string value)
        {
            return NewToken(value, 0x70000000);
        }

        public int NewToken(MethodSignature value)
        {
            return NewToken(value, 0x11000000);
        }

        public int NewLocal(TypeDesc localType, bool isPinned = false)
        {
            int index = _locals.Count;
            _locals.Add(new LocalVariableDefinition(localType, isPinned));
            return index;
        }

        public ILCodeLabel NewCodeLabel()
        {
            var newLabel = new ILCodeLabel();
            return newLabel;
        }

        public MethodIL Link()
        {
            int totalLength = 0;
            for (int i = 0; i < _codeStreams.Count; i++)
            {
                ILCodeStream ilCodeStream = _codeStreams[i];
                ilCodeStream._startOffsetForLinking = totalLength;
                totalLength += ilCodeStream._length;
            }

            byte[] ilInstructions = new byte[totalLength];
            int copiedLength = 0;
            for (int i = 0; i < _codeStreams.Count; i++)
            {
                ILCodeStream ilCodeStream = _codeStreams[i];
                ilCodeStream.PatchLabels();
                Array.Copy(ilCodeStream._instructions, 0, ilInstructions, copiedLength, ilCodeStream._length);
                copiedLength += ilCodeStream._length;
            }

            return new ILStubMethodIL(ilInstructions, _locals.ToArray(), _tokens.ToArray());
        }
    }

    public abstract class ILStubMethod : MethodDesc
    {
        public abstract MethodIL EmitIL();

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return false;
        }
    }
}
