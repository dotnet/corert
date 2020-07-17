// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;

namespace Internal.IL
{
    internal struct ILReader
    {
        private int _currentOffset;
        private readonly byte[] _ilBytes;

        public int Offset
        {
            get
            {
                return _currentOffset;
            }
        }

        public int Size
        {
            get
            {
                return _ilBytes.Length;
            }
        }

        public bool HasNext
        {
            get
            {
                return _currentOffset < _ilBytes.Length;
            }
        }

        public ILReader(byte[] ilBytes)
        {
            _ilBytes = ilBytes;
            _currentOffset = 0;
        }

        //
        // IL stream reading
        //

        public byte ReadILByte()
        {
            if (_currentOffset + 1 > _ilBytes.Length)
                ThrowHelper.ThrowInvalidProgramException();

            return _ilBytes[_currentOffset++];
        }

        public UInt16 ReadILUInt16()
        {
            if (_currentOffset + 2 > _ilBytes.Length)
                ThrowHelper.ThrowInvalidProgramException();

            UInt16 val = (UInt16)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8));
            _currentOffset += 2;
            return val;
        }

        public UInt32 ReadILUInt32()
        {
            if (_currentOffset + 4 > _ilBytes.Length)
                ThrowHelper.ThrowInvalidProgramException();

            UInt32 val = (UInt32)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8) + (_ilBytes[_currentOffset + 2] << 16) + (_ilBytes[_currentOffset + 3] << 24));
            _currentOffset += 4;
            return val;
        }

        public int ReadILToken()
        {
            return (int)ReadILUInt32();
        }

        public ulong ReadILUInt64()
        {
            ulong value = ReadILUInt32();
            value |= (((ulong)ReadILUInt32()) << 32);
            return value;
        }

        public unsafe float ReadILFloat()
        {
            uint value = ReadILUInt32();
            return *(float*)(&value);
        }

        public unsafe double ReadILDouble()
        {
            ulong value = ReadILUInt64();
            return *(double*)(&value);
        }

        public ILOpcode ReadILOpcode()
        {
            ILOpcode opcode = (ILOpcode)ReadILByte();
            if (opcode == ILOpcode.prefix1)
            {
                opcode = (ILOpcode)(0x100 + ReadILByte());
            }

            return opcode;
        }

        public ILOpcode PeekILOpcode()
        {
            ILOpcode opcode = (ILOpcode)_ilBytes[_currentOffset];
            if (opcode == ILOpcode.prefix1)
            {
                if (_currentOffset + 2 > _ilBytes.Length)
                    ThrowHelper.ThrowInvalidProgramException();
                opcode = (ILOpcode)(0x100 + _ilBytes[_currentOffset + 1]);
            }

            return opcode;
        }

        public void Skip(ILOpcode opcode)
        {
            if (!opcode.IsValid())
                ThrowHelper.ThrowInvalidProgramException();

            if (opcode != ILOpcode.switch_)
            {
                int opcodeSize = (byte)opcode != (int)opcode ? 2 : 1;
                _currentOffset += opcode.GetSize() - opcodeSize;
            }
            else
            {
                // "switch" opcode is special
                uint count = ReadILUInt32();
                _currentOffset += checked((int)(count * 4));
            }
        }

        public void Seek(int offset)
        {
            _currentOffset = offset;
        }
    }
}
