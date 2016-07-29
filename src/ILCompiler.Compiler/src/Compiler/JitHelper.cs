// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.IL;
using Internal.Runtime;

namespace ILCompiler
{
    internal class JitHelper
    {
        /// <summary>
        /// Returns JIT helper entrypoint. JIT helpers can be either implemented by entrypoint with given mangled name or 
        /// by a method in class library.
        /// </summary>
        static public void GetEntryPoint(TypeSystemContext context, ReadyToRunHelper id, out string mangledName, out MethodDesc methodDesc)
        {
            mangledName = null;
            methodDesc = null;

            switch (id)
            {
                case ReadyToRunHelper.Throw:
                    mangledName = "RhpThrowEx";
                    break;
                case ReadyToRunHelper.Rethrow:
                    mangledName = "RhpRethrow";
                    break;

                case ReadyToRunHelper.Overflow:
                    methodDesc = context.GetHelperEntryPoint("ThrowHelpers", "ThrowOverflowException");
                    break;
                case ReadyToRunHelper.RngChkFail:
                    methodDesc = context.GetHelperEntryPoint("ThrowHelpers", "ThrowIndexOutOfRangeException");
                    break;
                case ReadyToRunHelper.FailFast:
                    mangledName = "__fail_fast"; // TODO: Report stack buffer overrun
                    break;
                case ReadyToRunHelper.ThrowNullRef:
                    methodDesc = context.GetHelperEntryPoint("ThrowHelpers", "ThrowNullReferenceException");
                    break;
                case ReadyToRunHelper.ThrowDivZero:
                    methodDesc = context.GetHelperEntryPoint("ThrowHelpers", "ThrowDivideByZeroException");
                    break;

                case ReadyToRunHelper.DebugBreak:
                    mangledName = "RhDebugBreak";
                    break;

                case ReadyToRunHelper.WriteBarrier:
                    mangledName = "RhpAssignRef";
                    break;
                case ReadyToRunHelper.CheckedWriteBarrier:
                    mangledName = "RhpCheckedAssignRef";
                    break;
                case ReadyToRunHelper.ByRefWriteBarrier:
                    mangledName = "RhpByRefAssignRef";
                    break;

                case ReadyToRunHelper.Box:
                case ReadyToRunHelper.Box_Nullable:
                    mangledName = "RhBox";
                    break;
                case ReadyToRunHelper.Unbox:
                    mangledName = "RhUnbox2";
                    break;
                case ReadyToRunHelper.Unbox_Nullable:
                    mangledName = "RhUnboxNullable";
                    break;

                case ReadyToRunHelper.NewMultiDimArr_NonVarArg:
                    methodDesc = context.GetHelperEntryPoint("ArrayHelpers", "NewObjArray");
                    break;

                case ReadyToRunHelper.Stelem_Ref:
                    mangledName = "RhpStelemRef";
                    break;
                case ReadyToRunHelper.Ldelema_Ref:
                    mangledName = "RhpLdelemaRef";
                    break;

                case ReadyToRunHelper.MemCpy:
                    mangledName = "memcpy"; // TODO: Null reference handling
                    break;
                case ReadyToRunHelper.MemSet:
                    mangledName = "memset"; // TODO: Null reference handling
                    break;

                case ReadyToRunHelper.GetRuntimeTypeHandle:
                    methodDesc = context.GetHelperEntryPoint("LdTokenHelpers", "GetRuntimeTypeHandle");
                    break;
                case ReadyToRunHelper.GetRuntimeMethodHandle: // TODO: Reflection
                case ReadyToRunHelper.GetRuntimeFieldHandle: // TODO: Reflection
                    mangledName = "__fail_fast";
                    break;

                case ReadyToRunHelper.Lng2Dbl:
                    mangledName = "RhpLng2Dbl";
                    break;
                case ReadyToRunHelper.ULng2Dbl:
                    mangledName = "RhpULng2Dbl";
                    break;

                case ReadyToRunHelper.Dbl2Lng:
                    mangledName = "RhpDbl2Lng";
                    break;
                case ReadyToRunHelper.Dbl2ULng:
                    mangledName = "RhpDbl2ULng";
                    break;

                case ReadyToRunHelper.Dbl2IntOvf:
                    methodDesc = context.GetHelperEntryPoint("MathHelpers", "Dbl2IntOvf");
                    break;
                case ReadyToRunHelper.Dbl2LngOvf:
                    methodDesc = context.GetHelperEntryPoint("MathHelpers", "Dbl2LngOvf");
                    break;
                case ReadyToRunHelper.Dbl2ULngOvf:
                    methodDesc = context.GetHelperEntryPoint("MathHelpers", "Dbl2ULngOvf");
                    break;

                case ReadyToRunHelper.DblRem:
                    mangledName = "RhpDblRem";
                    break;
                case ReadyToRunHelper.FltRem:
                    mangledName = "RhpFltRem";
                    break;

                case ReadyToRunHelper.PInvokeBegin:
                    mangledName = "RhpPInvoke";
                    break;
                case ReadyToRunHelper.PInvokeEnd:
                    mangledName = "RhpPInvokeReturn";
                    break;

                case ReadyToRunHelper.ReversePInvokeEnter:
                    mangledName = "RhpReversePInvoke2";
                    break;
                case ReadyToRunHelper.ReversePInvokeExit:
                    mangledName = "RhpReversePInvokeReturn2";
                    break;

                default:
                    throw new NotImplementedException(id.ToString());
            }
        }

        //
        // These methods are static compiler equivalent of RhGetRuntimeHelperForType
        //
        static public string GetNewObjectHelperForType(TypeDesc type)
        {
            if (EETypeBuilderHelpers.ComputeRequiresAlign8(type))
            {
                if (type.HasFinalizer)
                    return "RhpNewFinalizableAlign8";

                if (type.IsValueType)
                    return "RhpNewFastMisalign";

                return "RhpNewFastAlign8";
            }

            if (type.HasFinalizer)
                return "RhpNewFinalizable";

            return "RhpNewFast";
        }

        static public string GetNewArrayHelperForType(TypeDesc type)
        {
            if (EETypeBuilderHelpers.ComputeRequiresAlign8(type))
                return "RhpNewArrayAlign8";

            return "RhpNewArray";
        }

        static public string GetCastingHelperNameForType(TypeDesc type, bool throwing)
        {
            if (type.IsArray)
                return throwing ? "RhTypeCast_CheckCastArray" : "RhTypeCast_IsInstanceOfArray";

            if (type.IsInterface)
                return throwing ? "RhTypeCast_CheckCastInterface" : "RhTypeCast_IsInstanceOfInterface";

            return throwing ? "RhTypeCast_CheckCastClass" : "RhTypeCast_IsInstanceOfClass";
        }
    }
}
