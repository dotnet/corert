// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Internal.TypeSystem;

using Internal.IL;
using Internal.Runtime;

namespace ILCompiler
{
    public enum JitHelperId
    {
        //
        // Keep in sync with https://github.com/dotnet/coreclr/blob/master/src/inc/readytorun.h
        //

        // Exception handling helpers
        Throw = 0x20,
        Rethrow                     = 0x21,
        Overflow                    = 0x22,
        RngChkFail                  = 0x23,
        FailFast                    = 0x24,
        ThrowNullRef                = 0x25,
        ThrowDivZero                = 0x26,

        // Write barriers
        WriteBarrier                = 0x30,
        CheckedWriteBarrier         = 0x31,
        ByRefWriteBarrier           = 0x32,

        // Array helpers
        Stelem_Ref                  = 0x38,
        Ldelema_Ref                 = 0x39,

        MemSet                      = 0x40,
        MemCpy                      = 0x41,

        // Get string handle lazily
        GetString                   = 0x50,

        // Reflection helpers
        GetRuntimeTypeHandle        = 0x54,
        GetRuntimeMethodHandle      = 0x55,
        GetRuntimeFieldHandle       = 0x56,

        Box                         = 0x58,
        Box_Nullable                = 0x59,
        Unbox                       = 0x5A,
        Unbox_Nullable              = 0x5B,
        NewMultiDimArr              = 0x5C,

        // Long mul/div/shift ops
        LMul                        = 0xC0,
        LMulOfv                     = 0xC1,
        ULMulOvf                    = 0xC2,
        LDiv                        = 0xC3,
        LMod                        = 0xC4,
        ULDiv                       = 0xC5,
        ULMod                       = 0xC6,
        LLsh                        = 0xC7,
        LRsh                        = 0xC8,
        LRsz                        = 0xC9,
        Lng2Dbl                     = 0xCA,
        ULng2Dbl                    = 0xCB,

        // 32-bit division helpers
        Div                         = 0xCC,
        Mod                         = 0xCD,
        UDiv                        = 0xCE,
        UMod                        = 0xCF,

        // Floating point conversions
        Dbl2Int                     = 0xD0,
        Dbl2IntOvf                  = 0xD1,
        Dbl2Lng                     = 0xD2,
        Dbl2LngOvf                  = 0xD3,
        Dbl2UInt                    = 0xD4,
        Dbl2UIntOvf                 = 0xD5,
        Dbl2ULng                    = 0xD6,
        Dbl2ULngOvf                 = 0xD7,

        // Floating point ops
        DblRem                      = 0xE0,
        FltRem                      = 0xE1,
        DblRound                    = 0xE2,
        FltRound                    = 0xE3,
    }

    internal class JitHelper
    {
        /// <summary>
        /// Returns JIT helper entrypoint. JIT helpers can be either implemented by entrypoint with given mangled name or 
        /// by a method in class library.
        /// </summary>
        static public void GetEntryPoint(TypeSystemContext context, JitHelperId id, out string mangledName, out MethodDesc methodDesc)
        {
            mangledName = null;
            methodDesc = null;

            switch (id)
            {
                case JitHelperId.Throw:
                    mangledName = "RhpThrowEx";
                    break;
                case JitHelperId.Rethrow:
                    mangledName = "RhRethrow";
                    break;

                case JitHelperId.Overflow:
                    methodDesc = context.GetHelperEntryPoint("ThrowHelpers", "ThrowOverflowException");
                    break;
                case JitHelperId.RngChkFail:
                    methodDesc = context.GetHelperEntryPoint("ThrowHelpers", "ThrowIndexOutOfRangeException");
                    break;
                case JitHelperId.FailFast:
                    mangledName = "__fail_fast"; // TODO: Report stack buffer overrun
                    break;
                case JitHelperId.ThrowNullRef:
                    methodDesc = context.GetHelperEntryPoint("ThrowHelpers", "ThrowNullReferenceException");
                    break;
                case JitHelperId.ThrowDivZero:
                    methodDesc = context.GetHelperEntryPoint("ThrowHelpers", "ThrowDivideByZeroException");
                    break;

                case JitHelperId.WriteBarrier:
                    mangledName = "RhpAssignRef";
                    break;
                case JitHelperId.CheckedWriteBarrier:
                    mangledName = "RhpCheckedAssignRef";
                    break;
                case JitHelperId.ByRefWriteBarrier:
                    mangledName = "RhpByRefAssignRef";
                    break;

                case JitHelperId.Box:
                case JitHelperId.Box_Nullable:
                    mangledName = "RhBox";
                    break;
                case JitHelperId.Unbox:
                    mangledName = "RhUnbox2";
                    break;
                case JitHelperId.Unbox_Nullable:
                    mangledName = "RhUnboxNullable";
                    break;

                case JitHelperId.NewMultiDimArr:
                    mangledName = "RhNewMDArray";
                    break;

                case JitHelperId.Stelem_Ref:
                    mangledName = "RhpStelemRef";
                    break;
                case JitHelperId.Ldelema_Ref:
                    mangledName = "RhpLdelemaRef";
                    break;

                case JitHelperId.MemCpy:
                    mangledName = "memcpy"; // TODO: Null reference handling
                    break;
                case JitHelperId.MemSet:
                    mangledName = "memset"; // TODO: Null reference handling
                    break;

                case JitHelperId.GetRuntimeTypeHandle:
                    methodDesc = context.GetHelperEntryPoint("LdTokenHelpers", "GetRuntimeTypeHandle");
                    break;
                case JitHelperId.GetRuntimeMethodHandle: // TODO: Reflection
                case JitHelperId.GetRuntimeFieldHandle: // TODO: Reflection
                    mangledName = "__fail_fast";
                    break;

                case JitHelperId.Lng2Dbl:
                    mangledName = "RhpLng2Dbl";
                    break;
                case JitHelperId.ULng2Dbl:
                    mangledName = "RhpULng2Dbl";
                    break;

                case JitHelperId.Dbl2Lng:
                    mangledName = "RhpDbl2Lng";
                    break;
                case JitHelperId.Dbl2ULng:
                    mangledName = "RhpDbl2ULng";
                    break;

                case JitHelperId.Dbl2IntOvf:
                    methodDesc = context.GetHelperEntryPoint("MathHelpers", "Dbl2IntOvf");
                    break;
                case JitHelperId.Dbl2LngOvf:
                    methodDesc = context.GetHelperEntryPoint("MathHelpers", "Dbl2LngOvf");
                    break;
                case JitHelperId.Dbl2ULngOvf:
                    methodDesc = context.GetHelperEntryPoint("MathHelpers", "Dbl2ULngOvf");
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
            if (type.IsSzArray)
                return throwing ? "RhTypeCast_CheckCastArray" : "RhTypeCast_IsInstanceOfArray";

            if (type.IsInterface)
                return throwing ? "RhTypeCast_CheckCastInterface" : "RhTypeCast_IsInstanceOfInterface";

            return throwing ? "RhTypeCast_CheckCastClass" : "RhTypeCast_IsInstanceOfClass";
        }
    }
}
