// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.IL
{
    public static class ILDisassember
    {
        private static byte ReadILByte(byte[] _ilBytes, ref int _currentOffset)
        {
            return _ilBytes[_currentOffset++];
        }

        private static UInt16 ReadILUInt16(byte[] _ilBytes, ref int _currentOffset)
        {
            UInt16 val = (UInt16)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8));
            _currentOffset += 2;
            return val;
        }

        private static UInt32 ReadILUInt32(byte[] _ilBytes, ref int _currentOffset)
        {
            UInt32 val = (UInt32)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8) + (_ilBytes[_currentOffset + 2] << 16) + (_ilBytes[_currentOffset + 3] << 24));
            _currentOffset += 4;
            return val;
        }

        private static int ReadILToken(byte[] _ilBytes, ref int _currentOffset)
        {
            return (int)ReadILUInt32(_ilBytes, ref _currentOffset);
        }

        private static ulong ReadILUInt64(byte[] _ilBytes, ref int _currentOffset)
        {
            ulong value = ReadILUInt32(_ilBytes, ref _currentOffset);
            value |= (((ulong)ReadILUInt32(_ilBytes, ref _currentOffset)) << 32);
            return value;
        }

        private static unsafe float ReadILFloat(byte[] _ilBytes, ref int _currentOffset)
        {
            uint value = ReadILUInt32(_ilBytes, ref _currentOffset);
            return *(float*)(&value);
        }

        private static unsafe double ReadILDouble(byte[] _ilBytes, ref int _currentOffset)
        {
            ulong value = ReadILUInt64(_ilBytes, ref _currentOffset);
            return *(double*)(&value);
        }

        public static string FormatOffset(int offset)
        {
            return "IL_" + offset.ToString("X4");
        }

        public static string Disassemble(Func<int, string> tokenResolver, byte[] instructionStream, ref int offset)
        {
            string opCodeName = "";

        again:

            ILOpcode opCode = (ILOpcode)ReadILByte(instructionStream, ref offset);
            if (opCode == ILOpcode.prefix1)
            {
                opCode = (ILOpcode)(0x100 + ReadILByte(instructionStream, ref offset));
            }

            opCodeName += opCode.ToString();
            opCodeName = opCodeName.Replace("_", ".");

            switch (opCode)
            {
                case ILOpcode.ldarg_s:
                case ILOpcode.ldarga_s:
                case ILOpcode.starg_s:
                case ILOpcode.ldloc_s:
                case ILOpcode.ldloca_s:
                case ILOpcode.stloc_s:
                case ILOpcode.ldc_i4_s:
                    return opCodeName + " " + ReadILByte(instructionStream, ref offset);

                case ILOpcode.unaligned:
                    opCodeName += " " + ReadILByte(instructionStream, ref offset) + " ";
                    goto again;

                case ILOpcode.ldarg:
                case ILOpcode.ldarga:
                case ILOpcode.starg:
                case ILOpcode.ldloc:
                case ILOpcode.ldloca:
                case ILOpcode.stloc:
                    return opCodeName + " " + ReadILUInt16(instructionStream, ref offset);

                case ILOpcode.ldc_i4:
                    return opCodeName + " " + ReadILUInt32(instructionStream, ref offset);

                case ILOpcode.ldc_r4:
                    return opCodeName + " " + ReadILFloat(instructionStream, ref offset);

                case ILOpcode.ldc_i8:
                    return opCodeName + " " + ReadILUInt64(instructionStream, ref offset);

                case ILOpcode.ldc_r8:
                    return opCodeName + " " + ReadILDouble(instructionStream, ref offset);

                case ILOpcode.jmp:
                case ILOpcode.call:
                case ILOpcode.calli:
                case ILOpcode.callvirt:
                case ILOpcode.cpobj:
                case ILOpcode.ldobj:
                case ILOpcode.ldstr:
                case ILOpcode.newobj:
                case ILOpcode.castclass:
                case ILOpcode.isinst:
                case ILOpcode.unbox:
                case ILOpcode.ldfld:
                case ILOpcode.ldflda:
                case ILOpcode.stfld:
                case ILOpcode.ldsfld:
                case ILOpcode.ldsflda:
                case ILOpcode.stsfld:
                case ILOpcode.stobj:
                case ILOpcode.box:
                case ILOpcode.newarr:
                case ILOpcode.ldelema:
                case ILOpcode.ldelem:
                case ILOpcode.stelem:
                case ILOpcode.unbox_any:
                case ILOpcode.refanyval:
                case ILOpcode.mkrefany:
                case ILOpcode.ldtoken:
                case ILOpcode.ldftn:
                case ILOpcode.ldvirtftn:
                case ILOpcode.initobj:
                case ILOpcode.constrained:
                case ILOpcode.sizeof_:
                    return opCodeName + " " + tokenResolver(ReadILToken(instructionStream, ref offset));

                case ILOpcode.br_s:
                case ILOpcode.leave_s:
                case ILOpcode.brfalse_s:
                case ILOpcode.brtrue_s:
                case ILOpcode.beq_s:
                case ILOpcode.bge_s:
                case ILOpcode.bgt_s:
                case ILOpcode.ble_s:
                case ILOpcode.blt_s:
                case ILOpcode.bne_un_s:
                case ILOpcode.bge_un_s:
                case ILOpcode.bgt_un_s:
                case ILOpcode.ble_un_s:
                case ILOpcode.blt_un_s:
                    return opCodeName + " " + FormatOffset((sbyte)ReadILByte(instructionStream, ref offset) + offset);

                case ILOpcode.br:
                case ILOpcode.leave:
                case ILOpcode.brfalse:
                case ILOpcode.brtrue:
                case ILOpcode.beq:
                case ILOpcode.bge:
                case ILOpcode.bgt:
                case ILOpcode.ble:
                case ILOpcode.blt:
                case ILOpcode.bne_un:
                case ILOpcode.bge_un:
                case ILOpcode.bgt_un:
                case ILOpcode.ble_un:
                case ILOpcode.blt_un:
                    return opCodeName + " " + FormatOffset((int)ReadILUInt32(instructionStream, ref offset) + offset);

                case ILOpcode.switch_:
                    {
                        opCodeName = "switch (";
                        uint count = ReadILUInt32(instructionStream, ref offset);
                        int jmpBase = offset + (int)(4 * count);
                        for (uint i = 0; i < count; i++)
                        {
                            if (i != 0)
                                opCodeName += ", ";
                            int delta = (int)ReadILUInt32(instructionStream, ref offset);
                            opCodeName += FormatOffset(jmpBase + delta);
                        }
                        opCodeName += ")";
                        return opCodeName;
                    }

                default:
                    return opCodeName;
            }
        }
    }
}
