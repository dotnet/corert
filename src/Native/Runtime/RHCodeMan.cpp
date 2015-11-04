//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "assert.h"
#include "CommonMacros.inl"
#include "regdisplay.h"
#include "TargetPtrs.h"
#include "eetype.h"
#include "ObjectLayout.h"
#include "varint.h"

#include "gcinfo.h"
#include "RHCodeMan.h"

#include "ICodeManager.h"


// Ensure that EEMethodInfo fits into the space reserved by MethodInfo
STATIC_ASSERT(sizeof(EEMethodInfo) <= sizeof(MethodInfo));

EEMethodInfo * GetEEMethodInfo(MethodInfo * pMethodInfo)
{
    return (EEMethodInfo *)pMethodInfo;
}

inline void ReportObject(GCEnumContext * hCallback, PTR_PTR_Object p, UInt32 flags)
{
    (hCallback->pCallback)(hCallback, (PTR_PTR_VOID)p, flags);
}

//
// This template is used to map from the CalleeSavedRegNum enum to the correct field in the REGDISPLAY struct.
// It should compile away to simply an inlined field access.  Since we intentionally have conditionals that 
// are constant at compile-time, we need to disable the level-4 warning related to that.
//
#ifdef TARGET_ARM

#pragma warning(push)
#pragma warning(disable:4127)   // conditional expression is constant
template <CalleeSavedRegNum regNum>
PTR_PTR_Object GetRegObjectAddr(REGDISPLAY * pContext)
{
    switch (regNum)
    {
    case CSR_NUM_R4:    return (PTR_PTR_Object)pContext->pR4;
    case CSR_NUM_R5:    return (PTR_PTR_Object)pContext->pR5;
    case CSR_NUM_R6:    return (PTR_PTR_Object)pContext->pR6;
    case CSR_NUM_R7:    return (PTR_PTR_Object)pContext->pR7;
    case CSR_NUM_R8:    return (PTR_PTR_Object)pContext->pR8;
    case CSR_NUM_R9:    return (PTR_PTR_Object)pContext->pR9;
    case CSR_NUM_R10:   return (PTR_PTR_Object)pContext->pR10;
    case CSR_NUM_R11:   return (PTR_PTR_Object)pContext->pR11;
    // NOTE: LR is omitted because it may not be live except as a 'scratch' reg
    }
    UNREACHABLE_MSG("unexpected CalleeSavedRegNum");
}
#pragma warning(pop)

PTR_PTR_Object GetRegObjectAddr(CalleeSavedRegNum regNum, REGDISPLAY * pContext)
{
    switch (regNum)
    {
    case CSR_NUM_R4:    return (PTR_PTR_Object)pContext->pR4;
    case CSR_NUM_R5:    return (PTR_PTR_Object)pContext->pR5;
    case CSR_NUM_R6:    return (PTR_PTR_Object)pContext->pR6;
    case CSR_NUM_R7:    return (PTR_PTR_Object)pContext->pR7;
    case CSR_NUM_R8:    return (PTR_PTR_Object)pContext->pR8;
    case CSR_NUM_R9:    return (PTR_PTR_Object)pContext->pR9;
    case CSR_NUM_R10:   return (PTR_PTR_Object)pContext->pR10;
    case CSR_NUM_R11:   return (PTR_PTR_Object)pContext->pR11;
    // NOTE: LR is omitted because it may not be live except as a 'scratch' reg
    }
    UNREACHABLE_MSG("unexpected CalleeSavedRegNum");
}

PTR_PTR_Object GetScratchRegObjectAddr(ScratchRegNum regNum, REGDISPLAY * pContext)
{
    switch (regNum)
    {
    case SR_NUM_R0:     return (PTR_PTR_Object)pContext->pR0;
    case SR_NUM_R1:     return (PTR_PTR_Object)pContext->pR1;
    case SR_NUM_R2:     return (PTR_PTR_Object)pContext->pR2;
    case SR_NUM_R3:     return (PTR_PTR_Object)pContext->pR3;
    case SR_NUM_R12:    return (PTR_PTR_Object)pContext->pR12;
    case SR_NUM_LR:     return (PTR_PTR_Object)pContext->pLR;
    }
    UNREACHABLE_MSG("unexpected ScratchRegNum");
}

void ReportRegisterSet(UInt8 regSet, REGDISPLAY * pContext, GCEnumContext * hCallback)
{
    // 2.  00lRRRRR - normal "register set" encoding, pinned and interior attributes both false
    //      a.  l - this is the last descriptor
    //      b.  RRRRR - this is the register mask for { r4, r5, r6, r7, r8 }

    if (regSet & CSR_MASK_R4) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_R4>(pContext), 0); }
    if (regSet & CSR_MASK_R5) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_R5>(pContext), 0); }
    if (regSet & CSR_MASK_R6) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_R6>(pContext), 0); }
    if (regSet & CSR_MASK_R7) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_R7>(pContext), 0); }
    if (regSet & CSR_MASK_R8) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_R8>(pContext), 0); }
}



#else // TARGET_ARM

#pragma warning(push)
#pragma warning(disable:4127)   // conditional expression is constant
template <CalleeSavedRegNum regNum>
PTR_PTR_Object GetRegObjectAddr(REGDISPLAY * pContext)
{
    switch (regNum)
    {
    case CSR_NUM_RBX:  return (PTR_PTR_Object)pContext->pRbx;
    case CSR_NUM_RSI:  return (PTR_PTR_Object)pContext->pRsi;
    case CSR_NUM_RDI:  return (PTR_PTR_Object)pContext->pRdi;
    case CSR_NUM_RBP:  return (PTR_PTR_Object)pContext->pRbp;
#ifdef TARGET_AMD64
    case CSR_NUM_R12:  return (PTR_PTR_Object)pContext->pR12;
    case CSR_NUM_R13:  return (PTR_PTR_Object)pContext->pR13;
    case CSR_NUM_R14:  return (PTR_PTR_Object)pContext->pR14;
    case CSR_NUM_R15:  return (PTR_PTR_Object)pContext->pR15;
#endif // TARGET_AMD64
    }
    UNREACHABLE_MSG("unexpected CalleeSavedRegNum");
}
#pragma warning(pop)

PTR_PTR_Object GetRegObjectAddr(CalleeSavedRegNum regNum, REGDISPLAY * pContext)
{
    switch (regNum)
    {
    case CSR_NUM_RBX:  return (PTR_PTR_Object)pContext->pRbx;
    case CSR_NUM_RSI:  return (PTR_PTR_Object)pContext->pRsi;
    case CSR_NUM_RDI:  return (PTR_PTR_Object)pContext->pRdi;
    case CSR_NUM_RBP:  return (PTR_PTR_Object)pContext->pRbp;
#ifdef TARGET_AMD64
    case CSR_NUM_R12:  return (PTR_PTR_Object)pContext->pR12;
    case CSR_NUM_R13:  return (PTR_PTR_Object)pContext->pR13;
    case CSR_NUM_R14:  return (PTR_PTR_Object)pContext->pR14;
    case CSR_NUM_R15:  return (PTR_PTR_Object)pContext->pR15;
#endif // TARGET_AMD64
    }
    UNREACHABLE_MSG("unexpected CalleeSavedRegNum");
}

PTR_PTR_Object GetScratchRegObjectAddr(ScratchRegNum regNum, REGDISPLAY * pContext)
{
    switch (regNum)
    {
    case SR_NUM_RAX:  return (PTR_PTR_Object)pContext->pRax;
    case SR_NUM_RCX:  return (PTR_PTR_Object)pContext->pRcx;
    case SR_NUM_RDX:  return (PTR_PTR_Object)pContext->pRdx;
#ifdef TARGET_AMD64
    case SR_NUM_R8 :  return (PTR_PTR_Object)pContext->pR8;
    case SR_NUM_R9 :  return (PTR_PTR_Object)pContext->pR9;
    case SR_NUM_R10:  return (PTR_PTR_Object)pContext->pR10;
    case SR_NUM_R11:  return (PTR_PTR_Object)pContext->pR11;
#endif // TARGET_AMD64
    }
    UNREACHABLE_MSG("unexpected ScratchRegNum");
}

void ReportRegisterSet(UInt8 regSet, REGDISPLAY * pContext, GCEnumContext * hCallback)
{
    // 2.  00lRRRRR - normal "register set" encoding, pinned and interior attributes both false
    //      a.  l - this is the last descriptor
    //      b.  RRRRR - this is the register mask for { rbx, rsi, rdi, rbp, r12 }

    if (regSet & CSR_MASK_RBX) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_RBX>(pContext), 0); }
    if (regSet & CSR_MASK_RSI) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_RSI>(pContext), 0); }
    if (regSet & CSR_MASK_RDI) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_RDI>(pContext), 0); }
    if (regSet & CSR_MASK_RBP) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_RBP>(pContext), 0); }
#ifdef TARGET_AMD64                                                           
    if (regSet & CSR_MASK_R12) { ReportObject(hCallback, GetRegObjectAddr<CSR_NUM_R12>(pContext), 0); }
#endif
}

#endif // TARGET_ARM

void ReportRegister(UInt8 regEnc, REGDISPLAY * pContext, GCEnumContext * hCallback)
{
    // 3.  01liprrr - more general register encoding with pinned and interior attributes
    //      a.  l - last descriptor
    //      b.  i - interior
    //      c.  p - pinned
    //      d.  rrr - register number { rbx, rsi, rdi, rbp, r12, r13, r14, r15 }, ARM = { r4-r11 }

    UInt32 flags = 0;
    if (regEnc & 0x08) { flags |= GC_CALL_PINNED; }
    if (regEnc & 0x10) { flags |= GC_CALL_INTERIOR; }

    PTR_PTR_Object pRoot = GetRegObjectAddr((CalleeSavedRegNum)(regEnc & 0x07), pContext);
    ReportObject(hCallback, pRoot, flags);
}

void ReportLocalSlot(UInt32 slotNum, REGDISPLAY * pContext, GCEnumContext * hCallback, GCInfoHeader * pHeader)
{
    // In order to map from a 'local slot' to a frame pointer offset, we need to consult the GCInfoHeader of
    // the main code body, but all we have is the GCInfoHeader of the funclet.  So, for now, this is 
    // disallowed.  A larger encoding must be used.
    ASSERT_MSG(!pHeader->IsFunclet(), "A 'local slot' encoding should not be used in a funclet.");

    if (pHeader->HasFramePointer())
    {
        Int32 rbpOffset;
#ifdef TARGET_ARM
        // ARM places the FP at the top of the locals area.
        rbpOffset = pHeader->GetFrameSize() - ((slotNum + 1) * sizeof(void *));
#else
#  ifdef TARGET_AMD64
        if (pHeader->GetFramePointerOffset() != 0)
            rbpOffset = (slotNum * sizeof(void *));
        else
#  endif // TARGET_AMD64
            rbpOffset = -pHeader->GetPreservedRegsSaveSize() - (slotNum * sizeof(void *));
#endif
        PTR_PTR_Object pRoot = (PTR_PTR_Object)(pContext->GetFP() + rbpOffset);
        ReportObject(hCallback, pRoot, 0);
    }
    else
    {
#ifdef TARGET_X86
        // @TODO: X86: need to pass in current stack level
        UNREACHABLE_MSG("NYI - ESP frames");
#endif // TARGET_X86

        Int32 rspOffset = pHeader->GetFrameSize() - ((slotNum + 1) * sizeof(void *));
        PTR_PTR_Object pRoot = (PTR_PTR_Object)(pContext->GetSP() + rspOffset);
        ReportObject(hCallback, pRoot, 0);
    }
}

void ReportStackSlot(bool framePointerBased, Int32 offset, UInt32 gcFlags, REGDISPLAY * pContext, 
                     GCEnumContext * hCallback, bool hasDynamicAlignment)
{
    UIntNative basePointer;
    if (framePointerBased)
    {
#ifdef TARGET_X86
        if (hasDynamicAlignment && offset >= 0)
            basePointer = pContext->GetPP();
        else
#else
            // avoid warning about unused parameter
            hasDynamicAlignment;
#endif // TARGET_X86
            basePointer = pContext->GetFP();
    }
    else
    {
        basePointer = pContext->GetSP();
    }
    PTR_PTR_Object pRoot = (PTR_PTR_Object)(basePointer + offset);
    ReportObject(hCallback, pRoot, gcFlags);
}

void ReportLocalSlots(UInt8 localsEnc, REGDISPLAY * pContext, GCEnumContext * hCallback, GCInfoHeader * pHeader)
{
    if (localsEnc & 0x10)
    {
        // 4.  10l1SSSS - "local stack slot set" encoding, pinned and interior attributes both false
        //      a.  l - last descriptor
        //      b.  SSSS - set of "local slots" #0 - #3  - local slot 0 is at offset -8 from the last pushed 
        //          callee saved register, local slot 1 is at offset - 16, etc - in other words, these are the 
        //          slots normally used for locals
        if (localsEnc & 0x01) { ReportLocalSlot(0, pContext, hCallback, pHeader); }
        if (localsEnc & 0x02) { ReportLocalSlot(1, pContext, hCallback, pHeader); }
        if (localsEnc & 0x04) { ReportLocalSlot(2, pContext, hCallback, pHeader); }
        if (localsEnc & 0x08) { ReportLocalSlot(3, pContext, hCallback, pHeader); }
    }
    else
    {
        // 5.  10l0ssss - "local slot" encoding, pinned and interior attributes are both false
        //      a.  l - last descriptor
        //      b.  ssss - "local slot" #4 - #19
        UInt32 localNum = (localsEnc & 0xF) + 4;
        ReportLocalSlot(localNum, pContext, hCallback, pHeader);
    }
}

void ReportStackSlots(UInt8 firstEncByte, REGDISPLAY * pContext, GCEnumContext * hCallback, PTR_UInt8 & pCursor, bool hasDynamicAlignment)
{
    // 6.  11lipfsm {offset} [mask] - [multiple] stack slot encoding
    //      a.  l - last descriptor
    //      b.  i - interior attribute
    //      c.  p - pinned attribute
    //      d.  f - 1: frame pointer relative, 0: sp relative
    //      e.  s - offset sign
    //      f.  m - mask follows
    //      g.  offset - variable length unsigned integer
    //      h.  mask - variable length unsigned integer (only present if m-bit is 1) - this can describe 
    //          multiple stack locations with the same attributes. E.g., if you want to describe stack 
    //          locations 0x20, 0x28, 0x38, you would give a (starting) offset of 0x20 and a mask of 
    //          000000101 = 0x05. Up to 33 stack locations can be described.

    UInt32 flags = 0;
    if (firstEncByte & 0x08) { flags |= GC_CALL_PINNED; }
    if (firstEncByte & 0x10) { flags |= GC_CALL_INTERIOR; }

    bool framePointerBased  = (firstEncByte & 0x04);
    bool isNegative         = (firstEncByte & 0x02);
    bool hasMask            = (firstEncByte & 0x01);

    Int32 offset = (Int32) VarInt::ReadUnsigned(pCursor);
    ASSERT(offset >= 0);

    ReportStackSlot(framePointerBased, (isNegative ? -offset : offset), flags, 
                    pContext, hCallback, hasDynamicAlignment);

    if (hasMask)
    {
        UInt32 mask = VarInt::ReadUnsigned(pCursor);
        while (mask != 0)
        {
            offset += sizeof(void *);
            if (mask & 0x01)
            {
                ReportStackSlot(framePointerBased, (isNegative ? -offset : offset), flags, 
                                pContext, hCallback, hasDynamicAlignment);
            }
            mask >>= 1;
        }
    }
}

void ReportScratchRegs(UInt8 firstEncByte, REGDISPLAY * pContext, GCEnumContext * hCallback, PTR_UInt8 & pCursor)
{
    // 7. 11lip010 0RRRRRRR [0IIIIIII] [0PPPPPPP] - live scratch reg reporting, this uses the SP-xxx encoding
    //                                              from #6 since we cannot have stack locations at negative
    //                                              offsets from SP.
    //      a.  l - last descriptor
    //      b.  i - interior byte present
    //      c.  p - pinned byte present
    //      d.  RRRRRRR - scratch register mask for { rax, rcx, rdx, r8, r9, r10, r11 }, ARM = { r0-r3, r12 }
    //      e.  IIIIIII - interior scratch register mask for { rax, rcx, rdx, r8, r9, r10, r11 } iff  'i' is 1
    //      f.  PPPPPPP - pinned scratch register mask for { rax, rcx, rdx, r8, r9, r10, r11 } iff  'p' is 1
    //

    UInt8 regs       = *pCursor++;
    UInt8 byrefRegs  = (firstEncByte & 0x10) ? *pCursor++ : 0;
    UInt8 pinnedRegs = (firstEncByte & 0x08) ? *pCursor++ : 0;

    for (UInt32 reg = 0; reg < RBM_SCRATCH_REG_COUNT; reg++)
    {
        UInt8 regMask = (1 << reg);

        if (regs & regMask)
        {
            UInt32 flags = 0;
            if (pinnedRegs & regMask) { flags |= GC_CALL_PINNED; }
            if (byrefRegs  & regMask) { flags |= GC_CALL_INTERIOR; }

            PTR_PTR_Object pRoot = GetScratchRegObjectAddr((ScratchRegNum)reg, pContext);
            if (pRoot != NULL)
                ReportObject(hCallback, pRoot, flags);
        }
    }
}

// Enumerate all live object references in that function using the virtual register set. Same reference 
// location cannot be enumerated multiple times (but all differenct references pointing to the same object 
// have to be individually enumerated). 
// Returns success of operation.
void EECodeManager::EnumGcRefs(EEMethodInfo *   pMethodInfo,
                               UInt32           codeOffset,
                               REGDISPLAY *     pContext,
                               GCEnumContext *  hCallback,
                               PTR_UInt8        pbCallsiteStringBlob,
                               PTR_UInt8        pbDeltaShortcutTable)
{
    PTR_UInt8 pCursor = pMethodInfo->GetGCInfo();

    // Early-out for the common case of no callsites 
    if (*pCursor == 0xFF)
        return;


    // -------------------------------------------------------------------------------------------------------
    // Decode the method GC info 
    // -------------------------------------------------------------------------------------------------------
    // 
    // This loop scans through the 'method info' to find a callsite offset which matches the incoming code 
    // offset.  Once it's found, we break out and have a pointer into the 'callsite info blob' which will
    // point at a string describing the roots that must be reported at this particular callsite.  This loop 
    // needs to be fast because it's linear with respect to the number of callsites in a method.
    //
    // -------------------------------------------------------------------------------------------------------
    //
    // 0ddddccc -- SMALL ENCODING
    // 
    //              -- dddd is an index into the delta shortcut table
    //              -- ccc is an offset into the callsite strings blob
    //
    // 1ddddddd { info offset } -- BIG ENCODING
    //
    //              -- ddddddd is a 7-bit delta
    //              -- { info offset } is a variable-length unsigned encoding of the offset into the callsite
    //                 strings blob for this callsite.
    //
    // 10000000 { delta } -- FORWARDER
    //
    //              -- { delta } is a variable-length unsigned encoding of the offset to the next callsite
    //
    // 11111111 -- STRING TERMINATOR
    //

    UInt32 callCodeOffset = codeOffset;
    UInt32 curCodeOffset = 0;
    IntNative infoOffset = 0;

    while (curCodeOffset < callCodeOffset)
    {
ContinueUnconditionally:
        UInt8 b = *pCursor++;

        if ((b & 0x80) == 0)
        {
            // SMALL ENCODING
            infoOffset = (b & 0x7);
            curCodeOffset += pbDeltaShortcutTable[b >> 3];
        }
        else
        {
            UInt8 lowBits = (b & 0x7F);
            // FORWARDER
            if (lowBits == 0)
            {
                curCodeOffset += VarInt::ReadUnsigned(pCursor);
                // N.B. a forwarder entry is always followed by another 'real' entry.  The curCodeOffset that 
                // results from consuming the forwarder entry is an INTERMEDIATE VALUE and doesn't represent 
                // a code offset of an actual callsite-with-GC-info.  But this intermediate value could 
                // inadvertently match some other callsite between the last callsite-with-GC-info and the next
                // callsite-with-GC-info.  To prevent this inadvertent match from happening, we must bypass 
                // the loop termination-condition test.  Therefore, 'continue' cannot be used here and we must
                // use a goto.
                goto ContinueUnconditionally;
            }
            else 
            if (lowBits == 0x7F) // STRING TERMINATOR
                break;

            // BIG ENCODING
            curCodeOffset += lowBits;

            // N.B. this returns the negative of the length of the unsigned!
            infoOffset = VarInt::SkipUnsigned(pCursor); 
        }
    }

    // If we reached the end of the scan loop without finding a matching callsite offset, then there must not 
    // be any roots to report to the GC.
    if (curCodeOffset != callCodeOffset)
        return;

    // If we were in the BIG ENCODING case, the infoOffset wil be negative.  So we backup pCursor and actually
    // decode the unsigned here.  This keeps the main loop above tighter by removing the conditional and 
    // decode from the body of the loop.
    if (infoOffset < 0)
    {
        pCursor += infoOffset;
        infoOffset = VarInt::ReadUnsigned(pCursor);
    }

    //
    // -------------------------------------------------------------------------------------------------------
    // Decode the callsite root string
    // -------------------------------------------------------------------------------------------------------
    //
    // 1.  Call sites with nothing to report are not encoded
    // 
    // 2.  00lRRRRR - normal "register set" encoding, pinned and interior attributes both false
    //      a.  l - this is the last descriptor
    //      b.  RRRRR - this is the register mask for { rbx, rsi, rdi, rbp, r12 }, ARM = { r4-r8 }
    // 
    // 3.  01liprrr - more general register encoding with pinned and interior attributes
    //      a.  l - last descriptor
    //      b.  i - interior
    //      c.  p - pinned
    //      d.  rrr - register number { rbx, rsi, rdi, rbp, r12, r13, r14, r15 }, ARM = { r4-r11 }
    // 
    // 4.  10l1SSSS - "local stack slot set" encoding, pinned and interior attributes both false
    //      a.  l - last descriptor
    //      b.  SSSS - set of "local slots" #0 - #3  - local slot 0 is at offset -8 from the last pushed 
    //          callee saved register, local slot 1 is at offset - 16, etc - in other words, these are the 
    //          slots normally used for locals
    // 
    // 5.  10l0ssss - "local slot" encoding
    //      a.  l - last descriptor
    //      b.  ssss - "local slot" #4 - #19
    //
    // 6.  11lipfsm {offset} [mask] - [multiple] stack slot encoding
    //      a.  l - last descriptor
    //      b.  i - interior attribute
    //      c.  p - pinned attribute
    //      d.  f - 1: frame pointer relative, 0: sp relative
    //      e.  s - offset sign
    //      f.  m - mask follows
    //      g.  offset - variable length unsigned integer
    //      h.  mask - variable length unsigned integer (only present if m-bit is 1) - this can describe 
    //          multiple stack locations with the same attributes. E.g., if you want to describe stack 
    //          locations 0x20, 0x28, 0x38, you would give a (starting) offset of 0x20 and a mask of 
    //          000000101 = 0x05. Up to 33 stack locations can be described.
    //
    // 7. 11lip010 0RRRRRRR [0IIIIIII] [0PPPPPPP] - live scratch reg reporting, this uses the SP-xxx encoding
    //                                              from #6 since we cannot have stack locations at negative
    //                                              offsets from SP.
    //      a.  l - last descriptor
    //      b.  i - interior byte present
    //      c.  p - pinned byte present
    //      d.  RRRRRRR - scratch register mask for { rax, rcx, rdx, r8, r9, r10, r11 }, ARM = { r0-r3, r12 }
    //      e.  IIIIIII - interior scratch register mask for { rax, rcx, rdx, r8, r9, r10, r11 } iff  'i' is 1
    //      f.  PPPPPPP - pinned scratch register mask for { rax, rcx, rdx, r8, r9, r10, r11 } iff  'p' is 1
    //
    PTR_UInt8 pbCallsiteString = pbCallsiteStringBlob + (int)infoOffset;

    bool isLastEncoding;
    pCursor = pbCallsiteString;
    do
    {
        UInt8 b = *pCursor++;
        isLastEncoding = ((b & 0x20) == 0x20);

        switch (b & 0xC0)
        {
        case 0x00:
            // case 2 -- "register set"
            ReportRegisterSet(b, pContext, hCallback);
            break;
        case 0x40:
            // case 3 -- "register"
            ReportRegister(b, pContext, hCallback);
            break;
        case 0x80:
            // case 4 -- "local slot set"
            // case 5 -- "local slot"
            ReportLocalSlots(b, pContext, hCallback, pMethodInfo->GetGCInfoHeader());
            break;
        case 0xC0:
            if ((b & 0xC7) == 0xC2)
                // case 7 -- "scratch reg reporting"
                ReportScratchRegs(b, pContext, hCallback, pCursor);
            else
            {
                bool hasDynamicAlignment = pMethodInfo->GetGCInfoHeader()->HasDynamicAlignment();
#ifdef TARGET_X86
                ASSERT_MSG(!hasDynamicAlignment || pMethodInfo->GetGCInfoHeader()->GetParamPointerReg() == RN_EBX, "NYI: non-EBX param pointer");
#endif
                // case 6 -- "stack slot" / "stack slot set"
                ReportStackSlots(b, pContext, hCallback, pCursor, hasDynamicAlignment);
            }
            break;
        }
    }
    while (!isLastEncoding);

    return;
}

#ifdef DACCESS_COMPILE
#define ASSERT_OR_DAC_RETURN_FALSE(x) if(!(x)) return false;
#else
#define ASSERT_OR_DAC_RETURN_FALSE(x) ASSERT(x)
#endif

// Unwind the current stack frame, i.e. update the virtual register set in pContext. This will be similar to 
// the state after the function returns back to caller (IP points to after the call, Frame and Stack pointer 
// has been reset, callee-saved registers restored, callee-UNsaved registers are trashed) 
// Returns success of operation.
bool EECodeManager::UnwindStackFrame(EEMethodInfo * pMethodInfo,
                                     UInt32         codeOffset,
                                     REGDISPLAY *   pContext)
{
    GCInfoHeader *  pInfoHeader = pMethodInfo->GetGCInfoHeader();

    // We could implement this unwind if we wanted, but there really isn't any reason
    ASSERT(pInfoHeader->GetReturnKind() != GCInfoHeader::MRK_ReturnsToNative);

#if defined(_DEBUG) || defined(DACCESS_COMPILE)
    // unwinding in the prolog is unsupported
    ASSERT_OR_DAC_RETURN_FALSE(codeOffset >= pInfoHeader->GetPrologSize());

    // unwinding in the epilog is unsupported
    UInt32 epilogOffset = 0;
    UInt32 epilogSize = 0;
    ASSERT_OR_DAC_RETURN_FALSE(!GetEpilogOffset(pMethodInfo, codeOffset, &epilogOffset, &epilogSize));
#else 
    UNREFERENCED_PARAMETER(codeOffset);
#endif

    bool ebpFrame = pInfoHeader->HasFramePointer();

#ifdef TARGET_X86
    // @TODO .. ESP-based methods with stack changes
    ASSERT_MSG(ebpFrame || !pInfoHeader->HasStackChanges(), "NYI -- ESP-based methods with stack changes");
#endif // TARGET_X86

    //
    // Just unwind based on the info header
    //
    Int32 saveSize = pInfoHeader->GetPreservedRegsSaveSize();
    UIntNative rawRSP;
    if (ebpFrame)
    {
#ifdef TARGET_ARM
        rawRSP = pContext->GetFP() + pInfoHeader->GetFrameSize();
#else
        saveSize -= sizeof(void *); // don't count RBP
        Int32 framePointerOffset = 0;
#ifdef TARGET_AMD64
        framePointerOffset = pInfoHeader->GetFramePointerOffset();
#endif
        rawRSP = pContext->GetFP() - saveSize - framePointerOffset;
#endif
    }
    else
    {
        rawRSP = pContext->GetSP() + pInfoHeader->GetFrameSize();
    }
    PTR_UIntNative RSP = (PTR_UIntNative)rawRSP;

#if defined(TARGET_AMD64)
    if (pInfoHeader->HasSavedXmmRegs())
    {
        typedef DPTR(Fp128) PTR_Fp128;
        PTR_Fp128 xmmSaveArea = (PTR_Fp128)(rawRSP & ~0xf);
        UInt32 savedXmmRegMask = pInfoHeader->GetSavedXmmRegMask();
        // should be a subset of xmm6-xmm15
        ASSERT((savedXmmRegMask & 0xffff003f) == 0);
        savedXmmRegMask >>= 6;
        for (int regIndex = 0; savedXmmRegMask != 0; regIndex++, savedXmmRegMask >>= 1)
        {
            if (savedXmmRegMask & 1)
            {
                --xmmSaveArea;
                pContext->Xmm[regIndex] = *xmmSaveArea;
            }
        }
    }
#elif defined(TARGET_ARM)
    UInt8 vfpRegPushedCount = pInfoHeader->GetVfpRegPushedCount();
    UInt8 vfpRegFirstPushed = pInfoHeader->GetVfpRegFirstPushed();
    UInt32 regIndex = vfpRegFirstPushed - 8;
    while (vfpRegPushedCount-- > 0)
    {
        ASSERT(regIndex < 8);
        pContext->D[regIndex] = *(PTR_UInt64)RSP;
        regIndex++;
        RSP = (PTR_UIntNative)((PTR_UInt8)RSP + sizeof(UInt64));
    }
#endif

#if defined(TARGET_X86)
    int registerSaveDisplacement = 0;
    // registers saved at bottom of frame in Project N
    registerSaveDisplacement = pInfoHeader->GetFrameSize();
#endif

    if (saveSize > 0)
    {
        CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();
#ifdef TARGET_AMD64
        if (regMask & CSR_MASK_R15) { pContext->pR15 = RSP++; }
        if (regMask & CSR_MASK_R14) { pContext->pR14 = RSP++; }
        if (regMask & CSR_MASK_R13) { pContext->pR13 = RSP++; }
        if (regMask & CSR_MASK_R12) { pContext->pR12 = RSP++; }
        if (regMask & CSR_MASK_RDI) { pContext->pRdi = RSP++; }
        if (regMask & CSR_MASK_RSI) { pContext->pRsi = RSP++; }
        if (regMask & CSR_MASK_RBX) { pContext->pRbx = RSP++; }
#elif defined(TARGET_X86)
        ASSERT_MSG(ebpFrame || !(regMask & CSR_MASK_RBP), "We should never use EBP as a preserved register");
        ASSERT_MSG(!(regMask & CSR_MASK_RBX) || !pInfoHeader->HasDynamicAlignment(), "Can't have EBX as preserved regster and dynamic alignment frame pointer")
        if (regMask & CSR_MASK_RBX) { pContext->pRbx = (PTR_UIntNative)((PTR_UInt8)RSP - registerSaveDisplacement); ++RSP; } // registers saved at bottom of frame
        if (regMask & CSR_MASK_RSI) { pContext->pRsi = (PTR_UIntNative)((PTR_UInt8)RSP - registerSaveDisplacement); ++RSP; } // registers saved at bottom of frame
        if (regMask & CSR_MASK_RDI) { pContext->pRdi = (PTR_UIntNative)((PTR_UInt8)RSP - registerSaveDisplacement); ++RSP; } // registers saved at bottom of frame
#elif defined(TARGET_ARM)       
        if (regMask & CSR_MASK_R4) { pContext->pR4 = RSP++; }
        if (regMask & CSR_MASK_R5) { pContext->pR5 = RSP++; }
        if (regMask & CSR_MASK_R6) { pContext->pR6 = RSP++; }
        if (regMask & CSR_MASK_R7) { pContext->pR7 = RSP++; }
        if (regMask & CSR_MASK_R8) { pContext->pR8 = RSP++; }
        if (regMask & CSR_MASK_R9) { pContext->pR9 = RSP++; }
        if (regMask & CSR_MASK_R10) { pContext->pR10 = RSP++; }
        if (regMask & CSR_MASK_R11) { pContext->pR11 = RSP++; }
#endif // TARGET_AMD64
    }

#ifndef TARGET_ARM
    if (ebpFrame)
        pContext->pRbp = RSP++;
#endif

    // handle dynamic frame alignment
    if (pInfoHeader->HasDynamicAlignment())
    {
#ifdef TARGET_X86
        ASSERT_MSG(pInfoHeader->GetParamPointerReg() == RN_EBX, "NYI: non-EBX param pointer");
        // For x86 dynamically-aligned frames, we have two frame pointers, like this:
        //
        // esp -> [main frame]
        // ebp -> ebp save
        //        return address (copy)
        //        [variable-sized alignment allocation]
        // ebx -> ebx save
        //        Return Address
        //
        // We've unwound the stack to the copy of the return address. We must continue to unwind the stack
        // and restore EBX. Because of the variable sized space on the stack, the only way to get at EBX's
        // saved location is to read it from the current value of EBX. EBX points at the stack location to
        // which previous EBX was saved.
        RSP = (PTR_UIntNative)*(pContext->pRbx); // RSP now points to EBX save location
        pContext->pRbx = RSP++;                  // RSP now points to original caller pushed return address.
#else
        UNREACHABLE_MSG("Dynamic frame alignment not supported on this platform");
#endif
    }

    pContext->SetAddrOfIP((PTR_PCODE)RSP); // save off the return address location
    pContext->SetIP(*RSP++);    // pop the return address
#ifdef TARGET_X86
    // pop the callee-popped args
    RSP += (pInfoHeader->GetReturnPopSize() / sizeof(UIntNative));
#endif

#ifdef TARGET_ARM
    RSP += pInfoHeader->ParmRegsPushedCount();
#endif

    pContext->SetSP((UIntNative) dac_cast<TADDR>(RSP));
    return true;
}

PTR_VOID EECodeManager::GetReversePInvokeSaveFrame(EEMethodInfo * pMethodInfo, REGDISPLAY * pContext)
{
    GCInfoHeader *  pHeader = pMethodInfo->GetGCInfoHeader();

    if (pHeader->GetReturnKind() != GCInfoHeader::MRK_ReturnsToNative)
        return NULL;

    Int32 frameOffset = pHeader->GetReversePinvokeFrameOffset();

    return *(PTR_PTR_VOID)(pContext->GetFP() + frameOffset);
}

PTR_VOID EECodeManager::GetFramePointer(EEMethodInfo *  pMethodInfo, 
                                        REGDISPLAY *    pContext)
{
    GCInfoHeader* pUnwindInfo = pMethodInfo->GetGCInfoHeader();
    return (pUnwindInfo->HasFramePointer() || pUnwindInfo->IsFunclet())
                        ? (PTR_VOID)pContext->GetFP()
                        : NULL;
}

#ifndef DACCESS_COMPILE

PTR_PTR_VOID EECodeManager::GetReturnAddressLocationForHijack(EEMethodInfo *    pMethodInfo,
                                                              UInt32            codeOffset,
                                                              REGDISPLAY *      pContext)
{
    GCInfoHeader * pHeader = pMethodInfo->GetGCInfoHeader();

    // We *could* hijack a reverse-pinvoke method, but it doesn't get us much because we already synchronize
    // with the GC on the way back to native code.
    if (pHeader->GetReturnKind() == GCInfoHeader::MRK_ReturnsToNative)
        return NULL;

    if (pHeader->IsFunclet())
        return NULL;

    if (codeOffset < pHeader->GetPrologSize())
    {
        // @TODO: NYI -- hijack in prolog
        return NULL;
    }

#ifdef _ARM_
    // We cannot get the return addres unless LR has 
    // be saved in the prolog.
    if (!pHeader->IsRegSaved(CSR_MASK_LR))
        return NULL;
#endif // _ARM_

    void ** ppvResult;

    UInt32 epilogOffset = 0;
    UInt32 epilogSize = 0;
    if (GetEpilogOffset(pMethodInfo, codeOffset, &epilogOffset, &epilogSize)) 
    {
#ifdef _ARM_
        // Disable hijacking from epilogs on ARM until we implement GetReturnAddressLocationFromEpilog.
        return NULL;
#else
        ppvResult = GetReturnAddressLocationFromEpilog(pHeader, pContext, epilogOffset, epilogSize);
        // Early out if GetReturnAddressLocationFromEpilog indicates a non-hijackable epilog (e.g. exception
        // throw epilog or tail call).
        if (ppvResult == NULL)
            return NULL;
        goto Finished;
#endif
    }

#ifdef _ARM_
    // ARM always sets up R11 as an OS frame chain pointer to enable fast ETW stack walking (except in the
    // case where LR is not pushed, but that was handled above). The protocol specifies that the return
    // address is pushed at [r11, #4].
    ppvResult = (void **)((*pContext->pR11) + sizeof(void *));
    goto Finished;
#else

    // We are in the body of the method, so just find the return address using the unwind info.
    if (pHeader->HasFramePointer())
    {
#ifdef _X86_
        if (pHeader->HasDynamicAlignment())
        {
            // In this case, we have the normal EBP frame pointer, but also an EBX frame pointer.  Use the EBX
            // one, because the return address associated with that frame pointer is the one we're actually 
            // going to return to.  The other one (next to EBP) is only for EBP-chain-walking.
            ppvResult = (void **)((*pContext->pRbx) + sizeof(void *));
            goto Finished;
        }
#endif

        Int32 framePointerOffset = 0;
#ifdef _AMD64_
        framePointerOffset = pHeader->GetFramePointerOffset();
#endif
        ppvResult = (void **)((*pContext->pRbp) + sizeof(void *) - framePointerOffset);
        goto Finished;
    }

    {
        // We do not have a frame pointer, but we are also not in the prolog or epilog

        UInt8 * RSP = (UInt8 *)pContext->GetSP();
        RSP += pHeader->GetFrameSize();
        RSP += pHeader->GetPreservedRegsSaveSize();

        // RSP should point to the return address now.
        ppvResult = (void**)RSP;
    }
    goto Finished;
#endif

  Finished:
    return ppvResult;
}

#endif

GCRefKind EECodeManager::GetReturnValueKind(EEMethodInfo * pMethodInfo)
{
    STATIC_ASSERT((GCRefKind)GCInfoHeader::MRK_ReturnsScalar == GCRK_Scalar);
    STATIC_ASSERT((GCRefKind)GCInfoHeader::MRK_ReturnsObject == GCRK_Object);
    STATIC_ASSERT((GCRefKind)GCInfoHeader::MRK_ReturnsByref  == GCRK_Byref);

    GCInfoHeader::MethodReturnKind retKind = pMethodInfo->GetGCInfoHeader()->GetReturnKind();
    switch (retKind)
    {
        case GCInfoHeader::MRK_ReturnsScalar:
        case GCInfoHeader::MRK_ReturnsToNative:
            return GCRK_Scalar;
        case GCInfoHeader::MRK_ReturnsObject:
            return GCRK_Object;
        case GCInfoHeader::MRK_ReturnsByref:
            return GCRK_Byref;
        default:
            break;
    }
    UNREACHABLE_MSG("unexpected return kind");
}

bool EECodeManager::GetEpilogOffset(EEMethodInfo * pMethodInfo, UInt32 codeOffset, UInt32 * epilogOffsetOut, UInt32 * epilogSizeOut)
{
    GCInfoHeader * pInfoHeader = pMethodInfo->GetGCInfoHeader();

    UInt32 epilogStart;

    if (pInfoHeader->IsEpilogAtEnd())
    {
        ASSERT(pInfoHeader->GetEpilogCount() == 1);
        UInt32 epilogSize = pInfoHeader->GetFixedEpilogSize();

        epilogStart = pMethodInfo->GetCodeSize() - epilogSize;

        // If we're at offset 0, it's equivalent to being in the body of the method
        if (codeOffset > epilogStart)
        {
            *epilogOffsetOut = codeOffset - epilogStart;
            ASSERT(pInfoHeader->IsValidEpilogOffset(*epilogOffsetOut, epilogSize));
            *epilogSizeOut = epilogSize;
            return true;
        }
        return false;
    }

    PTR_UInt8 pbEpilogTable = pMethodInfo->GetEpilogTable();
    epilogStart = 0;
    bool hasVaryingEpilogSizes = pInfoHeader->HasVaryingEpilogSizes();
    for (UInt32 idx = 0; idx < pInfoHeader->GetEpilogCount(); idx++)
    {
        epilogStart += VarInt::ReadUnsigned(pbEpilogTable);
        UInt32 epilogSize = hasVaryingEpilogSizes ? VarInt::ReadUnsigned(pbEpilogTable) : pInfoHeader->GetFixedEpilogSize();

        // If we're at offset 0, it's equivalent to being in the body of the method
        if ((epilogStart < codeOffset) && (codeOffset < (epilogStart + epilogSize)))
        {
            *epilogOffsetOut = codeOffset - epilogStart;
            ASSERT(pInfoHeader->IsValidEpilogOffset(*epilogOffsetOut, epilogSize));
            *epilogSizeOut = epilogSize;
            return true;
        }
    }
    return false;
}

#ifndef DACCESS_COMPILE

void ** EECodeManager::GetReturnAddressLocationFromEpilog(GCInfoHeader * pInfoHeader, REGDISPLAY * pContext,
                                                          UInt32 epilogOffset, UInt32 epilogSize)
{
    UNREFERENCED_PARAMETER(epilogSize);
    ASSERT(pInfoHeader->IsValidEpilogOffset(epilogOffset, epilogSize));
    UInt8 * pbCurrentIP   = (UInt8 *) pContext->GetIP();
    UInt8 * pbEpilogStart = pbCurrentIP - epilogOffset;

    //ASSERT(VerifyEpilogBytes(pInfoHeader, (Code *)pbEpilogStart));
    // We could find the return address of a native-callable method, but it's not very useful at the moment.
    ASSERT(pInfoHeader->GetReturnKind() != GCInfoHeader::MRK_ReturnsToNative); 
    UInt8 * pbEpilog = pbEpilogStart;

#ifdef _X86_

    if (pInfoHeader->HasFramePointer())
    {
        {
            // New Project N frames

            int frameSize = pInfoHeader->GetFrameSize();
            Int32 saveSize = pInfoHeader->GetPreservedRegsSaveSize() - sizeof(void*);
            int distance = frameSize + saveSize;

            if (saveSize > 0 || (0x8D == *pbEpilog) /* localloc frame */ )
            {
                // regenerate original sp

                // lea esp, [ebp-xxx]
                ASSERT_MSG(0x8D == *pbEpilog, "expected lea esp, [ebp-frame size]");

                if (distance <= 128)
                {
                    // short format (constant as 8-bit integer
                    ASSERT_MSG(0x65 == *(pbEpilog + 1), "expected lea esp, [ebp-frame size]");
                    ASSERT_MSG((UInt8)(-distance) == *(pbEpilog + 2), "expected lea esp, [ebp-frame size]");
                    pbEpilog += 3;
                }
                else
                {
                    // long formant (constant as 32-bit integer)
                    ASSERT_MSG(0xA5 == *(pbEpilog + 1), "expected lea esp, [ebp-frame size]");
                    ASSERT_MSG(-distance == *(Int32*)(pbEpilog + 2), "expected lea esp, [ebp-frame size]");
                    pbEpilog += 6;
                }

                CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();
                if (regMask & CSR_MASK_RBX) pbEpilog++; // pop ebx -- 5B
                if (regMask & CSR_MASK_RSI) pbEpilog++; // pop esi -- 5E
                if (regMask & CSR_MASK_RDI) pbEpilog++; // pop edi -- 5F
            }

            if (frameSize > 0)
            {
                // set esp to to EBP frame chain location
                ASSERT_MSG(0x8B == *pbEpilog, "expected 'mov esp, ebp'");
                ASSERT_MSG(0xE5 == *(pbEpilog + 1), "expected 'mov esp, ebp'");
                pbEpilog += 2;
            }

            ASSERT_MSG(0x5d == *pbEpilog, "expected 'pop ebp'");

            // Just use the EBP frame if we haven't popped it yet
            if (pbCurrentIP <= pbEpilog)
                return (void **)((*(pContext->pRbp)) + sizeof(void *));

            ++pbEpilog; // advance past 'pop ebp'

            if (pInfoHeader->HasDynamicAlignment())
            {
                // For x86 dynamically-aligned frames, we have two frame pointers, like this:
                //
                // esp -> [main frame]
                // ebp -> ebp save
                //        return address
                //        [variable-sized alignment allocation]
                // ebx -> ebx save
                //        Return Address
                //
                // The epilog looks like this, with the corresponding changes to the return address location.
                //
                //                                       Correct return address location
                //                                       --------------------------------
                //      -------------------------------> ebp + 4  (or ebx + 4)
                //      lea     esp, [ebp-XXX]
                //      pop     esi
                //      mov     esp, ebp
                //      pop     ebp
                //      -------------------------------> ebx + 4
                //      mov     esp, ebx
                //      pop     ebx
                //      -------------------------------> esp
                //      ret

                ASSERT_MSG(pInfoHeader->GetParamPointerReg() == RN_EBX, "NYI: non-EBX param pointer");

                ASSERT_MSG(0x8B == *pbEpilog, "expected 'mov esp, ebx'");
                ASSERT_MSG(0xE3 == *(pbEpilog + 1), "expected 'mov esp, ebx'");

                // At this point the return address is at EBX+4, we fall-through to the code below since it's 
                // the same there as well.

                pbEpilog += 2; // advance past 'mov esp, ebx'

                ASSERT_MSG(0x5b == *pbEpilog, "expected 'pop ebx'");

                // at this point the return address is at EBX+4
                if (pbCurrentIP == pbEpilog)
                    return (void **)((*(pContext->pRbx)) + sizeof(void *));

                ++pbEpilog; // advance past 'pop ebx'
            }

            // EBP has been popped, dynamic alignment has been undone, so ESP points at the return address
            return (void **)(pContext->SP);
        }
    }
    else
    {
        ASSERT_MSG(!pInfoHeader->HasStackChanges(), "NYI -- dynamic push/pop");

        UIntNative RSP = pContext->SP;

        int frameSize = pInfoHeader->GetFrameSize();

        if (pbCurrentIP <= pbEpilog)
            RSP += frameSize;

        if (frameSize == sizeof(void*))
            pbEpilog++; // 0x59, pop ecx
        else if ((Int8)frameSize == frameSize)
            pbEpilog += 3; // add esp, imm8  -- 83 c4 BYTE(frameSize)
        else
            pbEpilog += 6; // add esp, imm32 -- 81 c4 DWORD(frameSize)

        CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();

        ASSERT_MSG(!(regMask & CSR_MASK_RBP),
            "We only expect RBP to be used as the frame pointer, never as a free preserved reg");

        if (regMask & CSR_MASK_RBX)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 1;                                          // pop ebx -- 5B
        }

        if (regMask & CSR_MASK_RSI)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 1;                                          // pop esi -- 5E
        }

        if (regMask & CSR_MASK_RDI)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 1;                                          // pop edi -- 5F
        }

        return (void **)(RSP);
    }

#elif defined(_AMD64_)

    int frameSize = pInfoHeader->GetFrameSize();
    if (pInfoHeader->HasFramePointer())
    {
        bool isNewStyleFP = pInfoHeader->IsFramePointerOffsetFromSP();
        int preservedRegSize = pInfoHeader->GetPreservedRegsSaveSize();

        int encodedFPOffset = isNewStyleFP ? frameSize - pInfoHeader->GetFramePointerOffsetFromSP()
                                           : -preservedRegSize + sizeof(void*);

        // 'lea rsp, [rbp + offset]'    // 48 8d 65 xx
                                        // 48 8d a5 xx xx xx xx
        if ((encodedFPOffset > 127) || (encodedFPOffset < -128))
            pbEpilog += 7;
        else
            pbEpilog += 4;

        CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();

        if (regMask & CSR_MASK_R15) pbEpilog += 2;  // pop r15 -- 41 5F
        if (regMask & CSR_MASK_R14) pbEpilog += 2;  // pop r14 -- 41 5E
        if (regMask & CSR_MASK_R13) pbEpilog += 2;  // pop r13 -- 41 5D
        if (regMask & CSR_MASK_R12) pbEpilog += 2;  // pop r12 -- 41 5C
        if (regMask & CSR_MASK_RDI) pbEpilog++;     // pop rdi -- 5F
        if (regMask & CSR_MASK_RSI) pbEpilog++;     // pop rsi -- 5E
        if (regMask & CSR_MASK_RBX) pbEpilog++;     // pop rbx -- 5B

        ASSERT_MSG(0x5d == *pbEpilog, "expected pop ebp");

        // If RBP hasn't been popped yet, we can calculate the return address location from RBP.
        if (pbCurrentIP <= pbEpilog)
            return (void **)(*(pContext->pRbp) + encodedFPOffset + preservedRegSize);

        // EBP has been popped, so RSP points at the return address
        return (void **) (pContext->SP);
    }
    else
    {
        UIntNative RSP = pContext->SP;

        if (frameSize)
        {
            if (pbCurrentIP <= pbEpilog)
                RSP += frameSize;

            if (frameSize < 128)
            {
                // 'add rsp, frameSize'     // 48 83 c4 xx
                pbEpilog += 4;
            }
            else 
            {
                // 'add rsp, frameSize'     // 48 81 c4 xx xx xx xx
                pbEpilog += 7;
            }
        }

        CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();

        ASSERT_MSG(!(regMask & CSR_MASK_RBP),
                   "We only expect RBP to be used as the frame pointer, never as a free preserved reg");

        if (regMask & CSR_MASK_R15)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 2;                                          // pop r15 -- 41 5F
        }

        if (regMask & CSR_MASK_R14)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 2;                                          // pop r14 -- 41 5E
        }

        if (regMask & CSR_MASK_R13)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 2;                                          // pop r13 -- 41 5D
        }

        if (regMask & CSR_MASK_R12)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 2;                                          // pop r12 -- 41 5C
        }

        if (regMask & CSR_MASK_RDI)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 1;                                          // pop rdi -- 5F
        }

        if (regMask & CSR_MASK_RSI)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 1;                                          // pop rsi -- 5E
        }

        if (regMask & CSR_MASK_RBX)
        {
            if (pbCurrentIP <= pbEpilog) { RSP += sizeof(void*); }
            pbEpilog += 1;                                          // pop rbx -- 5B
        }

        return (void **) (RSP);
    }

#elif defined(_ARM_)

    UInt16 * pwEpilog = (UInt16*)pbEpilog;

    if (pwEpilog[0] == 0x46bd)
    {
        // mov sp, fp
        ASSERT(pInfoHeader->HasFramePointer());
        pwEpilog++;
    }

    if (pInfoHeader->HasFramePointer() || pInfoHeader->GetFrameSize() > 0)
    {
        if ((pwEpilog[0] & 0xff80) == 0xb000)
        {
            // add sp, sp, #frameSize
            pwEpilog++;
        }
        else if (((pwEpilog[0] & 0xfbf0) == 0xf200) && ((pwEpilog[1] & 0x8f00) == 0x0d00))
        {
            // add sp, reg, #imm12
            pwEpilog += 2;
        }
        else if (((pwEpilog[0] & 0xfbf0) == 0xf240) && ((pwEpilog[1] & 0x8f00) == 0x0c00))
        {
            // movw r12, #imm16
            pwEpilog += 2;

            if (((pwEpilog[0] & 0xfbf0) == 0xf2c0) && ((pwEpilog[1] & 0x8f00) == 0x0c00))
            {
                // movt r12, #imm16
                pwEpilog += 2;
            }

            // add sp, sp, r12
            ASSERT((pwEpilog[0] == 0xeb0d) && (pwEpilog[1] == 0x0d0c));
            pwEpilog += 2;
        }
    }

    // vpop {...}
    while (((pwEpilog[0] & ~(1<<6)) == 0xecbd) && ((pwEpilog[1] & 0x0f01) == 0x0b00))
        pwEpilog += 2;

    // pop {...}
    UInt16 wPopRegs = 0;
    if ((pwEpilog[0] & 0xfe00) == 0xbc00)
    {
        // 16-bit pop.
        wPopRegs = pwEpilog[0] & 0xff;
        if ((pwEpilog[0] & 0x100) != 0)
            wPopRegs |= 1<<15;
        pwEpilog++;
    }
    else if (pwEpilog[0] == 0xe8bd)
    {
        // 32-bit pop.
        wPopRegs = pwEpilog[1];
        pwEpilog += 2;
    }
    else if ((pwEpilog[0] == 0xf85d) && ((pwEpilog[1] & 0x0fff) == 0xb04))
    {
        // Single register pop.
        int reg = pwEpilog[1] >> 12;
        wPopRegs |= 1 << reg;
        pwEpilog += 2;
    }

    if (wPopRegs & (1 << 11))
    {
        // Popped r11 (the OS frame chain pointer). If we pushed this then we were required to push lr
        // immediately under it. (Can't directly assert that LR is popped since there are several ways we
        // might do this).
        if (pbCurrentIP < (UInt8*)pwEpilog)
        {
            // Executing in epilog prior to pop, so the return address is at [r11, #4].
            return (void**)((*pContext->pR11) + 4);
        }
    }
    else
    {
        // We didn't push r11 so therefore we didn't push lr (the invariant is that both or neither are
        // pushed). So it doesn't matter where in the epilog we're executing, the return address has always
        // been in lr.
        return (void**)pContext->pLR;
    }

    if (wPopRegs & (1 << 15))
    {
        // Popped pc. This is a direct result of pushing lr and we only ever push lr if and only if we're also
        // pushing r11 to form an OS frame chain. If we didn't return above that means we somehow popped r11
        // and lr into pc and somehow landed up at the next instruction (i.e. past the end of the epilog). So
        // this case is an error.
        ASSERT_UNCONDITIONALLY("Walked off end of epilog");
        return NULL;
    }

    if ((pwEpilog[0] == 0xf85d) && ((pwEpilog[1] & 0xff00) == 0xfb00))
    {
        // ldr pc, [sp], #imm8
        // Case where lr was pushed but we couldn't pop it with the other registers because we had some
        // additional stack to clean up (homed argument registers). Return address is at the top of the stack
        // in this case.
        return (void**)pContext->SP;
    }

    if ((pwEpilog[0] & 0xff80) == 0xb000)
    {
        // add sp, sp, #imm7
        // Case where we have stack cleanup (homed argument registers) but we need to return via a branch for
        // some reason (such as tail calls).
        pwEpilog++;
    }

    if ((pwEpilog[0] & 0xff87) == 0x4700)
    {
        // bx <reg>
        // Branch via register. This is a simple return if <reg> is lr, otherwise assume it's an EH throw and
        // return NULL to indicate do not hijack.
        if (((pwEpilog[0] & 0x0078) >> 3) == 14)
            return (void**)pContext->pLR;
        return NULL;
    }

    if (((pwEpilog[0] & 0xf800) == 0xf000) && ((pwEpilog[1] & 0xd000) == 0x9000))
    {
        // b <imm>
        // Direct branch. Looks like a tail call. These aren't hijackable (without writing the instruction
        // stream) so return NULL to indicate do not hijack here.
        return NULL;
    }

    // Shouldn't be any other instructions in the epilog.
    UNREACHABLE_MSG("Unknown epilog instruction");
    return NULL;
#endif // _X86_
}

#ifdef _DEBUG

bool EECodeManager::FindNextEpilog(GCInfoHeader * pInfoHeader, UInt32 methodSize, PTR_UInt8 pbEpilogTable, 
                                   Int32 * pEpilogStartOffsetInOut, UInt32 * pEpilogSizeOut)
{
    Int32 startOffset = *pEpilogStartOffsetInOut;
    Int32 thisOffset = 0;

    if (pInfoHeader->IsEpilogAtEnd())
    {
        ASSERT(pInfoHeader->GetEpilogCount() == 1);
        UInt32 epilogSize = pInfoHeader->GetFixedEpilogSize();
        thisOffset = methodSize - epilogSize;
        *pEpilogStartOffsetInOut = thisOffset;
        *pEpilogSizeOut = epilogSize;
        return (thisOffset > startOffset);
    }

    bool hasVaryingEpilogSizes = pInfoHeader->HasVaryingEpilogSizes();
    for (UInt32 idx = 0; idx < pInfoHeader->GetEpilogCount(); idx++)
    {
        thisOffset += VarInt::ReadUnsigned(pbEpilogTable);
        UInt32 epilogSize = hasVaryingEpilogSizes ? VarInt::ReadUnsigned(pbEpilogTable) : pInfoHeader->GetFixedEpilogSize();
        if (thisOffset > startOffset)
        {
            *pEpilogStartOffsetInOut = thisOffset;
            *pEpilogSizeOut = epilogSize;
            return true;
        }
    }

    return false;
}

#ifdef _ARM_
#define IS_FRAMELESS() ((pInfoHeader->GetSavedRegs() & CSR_MASK_LR) == 0)
#else
#define IS_FRAMELESS() (!pInfoHeader->HasFramePointer())
#endif

void CheckHijackInEpilog(GCInfoHeader * pInfoHeader, Code * pEpilog, Code * pEpilogStart, UInt32 epilogSize)
{
    ASSERT(pInfoHeader->GetReturnKind() != GCInfoHeader::MRK_ReturnsToNative);
    if (IS_FRAMELESS())
        return;

    UIntNative SUCCESS_VAL = 0x22222200;
    UIntNative RSP_TEST_VAL = SUCCESS_VAL;
    UIntNative RBP_TEST_VAL = (RSP_TEST_VAL - sizeof(void *));

    REGDISPLAY context;
#if defined(_X86_)
    context.pRbx = &RBP_TEST_VAL;
    context.pRbp = &RBP_TEST_VAL;
    context.SP = RSP_TEST_VAL;
#elif defined(_AMD64_)

    int frameSize = pInfoHeader->GetFrameSize();
    bool isNewStyleFP = pInfoHeader->IsFramePointerOffsetFromSP();
    int preservedRegSize = pInfoHeader->GetPreservedRegsSaveSize();

    int encodedFPOffset = isNewStyleFP ? frameSize - pInfoHeader->GetFramePointerOffsetFromSP()
                                        : -preservedRegSize + sizeof(void*);

    RBP_TEST_VAL = SUCCESS_VAL - encodedFPOffset - preservedRegSize;

    context.pRbp = &RBP_TEST_VAL;
    context.SP = RSP_TEST_VAL;
#elif defined(_ARM_)
    context.pR11 = &RBP_TEST_VAL;
    context.SP = RSP_TEST_VAL; 
#endif

    context.SetIP((PCODE)pEpilog);

    void ** result = EECodeManager::GetReturnAddressLocationFromEpilog(pInfoHeader, &context, 
        (UInt32)((Code*)pEpilog - pEpilogStart), epilogSize);

    ASSERT(SUCCESS_VAL == (UIntNative)result || NULL == result);
}

#define CHECK_HIJACK_IN_EPILOG() CheckHijackInEpilog(pInfoHeader, (Code *)pEpilog, (Code *)pEpilogStart, epilogSize)

#define VERIFY_FAILURE() \
{ \
  ASSERT_UNCONDITIONALLY("VERIFY_FAILURE"); \
  return false; \
} \

#ifdef _X86_
bool VerifyEpilogBytesX86(GCInfoHeader * pInfoHeader, Code * pEpilogStart, UInt32 epilogSize)
{
    Code * pEpilog = pEpilogStart;

    // NativeCallable methods aren't return-address-hijacked, so we don't care about the epilog format.
    bool returnsToNative = (pInfoHeader->GetReturnKind() == GCInfoHeader::MRK_ReturnsToNative);
    if (returnsToNative)
        return true;

    if (pInfoHeader->HasFramePointer())
    {
        {
            // ProjectN frames

            CHECK_HIJACK_IN_EPILOG();

            int frameSize = pInfoHeader->GetFrameSize();
            Int32 saveSize = pInfoHeader->GetPreservedRegsSaveSize() - sizeof(void*); // don't count EBP
            int distance = frameSize + saveSize;

            if (saveSize > 0 || (*pEpilog==0x8d) /* localloc frame */ )
            {
                // lea esp, [ebp-xxx]

                if (*pEpilog++ != 0x8d)
                    VERIFY_FAILURE();

                if (distance <= 128)
                {
                    if (*pEpilog++ != 0x65)
                        VERIFY_FAILURE();
                    if (*pEpilog++ != ((UInt8)-distance))
                        VERIFY_FAILURE();
                }
                else
                {
                    if (*pEpilog++ != 0xa5)
                        VERIFY_FAILURE();
                    if (*((Int32*&)pEpilog)++ != -distance)
                        VERIFY_FAILURE();
                }

                CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();

                CHECK_HIJACK_IN_EPILOG();
                if (regMask & CSR_MASK_RBX)
                if (*pEpilog++ != 0x5b) // pop ebx
                    VERIFY_FAILURE();

                CHECK_HIJACK_IN_EPILOG();
                if (regMask & CSR_MASK_RSI)
                if (*pEpilog++ != 0x5e) // pop esi
                    VERIFY_FAILURE();

                CHECK_HIJACK_IN_EPILOG();
                if (regMask & CSR_MASK_RDI)
                if (*pEpilog++ != 0x5f) // pop edi
                    VERIFY_FAILURE();
            }

            // Reset ESP if necessary
            if (frameSize > 0)
            {
                // 'mov esp, ebp'
                CHECK_HIJACK_IN_EPILOG();
                if (*pEpilog++ != 0x8b)
                    VERIFY_FAILURE();
                if (*pEpilog++ != 0xE5)
                    VERIFY_FAILURE();
            }

            // pop ebp
            CHECK_HIJACK_IN_EPILOG();
            if (*pEpilog++ != 0x5d)
                VERIFY_FAILURE();

            if (pInfoHeader->HasDynamicAlignment())
            {
                ASSERT_MSG(pInfoHeader->GetParamPointerReg() == RN_EBX, "Expecting EBX as param pointer reg");
                ASSERT_MSG(!(pInfoHeader->GetSavedRegs() & CSR_MASK_RBX), "Not expecting param pointer reg to be saved explicitly");

                // expect 'mov esp, ebx'
                CHECK_HIJACK_IN_EPILOG();
                if (*pEpilog++ != 0x8b || *pEpilog++ != 0xE3)
                {
                    VERIFY_FAILURE();
                }

                // pop ebx
                CHECK_HIJACK_IN_EPILOG();
                if (*pEpilog++ != 0x5b)
                    VERIFY_FAILURE();
            }
        }
    }
    else
    {
        CHECK_HIJACK_IN_EPILOG();
        int frameSize = pInfoHeader->GetFrameSize();
        if (frameSize == 0)
        {
        }
        else if (frameSize == sizeof(void*))
        {
            if (*pEpilog++ != 0x59) // pop ecx
                VERIFY_FAILURE(); 
        }
        else if ((Int8)frameSize == frameSize)
        {
            // add esp, imm8
            if (*pEpilog++ != 0x83)
                VERIFY_FAILURE();
            if (*pEpilog++ != 0xc4)
                VERIFY_FAILURE();
            if (*pEpilog++ != frameSize)
                VERIFY_FAILURE();
        }
        else
        {
            // add esp, imm32
            if (*pEpilog++ != 0x81)
                VERIFY_FAILURE();
            if (*pEpilog++ != 0xc4)
                VERIFY_FAILURE();
            if ((*((Int32*)pEpilog))++ != frameSize)
                VERIFY_FAILURE();
        }

        CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();

#if 1
        ASSERT_MSG(!(pInfoHeader->GetSavedRegs() & CSR_MASK_RBP),
                   "We only expect RBP to be used as the frame pointer, never as a free preserved reg");
#else
        CHECK_HIJACK_IN_EPILOG();
        if (regMask & CSR_MASK_RBP)
            if (*pEpilog++ != 0x5d) // pop ebp
                VERIFY_FAILURE();
#endif

        CHECK_HIJACK_IN_EPILOG();
        if (regMask & CSR_MASK_RBX)
            if (*pEpilog++ != 0x5b) // pop ebx
                VERIFY_FAILURE();

        CHECK_HIJACK_IN_EPILOG();
        if (regMask & CSR_MASK_RSI)
            if (*pEpilog++ != 0x5e) // pop esi
                VERIFY_FAILURE();

        CHECK_HIJACK_IN_EPILOG();
        if (regMask & CSR_MASK_RDI)
            if (*pEpilog++ != 0x5f) // pop edi
                VERIFY_FAILURE();
    }

    CHECK_HIJACK_IN_EPILOG();

    // Note: the last instruction of the epilog may be one of many possibilities: ret, rep ret, jmp offset,
    // or jmp [offset]. Each is a different size, but still just one instruction, which is just fine. 
    // Therefore, from here down, pEpilog may be beyond "epilog start + size".

    if (*pEpilog == 0xE9)
    {
        pEpilog += 5; // jmp offset   (tail call direct)
    }
    else if (*pEpilog == 0xFF)
    {
        pEpilog += 6; // jmp [offset] (tail call indirect)
    }
    else
    {
        if (*pEpilog == 0xf3) // optional:  rep prefix
            pEpilog++;

        UInt32 retPopSize = pInfoHeader->GetReturnPopSize();
        if (retPopSize == 0)
        {
            if (*pEpilog++ != 0xC3) // ret
                VERIFY_FAILURE();
        }
        else
        {
            if (*pEpilog++ != 0xC2) // ret NNNN
                VERIFY_FAILURE();
            if (*((UInt16 *)pEpilog) != retPopSize)
                VERIFY_FAILURE();
            pEpilog += 2;
        }
    }

    return true;
}
#endif // _X86_
#ifdef _AMD64_
bool VerifyEpilogBytesAMD64(GCInfoHeader * pInfoHeader, Code * pEpilogStart, UInt32 epilogSize)
{
    Code * pEpilog = pEpilogStart;

    // NativeCallable methods aren't return-address-hijacked, so we don't care about the epilog format.
    bool returnsToNative = (pInfoHeader->GetReturnKind() == GCInfoHeader::MRK_ReturnsToNative);
    if (returnsToNative)
        return true;

    CHECK_HIJACK_IN_EPILOG();

    bool ebpFrame = pInfoHeader->HasFramePointer();
    int frameSize = pInfoHeader->GetFrameSize();
    if (ebpFrame)
    {
        ASSERT(RN_EBP == pInfoHeader->GetFramePointerReg());

        bool isNewStyleFP = pInfoHeader->IsFramePointerOffsetFromSP();
        int preservedRegSize = pInfoHeader->GetPreservedRegsSaveSize();

        Int32 offset = isNewStyleFP ? frameSize - pInfoHeader->GetFramePointerOffsetFromSP()
                                    : -preservedRegSize + sizeof(void*);

        // 'lea rsp, [rbp - offset]'
        if (*pEpilog++ != 0x48)
            VERIFY_FAILURE();
        if (*pEpilog++ != 0x8d)
            VERIFY_FAILURE();

        if ((offset > 127) || (offset < -128))
        {
            if (*pEpilog++ != 0xA5)
                VERIFY_FAILURE();
            if (*((Int32*&)pEpilog)++ != offset)
                VERIFY_FAILURE();
        }
        else
        {
            if (*pEpilog++ != 0x65)
                VERIFY_FAILURE();
            if (((Int8)*pEpilog++) != offset)
                VERIFY_FAILURE();
        }
    }
    else if (frameSize)
    {
        if (frameSize < 128)
        {
            // 'add rsp, frameSize'     // 48 83 c4 xx
            if (*pEpilog++ != 0x48)
                VERIFY_FAILURE();
            if (*pEpilog++ != 0x83)
                VERIFY_FAILURE();
            if (*pEpilog++ != 0xc4)
                VERIFY_FAILURE();
            if (*pEpilog++ != ((UInt8)frameSize))
                VERIFY_FAILURE();
        }
        else
        {
            // 'add rsp, frameSize'     // 48 81 c4 xx xx xx xx
            if (*pEpilog++ != 0x48)
                VERIFY_FAILURE();
            if (*pEpilog++ != 0x81)
                VERIFY_FAILURE();
            if (*pEpilog++ != 0xc4)
                VERIFY_FAILURE();
            if (*((Int32*&)pEpilog)++ != frameSize)
                VERIFY_FAILURE();
        }
    }

    CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();

    CHECK_HIJACK_IN_EPILOG();
    if (regMask & CSR_MASK_R15)
    {
        // pop r15
        if (*pEpilog++ != 0x41)
            VERIFY_FAILURE();
        if (*pEpilog++ != 0x5f)
            VERIFY_FAILURE();
    }

    CHECK_HIJACK_IN_EPILOG();
    if (regMask & CSR_MASK_R14)
    {
        // pop r14
        if (*pEpilog++ != 0x41)
            VERIFY_FAILURE();
        if (*pEpilog++ != 0x5e)
            VERIFY_FAILURE();
    }

    CHECK_HIJACK_IN_EPILOG();
    if (regMask & CSR_MASK_R13)
    {
        // pop r13
        if (*pEpilog++ != 0x41)
            VERIFY_FAILURE();
        if (*pEpilog++ != 0x5d)
            VERIFY_FAILURE();
    }

    CHECK_HIJACK_IN_EPILOG();
    if (regMask & CSR_MASK_R12)
    {
        // pop r12
        if (*pEpilog++ != 0x41)
            VERIFY_FAILURE();
        if (*pEpilog++ != 0x5c)
            VERIFY_FAILURE();
    }

    CHECK_HIJACK_IN_EPILOG();
    if (regMask & CSR_MASK_RDI)
        if (*pEpilog++ != 0x5f) // pop rdi
            VERIFY_FAILURE();

    CHECK_HIJACK_IN_EPILOG();
    if (regMask & CSR_MASK_RSI)
        if (*pEpilog++ != 0x5e) // pop rsi
            VERIFY_FAILURE();

    CHECK_HIJACK_IN_EPILOG();
    if (regMask & CSR_MASK_RBX)
        if (*pEpilog++ != 0x5b) // pop rbx
            VERIFY_FAILURE();

    if (ebpFrame)
    {
        CHECK_HIJACK_IN_EPILOG();
        if (*pEpilog++ != 0x5d)     // pop rbp
            VERIFY_FAILURE();
    }

    CHECK_HIJACK_IN_EPILOG();

    // Note: the last instruction of the epilog may be one of many possibilities: ret, rep ret, rex jmp rax.
    // Each is a different size, but still just one instruction, which is just fine. Therefore, from here
    // down, pEpilog may be beyond "epilog start + size".

    if (*pEpilog == 0x48)
    {
        // rex jmp rax (tail call)
        pEpilog++;

        if (*pEpilog++ != 0xff)
            VERIFY_FAILURE();
        if (*pEpilog++ != 0xe0)
            VERIFY_FAILURE();
    }
    else
    {
        // rep (OPTIONAL)
        if (*pEpilog == 0xf3)
            pEpilog++;
        // ret
        if (*pEpilog++ != 0xc3)
            VERIFY_FAILURE();
    }

    return true;
}
#endif // _AMD64_

#ifdef _ARM_
bool VerifyEpilogBytesARM(GCInfoHeader * pInfoHeader, Code * pEpilogStart, UInt32 epilogSize)
{
    if (((size_t)pEpilogStart) & 1)
        pEpilogStart--;

    UInt16 * pEpilog = (UInt16 *)pEpilogStart;

    // NativeCallable methods aren't return-address-hijacked, so we don't care about the epilog format.
    bool returnsToNative = (pInfoHeader->GetReturnKind() == GCInfoHeader::MRK_ReturnsToNative);
    if (returnsToNative)
        return true;

    CHECK_HIJACK_IN_EPILOG();

    int stackPopSize = 0;
    bool r7Cleanup = false;

    int frameSize = pInfoHeader->GetFrameSize();
    bool r7Frame = pInfoHeader->HasFramePointer();

    if (pEpilog[0] == 0x46bd)
    {
        // 'mov sp,fp'
        if (!r7Frame)
            VERIFY_FAILURE();
        r7Cleanup = true;
        pEpilog++;
    }

    CHECK_HIJACK_IN_EPILOG();

    if (frameSize > 0 || r7Frame)
    {
        if ((pEpilog[0] & 0xff80) == 0xb000)
        {
            // 'add sp, sp, #frameSize'     // b0xx
            stackPopSize = (*pEpilog & 0x7f) << 2;
            pEpilog++;
        }
        else if ((pEpilog[0] & 0xfbf0) == 0xf200 && (pEpilog[1] & 0x8f00) == 0x0d00)
        {
            // 'add sp,reg,#imm12
            int reg = pEpilog[0] & 0x000f;
            if (reg == 0xd)
                ;
            else if (reg == 0x7 && r7Frame)
                r7Cleanup = true;
            else
                VERIFY_FAILURE();
            stackPopSize = (((pEpilog[0] >> 10) & 0x1) << 11) + (((pEpilog[1] >> 12) & 0x07) << 8) + (pEpilog[1] & 0xff);
            pEpilog += 2;
        }
        else if ((pEpilog[0] & 0xfbf0) == 0xf240 && (pEpilog[1] & 0x8f00) == 0x0c00)
        {
            // movw r12,imm16
            stackPopSize = ((pEpilog[0] & 0xf) << 12) + (((pEpilog[0] >> 10) & 0x1) << 11) + (((pEpilog[1] >> 12) & 0x07) << 8) + (pEpilog[1] & 0xff);
            pEpilog += 2;
            
            // movt present as well?
            if ((pEpilog[0] & 0xfbf0) == 0xf2c0 && (pEpilog[1] & 0x8f00) == 0x0c00)
            {
                int highWord = ((pEpilog[0] & 0xf) << 12) + (((pEpilog[0] >> 10) & 0x1) << 11) + (((pEpilog[1] >> 12) & 0x07) << 8) + (pEpilog[1] & 0xff);
                stackPopSize += highWord << 16;
                pEpilog += 2;
            }

            // expect add sp,sp,r12
            if (pEpilog[0] != 0xeb0d || pEpilog[1] != 0x0d0c)
                VERIFY_FAILURE();
            pEpilog += 2;
        }
    }

    CHECK_HIJACK_IN_EPILOG();

    // check for vpop instructions to match what's in the info hdr
    Int32 vfpRegFirstPushedExpected = pInfoHeader->GetVfpRegFirstPushed();
    Int32 vfpRegPushedCountExpected = pInfoHeader->GetVfpRegPushedCount();
    while ((pEpilog[0] & ~(1<<6)) == 0xecbd && (pEpilog[1] & 0x0f01) == 0x0b00)
    {
        Int32 vfpRegFirstPushedActual = (((pEpilog[0] >> 6) & 1) << 4) | (pEpilog[1] >> 12);
        Int32 vfpRegPushedCountActual = (pEpilog[1] & 0xff) >> 1;
        if (vfpRegFirstPushedExpected == 0 && vfpRegPushedCountExpected == 0)
        {
            VERIFY_FAILURE();
        }
        else
        {
            if (vfpRegFirstPushedActual != vfpRegFirstPushedExpected || vfpRegPushedCountActual > vfpRegPushedCountExpected)
                VERIFY_FAILURE();

            // if we are still here, there are more than 16 registers to pop, so we expect another vpop
            // adjust the "expected" variables accordingly
            vfpRegFirstPushedExpected += vfpRegPushedCountActual;
            vfpRegPushedCountExpected -= vfpRegPushedCountActual;
        }

        pEpilog += 2;

        CHECK_HIJACK_IN_EPILOG();
    }
    if (vfpRegPushedCountExpected != 0)
        VERIFY_FAILURE();

    CalleeSavedRegMask regMask = pInfoHeader->GetSavedRegs();

    // figure out what set of registers should be popped
    int shouldPopRegMask = 0;
    if (regMask & CSR_MASK_R4)
        shouldPopRegMask |= 1<<4;
    if (regMask & CSR_MASK_R5)
        shouldPopRegMask |= 1<<5;
    if (regMask & CSR_MASK_R6)
        shouldPopRegMask |= 1<<6;
    if (regMask & CSR_MASK_R7)
        shouldPopRegMask |= 1<<7;
    if (regMask & CSR_MASK_R8)
        shouldPopRegMask |= 1<<8;
    if (regMask & CSR_MASK_R9)
        shouldPopRegMask |= 1<<9;
    if (regMask & CSR_MASK_R10)
        shouldPopRegMask |= 1<<10;
    if (regMask & CSR_MASK_R11)
        shouldPopRegMask |= 1<<11;
    if (regMask & CSR_MASK_LR)
        shouldPopRegMask |= 1<<15;

    // figure out what set of registers is actually popped
    int actuallyPopRegMask = 0;
    if ((pEpilog[0] & 0xfe00) == 0xbc00)
    {
        actuallyPopRegMask = pEpilog[0] & 0xff;
        if ((pEpilog[0] & 0x100) != 0)
            actuallyPopRegMask |= 1<<15;
        pEpilog++;
    }
    else if (pEpilog[0] == 0xe8bd)
    {
        // 32-bit instruction
        actuallyPopRegMask = pEpilog[1];
        pEpilog += 2;
    }
    else if (pEpilog[0] == 0xf85d && (pEpilog[1] & 0x0fff) == 0xb04)
    {
        // we just pop one register
        int reg = pEpilog[1] >> 12;
        actuallyPopRegMask |= 1 << reg;
        pEpilog += 2;
    }

    // have we popped some low registers to clean up the stack?
    if (stackPopSize == 0 && (actuallyPopRegMask & 0x0f) != 0)
    {
        // the low registers count towards the stack pop size
        if (actuallyPopRegMask & 0x1)
            stackPopSize += POINTER_SIZE;
        if (actuallyPopRegMask & 0x2)
            stackPopSize += POINTER_SIZE;
        if (actuallyPopRegMask & 0x4)
            stackPopSize += POINTER_SIZE;
        if (actuallyPopRegMask & 0x8)
            stackPopSize += POINTER_SIZE;

        // remove the bits now accounted for
        actuallyPopRegMask &= ~0x0f;
    }

    if (r7Cleanup)
    {
        if (stackPopSize != frameSize)
            VERIFY_FAILURE();
    }
    else
    {
        if (r7Frame)
        {
            // in this case the whole frame size may be larger than the r7 frame size we know about
            if (stackPopSize < frameSize)
                VERIFY_FAILURE();
        }
        else
        {
            if (stackPopSize != frameSize)
                VERIFY_FAILURE();
        }
    }

    UInt16 stackCleanupWords = pInfoHeader->ParmRegsPushedCount();

    if (shouldPopRegMask == actuallyPopRegMask)
    {
        // we got what we expected

        if ((actuallyPopRegMask & (1<<15)) != 0)
        {
            // if we popped pc, then this is the end of the epilog

            // however, if we still have pushed argument registers to cleanup,
            // we shouldn't get here
            if (pInfoHeader->AreParmRegsPushed())
                VERIFY_FAILURE();

            return true;
        }
    }
    else
    {
        // does this work out if we assume it's a call that pops
        // lr instead of pc and then terminates in a jump to reg?
        shouldPopRegMask ^= (1<<15)|(1<<14);
        if (shouldPopRegMask == actuallyPopRegMask)
        {
            // fine
        }
        else if (shouldPopRegMask == actuallyPopRegMask + (1<<14))
        {
            // we expected the epilog to pop lr, but it didn't
            // this may be a return with an additional stack cleanup
            // or a throw epilog that doesn't need lr anymore
            stackCleanupWords += 1;
        }
        else
        {
            VERIFY_FAILURE();
        }
    }

    if (stackCleanupWords)
    {
        CHECK_HIJACK_IN_EPILOG();

        // we may have "ldr pc,[sp],#stackCleanupWords*4"
        if (pEpilog[0] == 0xf85d && pEpilog[1] == 0xfb00 + stackCleanupWords*4)
        {
            // fine, and end of the epilog
            pEpilog += 2;
            return true;
        }
        // otherwise we should just have "add sp,#stackCleanupWords*4"
        else if (*pEpilog == 0xb000 + stackCleanupWords)
        {
            pEpilog += 1;
        }
        else
        {
            // 
            VERIFY_FAILURE();
        }
    }

    CHECK_HIJACK_IN_EPILOG();

    // we are satisfied if we see indirect jump through a register here
    // may be lr for normal return, or another register for tail calls
    if ((*pEpilog & 0xff87) == 0x4700)
        return true;

    // otherwise we expect to see a 32-bit branch
    if ((pEpilog[0] & 0xf800) == 0xf000 && (pEpilog[1] & 0xd000) == 0x9000)
        return true;

    VERIFY_FAILURE();

    return false;
}
#endif // _ARM_

bool EECodeManager::VerifyEpilogBytes(GCInfoHeader * pInfoHeader, Code * pEpilogStart, UInt32 epilogSize)
{
#ifdef _X86_
    return VerifyEpilogBytesX86(pInfoHeader, pEpilogStart, epilogSize);
#endif // _X86_
#ifdef _AMD64_
    return VerifyEpilogBytesAMD64(pInfoHeader, pEpilogStart, epilogSize);
#endif // _AMD64_
#ifdef _ARM_
    return VerifyEpilogBytesARM(pInfoHeader, pEpilogStart, epilogSize);
#endif
}

void EECodeManager::VerifyProlog(EEMethodInfo * /*pMethodInfo*/)
{
}

void EECodeManager::VerifyEpilog(EEMethodInfo * pMethodInfo)
{
    // @TODO: verify epilogs of funclets
    GCInfoHeader * pHeader = pMethodInfo->GetGCInfoHeader();

    Int32 epilogStart = -1;
    UInt32 epilogCount = 0;
    UInt32 epilogSize = 0;

    while (FindNextEpilog(pHeader, pMethodInfo->GetCodeSize(), 
                          pMethodInfo->GetEpilogTable(), &epilogStart, &epilogSize))
    {
        ASSERT(epilogStart >= 0);
        epilogCount++;
        Int32 codeOffset = epilogStart;
        Code * ip = ((Code *)pMethodInfo->GetCode()) + codeOffset;

        ASSERT(VerifyEpilogBytes(pHeader, ip, epilogSize));
    }

    ASSERT(epilogCount == pHeader->GetEpilogCount());
}

#include "gcdump.h"
void EECodeManager::DumpGCInfo(EEMethodInfo * pMethodInfo,
                               UInt8 * pbDeltaShortcutTable, 
                               UInt8 * pbUnwindInfoBlob, 
                               UInt8 * pbCallsiteInfoBlob)
{
    GCDump gcd;
    GCInfoHeader hdr;

    UInt8 * pbRawGCInfo = pMethodInfo->GetRawGCInfo();

    GCDump::Tables tables = { pbDeltaShortcutTable, pbUnwindInfoBlob, pbCallsiteInfoBlob };

    size_t cbHdr = gcd.DumpInfoHeader(pbRawGCInfo, &tables, &hdr);
    gcd.DumpGCTable(pbRawGCInfo + cbHdr, &tables, hdr);
}

#endif // _DEBUG
#endif // !DACCESS_COMPILE


// The controlPC parameter is used to decode the right GCInfoHeader in the case of an EH funclet
void EEMethodInfo::Init(PTR_VOID pvCode, UInt32 cbCodeSize, PTR_UInt8 pbRawGCInfo, PTR_VOID pvEHInfo)
{
    m_pvCode = pvCode;
    m_cbCodeSize = cbCodeSize;
    m_pbRawGCInfo = pbRawGCInfo;
    m_pvEHInfo = pvEHInfo;

    m_pbGCInfo = (PTR_UInt8)(size_t)-1;

    m_infoHdr.Init();
}

void EEMethodInfo::DecodeGCInfoHeader(UInt32 methodOffset, PTR_UInt8 pbUnwindInfoBlob)
{
    PTR_UInt8   pbGcInfo = m_pbRawGCInfo;
    PTR_UInt8   pbStackChangeString;
    PTR_UInt8   pbUnwindInfo;

    UInt32  unwindInfoBlobOffset = VarInt::ReadUnsigned(pbGcInfo);
    bool    inlineUnwindInfo = (unwindInfoBlobOffset == 0);

    if (inlineUnwindInfo)
    {
        // it is inline..
        pbUnwindInfo = pbGcInfo;
        size_t headerSize;
        pbStackChangeString = m_infoHdr.DecodeHeader(methodOffset, pbUnwindInfo, &headerSize);
        pbGcInfo += headerSize;
    }
    else
    {
        // The offset was adjusted by 1 to reserve the 0 encoding for the inline case, so we re-adjust it to
        // the actual offset here.
        pbUnwindInfo = pbUnwindInfoBlob + unwindInfoBlobOffset - 1;
        pbStackChangeString = m_infoHdr.DecodeHeader(methodOffset, pbUnwindInfo, NULL);
    }

    m_pbEpilogTable = pbGcInfo;

    //
    // skip past epilog table
    //
    if (!m_infoHdr.IsEpilogAtEnd())
    {
        for (UInt32 i = 0; i < m_infoHdr.GetEpilogCount(); i++)
        {
            VarInt::SkipUnsigned(pbGcInfo);
            if (m_infoHdr.HasVaryingEpilogSizes())
                VarInt::SkipUnsigned(pbGcInfo);
        }
    }

    m_pbGCInfo = pbGcInfo;
}

PTR_UInt8 EEMethodInfo::GetGCInfo()
{
    ASSERT_MSG(m_pbGCInfo != (PTR_UInt8)(size_t)-1, 
               "You must call DecodeGCInfoHeader first");

    ASSERT(m_pbGCInfo != NULL);
    return m_pbGCInfo;
}

PTR_UInt8 EEMethodInfo::GetEpilogTable()
{
    ASSERT_MSG(m_pbGCInfo != (PTR_UInt8)(size_t)-1, 
               "You must call DecodeGCInfoHeader first");

    ASSERT(m_pbEpilogTable != NULL);
    return m_pbEpilogTable;
}

GCInfoHeader * EEMethodInfo::GetGCInfoHeader()
{ 
    ASSERT_MSG(m_pbGCInfo != (PTR_UInt8)(size_t)-1, 
               "You must call DecodeGCInfoHeader first");

    return &m_infoHdr; 
}
