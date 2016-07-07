// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Keep in sync with https://github.com/dotnet/coreclr/blob/master/src/inc/readytorun.h
//

namespace ILCompiler
{
    //
    // Constants for fixup signature encoding
    //

    enum ReadyToRunFixupKind
    {
        ThisObjDictionaryLookup    = 0x07,
        TypeDictionaryLookup       = 0x08,
        MethodDictionaryLookup     = 0x09,

        TypeHandle                 = 0x10,
        MethodHandle               = 0x11,
        FieldHandle                = 0x12,

        MethodEntry                = 0x13, /* For calling a method entry point */
        MethodEntry_DefToken       = 0x14, /* Smaller version of MethodEntry - method is def token */
        MethodEntry_RefToken       = 0x15, /* Smaller version of MethodEntry - method is ref token */

        VirtualEntry               = 0x16, /* For invoking a virtual method */
        VirtualEntry_DefToken      = 0x17, /* Smaller version of VirtualEntry - method is def token */
        VirtualEntry_RefToken      = 0x18, /* Smaller version of VirtualEntry - method is ref token */
        VirtualEntry_Slot          = 0x19, /* Smaller version of VirtualEntry - type & slot */

        Helper                     = 0x1A, /* Helper */
        StringHandle               = 0x1B, /* String handle */

        NewObject                  = 0x1C, /* Dynamically created new helper */
        NewArray                   = 0x1D,

        IsInstanceOf               = 0x1E, /* Dynamically created casting helper */
        ChkCast                    = 0x1F,

        FieldAddress               = 0x20, /* For accessing a cross-module static fields */
        CctorTrigger               = 0x21, /* Static constructor trigger */

        StaticBaseNonGC            = 0x22, /* Dynamically created static base helpers */
        StaticBaseGC               = 0x23,
        ThreadStaticBaseNonGC      = 0x24,
        ThreadStaticBaseGC         = 0x25,

        FieldBaseOffset            = 0x26, /* Field base offset */
        FieldOffset                = 0x27, /* Field offset */

        TypeDictionary             = 0x28,
        MethodDictionary           = 0x29,

        Check_TypeLayout           = 0x2A, /* size, alignment, HFA, reference map */
        Check_FieldOffset          = 0x2B,

        DelegateCtor               = 0x2C, /* optimized delegate ctor */
    }

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
    }
}
