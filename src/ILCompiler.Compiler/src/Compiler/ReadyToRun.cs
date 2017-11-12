// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Keep in sync with https://github.com/dotnet/coreclr/blob/master/src/inc/readytorun.h
//

namespace ILCompiler
{
    //
    // Intrinsics and helpers
    //

    public enum ReadyToRunHelper
    {
        // Exception handling helpers
        Throw                       = 0x20,
        Rethrow                     = 0x21,
        Overflow                    = 0x22,
        RngChkFail                  = 0x23,
        FailFast                    = 0x24,
        ThrowNullRef                = 0x25,
        ThrowDivZero                = 0x26,
        ThrowArgumentOutOfRange     = 0x27,
        ThrowArgument               = 0x28,
        ThrowPlatformNotSupported   = 0x29,

        DebugBreak                  = 0x2F,

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
        NewMultiDimArr_NonVarArg    = 0x5D,

        // Helpers used with generic handle lookup cases
        NewObject                   = 0x60,
        NewArray                    = 0x61,
        CheckCastAny                = 0x62,
        CheckInstanceAny            = 0x63,
        GenericGcStaticBase         = 0x64,
        GenericNonGcStaticBase      = 0x65,
        GenericGcTlsBase            = 0x66,
        GenericNonGcTlsBase         = 0x67,
        VirtualFuncPtr              = 0x68,

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

        // P/Invoke support
        PInvokeBegin                = 0xF0,
        PInvokeEnd                  = 0xF1,

        // P/Invoke support
        ReversePInvokeEnter         = 0xF2,
        ReversePInvokeExit          = 0xF3,

        // Synchronized methods
        MonitorEnter                = 0xF8,
        MonitorExit                 = 0xF9,
        MonitorEnterStatic          = 0xFA,
        MonitorExitStatic           = 0xFB,

        // GVM lookup helper
        GVMLookupForSlot            = 0x100,

        // TypedReference
        TypeHandleToRuntimeType     = 0x110,
        GetRefAny                   = 0x111,
    }
}
