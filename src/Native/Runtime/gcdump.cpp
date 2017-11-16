// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
/*****************************************************************************
 *                                  GCDump.cpp
 *
 * Defines functions to display the GCInfo as defined by the GC-encoding 
 * spec. The GC information may be either dynamically created by a 
 * Just-In-Time compiler conforming to the standard code-manager spec,
 * or may be persisted by a managed native code compiler conforming
 * to the standard code-manager spec.
 */
#include "common.h"

#if (defined(_DEBUG) || defined(DACCESS_COMPILE))

#include "gcenv.h"
#include "varint.h"
#include "gcinfo.h"
#include "gcdump.h"

/*****************************************************************************/

#ifdef DACCESS_COMPILE
static void DacNullPrintf(const char* , ...) {}
#endif

GCDump::GCDump()
{
#ifndef DACCESS_COMPILE
    // By default, use the standard printf function to dump 
    GCDump::gcPrintf = (printfFtn) ::printf;
#else
    // Default for DAC is a no-op.
    GCDump::gcPrintf = DacNullPrintf;
#endif
}



/*****************************************************************************/

static const char * const calleeSaveRegMaskBitNumberToName[] = 
{
#ifdef _ARM_
    "R4",
    "R5",
    "R6",
    "R7",
    "R8",
    "R9",
    "R10",
    "R11",
    "LR",
#else // _ARM_
    "EBX",
    "ESI",
    "EDI",
    "EBP",
    "R12",
    "R13",
    "R14",
    "R15"
#endif // _ARM_
};

size_t FASTCALL   GCDump::DumpInfoHeader (PTR_UInt8      gcInfo,
                                          Tables *       pTables,
                                          GCInfoHeader * pHeader         /* OUT */
                                          )
{
    size_t    headerSize = 0;
    PTR_UInt8 gcInfoStart = gcInfo;
    PTR_UInt8 pbStackChanges = 0;
    PTR_UInt8 pbUnwindInfo = 0;

    unsigned unwindInfoBlobOffset = VarInt::ReadUnsigned(gcInfo);
    bool    inlineUnwindInfo = (unwindInfoBlobOffset == 0);

    if (inlineUnwindInfo)
    {
        // it is inline..
        pbUnwindInfo = gcInfo;
    }
    else
    {
        // The offset was adjusted by 1 to reserve the 0 encoding for the inline case, so we re-adjust it to
        // the actual offset here.
        pbUnwindInfo = pTables->pbUnwindInfoBlob + unwindInfoBlobOffset - 1;
    }

    // @TODO: decode all funclet headers as well.
    pbStackChanges = pHeader->DecodeHeader(0, pbUnwindInfo, &headerSize );

    if (inlineUnwindInfo)
        gcInfo += headerSize;

    unsigned epilogCount = pHeader->GetEpilogCount();
    bool     epilogAtEnd = pHeader->IsEpilogAtEnd();

    gcPrintf("   prologSize:     %d\n", pHeader->GetPrologSize());
    if (pHeader->HasVaryingEpilogSizes())
        gcPrintf("   epilogSize:     (varies)\n");
    else
        gcPrintf("   epilogSize:     %d\n", pHeader->GetFixedEpilogSize());

    gcPrintf("   epilogCount:    %d %s\n", epilogCount, epilogAtEnd ? "[end]" : "");
    const char * returnKind = "????";
    unsigned reversePinvokeFrameOffset = 0;     // it can't be 0 because [ebp+0] is the previous ebp
    switch (pHeader->GetReturnKind())
    {
        case GCInfoHeader::MRK_ReturnsScalar:   returnKind = "scalar";    break;
        case GCInfoHeader::MRK_ReturnsObject:   returnKind = "object";    break;
        case GCInfoHeader::MRK_ReturnsByref:    returnKind = "byref";     break;
        case GCInfoHeader::MRK_ReturnsToNative: 
            returnKind = "to native"; 
            reversePinvokeFrameOffset = pHeader->GetReversePinvokeFrameOffset();
            break;
        case GCInfoHeader::MRK_Unknown:
            //ASSERT("Unexpected return kind")
            break;
    }
    gcPrintf("   returnKind:     %s\n", returnKind);
    gcPrintf("   frameKind:      %s", pHeader->HasFramePointer() ? "EBP" : "ESP");
#ifdef _TARGET_AMD64_
    if (pHeader->HasFramePointer())
        gcPrintf(" offset: %d", pHeader->GetFramePointerOffset());
#endif // _AMD64_
    gcPrintf("\n");
    gcPrintf("   frameSize:      %d\n", pHeader->GetFrameSize());

    if (pHeader->HasDynamicAlignment()) {
        gcPrintf("   alignment:      %d\n", (1 << pHeader->GetDynamicAlignment()));
        if (pHeader->GetParamPointerReg() != RN_NONE) {
            gcPrintf("   paramReg:       %d\n", pHeader->GetParamPointerReg());
        }
    }

    gcPrintf("   savedRegs:      ");
    CalleeSavedRegMask savedRegs = pHeader->GetSavedRegs();
    CalleeSavedRegMask mask = (CalleeSavedRegMask) 1;
    for (int i = 0; i < RBM_CALLEE_SAVED_REG_COUNT; i++)
    {
        if (savedRegs & mask)
        {
            gcPrintf("%s ", calleeSaveRegMaskBitNumberToName[i]);
        }
        mask = (CalleeSavedRegMask)(mask << 1);
    }
    gcPrintf("\n");

#ifdef _TARGET_ARM_
    gcPrintf("   parmRegsPushedCount: %d\n", pHeader->ParmRegsPushedCount());
#endif

#ifdef _TARGET_X86_
    gcPrintf("   returnPopSize:  %d\n", pHeader->GetReturnPopSize());
    if (pHeader->HasStackChanges())
    {
        // @TODO: need to read the stack changes string that follows
        ASSERT(!"NYI -- stack changes for ESP frames");
    }
#endif

    if (reversePinvokeFrameOffset != 0)
    {
        gcPrintf("   reversePinvokeFrameOffset: 0x%02x\n", reversePinvokeFrameOffset);
    }


    if (!epilogAtEnd || (epilogCount > 2))
    {
        gcPrintf("   epilog offsets: ");
        unsigned previousOffset = 0;
        for (unsigned idx = 0; idx < epilogCount; idx++)
        {
            unsigned newOffset = previousOffset + VarInt::ReadUnsigned(gcInfo);
            gcPrintf("0x%04x ", newOffset);
            if (pHeader->HasVaryingEpilogSizes())
                gcPrintf("(%u bytes) ", VarInt::ReadUnsigned(gcInfo));
            previousOffset = newOffset;
        }
        gcPrintf("\n");
    }

    return gcInfo - gcInfoStart;
}

void GCDump::PrintLocalSlot(UInt32 slotNum, GCInfoHeader const * pHeader)
{
    // @TODO: print both EBP/ESP offsets where appropriate
#ifdef _TARGET_ARM_
    gcPrintf("local slot 0n%d, [R7+%02X] \n", slotNum, 
                ((GCInfoHeader*)pHeader)->GetFrameSize() - ((slotNum + 1) * POINTER_SIZE));
#else
    const char* regAndSign = "EBP-";
    size_t offset = pHeader->GetPreservedRegsSaveSize() + (slotNum * POINTER_SIZE);
# ifdef _TARGET_AMD64_
    if (((GCInfoHeader*)pHeader)->GetFramePointerOffset() == 0)
    {
        regAndSign = "RBP-";
    }
    else
    {
        regAndSign = "RBP+";
        offset = (slotNum * POINTER_SIZE);
    }
# endif
    gcPrintf("local slot 0n%d, [%s%02X] \n", slotNum, regAndSign, offset);
#endif
}

void GCDump::DumpCallsiteString(UInt32 callsiteOffset, PTR_UInt8 pbCallsiteString, 
                                GCInfoHeader const * pHeader)
{
    gcPrintf("%04x: ", callsiteOffset);

    int count = 0;
    UInt8 b;
    PTR_UInt8 pCursor = pbCallsiteString;

    bool last = false;
    bool first = true;

    do
    {
        if (!first)
            gcPrintf("      ");

        first = false;

        b = *pCursor++;
        last = ((b & 0x20) == 0x20);

        switch (b & 0xC0)
        {
        case 0x00:
            {
                // case 2 -- "register set"
                gcPrintf("%02x          | 2  ", b);
#ifdef _TARGET_ARM_
                if (b & CSR_MASK_R4) { gcPrintf("R4 "); count++; }
                if (b & CSR_MASK_R5) { gcPrintf("R5 "); count++; }
                if (b & CSR_MASK_R6) { gcPrintf("R6 "); count++; }
                if (b & CSR_MASK_R7) { gcPrintf("R7 "); count++; }
                if (b & CSR_MASK_R8) { gcPrintf("R8 "); count++; }
#elif defined(_TARGET_ARM64_)
                // ARM64TODO: not all of these are needed?
                if (b & CSR_MASK_X19) { gcPrintf("X19 "); count++; }
                if (b & CSR_MASK_X20) { gcPrintf("X20 "); count++; }
                if (b & CSR_MASK_X21) { gcPrintf("X21 "); count++; }
                if (b & CSR_MASK_X22) { gcPrintf("X22 "); count++; }
                if (b & CSR_MASK_X23) { gcPrintf("X23 "); count++; }
                if (b & CSR_MASK_X24) { gcPrintf("X24 "); count++; }
                if (b & CSR_MASK_X25) { gcPrintf("X25 "); count++; }
                if (b & CSR_MASK_X26) { gcPrintf("X26 "); count++; }
                if (b & CSR_MASK_X27) { gcPrintf("X27 "); count++; }
                if (b & CSR_MASK_X28) { gcPrintf("X28 "); count++; }
#else // _ARM_
                if (b & CSR_MASK_RBX) { gcPrintf("RBX "); count++; }
                if (b & CSR_MASK_RSI) { gcPrintf("RSI "); count++; }
                if (b & CSR_MASK_RDI) { gcPrintf("RDI "); count++; }
                if (b & CSR_MASK_RBP) { gcPrintf("RBP "); count++; }
                if (b & CSR_MASK_R12) { gcPrintf("R12 "); count++; }
#endif // _ARM_
                gcPrintf("\n");
            }
            break;

        case 0x40:
            {
                // case 3 -- "register"
                const char* regName = "???";
                const char* interior = (b & 0x10) ? "+" : "";
                const char* pinned   = (b & 0x08) ? "!" : "";

                switch (b & 0x7)
                {
#ifdef _TARGET_ARM_
                case CSR_NUM_R4: regName = "R4"; break;
                case CSR_NUM_R5: regName = "R5"; break;
                case CSR_NUM_R6: regName = "R6"; break;
                case CSR_NUM_R7: regName = "R7"; break;
                case CSR_NUM_R8: regName = "R8"; break;
                case CSR_NUM_R9: regName = "R9"; break;
                case CSR_NUM_R10: regName = "R10"; break;
                case CSR_NUM_R11: regName = "R11"; break;
#elif defined(_TARGET_ARM64_)
                case CSR_NUM_X19: regName = "X19"; break;
                case CSR_NUM_X20: regName = "X20"; break;
                case CSR_NUM_X21: regName = "X21"; break;
                case CSR_NUM_X22: regName = "X22"; break;
                case CSR_NUM_X23: regName = "X23"; break;
                case CSR_NUM_X24: regName = "X24"; break;
                case CSR_NUM_X25: regName = "X25"; break;
                case CSR_NUM_X26: regName = "X26"; break;
                case CSR_NUM_X27: regName = "X27"; break;
                case CSR_NUM_X28: regName = "X28"; break;
#else // _ARM_
                case CSR_NUM_RBX: regName = "RBX"; break;
                case CSR_NUM_RSI: regName = "RSI"; break;
                case CSR_NUM_RDI: regName = "RDI"; break;
                case CSR_NUM_RBP: regName = "RBP"; break;
#ifdef _TARGET_AMD64_
                case CSR_NUM_R12: regName = "R12"; break;
                case CSR_NUM_R13: regName = "R13"; break;
                case CSR_NUM_R14: regName = "R14"; break;
                case CSR_NUM_R15: regName = "R15"; break;
#endif // _TARGET_AMD64_
#endif // _ARM_
                }
                gcPrintf("%02x          | 3  %s%s%s \n", b, regName, interior, pinned);
                count++; 
            }
            break;

        case 0x80:
            {
                if (b & 0x10)
                {
                    // case 4 -- "local slot set"
                    gcPrintf("%02x          | 4  ", b);
                    bool isFirst = true;

                    int mask = 0x01;
                    int slotNum = 0;
                    while (mask <= 0x08)
                    {
                        if (b & mask)
                        {
                            if (!isFirst)
                            {
                                if (!first)
                                    gcPrintf("      ");
                                gcPrintf("            |    ");
                            }

                            PrintLocalSlot(slotNum, pHeader);

                            isFirst = false;
                            count++; 
                        }
                        mask <<= 1;
                        slotNum++;
                    }
                }
                else
                {
                    // case 5 -- "local slot"
                    int slotNum = (int)(b & 0xF) + 4;
                    gcPrintf("%02x          | 5  ", b);
                    PrintLocalSlot(slotNum, pHeader);

                    count++;
                }
            }
            break;
        case 0xC0:
            {
                gcPrintf("%02x ", b);
                unsigned mask = 0;
                PTR_UInt8 pInts = pCursor;
                unsigned offset = VarInt::ReadUnsigned(pCursor);
                const char* interior = (b & 0x10) ? "+" : "";
                const char* pinned   = (b & 0x08) ? "!" : "";
#ifdef _TARGET_ARM_
                const char* baseReg  = (b & 0x04) ? "R7" : "SP";
#else
                const char* baseReg  = (b & 0x04) ? "EBP" : "ESP";
#endif
                const char* sign     = (b & 0x02) ? "-" : "+";
                if (b & 0x01)
                {
                    mask = VarInt::ReadUnsigned(pCursor);
                }

                int c = 1;
                while (pInts != pCursor)
                {
                    gcPrintf("%02x ", *pInts++);
                    c++;
                }

                for (; c < 4; c++)
                {
                    gcPrintf("   ");
                }

                gcPrintf("| 6  [%s%s%02X]%s%s\n", baseReg, sign, offset, interior, pinned);
                count++; 

                while (mask > 0)
                {
                    offset += POINTER_SIZE;
                    if (mask & 1)
                    {
                        if (!first)
                            gcPrintf("      ");

                        gcPrintf("            |    [%s%s%02X]%s%s\n", baseReg, sign, offset, interior, pinned);
                        count++; 
                    }
                    mask >>= 1;
                }
            }
            break;
        }
    }
    while (!last);

    //gcPrintf("\n");
}




size_t   FASTCALL   GCDump::DumpGCTable (PTR_UInt8              gcInfo,
                                         Tables *               pTables,
                                         const GCInfoHeader&    header)
{
    //
    // Decode the method GC info 
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

    PTR_UInt8 pCursor = gcInfo;
    UInt32 curOffset = 0;

    for (;;)
    {
        UInt8 b = *pCursor++;
        unsigned infoOffset;

        if (b & 0x80)
        {
            UInt8 lowBits = (b & 0x7F);
            // FORWARDER
            if (lowBits == 0)
            {
                curOffset += VarInt::ReadUnsigned(pCursor);
                continue;
            }
            else 
            if (lowBits == 0x7F) // STRING TERMINATOR
                break;

            // BIG ENCODING
            curOffset += lowBits;
            infoOffset = VarInt::ReadUnsigned(pCursor);
        }
        else
        {
            // SMALL ENCODING
            infoOffset = (b & 0x7);
            curOffset += pTables->pbDeltaShortcutTable[b >> 3];
        }

        DumpCallsiteString(curOffset, pTables->pbCallsiteInfoBlob + infoOffset, &header);
    }

    gcPrintf("-------\n");

    return 0;
}

#endif // _DEBUG || DACCESS_COMPILE
