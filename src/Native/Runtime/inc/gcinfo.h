// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*****************************************************************************/
#ifndef _GCINFO_H_
#define _GCINFO_H_
/*****************************************************************************/

// Keep definitions in this file in sync with Nutc\UTC\gcinfo.h

#ifdef _TARGET_ARM_

#define NUM_PRESERVED_REGS 9

enum RegMask
{
    RBM_R0  = 0x0001,
    RBM_R1  = 0x0002,
    RBM_R2  = 0x0004,
    RBM_R3  = 0x0008,
    RBM_R4  = 0x0010,   // callee saved
    RBM_R5  = 0x0020,   // callee saved
    RBM_R6  = 0x0040,   // callee saved
    RBM_R7  = 0x0080,   // callee saved
    RBM_R8  = 0x0100,   // callee saved
    RBM_R9  = 0x0200,   // callee saved
    RBM_R10 = 0x0400,   // callee saved
    RBM_R11 = 0x0800,   // callee saved
    RBM_R12 = 0x1000,
    RBM_SP  = 0x2000,
    RBM_LR  = 0x4000,   // callee saved, but not valid to be alive across a call!
    RBM_PC  = 0x8000,
    RBM_RETVAL = RBM_R0,
    RBM_CALLEE_SAVED_REGS = (RBM_R4|RBM_R5|RBM_R6|RBM_R7|RBM_R8|RBM_R9|RBM_R10|RBM_R11|RBM_LR),
    RBM_CALLEE_SAVED_REG_COUNT = 9,
    // Special case: LR is callee saved, but may not appear as a live GC ref except 
    // in the leaf frame because calls will trash it.  Therefore, we ALSO consider 
    // it a scratch register.
    RBM_SCRATCH_REGS = (RBM_R0|RBM_R1|RBM_R2|RBM_R3|RBM_R12|RBM_LR),
    RBM_SCRATCH_REG_COUNT = 6,
};

enum RegNumber
{
    RN_R0   = 0,
    RN_R1   = 1,
    RN_R2   = 2,
    RN_R3   = 3,
    RN_R4   = 4,
    RN_R5   = 5,
    RN_R6   = 6,
    RN_R7   = 7,
    RN_R8   = 8,
    RN_R9   = 9,
    RN_R10  = 10,
    RN_R11  = 11,
    RN_R12  = 12,
    RN_SP   = 13,
    RN_LR   = 14,
    RN_PC   = 15,

    RN_NONE = 16,
};

enum CalleeSavedRegNum
{
    CSR_NUM_R4  = 0x00,
    CSR_NUM_R5  = 0x01,
    CSR_NUM_R6  = 0x02,
    CSR_NUM_R7  = 0x03,
    CSR_NUM_R8  = 0x04,
    CSR_NUM_R9  = 0x05,
    CSR_NUM_R10 = 0x06,
    CSR_NUM_R11 = 0x07,
    // NOTE: LR is omitted because it may not be live except as a 'scratch' reg
};

enum CalleeSavedRegMask
{
    CSR_MASK_NONE = 0x00,
    CSR_MASK_R4   = 0x001,
    CSR_MASK_R5   = 0x002,
    CSR_MASK_R6   = 0x004,
    CSR_MASK_R7   = 0x008,
    CSR_MASK_R8   = 0x010,
    CSR_MASK_R9   = 0x020,
    CSR_MASK_R10  = 0x040,
    CSR_MASK_R11  = 0x080,
    CSR_MASK_LR   = 0x100,

    CSR_MASK_ALL  = 0x1ff,
    CSR_MASK_HIGHEST = 0x100,
};

enum ScratchRegNum
{
    SR_NUM_R0   = 0x00,
    SR_NUM_R1   = 0x01,
    SR_NUM_R2   = 0x02,
    SR_NUM_R3   = 0x03,
    SR_NUM_R12  = 0x04,
    SR_NUM_LR   = 0x05,
};

enum ScratchRegMask
{
    SR_MASK_NONE = 0x00,
    SR_MASK_R0   = 0x01,
    SR_MASK_R1   = 0x02,
    SR_MASK_R2   = 0x04,
    SR_MASK_R3   = 0x08,
    SR_MASK_R12  = 0x10,
    SR_MASK_LR   = 0x20,
};

#elif defined(_TARGET_ARM64_)

enum RegMask
{
    RBM_NONE = 0,

    RBM_X0 = 0x00000001,
    RBM_X1 = 0x00000002,
    RBM_X2 = 0x00000004,
    RBM_X3 = 0x00000008,
    RBM_X4 = 0x00000010,
    RBM_X5 = 0x00000020,
    RBM_X6 = 0x00000040,
    RBM_X7 = 0x00000080,
    RBM_X8 = 0x00000100, // ARM64 ABI: indirect result register
    RBM_X9 = 0x00000200,
    RBM_X10 = 0x00000400,
    RBM_X11 = 0x00000800,
    RBM_X12 = 0x00001000,
    RBM_X13 = 0x00002000,
    RBM_X14 = 0x00004000,
    RBM_X15 = 0x00008000,

    RBM_XIP0 = 0x00010000, // This one is occasionally used as a scratch register (but can be destroyed by branching or a call)
    RBM_XIP1 = 0x00020000, // This one may be also used as a scratch register (but can be destroyed by branching or a call)
    RBM_XPR = 0x00040000,

    RBM_X19 = 0x00080000, // RA_CALLEESAVE
    RBM_X20 = 0x00100000, // RA_CALLEESAVE
    RBM_X21 = 0x00200000, // RA_CALLEESAVE
    RBM_X22 = 0x00400000, // RA_CALLEESAVE
    RBM_X23 = 0x00800000, // RA_CALLEESAVE
    RBM_X24 = 0x01000000, // RA_CALLEESAVE
    RBM_X25 = 0x02000000, // RA_CALLEESAVE
    RBM_X26 = 0x04000000, // RA_CALLEESAVE
    RBM_X27 = 0x08000000, // RA_CALLEESAVE
    RBM_X28 = 0x10000000, // RA_CALLEESAVE

    RBM_FP = 0x20000000,
    RBM_LR = 0x40000000,
    RBM_SP = 0x80000000,

    RBM_RETVAL = RBM_X8,
    // Note: Callee saved regs: X19-X28; FP and LR are treated as callee-saved in unwinding code
    RBM_CALLEE_SAVED_REG_COUNT = 12,

    // Scratch regs: X0-X15, XIP0, XIP1, LR
    RBM_SCRATCH_REG_COUNT = 19,
};

#define NUM_PRESERVED_REGS RBM_CALLEE_SAVED_REG_COUNT

// Number of the callee-saved registers stored in the fixed header
#define NUM_PRESERVED_REGS_LOW 9
#define MASK_PRESERVED_REGS_LOW ((1 << NUM_PRESERVED_REGS_LOW) - 1)

enum RegNumber
{
    RN_X0 = 0,
    RN_X1 = 1,
    RN_X2 = 2,
    RN_X3 = 3,
    RN_X4 = 4,
    RN_X5 = 5,
    RN_X6 = 6,
    RN_X7 = 7,
    RN_X8 = 8, // indirect result register
    RN_X9 = 9,
    RN_X10 = 10,
    RN_X11 = 11,
    RN_X12 = 12,
    RN_X13 = 13,
    RN_X14 = 14,
    RN_X15 = 15,

    RN_XIP0 = 16,
    RN_XIP1 = 17,
    RN_XPR = 18,

    RN_X19 = 19, // RA_CALLEESAVE
    RN_X20 = 20, // RA_CALLEESAVE
    RN_X21 = 21, // RA_CALLEESAVE
    RN_X22 = 22, // RA_CALLEESAVE
    RN_X23 = 23, // RA_CALLEESAVE
    RN_X24 = 24, // RA_CALLEESAVE
    RN_X25 = 25, // RA_CALLEESAVE
    RN_X26 = 26, // RA_CALLEESAVE
    RN_X27 = 27, // RA_CALLEESAVE
    RN_X28 = 28, // RA_CALLEESAVE

    RN_FP = 29,
    RN_LR = 30,
    RN_SP = 31,

    RN_NONE = 32,
};

enum CalleeSavedRegNum
{
    // NOTE: LR is omitted because it may not be live except as a 'scratch' reg
    CSR_NUM_X19 = 1,
    CSR_NUM_X20 = 2,
    CSR_NUM_X21 = 3,
    CSR_NUM_X22 = 4,
    CSR_NUM_X23 = 5,
    CSR_NUM_X24 = 6,
    CSR_NUM_X25 = 7,
    CSR_NUM_X26 = 8,
    CSR_NUM_X27 = 9,
    CSR_NUM_X28 = 10,
    CSR_NUM_FP = 11,
    CSR_NUM_NONE = 12,
};

enum CalleeSavedRegMask
{
    CSR_MASK_NONE = 0x00,
    // LR is placed here to reduce the frequency of the long encoding
    CSR_MASK_LR = 0x001,
    CSR_MASK_X19 = 0x002,
    CSR_MASK_X20 = 0x004,
    CSR_MASK_X21 = 0x008,
    CSR_MASK_X22 = 0x010,
    CSR_MASK_X23 = 0x020,
    CSR_MASK_X24 = 0x040,
    CSR_MASK_X25 = 0x080,
    CSR_MASK_X26 = 0x100,
    CSR_MASK_X27 = 0x200,
    CSR_MASK_X28 = 0x400,
    CSR_MASK_FP = 0x800,

    CSR_MASK_ALL = 0xfff,
    CSR_MASK_HIGHEST = 0x800,
};

enum ScratchRegNum
{
    SR_NUM_X0 = 0,
    SR_NUM_X1 = 1,
    SR_NUM_X2 = 2,
    SR_NUM_X3 = 3,
    SR_NUM_X4 = 4,
    SR_NUM_X5 = 5,
    SR_NUM_X6 = 6,
    SR_NUM_X7 = 7,
    SR_NUM_X8 = 8,
    SR_NUM_X9 = 9,
    SR_NUM_X10 = 10,
    SR_NUM_X11 = 11,
    SR_NUM_X12 = 12,
    SR_NUM_X13 = 13,
    SR_NUM_X14 = 14,
    SR_NUM_X15 = 15,

    SR_NUM_XIP0 = 16,
    SR_NUM_XIP1 = 17,
    SR_NUM_LR = 18,

    SR_NUM_NONE = 19,
};

enum ScratchRegMask
{
    SR_MASK_NONE = 0x00,
    SR_MASK_X0 = 0x01,
    SR_MASK_X1 = 0x02,
    SR_MASK_X2 = 0x04,
    SR_MASK_X3 = 0x08,
    SR_MASK_X4 = 0x10,
    SR_MASK_X5 = 0x20,
    SR_MASK_X6 = 0x40,
    SR_MASK_X7 = 0x80,
    SR_MASK_X8 = 0x100,
    SR_MASK_X9 = 0x200,
    SR_MASK_X10 = 0x400,
    SR_MASK_X11 = 0x800,
    SR_MASK_X12 = 0x1000,
    SR_MASK_X13 = 0x2000,
    SR_MASK_X14 = 0x4000,
    SR_MASK_X15 = 0x8000,

    SR_MASK_XIP0 = 0x10000,
    SR_MASK_XIP1 = 0x20000,
    SR_MASK_LR = 0x40000,
};

#else // _TARGET_ARM_

#ifdef _TARGET_AMD64_
#define NUM_PRESERVED_REGS 8
#else
#define NUM_PRESERVED_REGS 4
#endif

enum RegMask
{
    RBM_EAX = 0x0001,
    RBM_ECX = 0x0002,
    RBM_EDX = 0x0004,
    RBM_EBX = 0x0008,   // callee saved
    RBM_ESP = 0x0010,
    RBM_EBP = 0x0020,   // callee saved
    RBM_ESI = 0x0040,   // callee saved
    RBM_EDI = 0x0080,   // callee saved

    RBM_R8  = 0x0100,
    RBM_R9  = 0x0200,
    RBM_R10 = 0x0400,
    RBM_R11 = 0x0800,
    RBM_R12 = 0x1000,   // callee saved
    RBM_R13 = 0x2000,   // callee saved
    RBM_R14 = 0x4000,   // callee saved
    RBM_R15 = 0x8000,   // callee saved

    RBM_RETVAL = RBM_EAX,

#ifdef _TARGET_AMD64_
    RBM_CALLEE_SAVED_REGS = (RBM_EDI|RBM_ESI|RBM_EBX|RBM_EBP|RBM_R12|RBM_R13|RBM_R14|RBM_R15),
    RBM_CALLEE_SAVED_REG_COUNT = 8,
    RBM_SCRATCH_REGS = (RBM_EAX|RBM_ECX|RBM_EDX|RBM_R8|RBM_R9|RBM_R10|RBM_R11),
    RBM_SCRATCH_REG_COUNT = 7,
#else
    RBM_CALLEE_SAVED_REGS = (RBM_EDI|RBM_ESI|RBM_EBX|RBM_EBP),
    RBM_CALLEE_SAVED_REG_COUNT = 4,
    RBM_SCRATCH_REGS = (RBM_EAX|RBM_ECX|RBM_EDX),
    RBM_SCRATCH_REG_COUNT = 3,
#endif // _TARGET_AMD64_
};

enum RegNumber
{
    RN_EAX = 0,
    RN_ECX = 1,
    RN_EDX = 2,
    RN_EBX = 3,
    RN_ESP = 4,
    RN_EBP = 5,
    RN_ESI = 6,
    RN_EDI = 7,
    RN_R8  = 8,
    RN_R9  = 9,
    RN_R10 = 10,
    RN_R11 = 11,
    RN_R12 = 12,
    RN_R13 = 13,
    RN_R14 = 14,
    RN_R15 = 15,

    RN_NONE = 16,
};

enum CalleeSavedRegNum
{
    CSR_NUM_RBX = 0x00,
    CSR_NUM_RSI = 0x01,
    CSR_NUM_RDI = 0x02,
    CSR_NUM_RBP = 0x03,
#ifdef _TARGET_AMD64_
    CSR_NUM_R12 = 0x04,
    CSR_NUM_R13 = 0x05,
    CSR_NUM_R14 = 0x06,
    CSR_NUM_R15 = 0x07,
#endif // _TARGET_AMD64_
};

enum CalleeSavedRegMask
{
    CSR_MASK_NONE = 0x00,
    CSR_MASK_RBX = 0x01,
    CSR_MASK_RSI = 0x02,
    CSR_MASK_RDI = 0x04,
    CSR_MASK_RBP = 0x08,
    CSR_MASK_R12 = 0x10,
    CSR_MASK_R13 = 0x20,
    CSR_MASK_R14 = 0x40,
    CSR_MASK_R15 = 0x80,

#ifdef _TARGET_AMD64_
    CSR_MASK_ALL = 0xFF,
    CSR_MASK_HIGHEST = 0x80,
#else
    CSR_MASK_ALL = 0x0F,
    CSR_MASK_HIGHEST = 0x08,
#endif
};

enum ScratchRegNum
{
    SR_NUM_RAX = 0x00,
    SR_NUM_RCX = 0x01,
    SR_NUM_RDX = 0x02,
#ifdef _TARGET_AMD64_
    SR_NUM_R8  = 0x03,
    SR_NUM_R9  = 0x04,
    SR_NUM_R10 = 0x05,
    SR_NUM_R11 = 0x06,
#endif // _TARGET_AMD64_
};

enum ScratchRegMask
{
    SR_MASK_NONE = 0x00,
    SR_MASK_RAX  = 0x01,
    SR_MASK_RCX  = 0x02,
    SR_MASK_RDX  = 0x04,
    SR_MASK_R8   = 0x08,
    SR_MASK_R9   = 0x10,
    SR_MASK_R10  = 0x20,
    SR_MASK_R11  = 0x40,
};

#endif // _TARGET_ARM_

struct GCInfoHeader
{
private:
    UInt16  prologSize               : 6; // 0 [0:5]  // @TODO: define an 'overflow' encoding for big prologs?
    UInt16  hasFunclets              : 1; // 0 [6]
    UInt16  fixedEpilogSize          : 6; // 0 [7] + 1 [0:4]  '0' encoding implies that epilog size varies and is encoded for each epilog
    UInt16  epilogCountSmall         : 2; // 1 [5:6] '3' encoding implies the number of epilogs is encoded separately
    UInt16  hasExtraData             : 1; // 1 [7]  1: more data follows (dynamic alignment, GS cookie, common vars, etc.)

#ifdef _TARGET_ARM_
    UInt16  returnKind              : 2; // 2 [0:1] one of: MethodReturnKind enum
    UInt16  ebpFrame                : 1; // 2 [2]   on x64, this means "has frame pointer and it is RBP", on ARM R7
    UInt16  epilogAtEnd             : 1; // 2 [3]
    UInt16  hasFrameSize            : 1; // 2 [4]   1: frame size is encoded below, 0: frame size is 0
    UInt16 calleeSavedRegMask       : NUM_PRESERVED_REGS;   // 2 [5:7]    3 [0:5]
    UInt16 arm_areParmOrVfpRegsPushed:1; // 3 [6]   1: pushed param reg set (R0-R3) and pushed fp reg start and count are encoded below, 0: no pushed param or fp registers
#elif defined (_TARGET_ARM64_)
    UInt16  returnKind              : 2; // 2 [0:1] one of: MethodReturnKind enum
    UInt16  ebpFrame                : 1; // 2 [2]   1: has frame pointer and it is FP
    UInt16  epilogAtEnd             : 1; // 2 [3]
    UInt16  hasFrameSize            : 1; // 2 [4]   1: frame size is encoded below, 0: frame size is 0
    UInt16  arm64_longCsrMask            : 1; // 2 [5]  1: high bits of calleeSavedRegMask are encoded below
    UInt16  arm64_areParmOrVfpRegsPushed : 1; // 2 [6]  1: pushed param reg count (X0-X7) and pushed fp reg set (D8-D15) are encoded below, 0: no pushed param or fp registers
    UInt16  arm64_calleeSavedRegMaskLow  : NUM_PRESERVED_REGS_LOW;  // 2 [7]    3 [0:7]
#else
    UInt8  returnKind               : 2; // 2 [0:1] one of: MethodReturnKind enum
    UInt8  ebpFrame                 : 1; // 2 [2]   on x64, this means "has frame pointer and it is RBP", on ARM R7
    UInt8  epilogAtEnd              : 1; // 2 [3]
#ifdef _TARGET_AMD64_
    UInt8  hasFrameSize             : 1; // 2 [4]   1: frame size is encoded below, 0: frame size is 0
    UInt8  x64_framePtrOffsetSmall  : 2; // 2 [5:6] 00: framePtrOffset = 0x20
                                         //         01: framePtrOffset = 0x30
                                         //         10: framePtrOffset = 0x40
                                         //         11: a variable-length integer 'x64_frameOffset' follows.
    UInt8  x64_hasSavedXmmRegs      : 1; // 2 [7]   any saved xmm registers?
#endif
                                                            // X86        X64
    UInt8  calleeSavedRegMask       : NUM_PRESERVED_REGS;   // 2 [4:7]    3 [0:7]

#ifdef _TARGET_X86_
    UInt8  x86_argCountLow          : 5; // 3 [0-4]  expressed in pointer-sized units    // @TODO: steal more bits here?
    UInt8  x86_argCountIsLarge      : 1; // 3 [5]    if this bit is set, then the high 8 bits are encoded in x86_argCountHigh
    UInt8  x86_hasStackChanges      : 1; // 3 [6]    x86-only, !ebpFrame-only, this method has pushes 
                                         //          and pops in it, and a string follows this header
                                         //          which describes them
    UInt8  hasFrameSize             : 1; // 3 [7]    1: frame size is encoded below, 0: frame size is 0
#endif
#endif

    //
    // OPTIONAL FIELDS FOLLOW
    //
    // The following values are encoded with variable-length integers on disk, but are decoded into these 
    // fields in memory.
    //

    // For ARM and ARM64 this field stores the offset of the callee-saved area relative to FP/SP
    UInt32  frameSize;                   // expressed in pointer-sized units, only encoded if hasFrameSize==1
    // OPTIONAL: only encoded if returnKind = MRK_ReturnsToNative
    UInt32  reversePinvokeFrameOffset;   // expressed in pointer-sized units away from the frame pointer

#ifdef _TARGET_AMD64_
    // OPTIONAL: only encoded if x64_framePtrOffsetSmall = 11
    //
    // ENCODING NOTE: In the encoding, the variable-sized unsigned will be 7 less than the total number
    // of 16-byte units that make up the frame pointer offset.  
    //
    // In memory, this value will always be set and will always be the total number of 16-byte units that make 
    // up the frame pointer offset.
    UInt8   x64_framePtrOffset;       // expressed in 16-byte unit

    // OPTIONAL: only encoded using a variable-sized unsigned if x64_hasSavedXmmRegs is set.
    //
    // An additional optimization is possible because registers xmm0 .. xmm5 should never be saved,
    // so they are not encoded in the variable-sized unsigned - instead the mask is shifted right 6 bits
    // for encoding. Thus, any subset of registers xmm6 .. xmm12 can be represented using one byte
    // - this covers the most frequent cases.
    //
    // The shift applies to decoding/encoding only though - the actual header field below uses the
    // straightforward mapping where bit 0 corresponds to xmm0, bit 1 corresponds to xmm1 and so on.
    //
    UInt16  x64_savedXmmRegMask;      // which xmm regs were saved
#elif defined(_TARGET_X86_)
    // OPTIONAL: only encoded if x86_argCountIsLarge = 1
    // NOTE: because we are using pointer-sized units, only 14 bits are required to represent the entire range
    // that can be expressed by a 'ret NNNN' instruction.  Therefore, with 6 in the 'low' field and 8 in the
    // 'high' field, we are not losing any range here.  (Although the need for that full range is debatable.)
    UInt8   x86_argCountHigh; 
#elif defined(_TARGET_ARM_)
    // OPTIONAL: only encoded if arm_areParmOrVfpRegsPushed = 1
    UInt8   arm_parmRegsPushedSet;
    UInt8   arm_vfpRegFirstPushed;
    UInt8   arm_vfpRegPushedCount;
#elif defined(_TARGET_ARM64_)
    // OPTIONAL: high bits of calleeSavedRegMask are encoded only if arm64_longCsrMask = 1; low bits equal to arm64_calleeSavedRegMaskLow
    UInt16  calleeSavedRegMask;

    // OPTIONAL: only encoded if arm64_areParmOrVfpRegsPushed = 1
    UInt8   arm64_parmRegsPushedCount;  // how many of X0-X7 registers are saved
    UInt8   arm64_vfpRegsPushedMask;    // which of D8-D15 registers are saved
#endif

    //
    // OPTIONAL: only encoded if hasExtraData = 1
    union
    {
        struct
        {
#if defined(_TARGET_ARM64_)
            UInt8 FPLRAreOnTop      : 1;    // [0]      1: FP and LR are saved on top of locals, not at the bottom (see MdmSaveFPAndLRAtTopOfLocalsArea)
            UInt8 reg1ReturnKind    : 2;    // [1:2]    One of MRK_Returns{Scalar|Object|Byref} constants describing value returned in x1 if any
            UInt8 hasGSCookie       : 1;    // [3]      1: frame uses GS cookie
            UInt8 hasCommonVars     : 1;    // [4]      1: method has a list of "common vars"
                                            //          as an optimization for methods with many call sites and variables
            UInt8                   : 3;    // [5:7]    unused bits
#else
            UInt8 logStackAlignment : 4;    // [0:3]    binary logarithm of frame alignment (3..15) or 0
            UInt8 hasGSCookie       : 1;    // [4]      1: frame uses GS cookie
            UInt8 hasCommonVars     : 1;    // [5]      1: method has a list of "common vars"
                                            //          as an optimization for methods with many call sites and variables
            UInt8                   : 2;    // [6:7]    unused bits
#endif
#pragma warning(suppress:4201) // nameless struct
        };
        UInt8 extraDataHeader;
    };

    // OPTIONAL: only encoded if logStackAlignment != 0
    UInt8 paramPointerReg;

    // OPTIONAL: only encoded if epilogCountSmall = 3
    UInt16 epilogCount;

    // OPTIONAL: only encoded if gsCookie = 1
    UInt32 gsCookieOffset;      // expressed in pointer-sized units away from the frame pointer

    //
    // OPTIONAL: only encoded if hasFunclets = 1
    //  {numFunclets}           // encoded as variable-length unsigned
    //      {start-funclet0}    // offset from start of previous funclet, encoded as variable-length unsigned
    //      {start-funclet1}    // 
    //      {start-funclet2}
    //       ...
    //      {sizeof-funclet(N-1)}   // numFunclets == N  (i.e. there are N+1 sizes here)
    //      -----------------
    //      {GCInfoHeader-funclet0}  // encoded as normal, must not have 'hasFunclets' set.
    //      {GCInfoHeader-funclet1}
    //       ...
    //      {GCInfoHeader-funclet(N-1)}

    // WARNING: 
    // WARNING: Do not add fields to the file-format after the funclet header encodings -- these are decoded
    // WARNING: recursively and 'in-place' when looking for the info associated with a funclet.  Therefore, 
    // WARNING: in that case, we cannot easily continue to decode things associated with the main body 
    // WARNING: GCInfoHeader once we start this recursive decode.
    // WARNING: 

    // -------------------------------------------------------------------------------------------------------
    // END of file-encoding-related-fields
    // -------------------------------------------------------------------------------------------------------

    // The following fields are not encoded in the file format, they are just used as convenience placeholders 
    // for decode state.
    UInt32 funcletOffset; // non-zero indicates that this GCInfoHeader is for a funclet

#if defined(BINDER)
public:
    UInt32 cbThisCodeBody;
    GCInfoHeader * pNextFunclet;
private:
    ;
#endif // BINDER


public:
    //
    // CONSTANTS / STATIC STUFF
    //

    enum MethodReturnKind
    {
        MRK_ReturnsScalar   = 0,
        MRK_ReturnsObject   = 1,
        MRK_ReturnsByref    = 2,
        MRK_ReturnsToNative = 3,

#if defined(_TARGET_ARM64_)
        // Cases for structs returned in two registers.
        // Naming scheme: MRK_reg0Kind_reg1Kind.
        // Encoding scheme: <two bits for reg1Kind> <two bits for reg0Kind>.
        // We do not distinguish returning a scalar in reg1 and no return value in reg1,
        // which means we can use MRK_ReturnsObject for MRK_Obj_Scalar, etc.
        MRK_Scalar_Obj      = (MRK_ReturnsObject << 2) | MRK_ReturnsScalar,
        MRK_Obj_Obj         = (MRK_ReturnsObject << 2) | MRK_ReturnsObject,
        MRK_Byref_Obj       = (MRK_ReturnsObject << 2) | MRK_ReturnsByref,
        MRK_Scalar_Byref    = (MRK_ReturnsByref  << 2) | MRK_ReturnsScalar,
        MRK_Obj_Byref       = (MRK_ReturnsByref  << 2) | MRK_ReturnsObject,
        MRK_Byref_Byref     = (MRK_ReturnsByref  << 2) | MRK_ReturnsByref,

        MRK_LastValid       = MRK_Byref_Byref,
        // Illegal or uninitialized value. Never written to the image.
        MRK_Unknown         = 0xff,
#else
        MRK_LastValid       = MRK_ReturnsToNative,
        // Illegal or uninitialized value. Never written to the image.
        MRK_Unknown         = 4,
#endif
    };

    enum EncodingConstants
    {
        EC_SizeOfFixedHeader = 4,
        EC_MaxFrameByteSize                 = 10*1024*1024,
        EC_MaxReversePInvokeFrameByteOffset = 10*1024*1024,
        EC_MaxX64FramePtrByteOffset         = UInt16_MAX * 0x10,
        EC_MaxEpilogCountSmall              = 3,
        EC_MaxEpilogCount                   = 64*1024 - 1,
    };

    //
    // MEMBER FUNCTIONS
    //

    void Init()
    {
        memset(this, 0, sizeof(GCInfoHeader));
    }

    //
    // SETTERS
    //

    void SetPrologSize(UInt32 sizeInBytes)
    {
#if defined (_TARGET_ARM64_)
        // For arm64 we encode multiples of 4, rather than raw bytes, since instructions are all same size.
        ASSERT((sizeInBytes & 3) == 0);
        prologSize = sizeInBytes >> 2;
        ASSERT(prologSize == sizeInBytes >> 2);
#else
        prologSize = sizeInBytes;
        ASSERT(prologSize == sizeInBytes);
#endif
    }

    void SetHasFunclets(bool fHasFunclets)
    {
        hasFunclets = fHasFunclets ? 1 : 0;
    }

    void PokeFixedEpilogSize(UInt32 sizeInBytes)
    {
#if defined (_TARGET_ARM64_)
        // For arm64 we encode multiples of 4, rather than raw bytes, since instructions are all same size.
        ASSERT((sizeInBytes & 3) == 0);
        fixedEpilogSize = sizeInBytes >> 2;
        ASSERT(fixedEpilogSize == sizeInBytes >> 2);
#else
        fixedEpilogSize = sizeInBytes;
        ASSERT(fixedEpilogSize == sizeInBytes);
#endif
    }

    void SetFixedEpilogSize(UInt32 sizeInBytes, bool varyingSizes)
    {
        if (varyingSizes)
            fixedEpilogSize = 0;
        else
        {
            ASSERT(sizeInBytes != 0);
#if defined (_TARGET_ARM64_)
            // For arm64 we encode multiples of 4, rather than raw bytes, since instructions are all same size.
            ASSERT((sizeInBytes & 3) == 0);
            fixedEpilogSize = sizeInBytes >> 2;
            ASSERT(fixedEpilogSize == sizeInBytes >> 2);
#else
            fixedEpilogSize = sizeInBytes;
            ASSERT(fixedEpilogSize == sizeInBytes);
#endif
        }
    }

    void SetEpilogCount(UInt32 count, bool isAtEnd)
    {
        epilogCount = ToUInt16(count);
        epilogAtEnd = isAtEnd ? 1 : 0;

        ASSERT(epilogCount == count);
        ASSERT((count == 1) || !isAtEnd);
        epilogCountSmall = count < EC_MaxEpilogCountSmall ? count : EC_MaxEpilogCountSmall;
    }

#if !defined(_TARGET_ARM64_)
    void SetReturnKind(MethodReturnKind kind)
    {
        ASSERT(kind <= MRK_ReturnsToNative); // not enough bits to encode 'unknown'
        returnKind = kind;
    }

    void SetDynamicAlignment(UInt8 logByteAlignment)
    {
#ifdef _TARGET_X86_
        ASSERT(logByteAlignment >= 3); // 4 byte aligned frames
#else
        ASSERT(logByteAlignment >= 4); // 8 byte aligned frames
#endif

        hasExtraData = 1;
        logStackAlignment = logByteAlignment;
        ASSERT(logStackAlignment == logByteAlignment);
        paramPointerReg = RN_NONE;
    }
#endif // !defined(_TARGET_ARM64_)

#if defined(_TARGET_ARM64_)
    void SetFPLROnTop(void)
    {
        hasExtraData = 1;
        FPLRAreOnTop = 1;
    }
#endif

    void SetGSCookieOffset(UInt32 offsetInBytes)
    {
        ASSERT(offsetInBytes != 0);
        ASSERT(0 == (offsetInBytes % POINTER_SIZE));
        hasExtraData = 1;
        hasGSCookie = 1;
        gsCookieOffset = offsetInBytes / POINTER_SIZE;
    }

    void SetHasCommonVars()
    {
        hasExtraData = 1;
        hasCommonVars = 1;
    }

    void SetParamPointer(RegNumber regNum, UInt32 offsetInBytes, bool isOffsetFromSP = false)
    {
        UNREFERENCED_PARAMETER(offsetInBytes);
        UNREFERENCED_PARAMETER(isOffsetFromSP);
        ASSERT(HasDynamicAlignment()); // only expected for dynamic aligned frames
        ASSERT(offsetInBytes==0); // not yet supported

        paramPointerReg = (UInt8)regNum;
    }

    void SetFramePointer(RegNumber regNum, UInt32 offsetInBytes, bool isOffsetFromSP = false)
    {
        UNREFERENCED_PARAMETER(offsetInBytes);
        UNREFERENCED_PARAMETER(isOffsetFromSP);

        if (regNum == RN_NONE)
        {
            ebpFrame = 0;
        }
        else
        {
#ifdef _TARGET_ARM_
            ASSERT(regNum == RN_R7);
#elif defined(_TARGET_AMD64_) || defined(_TARGET_X86_)
            ASSERT(regNum == RN_EBP);
#elif defined(_TARGET_ARM64_)
            ASSERT(regNum == RN_FP);
#else
            ASSERT(!"NYI");
#endif
            ebpFrame = 1;
        }
        ASSERT(offsetInBytes == 0 || isOffsetFromSP);

#ifdef _TARGET_AMD64_
        if (isOffsetFromSP)
            offsetInBytes += SKEW_FOR_OFFSET_FROM_SP;

        ASSERT((offsetInBytes % 0x10) == 0);
        UInt32 offsetInSlots = offsetInBytes / 0x10;
        if (offsetInSlots >= 3 && offsetInSlots <= 3 + 2)
        {
            x64_framePtrOffsetSmall = offsetInSlots - 3;
        }
        else
        {
            x64_framePtrOffsetSmall = 3;
        }
        x64_framePtrOffset = (UInt8)offsetInSlots;
        ASSERT(x64_framePtrOffset == offsetInSlots);
#else
        ASSERT(offsetInBytes == 0 && !isOffsetFromSP);
#endif // _TARGET_AMD64_
    }

    void SetFrameSize(UInt32 frameSizeInBytes)
    {
        ASSERT(0 == (frameSizeInBytes % POINTER_SIZE));
        frameSize = (frameSizeInBytes / POINTER_SIZE);
        ASSERT(frameSize == (frameSizeInBytes / POINTER_SIZE));
        if (frameSize != 0)
        {
            hasFrameSize = 1;
        }
    }

    void SetSavedRegs(CalleeSavedRegMask regMask)
    {
        calleeSavedRegMask = (UInt16)regMask;
    }

    void SetRegSaved(CalleeSavedRegMask regMask)
    {
        calleeSavedRegMask |= regMask;
    }

    void SetReversePinvokeFrameOffset(int offsetInBytes)
    {
        ASSERT(HasFramePointer());
        ASSERT((offsetInBytes % POINTER_SIZE) == 0);
        ASSERT(GetReturnKind() == MRK_ReturnsToNative);

#if defined(_TARGET_ARM_) || defined(_TARGET_AMD64_) || defined(_TARGET_ARM64_)
        // The offset can be either positive or negative on ARM and x64.
        bool isNeg = (offsetInBytes < 0);
        UInt32 uOffsetInBytes = isNeg ? -offsetInBytes : offsetInBytes;
        UInt32 uEncodedVal = ((uOffsetInBytes / POINTER_SIZE) << 1) | (isNeg ? 1 : 0);
        reversePinvokeFrameOffset = uEncodedVal;
        ASSERT(reversePinvokeFrameOffset == uEncodedVal);
#elif defined (_TARGET_X86_)
        // Use a positive number because it encodes better and 
        // the offset is always negative on x86.
        ASSERT(offsetInBytes < 0);
        reversePinvokeFrameOffset = (-offsetInBytes / POINTER_SIZE);
        ASSERT(reversePinvokeFrameOffset == (UInt32)(-offsetInBytes / POINTER_SIZE));
#else
        ASSERT(!"NYI");
#endif
    }

#ifdef _TARGET_X86_
    void SetReturnPopSize(UInt32 popSizeInBytes)
    {
        ASSERT(0 == (popSizeInBytes % POINTER_SIZE));
        ASSERT(GetReturnPopSize() == 0 || GetReturnPopSize() == (int)popSizeInBytes);

        UInt32 argCount = popSizeInBytes / POINTER_SIZE;
        x86_argCountLow = argCount & 0x1F;
        if (argCount != x86_argCountLow)
        {
            x86_argCountIsLarge = 1;
            x86_argCountHigh = (UInt8)(argCount >> 5);
        }
    }

    void SetHasStackChanges()
    {
        x86_hasStackChanges = 1;
    }
#endif // _TARGET_X86_

#ifdef _TARGET_ARM_
    void SetParmRegsPushed(ScratchRegMask pushedParmRegs)
    {
        // should be a subset of {RO-R3}
        ASSERT((pushedParmRegs & ~(SR_MASK_R0|SR_MASK_R1|SR_MASK_R2|SR_MASK_R3)) == 0);
        arm_areParmOrVfpRegsPushed = pushedParmRegs != 0 || arm_vfpRegPushedCount != 0;
        arm_parmRegsPushedSet = (UInt8)pushedParmRegs;
    }

    void SetVfpRegsPushed(UInt8 vfpRegFirstPushed, UInt8 vfpRegPushedCount)
    {
        // mrt100.dll really only supports pushing a subinterval of d8-d15
        // these are the preserved floating point registers according to the ABI spec
        ASSERT(8 <= vfpRegFirstPushed && vfpRegFirstPushed + vfpRegPushedCount <= 16 || vfpRegPushedCount == 0);
        arm_vfpRegFirstPushed = vfpRegFirstPushed;
        arm_vfpRegPushedCount = vfpRegPushedCount;
        arm_areParmOrVfpRegsPushed = arm_parmRegsPushedSet != 0 || vfpRegPushedCount != 0;
    }
#elif defined(_TARGET_ARM64_)
    void SetParmRegsPushedCount(UInt8 parmRegsPushedCount)
    {
        // pushed parameter registers are a subset of {R0-R7}
        ASSERT(parmRegsPushedCount <= 8);
        arm64_parmRegsPushedCount = parmRegsPushedCount;
        arm64_areParmOrVfpRegsPushed = (arm64_parmRegsPushedCount != 0) || (arm64_vfpRegsPushedMask != 0);
    }

    void SetVfpRegsPushed(UInt8 vfpRegsPushedMask)
    {
        arm64_vfpRegsPushedMask = vfpRegsPushedMask;
        arm64_areParmOrVfpRegsPushed = (arm64_parmRegsPushedCount != 0) || (arm64_vfpRegsPushedMask != 0);
    }
#elif defined(_TARGET_AMD64_)
    void SetSavedXmmRegs(UInt32 savedXmmRegMask)
    {
        // any subset of xmm6-xmm15 may be saved, but no registers in xmm0-xmm5 should be present
        ASSERT((savedXmmRegMask & 0xffff003f) == 0);
        x64_hasSavedXmmRegs = savedXmmRegMask != 0;
        x64_savedXmmRegMask = (UInt16)savedXmmRegMask;
    }
#endif

    void SetFuncletOffset(UInt32 offset)
    {
        funcletOffset = offset;
    }

    //
    // GETTERS
    //
    UInt32 GetPrologSize()
    {
#if defined (_TARGET_ARM64_)
        return prologSize << 2;
#else
        return prologSize;
#endif
    }

    bool HasFunclets()
    {
        return (hasFunclets != 0);
    }

    bool HasVaryingEpilogSizes()
    {
        return fixedEpilogSize == 0;
    }

    UInt32 PeekFixedEpilogSize()
    {
#if defined (_TARGET_ARM64_)
        return fixedEpilogSize << 2;
#else
        return fixedEpilogSize;
#endif
    }

    UInt32 GetFixedEpilogSize()
    {
        ASSERT(!HasVaryingEpilogSizes());
#if defined (_TARGET_ARM64_)
        return fixedEpilogSize << 2;
#else
        return fixedEpilogSize;
#endif
    }

    UInt32 GetEpilogCount()
    {
        return epilogCount;
    }
    
    bool IsEpilogAtEnd()
    {
        return (epilogAtEnd != 0);
    }

    MethodReturnKind GetReturnKind()
    {
#if defined(_TARGET_ARM64_)
        return (MethodReturnKind)((reg1ReturnKind << 2) | returnKind);
#else
        return (MethodReturnKind)returnKind;
#endif
    }

    bool ReturnsToNative()
    {
        return (GetReturnKind() == MRK_ReturnsToNative);
    }

    bool HasFramePointer() const
    {
        return !!ebpFrame;
    }

    bool IsFunclet()
    {
        return funcletOffset != 0;
    }

    UInt32 GetFuncletOffset()
    {
        return funcletOffset;
    }

    int GetPreservedRegsSaveSize() const // returned in bytes
    {
        UInt32 count = 0;
        UInt32 mask = calleeSavedRegMask;
        while (mask != 0)
        {
            count += mask & 1;
            mask >>= 1;
        }

        return count * POINTER_SIZE;
    }
    
    int GetParamPointerReg()
    {
        return paramPointerReg;
    }

    bool HasDynamicAlignment()
    {
#if defined(_TARGET_ARM64_)
        return false;
#else
        return !!logStackAlignment;
#endif
    }

    UInt32 GetDynamicAlignment()
    {
#if defined(_TARGET_ARM64_)
        ASSERT(!"Not supported");
        return 1;
#else
        return 1 << logStackAlignment;
#endif
    }

    bool HasGSCookie()
    {
        return hasGSCookie;
    }

#if defined(_TARGET_ARM64_)
    bool AreFPLROnTop() const
    {
        return FPLRAreOnTop;
    }
#endif

    UInt32 GetGSCookieOffset()
    {
        ASSERT(hasGSCookie);
        return gsCookieOffset * POINTER_SIZE;
    }

    bool HasCommonVars() const
    {
        return hasCommonVars;
    }

#if defined(RHDUMP) && !defined(_TARGET_AMD64_)
    // Due to the wackiness of RhDump, we need this method defined, even though it won't ever be called.
    int GetFramePointerOffset() { ASSERT(!"UNREACHABLE"); __assume(0); }
#endif // defined(RHDUMP) && !defined(_TARGET_AMD64_)

#ifdef _TARGET_AMD64_
    static const UInt32 SKEW_FOR_OFFSET_FROM_SP = 0x10;

    int GetFramePointerOffset() const // returned in bytes
    {
        // traditional frames where FP points to the pushed FP have fp offset == 0
        if (x64_framePtrOffset == 0)
            return 0;

        // otherwise it's an x64 style frame where the fp offset is measured from the sp
        // at the end of the prolog
        int offsetFromSP  = GetFramePointerOffsetFromSP();

        int preservedRegsSaveSize = GetPreservedRegsSaveSize();
        
        // we when called from the binder, rbp isn't set to be a preserved reg,
        // when called from the runtime, it is - compensate for this inconsistency
        if (IsRegSaved(CSR_MASK_RBP))
            preservedRegsSaveSize -= POINTER_SIZE;

        return offsetFromSP - preservedRegsSaveSize - GetFrameSize();
    }

    bool IsFramePointerOffsetFromSP() const
    {
        return x64_framePtrOffset != 0;
    }

    int GetFramePointerOffsetFromSP() const
    {
        ASSERT(IsFramePointerOffsetFromSP());
        int offsetFromSP;
        offsetFromSP = x64_framePtrOffset * 0x10;
        ASSERT(offsetFromSP >= SKEW_FOR_OFFSET_FROM_SP);
        offsetFromSP -= SKEW_FOR_OFFSET_FROM_SP;

        return offsetFromSP;
    }

    int GetFramePointerReg()
    {
        return RN_EBP;
    }
    
    bool HasSavedXmmRegs()
    {
        return x64_hasSavedXmmRegs != 0;
    }

    UInt16 GetSavedXmmRegMask()
    {
        ASSERT(x64_hasSavedXmmRegs);
        return x64_savedXmmRegMask;
    }
#elif defined(_TARGET_X86_)
    int GetReturnPopSize() // returned in bytes
    {
        if (!x86_argCountIsLarge)
        {
            return x86_argCountLow * POINTER_SIZE;
        }
        return ((x86_argCountHigh << 5) | x86_argCountLow) * POINTER_SIZE;
    }

    bool HasStackChanges()
    {
        return !!x86_hasStackChanges;
    }
#endif

    int GetFrameSize() const
    {
        return frameSize * POINTER_SIZE;
    }


    int GetReversePinvokeFrameOffset()
    {
#if defined(_TARGET_ARM_) || defined(_TARGET_AMD64_) || defined(_TARGET_ARM64_)
        // The offset can be either positive or negative on ARM.
        Int32 offsetInBytes;
        UInt32 uEncodedVal = reversePinvokeFrameOffset;
        bool isNeg = ((uEncodedVal & 1) == 1);
        offsetInBytes = (uEncodedVal >> 1) * POINTER_SIZE;
        offsetInBytes = isNeg ? -offsetInBytes : offsetInBytes;
        return offsetInBytes;
#elif defined(_TARGET_X86_)
        // it's always at "EBP - something", so we encode it as a positive 
        // number and then apply the negative here.
        int unsignedOffset = reversePinvokeFrameOffset * POINTER_SIZE;
        return -unsignedOffset;
#else
        ASSERT(!"NYI");
#endif
    }

    CalleeSavedRegMask GetSavedRegs()
    {
        return (CalleeSavedRegMask) calleeSavedRegMask;
    }

    bool IsRegSaved(CalleeSavedRegMask reg) const
    {
        return (0 != (calleeSavedRegMask & reg));
    }

#ifdef _TARGET_ARM_
    bool AreParmRegsPushed()
    {
        return arm_parmRegsPushedSet != 0;
    }

    UInt16 ParmRegsPushedCount()
    {
        UInt8 set = arm_parmRegsPushedSet;
        UInt8 count = 0;
        while (set != 0)
        {
            count += set & 1;
            set >>= 1;
        }
        return count;
    }

    UInt8 GetVfpRegFirstPushed()
    {
        return arm_vfpRegFirstPushed;
    }

    UInt8 GetVfpRegPushedCount()
    {
        return arm_vfpRegPushedCount;
    }
#elif defined(_TARGET_ARM64_)
    UInt8 ParmRegsPushedCount()
    {
        return arm64_parmRegsPushedCount;
    }

    UInt8 GetVfpRegsPushedMask()
    {
        return arm64_vfpRegsPushedMask;
    }
#endif

    //
    // ENCODING HELPERS
    //
#ifndef DACCESS_COMPILE
    size_t EncodeHeader(UInt8 * & pDest)
    {
#ifdef _DEBUG
        UInt8 * pStart = pDest;
#endif // _DEBUG

#if defined(_TARGET_ARM64_)
        UInt8 calleeSavedRegMaskHigh = calleeSavedRegMask >> NUM_PRESERVED_REGS_LOW;
        arm64_calleeSavedRegMaskLow = calleeSavedRegMask & MASK_PRESERVED_REGS_LOW;
        if (calleeSavedRegMaskHigh)
        {
            arm64_longCsrMask = 1;
        }
#endif

        size_t size = EC_SizeOfFixedHeader;
        if (pDest)
        {
            memcpy(pDest, this, EC_SizeOfFixedHeader);
            pDest += EC_SizeOfFixedHeader;
        }

        if (hasFrameSize)
            size += WriteUnsigned(pDest, frameSize);

        if (returnKind == MRK_ReturnsToNative)
            size += WriteUnsigned(pDest, reversePinvokeFrameOffset);

#ifdef _TARGET_AMD64_
        if (x64_framePtrOffsetSmall == 0x3)
            size += WriteUnsigned(pDest, x64_framePtrOffset);

        if (x64_hasSavedXmmRegs)
        {
            ASSERT((x64_savedXmmRegMask & 0x3f) == 0);
            UInt32 encodedValue = x64_savedXmmRegMask >> 6;
            size += WriteUnsigned(pDest, encodedValue);
        }
#elif defined(_TARGET_X86_)
        if (x86_argCountIsLarge)
        {
            size += 1;
            if (pDest)
                *pDest++ = x86_argCountHigh;
        }
        ASSERT(!x86_hasStackChanges || !"NYI -- stack changes for ESP frames");
#elif defined(_TARGET_ARM_)
        if (arm_areParmOrVfpRegsPushed)
        {
            // we encode a bit field where the low 4 bits represent the pushed parameter register
            // set, the next 8 bits are the number of pushed floating point registers, and the highest
            // bits are the first pushed floating point register plus 1. 
            // The 0 encoding means the first floating point register is 8 as this is the most frequent.
            UInt32 encodedValue = arm_parmRegsPushedSet | (arm_vfpRegPushedCount << 4);
            // usually, the first pushed floating point register is d8
            if (arm_vfpRegFirstPushed != 8)
                encodedValue |= (arm_vfpRegFirstPushed+1) << (8+4);

            size += WriteUnsigned(pDest, encodedValue);
        }
#elif defined(_TARGET_ARM64_)
        if (calleeSavedRegMaskHigh)
        {
            size += 1;
            if (pDest)
                *pDest++ = calleeSavedRegMaskHigh;
        }

        if (arm64_areParmOrVfpRegsPushed)
        {
            // At present arm64_parmRegsPushedCount is non-zero only for variadic functions, so place this field higher
            UInt32 encodedValue = arm64_vfpRegsPushedMask | (arm64_parmRegsPushedCount << 8);
            size += WriteUnsigned(pDest, encodedValue);
        }
#endif

        // encode dynamic alignment and GS cookie information
        if (hasExtraData)
        {
            size += WriteUnsigned(pDest, extraDataHeader);
        }
        if (HasDynamicAlignment())
        {
            size += WriteUnsigned(pDest, paramPointerReg);
        }
        if (hasGSCookie)
        {
            size += WriteUnsigned(pDest, gsCookieOffset);
        }

        if (epilogCountSmall == EC_MaxEpilogCountSmall)
        {
            size += WriteUnsigned(pDest, epilogCount);
        }

        // WARNING: 
        // WARNING: Do not add fields to the file-format after the funclet header encodings -- these are
        // WARNING: decoded recursively and 'in-place' when looking for the info associated with a funclet.
        // WARNING: Therefore, in that case, we cannot easily continue to decode things associated with the 
        // WARNING: main body GCInfoHeader once we start this recursive decode.
        // WARNING: 
        size += EncodeFuncletInfo(pDest);

#ifdef _DEBUG
        ASSERT(!pDest || (size == (size_t)(pDest - pStart)));
#endif // _DEBUG

        return size;
    }

    size_t WriteUnsigned(UInt8 * & pDest, UInt32 value)
    {
        size_t size = (size_t)VarInt::WriteUnsigned(pDest, value);
        pDest = pDest ? (pDest + size) : pDest;
        return size;
    }

    size_t EncodeFuncletInfo(UInt8 * & pDest)
    {
        UNREFERENCED_PARAMETER(pDest);
        size_t size = 0;
#if defined(BINDER)
        if (hasFunclets)
        {
            UInt32 nFunclets = 0;
            for (GCInfoHeader * pCur = pNextFunclet; pCur != NULL; pCur = pCur->pNextFunclet)
                nFunclets++;

            // first write out the number of funclets
            size += WriteUnsigned(pDest, nFunclets);

            // cbThisCodeBody is the size, but what we end up encoding is the size of all the code bodies 
            // except for the last one (because the last one's size can be implicitly figured out by the size
            // of the method).  So we have to save the size of the 'main body' and not the size of the last
            // funclet.  In the encoding, this will look like the offset of a given funclet from the start of
            // the previous code body.  We like relative offsets because they'll encode to be smaller.
            for (GCInfoHeader * pCur = this; pCur->pNextFunclet != NULL; pCur = pCur->pNextFunclet)
                size += WriteUnsigned(pDest, pCur->cbThisCodeBody);

            // now encode all the funclet headers
            for (GCInfoHeader * pCur = pNextFunclet; pCur != NULL; pCur = pCur->pNextFunclet)
                size += pCur->EncodeHeader(pDest);
        }
#else // BINDER
        ASSERT(!"NOT REACHABLE");
#endif // BINDER
        return size;
    }
#endif // DACCESS_COMPILE

    UInt16 ToUInt16(UInt32 val)
    {
        UInt16 result = (UInt16)val;
        ASSERT(val == result);
        return result;
    }

    UInt8 ToUInt8(UInt32 val)
    {
        UInt8 result = (UInt8)val;
        ASSERT(val == result);
        return result;
    }

    //
    // DECODING HELPERS
    //
    // Returns a pointer to the 'stack change string' on x86.
    PTR_UInt8 DecodeHeader(UInt32 methodOffset, PTR_UInt8 pbHeaderEncoding, size_t* pcbHeader)
    {
        PTR_UInt8 pbStackChangeString = NULL;

        TADDR pbTemp = PTR_TO_TADDR(pbHeaderEncoding);
        memcpy(this, PTR_READ(pbTemp, EC_SizeOfFixedHeader), EC_SizeOfFixedHeader);

        PTR_UInt8 pbDecode = pbHeaderEncoding + EC_SizeOfFixedHeader;
        frameSize = hasFrameSize 
            ? VarInt::ReadUnsigned(pbDecode)
            : 0;

        reversePinvokeFrameOffset = (returnKind == MRK_ReturnsToNative) 
            ? VarInt::ReadUnsigned(pbDecode)
            : 0;

#ifdef _TARGET_AMD64_
        x64_framePtrOffset = (x64_framePtrOffsetSmall == 0x3)
            ? ToUInt8(VarInt::ReadUnsigned(pbDecode))
            : x64_framePtrOffsetSmall + 3;


        x64_savedXmmRegMask = 0;
        if (x64_hasSavedXmmRegs)
        {
            UInt32 encodedValue = VarInt::ReadUnsigned(pbDecode);
            ASSERT((encodedValue & ~0x3ff) == 0);
            x64_savedXmmRegMask = ToUInt16(encodedValue << 6);
        }

#elif defined(_TARGET_X86_)
        if (x86_argCountIsLarge)
            x86_argCountHigh = *pbDecode++;
        else
            x86_argCountHigh = 0;

        if (x86_hasStackChanges)
        {
            pbStackChangeString = pbDecode;

            bool last = false;
            while (!last)
            {
                UInt8 b = *pbDecode++;
                // 00111111 {delta}     forwarder
                // 00dddddd             push 1, dddddd = delta
                // nnnldddd             pop nnn-1, l = last, dddd = delta (nnn=0 and nnn=1 are disallowed)
                if (b == 0x3F)
                {
                    // 00111111 {delta}     forwarder
                    VarInt::ReadUnsigned(pbDecode);
                }
                else if (0 != (b & 0xC0))
                {
                    // nnnldddd             pop nnn-1, l = last, dddd = delta (nnn=0 and nnn=1 are disallowed)
                    last = ((b & 0x10) == 0x10);
                }
            }
        }
#elif defined(_TARGET_ARM_)
        arm_parmRegsPushedSet = 0;
        arm_vfpRegPushedCount = 0;
        arm_vfpRegFirstPushed = 0;
        if (arm_areParmOrVfpRegsPushed)
        {
            UInt32 encodedValue = VarInt::ReadUnsigned(pbDecode);
            arm_parmRegsPushedSet = encodedValue & 0x0f;
            arm_vfpRegPushedCount = (UInt8)(encodedValue >> 4);
            UInt32 vfpRegFirstPushed = encodedValue >> (8 + 4);
            if (vfpRegFirstPushed == 0)
                arm_vfpRegFirstPushed = 8;
            else
                arm_vfpRegFirstPushed = (UInt8)(vfpRegFirstPushed - 1);
        }
#elif defined(_TARGET_ARM64_)
        calleeSavedRegMask = arm64_calleeSavedRegMaskLow;
        if (arm64_longCsrMask)
        {
            calleeSavedRegMask |= (*pbDecode++ << NUM_PRESERVED_REGS_LOW);
        }

        arm64_parmRegsPushedCount = 0;
        arm64_vfpRegsPushedMask = 0;
        if (arm64_areParmOrVfpRegsPushed)
        {
            UInt32 encodedValue = VarInt::ReadUnsigned(pbDecode);
            arm64_vfpRegsPushedMask = (UInt8)encodedValue;
            arm64_parmRegsPushedCount = (UInt8)(encodedValue >> 8);
            ASSERT(arm64_parmRegsPushedCount <= 8);
        }
#endif

        extraDataHeader = hasExtraData ? ToUInt8(VarInt::ReadUnsigned(pbDecode)) : 0;
        paramPointerReg = HasDynamicAlignment() ? ToUInt8(VarInt::ReadUnsigned(pbDecode)) : (UInt8)RN_NONE;
        gsCookieOffset = hasGSCookie ? VarInt::ReadUnsigned(pbDecode) : 0;

        epilogCount = epilogCountSmall < EC_MaxEpilogCountSmall ? epilogCountSmall : ToUInt16(VarInt::ReadUnsigned(pbDecode));

        this->funcletOffset = 0;
        if (hasFunclets)
        {
            // WORKAROUND: Epilog tables are still per-method instead of per-funclet, but we don't deal with
            //             them here.  So we will simply overwrite the funclet's epilogAtEnd and epilogCount
            //             with the values from the main code body -- these were the values used to generate
            //             the per-method epilog table, so at least we're consistent with what is encoded.
            UInt8  mainEpilogAtEnd      = epilogAtEnd;
            UInt16 mainEpilogCount      = epilogCount;
            UInt16 mainFixedEpilogSize  = fixedEpilogSize;  // Either in bytes or in instructions
            UInt8  mainHasCommonVars    = hasCommonVars;
            // -------

            int nFunclets = (int)VarInt::ReadUnsigned(pbDecode);
            int idxFunclet = -2;
            UInt32 offsetFunclet = 0;
            // Decode the funclet start offsets, remembering which one is of interest.
            UInt32 prevFuncletStart = 0;
            for (int i = 0; i < nFunclets; i++)
            {
                UInt32 offsetThisFunclet = prevFuncletStart + VarInt::ReadUnsigned(pbDecode);
                if ((idxFunclet == -2) && (methodOffset < offsetThisFunclet))
                {
                    idxFunclet = (i - 1);
                    offsetFunclet = prevFuncletStart;
                }
                prevFuncletStart = offsetThisFunclet; 
            }
            if ((idxFunclet == -2) && (methodOffset >= prevFuncletStart))
            {
                idxFunclet = (nFunclets - 1);
                offsetFunclet = prevFuncletStart;
            }

            // Now decode headers until we find the one we want.  Keep decoding if we need to report a size.
            if (pcbHeader || (idxFunclet >= 0))
            {
                for (int i = 0; i < nFunclets; i++)
                {
                    size_t hdrSize;
                    if (i == idxFunclet)
                    {
                        this->DecodeHeader(methodOffset, pbDecode, &hdrSize);
                        pbDecode += hdrSize;
                        this->funcletOffset = offsetFunclet;
                        if (!pcbHeader) // if nobody is going to look at the header size, we don't need to keep going
                            break;
                    }
                    else
                    {
                        // keep decoding into a temp just to get the right header size
                        GCInfoHeader tmp;
                        tmp.DecodeHeader(methodOffset, pbDecode, &hdrSize);
                        pbDecode += hdrSize;
                    }
                }
            }

            // WORKAROUND: see above
            this->epilogAtEnd      = mainEpilogAtEnd;
            this->epilogCount      = mainEpilogCount;
            this->PokeFixedEpilogSize(mainFixedEpilogSize);
            this->hasCommonVars    = mainHasCommonVars;

            // -------
        }

        // WARNING: 
        // WARNING: Do not add fields to the file-format after the funclet header encodings -- these are
        // WARNING: decoded recursively and 'in-place' when looking for the info associated with a funclet.
        // WARNING: Therefore, in that case, we cannot easily continue to decode things associated with the 
        // WARNING: main body GCInfoHeader once we start this recursive decode.
        // WARNING: 

        if (pcbHeader)
            *pcbHeader = pbDecode - pbHeaderEncoding;

        return pbStackChangeString;
    }

    void GetFuncletInfo(PTR_UInt8 pbHeaderEncoding, UInt32* pnFuncletsOut, PTR_UInt8* pEncodedFuncletStartOffsets)
    {
        ASSERT(hasFunclets);

        PTR_UInt8 pbDecode = pbHeaderEncoding + EC_SizeOfFixedHeader;
        if (hasFrameSize) { VarInt::SkipUnsigned(pbDecode); }
        if (returnKind == MRK_ReturnsToNative)  { VarInt::SkipUnsigned(pbDecode); }
        if (hasExtraData) { VarInt::SkipUnsigned(pbDecode); }
        if (HasDynamicAlignment()) { VarInt::SkipUnsigned(pbDecode); }
        if (hasGSCookie) { VarInt::SkipUnsigned(pbDecode); }

#ifdef _TARGET_AMD64_
        if (x64_framePtrOffsetSmall == 0x3) { VarInt::SkipUnsigned(pbDecode); }
#elif defined(_TARGET_X86_)
        if (x86_argCountIsLarge)
            pbDecode++;

        if (x86_hasStackChanges)
        {
            bool last = false;
            while (!last)
            {
                UInt8 b = *pbDecode++;
                // 00111111 {delta}     forwarder
                // 00dddddd             push 1, dddddd = delta
                // nnnldddd             pop nnn-1, l = last, dddd = delta (nnn=0 and nnn=1 are disallowed)
                if (b == 0x3F)
                {
                    // 00111111 {delta}     forwarder
                    VarInt::SkipUnsigned(pbDecode);
                }
                else if (0 != (b & 0xC0))
                {
                    // nnnldddd             pop nnn-1, l = last, dddd = delta (nnn=0 and nnn=1 are disallowed)
                    last = ((b & 0x10) == 0x10);
                }
            }
        }
#elif defined(_TARGET_ARM_)
        if (arm_areParmOrVfpRegsPushed) { VarInt::SkipUnsigned(pbDecode); }
#elif defined(_TARGET_ARM64_)
        if (arm64_longCsrMask) { pbDecode++; }
        if (arm64_areParmOrVfpRegsPushed) { VarInt::SkipUnsigned(pbDecode); }
#endif

        *pnFuncletsOut = VarInt::ReadUnsigned(pbDecode);
        *pEncodedFuncletStartOffsets = pbDecode;
    }

#ifdef BINDER
    bool IsOffsetInFunclet(UInt32 offset)
    {
        if (!hasFunclets)
            return false;

        return (offset >= cbThisCodeBody);
    }
#endif // BINDER

    bool IsValidEpilogOffset(UInt32 epilogOffset, UInt32 epilogSize)
    {
        if (!this->HasVaryingEpilogSizes())
            return (epilogOffset < this->GetFixedEpilogSize());
        else
            return (epilogOffset < epilogSize);
    }

#ifdef RHDUMP
    char const * GetBoolStr(bool val) { return val ? " true" : "false"; }

    char const * GetRetKindStr(MethodReturnKind kind)
    {
        switch (kind)
        {
        case MRK_ReturnsScalar:     return "scalar";
        case MRK_ReturnsObject:     return "object";
        case MRK_ReturnsByref:      return "byref";
        case MRK_ReturnsToNative:   return "native";
#if defined(_TARGET_ARM64_)
        case MRK_Scalar_Obj:        return "{scalar, object}";
        case MRK_Scalar_Byref:      return "{scalar, byref}";
        case MRK_Obj_Obj:           return "{object, object}";
        case MRK_Obj_Byref:         return "{object, byref}";
        case MRK_Byref_Obj:         return "{byref, object}";
        case MRK_Byref_Byref:       return "{byref, byref}";
#endif // defined(_TARGET_ARM64_)
        default:                    return "unknown";
        }
    }

#define PRINT_CALLEE_SAVE(name, mask, val) {if ((val) & (mask)) { printf(name); }}

    void PrintCalleeSavedRegs(UInt32 calleeSavedRegMask)
    {
#ifdef _TARGET_ARM_
        PRINT_CALLEE_SAVE(" r4" , CSR_MASK_R4 , calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r5" , CSR_MASK_R5 , calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r6" , CSR_MASK_R6 , calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r7" , CSR_MASK_R7 , calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r8" , CSR_MASK_R8 , calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r9" , CSR_MASK_R9 , calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r10", CSR_MASK_R10, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r11", CSR_MASK_R11, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" lr" , CSR_MASK_LR , calleeSavedRegMask);
#elif defined(_TARGET_ARM64_)
        PRINT_CALLEE_SAVE(" lr" , CSR_MASK_LR , calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x19", CSR_MASK_X19, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x20", CSR_MASK_X20, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x21", CSR_MASK_X21, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x22", CSR_MASK_X22, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x23", CSR_MASK_X23, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x24", CSR_MASK_X24, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x25", CSR_MASK_X25, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x26", CSR_MASK_X26, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x27", CSR_MASK_X27, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" x28", CSR_MASK_X28, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" fp" , CSR_MASK_FP , calleeSavedRegMask);
#elif defined(_TARGET_X86_)
        PRINT_CALLEE_SAVE(" ebx", CSR_MASK_RBX, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" esi", CSR_MASK_RSI, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" edi", CSR_MASK_RDI, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" ebp", CSR_MASK_RBP, calleeSavedRegMask);
#elif defined(_TARGET_AMD64_)
        PRINT_CALLEE_SAVE(" rbx", CSR_MASK_RBX, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" rsi", CSR_MASK_RSI, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" rdi", CSR_MASK_RDI, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" rbp", CSR_MASK_RBP, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r12", CSR_MASK_R12, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r13", CSR_MASK_R13, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r14", CSR_MASK_R14, calleeSavedRegMask);
        PRINT_CALLEE_SAVE(" r15", CSR_MASK_R15, calleeSavedRegMask);
#else
#error unknown architecture
#endif
    }

    void PrintRegNumber(UInt8 regNumber)
    {
        switch (regNumber)
        {
        default: printf("???"); break;
#ifdef _TARGET_ARM_
        case RN_R0:     printf(" r0"); break;
        case RN_R1:     printf(" r1"); break;
        case RN_R2:     printf(" r2"); break;
        case RN_R3:     printf(" r3"); break;
        case RN_R4:     printf(" r4"); break;
        case RN_R5:     printf(" r5"); break;
        case RN_R6:     printf(" r6"); break;
        case RN_R7:     printf(" r7"); break;
        case RN_R8:     printf(" r8"); break;
        case RN_R9:     printf(" r9"); break;
        case RN_R10:    printf("r10"); break;
        case RN_R11:    printf("r11"); break;
        case RN_R12:    printf("r12"); break;
        case RN_SP:     printf(" sp"); break;
        case RN_LR:     printf(" lr"); break;
        case RN_PC:     printf(" pc"); break;
#elif defined(_TARGET_ARM64_)
        case RN_X0:     printf(" x0"); break;
        case RN_X1:     printf(" x1"); break;
        case RN_X2:     printf(" x2"); break;
        case RN_X3:     printf(" x3"); break;
        case RN_X4:     printf(" x4"); break;
        case RN_X5:     printf(" x5"); break;
        case RN_X6:     printf(" x6"); break;
        case RN_X7:     printf(" x7"); break;
        case RN_X8:     printf(" x8"); break;
        case RN_X9:     printf(" x9"); break;
        case RN_X10:    printf("x10"); break;
        case RN_X11:    printf("x11"); break;
        case RN_X12:    printf("x12"); break;
        case RN_X13:    printf("x13"); break;
        case RN_X14:    printf("x14"); break;
        case RN_X15:    printf("x15"); break;

        case RN_XIP0:   printf("xip0"); break;
        case RN_XIP1:   printf("xip1"); break;
        case RN_XPR:    printf("xpr"); break;

        case RN_X19:    printf("x19"); break;
        case RN_X20:    printf("x20"); break;
        case RN_X21:    printf("x21"); break;
        case RN_X22:    printf("x22"); break;
        case RN_X23:    printf("x23"); break;
        case RN_X24:    printf("x24"); break;
        case RN_X25:    printf("x25"); break;
        case RN_X26:    printf("x26"); break;
        case RN_X27:    printf("x27"); break;
        case RN_X28:    printf("x28"); break;

        case RN_FP:     printf(" fp"); break;
        case RN_LR:     printf(" lr"); break;
        case RN_SP:     printf(" sp"); break;
#elif defined(_TARGET_X86_)
        case RN_EAX:    printf("eax"); break;
        case RN_ECX:    printf("ecx"); break;
        case RN_EDX:    printf("edx"); break;
        case RN_EBX:    printf("ebx"); break;
        case RN_ESP:    printf("esp"); break;
        case RN_EBP:    printf("ebp"); break;
        case RN_ESI:    printf("esi"); break;
        case RN_EDI:    printf("edi"); break;
#elif defined(_TARGET_AMD64_)
        case RN_EAX:    printf("rax"); break;
        case RN_ECX:    printf("rcx"); break;
        case RN_EDX:    printf("rdx"); break;
        case RN_EBX:    printf("rbx"); break;
        case RN_ESP:    printf("rsp"); break;
        case RN_EBP:    printf("rbp"); break;
        case RN_ESI:    printf("rsi"); break;
        case RN_EDI:    printf("rdi"); break;
        case RN_R8:     printf(" r8"); break;
        case RN_R9:     printf(" r9"); break;
        case RN_R10:    printf("r10"); break;
        case RN_R11:    printf("r11"); break;
        case RN_R12:    printf("r12"); break;
        case RN_R13:    printf("r13"); break;
        case RN_R14:    printf("r14"); break;
        case RN_R15:    printf("r15"); break;
#else
#error unknown architecture
#endif
        }
    }

    void Dump()
    {
        printf("  | prologSize:   %02X""  | epilogSize:    %02X""  | epilogCount:    %02X""  | epilogAtEnd:  %s\n",
            GetPrologSize(), HasVaryingEpilogSizes() ? 0 : GetFixedEpilogSize(), epilogCount, GetBoolStr(epilogAtEnd));
        printf("  | frameSize:  %04X""  | ebpFrame:   %s""  | hasFunclets: %s""  | returnKind:  %s\n",
            GetFrameSize(), GetBoolStr(ebpFrame), GetBoolStr(hasFunclets), GetRetKindStr(GetReturnKind()));
        printf("  | regMask:    %04X"  "  {", calleeSavedRegMask);
        PrintCalleeSavedRegs(calleeSavedRegMask);
        printf(" }\n");
        if (HasDynamicAlignment())
        {
            printf("  | stackAlign:   %02X""  | paramPtrReg:  ", GetDynamicAlignment());
            PrintRegNumber(paramPointerReg);
            printf("\n");
        }
        if (hasGSCookie)
        {
            printf("  | gsCookieOffset:   %04X\n", GetGSCookieOffset());
        }
#ifdef _TARGET_ARM_
        if (arm_areParmOrVfpRegsPushed)
        {
            if (arm_parmRegsPushedSet != 0)
            {
                printf("  | parmRegs:     %02X  {", arm_parmRegsPushedSet);
                PRINT_CALLEE_SAVE(" r0", SR_MASK_R0, arm_parmRegsPushedSet);
                PRINT_CALLEE_SAVE(" r1", SR_MASK_R1, arm_parmRegsPushedSet);
                PRINT_CALLEE_SAVE(" r2", SR_MASK_R2, arm_parmRegsPushedSet);
                PRINT_CALLEE_SAVE(" r3", SR_MASK_R3, arm_parmRegsPushedSet);
                printf(" }\n");
            }
            if (arm_vfpRegPushedCount != 0)
            {
                printf("  | vfpRegs:    %d(%d)  {", arm_vfpRegFirstPushed, arm_vfpRegPushedCount);
                printf(" d%d", arm_vfpRegFirstPushed);
                if (arm_vfpRegPushedCount > 1)
                    printf("-d%d", arm_vfpRegFirstPushed + arm_vfpRegPushedCount - 1);
                printf(" }\n");
            }
        }
#elif defined(_TARGET_ARM64_)
        if (arm64_areParmOrVfpRegsPushed)
        {
            if (arm64_parmRegsPushedCount != 0)
            {
                printf("  | parmRegsCount: %d\n", arm64_parmRegsPushedCount);
            }
            if (arm64_vfpRegsPushedMask != 0)
            {
                printf("  | vfpRegs:      %02X  {", arm64_vfpRegsPushedMask);
                for (int reg = 0; reg < 8; reg++)
                {
                    if (arm64_vfpRegsPushedMask & (1 << reg))
                        printf(" d%d", reg + 8);
                }
                printf(" }\n");
            }
        }
#elif defined(_TARGET_AMD64_)
        if (x64_hasSavedXmmRegs)
        {
            printf("  | xmmRegs:    %04X  {", x64_savedXmmRegMask);
            for (int reg = 6; reg < 16; reg++)
            {
                if (x64_savedXmmRegMask & (1<<reg))
                    printf(" xmm%d", reg);
            }
            printf(" }\n");
        }
#endif // _TARGET_ARM_

        // x64_framePtrOffsetSmall / [opt] x64_framePtrOffset
        // x86_argCountLow [opt] x86_argCountHigh
        // x86_argCountIsLarge
        // x86_hasStackChanges
        // [opt] reversePinvokeFrameOffset
        // [opt] numFunclets
    }
#endif // RHDUMP
};



/*****************************************************************************/
#endif //_GCINFO_H_
/*****************************************************************************/
