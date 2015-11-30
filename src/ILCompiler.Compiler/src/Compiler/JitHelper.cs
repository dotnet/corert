// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Internal.TypeSystem;

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
        static public string GetMangledName(JitHelperId id)
        {
            switch (id)
            {
                case JitHelperId.RngChkFail:
                    return "__range_check_fail";

                case JitHelperId.WriteBarrier:
                    return "RhpAssignRef";

                case JitHelperId.CheckedWriteBarrier:
                    return "RhpCheckedAssignRef";

                case JitHelperId.ByRefWriteBarrier:
                    return "RhpByRefAssignRef";

                case JitHelperId.Throw:
                    return "__throw_exception";

                case JitHelperId.FailFast:
                    return "__fail_fast";

                case JitHelperId.NewMultiDimArr:
                    return "RhNewMDArray";

                case JitHelperId.Stelem_Ref:
                    return "__stelem_ref";
                case JitHelperId.Ldelema_Ref:
                    return "__ldelema_ref";

                case JitHelperId.MemCpy:
                    return "memcpy";

                default:
                    // TODO: Uncomment once all helpers are implemented
                    // throw new NotImplementedException();
                    return "__fail_fast";
            }
        }
    }
}
