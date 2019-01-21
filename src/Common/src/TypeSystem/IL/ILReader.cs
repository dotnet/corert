// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public ILReader(MethodIL methodIL)
        {
            _ilBytes = methodIL.GetILBytes();
            _currentOffset = 0;
        }

        //
        // IL stream reading
        //

        public byte ReadILByte()
        {
            return _ilBytes[_currentOffset++];
        }

        public UInt16 ReadILUInt16()
        {
            UInt16 val = (UInt16)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8));
            _currentOffset += 2;
            return val;
        }

        public UInt32 ReadILUInt32()
        {
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

        public bool Read(out ILOpcode opcode)
        {
            opcode = default(byte);
            if (_currentOffset == _ilBytes.Length)
                return false;

            opcode = (ILOpcode)ReadILByte();
            if (opcode == ILOpcode.prefix1)
            {
                opcode = (ILOpcode)(0x100 + ReadILByte());
            }

            return true;
        }

        public void Seek(int offset)
        {
            _currentOffset = offset;
        }
    }
}
