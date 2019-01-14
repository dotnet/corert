// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;

namespace Internal.IL
{
    public struct ILReader
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

        private byte ReadILByte()
        {
            return _ilBytes[_currentOffset++];
        }

        private UInt16 ReadILUInt16()
        {
            UInt16 val = (UInt16)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8));
            _currentOffset += 2;
            return val;
        }

        private UInt32 ReadILUInt32()
        {
            UInt32 val = (UInt32)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8) + (_ilBytes[_currentOffset + 2] << 16) + (_ilBytes[_currentOffset + 3] << 24));
            _currentOffset += 4;
            return val;
        }

        private int ReadILToken()
        {
            return (int)ReadILUInt32();
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

        public bool Read(out ILInstruction instruction)
        {
            instruction = default(ILInstruction);
            if (_currentOffset == _ilBytes.Length)
                return false;

again:

            ILOpcode opcode = (ILOpcode)ReadILByte();
            if (opcode == ILOpcode.prefix1)
            {
                opcode = (ILOpcode)(0x100 + ReadILByte());
            }

            switch (opcode)
            {
                case ILOpcode.nop:
                case ILOpcode.ldarg_0:
                case ILOpcode.ldarg_1:
                case ILOpcode.ldarg_2:
                case ILOpcode.ldarg_3:
                case ILOpcode.ldloc_0:
                case ILOpcode.ldloc_1:
                case ILOpcode.ldloc_2:
                case ILOpcode.ldloc_3:
                case ILOpcode.stloc_0:
                case ILOpcode.stloc_1:
                case ILOpcode.stloc_2:
                case ILOpcode.stloc_3:
                case ILOpcode.ldnull:
                case ILOpcode.ldc_i4_m1:
                case ILOpcode.ldc_i4_0:
                case ILOpcode.ldc_i4_1:
                case ILOpcode.ldc_i4_2:
                case ILOpcode.ldc_i4_3:
                case ILOpcode.ldc_i4_4:
                case ILOpcode.ldc_i4_5:
                case ILOpcode.ldc_i4_6:
                case ILOpcode.ldc_i4_7:
                case ILOpcode.ldc_i4_8:
                case ILOpcode.dup:
                case ILOpcode.pop:
                case ILOpcode.ret:
                case ILOpcode.add:
                case ILOpcode.sub:
                case ILOpcode.mul:
                case ILOpcode.div:
                case ILOpcode.div_un:
                case ILOpcode.rem:
                case ILOpcode.rem_un:
                case ILOpcode.and:
                case ILOpcode.or:
                case ILOpcode.xor:
                case ILOpcode.shl:
                case ILOpcode.shr:
                case ILOpcode.shr_un:
                case ILOpcode.neg:
                case ILOpcode.not:
                case ILOpcode.conv_i1:
                case ILOpcode.conv_i2:
                case ILOpcode.conv_i4:
                case ILOpcode.conv_i8:
                case ILOpcode.conv_r4:
                case ILOpcode.conv_r8:
                case ILOpcode.conv_u:
                case ILOpcode.conv_u4:
                case ILOpcode.conv_u8:
                case ILOpcode.conv_ovf_i1_un:
                case ILOpcode.conv_ovf_i2_un:
                case ILOpcode.conv_ovf_i4_un:
                case ILOpcode.conv_ovf_i8_un:
                case ILOpcode.conv_ovf_u1_un:
                case ILOpcode.conv_ovf_u2_un:
                case ILOpcode.conv_ovf_u4_un:
                case ILOpcode.conv_ovf_u8_un:
                case ILOpcode.conv_ovf_i_un:
                case ILOpcode.conv_ovf_u_un:
                case ILOpcode.conv_r_un:
                case ILOpcode.conv_ovf_i1:
                case ILOpcode.conv_ovf_u1:
                case ILOpcode.conv_ovf_i2:
                case ILOpcode.conv_ovf_u2:
                case ILOpcode.conv_ovf_i4:
                case ILOpcode.conv_ovf_u4:
                case ILOpcode.conv_ovf_i8:
                case ILOpcode.conv_ovf_u8:
                case ILOpcode.conv_u2:
                case ILOpcode.conv_u1:
                case ILOpcode.conv_i:
                case ILOpcode.conv_ovf_i:
                case ILOpcode.conv_ovf_u:
                case ILOpcode.add_ovf:
                case ILOpcode.add_ovf_un:
                case ILOpcode.mul_ovf:
                case ILOpcode.mul_ovf_un:
                case ILOpcode.sub_ovf:
                case ILOpcode.sub_ovf_un:
                case ILOpcode.ceq:
                case ILOpcode.cgt:
                case ILOpcode.cgt_un:
                case ILOpcode.clt:
                case ILOpcode.clt_un:
                case ILOpcode.ldlen:
                case ILOpcode.ldelem_i1:
                case ILOpcode.ldelem_u1:
                case ILOpcode.ldelem_i2:
                case ILOpcode.ldelem_u2:
                case ILOpcode.ldelem_i4:
                case ILOpcode.ldelem_u4:
                case ILOpcode.ldelem_i8:
                case ILOpcode.ldelem_i:
                case ILOpcode.ldelem_r4:
                case ILOpcode.ldelem_r8:
                case ILOpcode.ldelem_ref:
                case ILOpcode.stelem_i:
                case ILOpcode.stelem_i1:
                case ILOpcode.stelem_i2:
                case ILOpcode.stelem_i4:
                case ILOpcode.stelem_i8:
                case ILOpcode.stelem_r4:
                case ILOpcode.stelem_r8:
                case ILOpcode.stelem_ref:
                case ILOpcode.ldind_i1:
                case ILOpcode.ldind_u1:
                case ILOpcode.ldind_i2:
                case ILOpcode.ldind_u2:
                case ILOpcode.ldind_i4:
                case ILOpcode.ldind_u4:
                case ILOpcode.ldind_i8:
                case ILOpcode.ldind_i:
                case ILOpcode.ldind_r4:
                case ILOpcode.ldind_r8:
                case ILOpcode.ldind_ref:
                case ILOpcode.stind_ref:
                case ILOpcode.stind_i:
                case ILOpcode.stind_i1:
                case ILOpcode.stind_i2:
                case ILOpcode.stind_i4:
                case ILOpcode.stind_i8:
                case ILOpcode.stind_r4:
                case ILOpcode.stind_r8:
                case ILOpcode.localloc:
                case ILOpcode.throw_:
                case ILOpcode.ckfinite:
                case ILOpcode.endfinally:
                case ILOpcode.endfilter:
                case ILOpcode.arglist:
                case ILOpcode.volatile_:
                case ILOpcode.tail:
                case ILOpcode.cpblk:
                case ILOpcode.initblk:
                case ILOpcode.rethrow:
                case ILOpcode.refanytype:
                case ILOpcode.readonly_:
                    instruction = new ILInstruction(opcode);
                    break;
                case ILOpcode.ldarg_s:
                case ILOpcode.ldarga_s:
                case ILOpcode.starg_s:
                case ILOpcode.ldloc_s:
                case ILOpcode.ldloca_s:
                case ILOpcode.stloc_s:
                case ILOpcode.no:
                    instruction = new ILInstruction(opcode, ILOperand.FromInt32(ReadILByte()));
                    break;
                case ILOpcode.ldc_i4_s:
                    instruction = new ILInstruction(opcode, ILOperand.FromInt32((sbyte)ReadILByte()));
                    break;
                case ILOpcode.ldc_i4:
                case ILOpcode.switch_:
                    instruction = new ILInstruction(opcode, ILOperand.FromInt32((int)ReadILUInt32()));
                    break;
                case ILOpcode.ldc_i8:
                    instruction = new ILInstruction(opcode, ILOperand.FromInt64((long)ReadILUInt64()));
                    break;
                case ILOpcode.ldc_r4:
                    instruction = new ILInstruction(opcode, ILOperand.FromDouble(ReadILFloat()));
                    break;
                case ILOpcode.ldc_r8:
                    instruction = new ILInstruction(opcode, ILOperand.FromDouble(ReadILDouble()));
                    break;
                case ILOpcode.br_s:
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
                case ILOpcode.leave_s:
                    {
                        int delta = (sbyte)ReadILByte();
                        instruction = new ILInstruction(opcode, ILOperand.FromInt32(_currentOffset + delta));
                    }
                    break;
                case ILOpcode.br:
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
                case ILOpcode.leave:
                    {
                        int delta = (int)ReadILUInt32();
                        instruction = new ILInstruction(opcode, ILOperand.FromInt32(_currentOffset + delta));
                    }
                    break;
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
                case ILOpcode.ldftn:
                case ILOpcode.ldvirtftn:
                case ILOpcode.mkrefany:
                case ILOpcode.ldtoken:
                case ILOpcode.initobj:
                case ILOpcode.constrained:
                case ILOpcode.sizeof_:
                    instruction = new ILInstruction(opcode, ILOperand.FromInt32(ReadILToken()));
                    break;
                case ILOpcode.ldarg:
                case ILOpcode.ldarga:
                case ILOpcode.starg:
                case ILOpcode.ldloc:
                case ILOpcode.ldloca:
                case ILOpcode.stloc:
                    instruction = new ILInstruction(opcode, ILOperand.FromInt32(ReadILUInt16()));
                    break;
                case ILOpcode.unaligned:
                    ReadILByte();
                    goto again;
                default:
                    break;
            }

            return true;
        }

        public void Seek(int offset)
        {
            _currentOffset = offset;
        }
    }
}
