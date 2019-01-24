// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

using Internal.TypeSystem;
using Internal.IL;

using Debug = System.Diagnostics.Debug;

namespace Internal.Compiler
{
    /// <summary>
    /// IL Opcode reader in external reader style where the reading is done by trying to read
    /// various opcodes, and the reader can indicate success or failure of reading a particular opcode
    /// 
    /// Used by logic which is designed to encode information in il structure, but not used
    /// to support general compilation of IL.
    /// </summary>
    public struct ILStreamReader
    {
        private byte[] _ilBytes;
        private MethodIL _methodIL;
        private int _currentOffset;

        public ILStreamReader(MethodIL methodIL)
        {
            _methodIL = methodIL;
            _ilBytes = methodIL.GetILBytes();
            _currentOffset = 0;
        }

        //
        // IL stream reading
        //

        private byte ReadILByte()
        {
            return _ilBytes[_currentOffset++];
        }

        private ILOpcode ReadILOpcode()
        {
            return (ILOpcode)ReadILByte();
        }
        
        private byte PeekILByte()
        {
            return _ilBytes[_currentOffset];
        }

        private ILOpcode PeekILOpcode()
        {
            return (ILOpcode)PeekILByte();
        }

        private bool TryReadILByte(out byte ilbyte)
        {
            if (_currentOffset >= _ilBytes.Length)
                throw new BadImageFormatException();

            ilbyte = ReadILByte();
            return true;
        }

        private UInt16 ReadILUInt16()
        {
            UInt16 val = (UInt16)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8));
            _currentOffset += 2;
            return val;
        }

        private bool TryReadILUInt16(out UInt16 ilUint16)
        {
            if (checked(_currentOffset + 1) >= _ilBytes.Length)
                throw new BadImageFormatException();

            ilUint16 = ReadILUInt16();
            return true;
        }

        private UInt32 ReadILUInt32()
        {
            UInt32 val = (UInt32)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8) + (_ilBytes[_currentOffset + 2] << 16) + (_ilBytes[_currentOffset + 3] << 24));
            _currentOffset += 4;
            return val;
        }

        private bool TryReadILUInt32(out UInt32 ilUint32)
        {
            if (checked(_currentOffset + 3) >= _ilBytes.Length)
                throw new BadImageFormatException();

            ilUint32 = ReadILUInt32();
            return true;
        }

        private int ReadILToken()
        {
            return (int)ReadILUInt32();
        }

        private bool TryReadILToken(out int ilToken)
        {
            uint ilTokenUint;
            bool result = TryReadILUInt32(out ilTokenUint);
            ilToken = (int)ilTokenUint;
            return result;
        }

        private ulong ReadILUInt64()
        {
            ulong value = ReadILUInt32();
            value |= (((ulong)ReadILUInt32()) << 32);
            return value;
        }

        private unsafe float ReadILFloat()
        {
            uint value = ReadILUInt32();
            return *(float*)(&value);
        }

        private unsafe double ReadILDouble()
        {
            ulong value = ReadILUInt64();
            return *(double*)(&value);
        }

        private void SkipIL(int bytes)
        {
            _currentOffset += bytes;
        }

        public bool HasNextInstruction
        {
            get
            {
                return _currentOffset < _ilBytes.Length;
            }
        }

        public int CodeSize
        {
            get
            {
                return _ilBytes.Length;
            }
        }

        public bool TryReadLdtoken(out int token)
        {
            if (PeekILOpcode() != ILOpcode.ldtoken)
            {
                token = 0;
                return false;
            }

            ReadILOpcode();
            token = ReadILToken();
            return true;
        }

        public int ReadLdtoken()
        {
            int result;
            if (!TryReadLdtoken(out result))
                throw new BadImageFormatException();

            return result;
        }

        public bool TryReadLdtokenAsTypeSystemEntity(out TypeSystemEntity entity)
        {
            int token;
            bool tokenResolved;
            try
            {
                tokenResolved = TryReadLdtoken(out token);
                entity = tokenResolved ? (TypeSystemEntity)_methodIL.GetObject(token) : null;
            }
            catch (TypeSystemException)
            {
                tokenResolved = false;
                entity = null;
            }

            return tokenResolved;
        }

        public TypeSystemEntity ReadLdtokenAsTypeSystemEntity()
        {
            TypeSystemEntity result;
            if (!TryReadLdtokenAsTypeSystemEntity(out result))
                throw new BadImageFormatException();

            return result;
        }

        public bool TryReadLdcI4(out int value)
        {
            ILOpcode opcode = PeekILOpcode();

            if (opcode == ILOpcode.ldc_i4) // ldc.i4
            {
                ReadILOpcode();
                value = unchecked((int)ReadILUInt32());
                return true;
            }

            if ((opcode >= ILOpcode.ldc_i4_m1) && (opcode <= ILOpcode.ldc_i4_8)) // ldc.m1 to ldc.i4.8
            {
                ReadILOpcode();
                value = -1 + ((int)opcode) - 0x15;
                return true;
            }

            if (opcode == ILOpcode.ldc_i4_s) // ldc.i4.s
            {
                ReadILOpcode();

                value = (int)unchecked((sbyte)ReadILByte());
                return true;
            }
            value = 0;
            return false;
        }

        public int ReadLdcI4()
        {
            int result;
            if (!TryReadLdcI4(out result))
                throw new BadImageFormatException();

            return result;
        }

        public bool TryReadRet()
        {
            ILOpcode opcode = PeekILOpcode();
            if (opcode == ILOpcode.ret)
            {
                ReadILOpcode();
                return true;
            }
            return false;
        }

        public void ReadRet()
        {
            if (!TryReadRet())
                throw new BadImageFormatException();
        }

        public bool TryReadPop()
        {
            ILOpcode opcode = PeekILOpcode();
            if (opcode == ILOpcode.pop)
            {
                ReadILOpcode();
                return true;
            }
            return false;
        }

        public void ReadPop()
        {
            if (!TryReadPop())
                throw new BadImageFormatException();
        }

        public bool TryReadLdstr(out string ldstrString)
        {
            if (PeekILOpcode() != ILOpcode.ldstr)
            {
                ldstrString = null;
                return false;
            }

            ReadILOpcode();
            int token = ReadILToken();
            ldstrString = (string)_methodIL.GetObject(token);
            return true;
        }

        public string ReadLdstr()
        {
            string result;
            if (!TryReadLdstr(out result))
                throw new BadImageFormatException();

            return result;
        }
    }
}
