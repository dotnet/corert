using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Internal.IL;
using Internal.Runtime.CallInterceptor;

namespace Internal.Runtime.Interpreter
{
    internal static class ILEvaluation
    {
        public static void EvaluateInstruction(ILInstruction instruction, LowLevelStack<object> stack, ref CallInterceptorArgs callInterceptorArgs)
        {
            switch (instruction.OpCode)
            {
                case ILOpcode.nop:
                    break;
                case ILOpcode.break_:
                    break;
                case ILOpcode.ldarg_0:
                    break;
                case ILOpcode.ldarg_1:
                    break;
                case ILOpcode.ldarg_2:
                    break;
                case ILOpcode.ldarg_3:
                    break;
                case ILOpcode.ldloc_0:
                    break;
                case ILOpcode.ldloc_1:
                    break;
                case ILOpcode.ldloc_2:
                    break;
                case ILOpcode.ldloc_3:
                    break;
                case ILOpcode.stloc_0:
                    break;
                case ILOpcode.stloc_1:
                    break;
                case ILOpcode.stloc_2:
                    break;
                case ILOpcode.stloc_3:
                    break;
                case ILOpcode.ldarg_s:
                    break;
                case ILOpcode.ldarga_s:
                    break;
                case ILOpcode.starg_s:
                    break;
                case ILOpcode.ldloc_s:
                    break;
                case ILOpcode.ldloca_s:
                    break;
                case ILOpcode.stloc_s:
                    break;
                case ILOpcode.ldnull:
                    break;
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
                case ILOpcode.ldc_i4_s:
                case ILOpcode.ldc_i4:
                case ILOpcode.ldc_i8:
                case ILOpcode.ldc_r4:
                case ILOpcode.ldc_r8:
                    EvaluateLdC(stack, instruction.Operand);
                    break;
                case ILOpcode.dup:
                    break;
                case ILOpcode.pop:
                    break;
                case ILOpcode.jmp:
                    break;
                case ILOpcode.call:
                    break;
                case ILOpcode.calli:
                    break;
                case ILOpcode.ret:
                    EvaluateRet(stack, ref callInterceptorArgs);
                    break;
                case ILOpcode.br_s:
                    break;
                case ILOpcode.brfalse_s:
                    break;
                case ILOpcode.brtrue_s:
                    break;
                case ILOpcode.beq_s:
                    break;
                case ILOpcode.bge_s:
                    break;
                case ILOpcode.bgt_s:
                    break;
                case ILOpcode.ble_s:
                    break;
                case ILOpcode.blt_s:
                    break;
                case ILOpcode.bne_un_s:
                    break;
                case ILOpcode.bge_un_s:
                    break;
                case ILOpcode.bgt_un_s:
                    break;
                case ILOpcode.ble_un_s:
                    break;
                case ILOpcode.blt_un_s:
                    break;
                case ILOpcode.br:
                    break;
                case ILOpcode.brfalse:
                    break;
                case ILOpcode.brtrue:
                    break;
                case ILOpcode.beq:
                    break;
                case ILOpcode.bge:
                    break;
                case ILOpcode.bgt:
                    break;
                case ILOpcode.ble:
                    break;
                case ILOpcode.blt:
                    break;
                case ILOpcode.bne_un:
                    break;
                case ILOpcode.bge_un:
                    break;
                case ILOpcode.bgt_un:
                    break;
                case ILOpcode.ble_un:
                    break;
                case ILOpcode.blt_un:
                    break;
                case ILOpcode.switch_:
                    break;
                case ILOpcode.ldind_i1:
                    break;
                case ILOpcode.ldind_u1:
                    break;
                case ILOpcode.ldind_i2:
                    break;
                case ILOpcode.ldind_u2:
                    break;
                case ILOpcode.ldind_i4:
                    break;
                case ILOpcode.ldind_u4:
                    break;
                case ILOpcode.ldind_i8:
                    break;
                case ILOpcode.ldind_i:
                    break;
                case ILOpcode.ldind_r4:
                    break;
                case ILOpcode.ldind_r8:
                    break;
                case ILOpcode.ldind_ref:
                    break;
                case ILOpcode.stind_ref:
                    break;
                case ILOpcode.stind_i1:
                    break;
                case ILOpcode.stind_i2:
                    break;
                case ILOpcode.stind_i4:
                    break;
                case ILOpcode.stind_i8:
                    break;
                case ILOpcode.stind_r4:
                    break;
                case ILOpcode.stind_r8:
                    break;
                case ILOpcode.add:
                    break;
                case ILOpcode.sub:
                    break;
                case ILOpcode.mul:
                    break;
                case ILOpcode.div:
                    break;
                case ILOpcode.div_un:
                    break;
                case ILOpcode.rem:
                    break;
                case ILOpcode.rem_un:
                    break;
                case ILOpcode.and:
                    break;
                case ILOpcode.or:
                    break;
                case ILOpcode.xor:
                    break;
                case ILOpcode.shl:
                    break;
                case ILOpcode.shr:
                    break;
                case ILOpcode.shr_un:
                    break;
                case ILOpcode.neg:
                    break;
                case ILOpcode.not:
                    break;
                case ILOpcode.conv_i1:
                    break;
                case ILOpcode.conv_i2:
                    break;
                case ILOpcode.conv_i4:
                    break;
                case ILOpcode.conv_i8:
                    break;
                case ILOpcode.conv_r4:
                    break;
                case ILOpcode.conv_r8:
                    break;
                case ILOpcode.conv_u4:
                    break;
                case ILOpcode.conv_u8:
                    break;
                case ILOpcode.callvirt:
                    break;
                case ILOpcode.cpobj:
                    break;
                case ILOpcode.ldobj:
                    break;
                case ILOpcode.ldstr:
                    break;
                case ILOpcode.newobj:
                    break;
                case ILOpcode.castclass:
                    break;
                case ILOpcode.isinst:
                    break;
                case ILOpcode.conv_r_un:
                    break;
                case ILOpcode.unbox:
                    break;
                case ILOpcode.throw_:
                    break;
                case ILOpcode.ldfld:
                    break;
                case ILOpcode.ldflda:
                    break;
                case ILOpcode.stfld:
                    break;
                case ILOpcode.ldsfld:
                    break;
                case ILOpcode.ldsflda:
                    break;
                case ILOpcode.stsfld:
                    break;
                case ILOpcode.stobj:
                    break;
                case ILOpcode.conv_ovf_i1_un:
                    break;
                case ILOpcode.conv_ovf_i2_un:
                    break;
                case ILOpcode.conv_ovf_i4_un:
                    break;
                case ILOpcode.conv_ovf_i8_un:
                    break;
                case ILOpcode.conv_ovf_u1_un:
                    break;
                case ILOpcode.conv_ovf_u2_un:
                    break;
                case ILOpcode.conv_ovf_u4_un:
                    break;
                case ILOpcode.conv_ovf_u8_un:
                    break;
                case ILOpcode.conv_ovf_i_un:
                    break;
                case ILOpcode.conv_ovf_u_un:
                    break;
                case ILOpcode.box:
                    break;
                case ILOpcode.newarr:
                    break;
                case ILOpcode.ldlen:
                    break;
                case ILOpcode.ldelema:
                    break;
                case ILOpcode.ldelem_i1:
                    break;
                case ILOpcode.ldelem_u1:
                    break;
                case ILOpcode.ldelem_i2:
                    break;
                case ILOpcode.ldelem_u2:
                    break;
                case ILOpcode.ldelem_i4:
                    break;
                case ILOpcode.ldelem_u4:
                    break;
                case ILOpcode.ldelem_i8:
                    break;
                case ILOpcode.ldelem_i:
                    break;
                case ILOpcode.ldelem_r4:
                    break;
                case ILOpcode.ldelem_r8:
                    break;
                case ILOpcode.ldelem_ref:
                    break;
                case ILOpcode.stelem_i:
                    break;
                case ILOpcode.stelem_i1:
                    break;
                case ILOpcode.stelem_i2:
                    break;
                case ILOpcode.stelem_i4:
                    break;
                case ILOpcode.stelem_i8:
                    break;
                case ILOpcode.stelem_r4:
                    break;
                case ILOpcode.stelem_r8:
                    break;
                case ILOpcode.stelem_ref:
                    break;
                case ILOpcode.ldelem:
                    break;
                case ILOpcode.stelem:
                    break;
                case ILOpcode.unbox_any:
                    break;
                case ILOpcode.conv_ovf_i1:
                    break;
                case ILOpcode.conv_ovf_u1:
                    break;
                case ILOpcode.conv_ovf_i2:
                    break;
                case ILOpcode.conv_ovf_u2:
                    break;
                case ILOpcode.conv_ovf_i4:
                    break;
                case ILOpcode.conv_ovf_u4:
                    break;
                case ILOpcode.conv_ovf_i8:
                    break;
                case ILOpcode.conv_ovf_u8:
                    break;
                case ILOpcode.refanyval:
                    break;
                case ILOpcode.ckfinite:
                    break;
                case ILOpcode.mkrefany:
                    break;
                case ILOpcode.ldtoken:
                    break;
                case ILOpcode.conv_u2:
                    break;
                case ILOpcode.conv_u1:
                    break;
                case ILOpcode.conv_i:
                    break;
                case ILOpcode.conv_ovf_i:
                    break;
                case ILOpcode.conv_ovf_u:
                    break;
                case ILOpcode.add_ovf:
                    break;
                case ILOpcode.add_ovf_un:
                    break;
                case ILOpcode.mul_ovf:
                    break;
                case ILOpcode.mul_ovf_un:
                    break;
                case ILOpcode.sub_ovf:
                    break;
                case ILOpcode.sub_ovf_un:
                    break;
                case ILOpcode.endfinally:
                    break;
                case ILOpcode.leave:
                    break;
                case ILOpcode.leave_s:
                    break;
                case ILOpcode.stind_i:
                    break;
                case ILOpcode.conv_u:
                    break;
                case ILOpcode.prefix1:
                    break;
                case ILOpcode.arglist:
                    break;
                case ILOpcode.ceq:
                    break;
                case ILOpcode.cgt:
                    break;
                case ILOpcode.cgt_un:
                    break;
                case ILOpcode.clt:
                    break;
                case ILOpcode.clt_un:
                    break;
                case ILOpcode.ldftn:
                    break;
                case ILOpcode.ldvirtftn:
                    break;
                case ILOpcode.ldarg:
                    break;
                case ILOpcode.ldarga:
                    break;
                case ILOpcode.starg:
                    break;
                case ILOpcode.ldloc:
                    break;
                case ILOpcode.ldloca:
                    break;
                case ILOpcode.stloc:
                    break;
                case ILOpcode.localloc:
                    break;
                case ILOpcode.endfilter:
                    break;
                case ILOpcode.unaligned:
                    break;
                case ILOpcode.volatile_:
                    break;
                case ILOpcode.tail:
                    break;
                case ILOpcode.initobj:
                    break;
                case ILOpcode.constrained:
                    break;
                case ILOpcode.cpblk:
                    break;
                case ILOpcode.initblk:
                    break;
                case ILOpcode.no:
                    break;
                case ILOpcode.rethrow:
                    break;
                case ILOpcode.sizeof_:
                    break;
                case ILOpcode.refanytype:
                    break;
                case ILOpcode.readonly_:
                    break;
                default:
                    break;
            }
        }

        private static void EvaluateLdC(LowLevelStack<object> stack, object constant)
        {
            stack.Push(constant);
        }

        private static void EvaluateRet(LowLevelStack<object> stack, ref CallInterceptorArgs callInterceptorArgs)
        {
            bool hasReturnValue = stack.TryPop(out object returnValue);
            if (hasReturnValue)
                callInterceptorArgs.ArgumentsAndReturnValue.SetVar(0, returnValue);
        }
    }
}
