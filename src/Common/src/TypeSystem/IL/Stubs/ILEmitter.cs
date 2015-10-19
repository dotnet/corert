// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.IL;
using Internal.TypeSystem;

namespace Internal.IL.Stubs
{
    public class ILCodeStream
    {
        static readonly byte[] s_empty = new byte[0];

        internal byte[] _instructions;
        internal int _length;

        internal ILCodeStream()
        {
            _instructions = s_empty;
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
    }

    class ILStubMethodIL : MethodIL
    {
        byte[] _ilBytes;
        TypeDesc[] _locals;
        Object[] _tokens;

        public ILStubMethodIL(byte[] ilBytes, TypeDesc[] locals, Object[] tokens)
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
            return new ILExceptionRegion[0]; // TODO: Array.Empty<ILExceptionRegion>()
        }
        public override bool GetInitLocals()
        {
            return true;
        }
        public override TypeDesc[] GetLocals()
        {
            return _locals;
        }
        public override Object GetObject(int token)
        {
            return _tokens[(token & 0xFFFFFF) - 1];
        }
    }

    public class ILEmitter
    {
        ArrayBuilder<ILCodeStream> _codeStreams;
        ArrayBuilder<TypeDesc> _locals;
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

        public int NewLocal(TypeDesc localType)
        {
            int index = _locals.Count;
            _locals.Add(localType);
            return index;
        }

        public MethodIL Link()
        {
            int totalLength = 0;
            for (int i = 0; i < _codeStreams.Count; i++)
                totalLength += _codeStreams[i]._length;

            byte[] ilInstructions = new byte[totalLength];
            int copiedLength = 0;
            for (int i = 0; i < _codeStreams.Count; i++)
            {
                ILCodeStream ilCodeStream = _codeStreams[i];
                Array.Copy(ilCodeStream._instructions, 0, ilInstructions, copiedLength, ilCodeStream._length);
                copiedLength += ilCodeStream._length;
            }

            return new ILStubMethodIL(ilInstructions, _locals.ToArray(), _tokens.ToArray());
        }
    }

    public abstract class ILStubMethod : MethodDesc
    {
        public abstract MethodIL EmitIL();
    }
}
