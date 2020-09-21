// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "daccess.h"
#include "rhassert.h"
#include "ICodeManager.h"
#include "UnixNativeCodeManager.h"

#define UNW_STEP_SUCCESS 1
#define UNW_STEP_END     0

#ifdef __APPLE__
#include <mach-o/getsect.h>
#endif

#include <regdisplay.h>
#include "UnwindHelpers.h"

// libunwind headers
#ifndef TARGET_ARM64
#include <libunwind.h>
#endif
#include <src/config.h>
#include <src/Registers.hpp>
#include <src/AddressSpace.hpp>
#if defined(TARGET_ARM)
#include <src/libunwind_ext.h>
#endif
#include <src/UnwindCursor.hpp>


#if defined(TARGET_AMD64)
using libunwind::Registers_x86_64;
#elif defined(TARGET_ARM)
using libunwind::Registers_arm;
#elif defined(TARGET_ARM64)
//Nothing to do here for ARM64 but we need to avoid the unsupported architecture error
#elif defined(TARGET_X86)
using libunwind::Registers_x86;
#else
#error "Unwinding is not implemented for this architecture yet."
#endif
using libunwind::LocalAddressSpace;
using libunwind::EHHeaderParser;
#if _LIBUNWIND_SUPPORT_DWARF_UNWIND
using libunwind::DwarfInstructions;
#endif
using libunwind::UnwindInfoSections;

LocalAddressSpace _addressSpace;

#ifdef TARGET_AMD64

// Shim that implements methods required by libunwind over REGDISPLAY
struct Registers_REGDISPLAY : REGDISPLAY
{
    static int  getArch() { return libunwind::REGISTERS_X86_64; }

    inline uint64_t getRegister(int regNum) const
    {
        switch (regNum)
        {
        case UNW_REG_IP:
            return IP;
        case UNW_REG_SP:
            return SP;
        case UNW_X86_64_RAX:
            return *pRax;
        case UNW_X86_64_RDX:
            return *pRdx;
        case UNW_X86_64_RCX:
            return *pRcx;
        case UNW_X86_64_RBX:
            return *pRbx;
        case UNW_X86_64_RSI:
            return *pRsi;
        case UNW_X86_64_RDI:
            return *pRdi;
        case UNW_X86_64_RBP:
            return *pRbp;
        case UNW_X86_64_RSP:
            return SP;
        case UNW_X86_64_R8:
            return *pR8;
        case UNW_X86_64_R9:
            return *pR9;
        case UNW_X86_64_R10:
            return *pR10;
        case UNW_X86_64_R11:
            return *pR11;
        case UNW_X86_64_R12:
            return *pR12;
        case UNW_X86_64_R13:
            return *pR13;
        case UNW_X86_64_R14:
            return *pR14;
        case UNW_X86_64_R15:
            return *pR15;
        }

        // Unsupported register requested
        abort();
    }

    inline void setRegister(int regNum, uint64_t value, uint64_t location)
    {
        switch (regNum)
        {
        case UNW_REG_IP:
            IP = value;
            pIP = (PTR_PCODE)location;
            return;
        case UNW_REG_SP:
            SP = value;
            return;
        case UNW_X86_64_RAX:
            pRax = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_RDX:
            pRdx = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_RCX:
            pRcx = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_RBX:
            pRbx = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_RSI:
            pRsi = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_RDI:
            pRdi = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_RBP:
            pRbp = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_RSP:
            SP = value;
            return;
        case UNW_X86_64_R8:
            pR8 = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_R9:
            pR9 = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_R10:
            pR10 = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_R11:
            pR11 = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_R12:
            pR12 = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_R13:
            pR13 = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_R14:
            pR14 = (PTR_UIntNative)location;
            return;
        case UNW_X86_64_R15:
            pR15 = (PTR_UIntNative)location;
            return;
        }

        // Unsupported x86_64 register
        abort();
    }

    // N/A for x86_64
    inline bool validFloatRegister(int) { return false; }
    inline bool validVectorRegister(int) { return false; }

    inline static int  lastDwarfRegNum() { return 16; }

    inline bool validRegister(int regNum) const
    {
        if (regNum == UNW_REG_IP)
            return true;
        if (regNum == UNW_REG_SP)
            return true;
        if (regNum < 0)
            return false;
        if (regNum > 15)
            return false;
        return true;
    }

    // N/A for x86_64
    inline double getFloatRegister(int) const { abort(); }
    inline   void setFloatRegister(int, double) { abort(); }
    inline double getVectorRegister(int) const { abort(); }
    inline   void setVectorRegister(int, ...) { abort(); }

    uint64_t  getSP() const { return SP; }
    void      setSP(uint64_t value, uint64_t location) { SP = value; }

    uint64_t  getIP() const { return IP; }

    void      setIP(uint64_t value, uint64_t location)
    {
        IP = value;
        pIP = (PTR_PCODE)location;
    }

    uint64_t  getRBP() const { return *pRbp; }
    void      setRBP(uint64_t value, uint64_t location) { pRbp = (PTR_UIntNative)location; }
    uint64_t  getRBX() const { return *pRbx; }
    void      setRBX(uint64_t value, uint64_t location) { pRbx = (PTR_UIntNative)location; }
    uint64_t  getR12() const { return *pR12; }
    void      setR12(uint64_t value, uint64_t location) { pR12 = (PTR_UIntNative)location; }
    uint64_t  getR13() const { return *pR13; }
    void      setR13(uint64_t value, uint64_t location) { pR13 = (PTR_UIntNative)location; }
    uint64_t  getR14() const { return *pR14; }
    void      setR14(uint64_t value, uint64_t location) { pR14 = (PTR_UIntNative)location; }
    uint64_t  getR15() const { return *pR15; }
    void      setR15(uint64_t value, uint64_t location) { pR15 = (PTR_UIntNative)location; }
};

#endif // TARGET_AMD64
#if defined(TARGET_X86)
struct Registers_REGDISPLAY : REGDISPLAY
{
    static int  getArch() { return libunwind::REGISTERS_X86; }

    inline uint64_t getRegister(int regNum) const
    {
        switch (regNum)
        {
        case UNW_REG_IP:
            return IP;
        case UNW_REG_SP:
            return SP;
        case UNW_X86_EAX:
            return *pRax;
        case UNW_X86_EDX:
            return *pRdx;
        case UNW_X86_ECX:
            return *pRcx;
        case UNW_X86_EBX:
            return *pRbx;
        case UNW_X86_ESI:
            return *pRsi;
        case UNW_X86_EDI:
            return *pRdi;
        case UNW_X86_EBP:
            return *pRbp;
        case UNW_X86_ESP:
            return SP;
        }

        // Unsupported register requested
        abort();
    }

    inline void setRegister(int regNum, uint64_t value, uint64_t location)
    {
        switch (regNum)
        {
        case UNW_REG_IP:
            IP = value;
            pIP = (PTR_PCODE)location;
            return;
        case UNW_REG_SP:
            SP = value;
            return;
        case UNW_X86_EAX:
            pRax = (PTR_UIntNative)location;
            return;
        case UNW_X86_EDX:
            pRdx = (PTR_UIntNative)location;
            return;
        case UNW_X86_ECX:
            pRcx = (PTR_UIntNative)location;
            return;
        case UNW_X86_EBX:
            pRbx = (PTR_UIntNative)location;
            return;
        case UNW_X86_ESI:
            pRsi = (PTR_UIntNative)location;
            return;
        case UNW_X86_EDI:
            pRdi = (PTR_UIntNative)location;
            return;
        case UNW_X86_EBP:
            pRbp = (PTR_UIntNative)location;
            return;
        case UNW_X86_ESP:
            SP = value;
            return;
        }

        // Unsupported x86_64 register
        abort();
    }

    // N/A for x86
    inline bool validFloatRegister(int) { return false; }
    inline bool validVectorRegister(int) { return false; }

    inline static int  lastDwarfRegNum() { return 16; }

    inline bool validRegister(int regNum) const
    {
        if (regNum == UNW_REG_IP)
            return true;
        if (regNum == UNW_REG_SP)
            return true;
        if (regNum < 0)
            return false;
        if (regNum > 15)
            return false;
        return true;
    }

    // N/A for x86
    inline double getFloatRegister(int) const { abort(); }
    inline   void setFloatRegister(int, double) { abort(); }
    inline double getVectorRegister(int) const { abort(); }
    inline   void setVectorRegister(int, ...) { abort(); }

    void      setSP(uint64_t value, uint64_t location) { SP = value; }

    uint64_t  getIP() const { return IP; }

    void      setIP(uint64_t value, uint64_t location)
    {
        IP = value;
        pIP = (PTR_PCODE)location;
    }

    uint64_t  getEBP() const { return *pRbp; }
    void      setEBP(uint64_t value, uint64_t location) { pRbp = (PTR_UIntNative)location; }
    uint64_t  getEBX() const { return *pRbx; }
    void      setEBX(uint64_t value, uint64_t location) { pRbx = (PTR_UIntNative)location; }
};

#endif // TARGET_X86
#if defined(TARGET_ARM)

class Registers_arm_rt: public libunwind::Registers_arm {
public:
    Registers_arm_rt() { abort(); };
    Registers_arm_rt(void *registers) { regs = (REGDISPLAY *)registers; };
    uint32_t    getRegister(int num);
    void        setRegister(int num, uint32_t value, uint32_t location);
    uint32_t    getRegisterLocation(int regNum) const { abort();}
    unw_fpreg_t getFloatRegister(int num) { abort();}
    void        setFloatRegister(int num, unw_fpreg_t value) {abort();}
    bool        validVectorRegister(int num) const { abort();}
    uint32_t    getVectorRegister(int num) const {abort();};
    void        setVectorRegister(int num, uint32_t value) {abort();};
    void        jumpto() { abort();};
    uint32_t    getSP() const         { return regs->SP;}
    void        setSP(uint32_t value, uint32_t location) { regs->SP = value;}
    uint32_t    getIP() const         { return regs->IP;}
    void        setIP(uint32_t value, uint32_t location)
    { regs->IP = value; regs->pIP = (PTR_UIntNative)location; }
    void saveVFPAsX() {abort();};
private:
    REGDISPLAY *regs;
};

inline uint32_t Registers_arm_rt::getRegister(int regNum) {
    if (regNum == UNW_REG_SP || regNum == UNW_ARM_SP)
        return regs->SP;

    if (regNum == UNW_ARM_LR)
        return *regs->pLR;

    if (regNum == UNW_REG_IP || regNum == UNW_ARM_IP)
        return regs->IP;

    switch (regNum)
    {
    case (UNW_ARM_R0):
        return *regs->pR0;
    case (UNW_ARM_R1):
        return *regs->pR1;
    case (UNW_ARM_R2):
        return *regs->pR2;
    case (UNW_ARM_R3):
        return *regs->pR3;
    case (UNW_ARM_R4):
        return *regs->pR4;
    case (UNW_ARM_R5):
        return *regs->pR5;
    case (UNW_ARM_R6):
        return *regs->pR6;
    case (UNW_ARM_R7):
        return *regs->pR7;
    case (UNW_ARM_R8):
        return *regs->pR8;
    case (UNW_ARM_R9):
        return *regs->pR9;
    case (UNW_ARM_R10):
        return *regs->pR10;
    case (UNW_ARM_R11):
        return *regs->pR11;
    case (UNW_ARM_R12):
        return *regs->pR12;
    }

    PORTABILITY_ASSERT("unsupported arm register");
}

void Registers_arm_rt::setRegister(int num, uint32_t value, uint32_t location)
{

    if (num == UNW_REG_SP || num == UNW_ARM_SP) {
        regs->SP = (UIntNative )value;
        return;
    }

    if (num == UNW_ARM_LR) {
        regs->pLR = (PTR_UIntNative)location;
        return;
    }

    if (num == UNW_REG_IP || num == UNW_ARM_IP) {
        regs->IP = value;
        /* the location could be NULL, we could try to recover
           pointer to value in stack from pLR */
        if ((!location) && (regs->pLR) && (*regs->pLR == value))
            regs->pIP = regs->pLR;
        else
            regs->pIP = (PTR_UIntNative)location;
        return;
    }

    switch (num)
    {
    case (UNW_ARM_R0):
        regs->pR0 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R1):
        regs->pR1 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R2):
        regs->pR2 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R3):
        regs->pR3 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R4):
        regs->pR4 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R5):
        regs->pR5 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R6):
        regs->pR6 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R7):
        regs->pR7 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R8):
        regs->pR8 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R9):
        regs->pR9 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R10):
        regs->pR10 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R11):
        regs->pR11 = (PTR_UIntNative)location;
        break;
    case (UNW_ARM_R12):
        regs->pR12 = (PTR_UIntNative)location;
        break;
    default:
        PORTABILITY_ASSERT("unsupported arm register");
    }
}

#endif // TARGET_ARM

#ifndef TARGET_ARM64

bool DoTheStep(uintptr_t pc, UnwindInfoSections uwInfoSections, REGDISPLAY *regs)
{
#if defined(TARGET_AMD64)
    libunwind::UnwindCursor<LocalAddressSpace, Registers_x86_64> uc(_addressSpace);
#elif defined(TARGET_ARM)
    libunwind::UnwindCursor<LocalAddressSpace, Registers_arm_rt> uc(_addressSpace, regs);
#elif defined(HOST_X86)
    libunwind::UnwindCursor<LocalAddressSpace, Registers_x86> uc(_addressSpace, regs);
#else
    #error "Unwinding is not implemented for this architecture yet."
#endif

#if _LIBUNWIND_SUPPORT_DWARF_UNWIND
    bool retVal = uc.getInfoFromDwarfSection(pc, uwInfoSections, 0 /* fdeSectionOffsetHint */);
    if (!retVal)
    {
        return false;
    }

    unw_proc_info_t procInfo;
    uc.getInfo(&procInfo);

#if defined(TARGET_ARM)
    DwarfInstructions<LocalAddressSpace, Registers_arm_rt> dwarfInst;
    int stepRet = dwarfInst.stepWithDwarf(_addressSpace, pc, procInfo.unwind_info, *(Registers_arm_rt*)regs);
#else
    DwarfInstructions<LocalAddressSpace, Registers_REGDISPLAY> dwarfInst;
    int stepRet = dwarfInst.stepWithDwarf(_addressSpace, pc, procInfo.unwind_info, *(Registers_REGDISPLAY*)regs);
#endif

    if (stepRet != UNW_STEP_SUCCESS)
    {
        return false;
    }

    regs->pIP = PTR_PCODE(regs->SP - sizeof(TADDR));

#elif defined(_LIBUNWIND_ARM_EHABI)
    uc.setInfoBasedOnIPRegister(true);
    int stepRet = uc.step();
    if ((stepRet != UNW_STEP_SUCCESS) && (stepRet != UNW_STEP_END))
    {
        return false;
    }
#endif

    return true;
}

#endif //!TARGET_ARM64

bool UnwindHelpers::StepFrame(MethodInfo* pMethodInfo, REGDISPLAY *regs)
{
#ifdef TARGET_ARM64
    int8_t mode;

    PTR_VOID pUnwindInfo = UnixNativeCodeManager::GetMethodUnwindInfo(pMethodInfo, mode);

    if (pUnwindInfo != NULL)
    {
        if (mode == 0)
        {
            return StepFrameCompact(regs, pUnwindInfo);
        }

        return StepFrame(regs, pUnwindInfo);
    }

    return false;
#else
    UnwindInfoSections uwInfoSections;
#if _LIBUNWIND_SUPPORT_DWARF_UNWIND
    uintptr_t pc = regs->GetIP();
    if (!_addressSpace.findUnwindSections(pc, uwInfoSections))
    {
        return false;
    }
    return DoTheStep(pc, uwInfoSections, regs);

    //return DoTheStep(pc, uwInfoSections, regs);
#elif defined(_LIBUNWIND_ARM_EHABI)
    // unwind section is located later for ARM
    // pc will be taked from regs parameter
    return DoTheStep(0, uwInfoSections, regs);
#else
    PORTABILITY_ASSERT("StepFrame");
#endif
#endif // TARGET_ARM64
}

#ifdef TARGET_ARM64

bool UnwindHelpers::StepFrame(REGDISPLAY* regs, PTR_VOID unwindInfo)
{
    uint8_t* pData = (uint8_t*)unwindInfo;

    int cfaRegister = *pData++;
    uint32_t cfaOffset = *((uint16_t*)pData) * 8;
    pData += sizeof(uint16_t);

    uint8_t* baseLocation;

    if (cfaRegister <= 30)
    {
        baseLocation = (uint8_t*)*((&regs->pX0)[cfaRegister]);
    }
    else
    {
        ASSERT(cfaRegister == 31);
        baseLocation = (uint8_t*)regs->GetSP();
    }

    regs->SetSP((UIntNative)baseLocation + cfaOffset);

    while (*pData != 0xFF)
    {
        int reg = *pData++;
        uint32_t offset = *((uint16_t*)pData) * 8;
        pData += sizeof(uint16_t);

        if (reg >= 0 && reg <= 32)
        {
            (&regs->pX0)[reg] = (PTR_UIntNative)(baseLocation + offset);
        }
        else
        {
            ASSERT(reg < 40);
            regs->D[reg - 32] = *((UInt64*)(baseLocation + offset));
        }
    }
    
    regs->SetAddrOfIP(regs->pLR);
    regs->SetIP(*regs->GetAddrOfIP());

    return true;
}

bool UnwindHelpers::StepFrameCompact(REGDISPLAY* regs, PTR_VOID unwindInfo)
{
    // Encoding
    // FSSR RRRO OOOO OOOO
    // F = Frame type 0 = FP ; 1 = SP
    // O = CFA offset / 8
    // R = number of additional registers X19 - X28
    // S = Offset for FP/LR / 8

    uint16_t code = *(uint16_t*)unwindInfo;
    uint32_t cfaOffset = (code & 0x1F) * 8;
    int regCount = (code >> 9) & 0xF;
    int frameOffset = ((code >> 13) & 0x3) * 8;
    uint8_t* baseLocation;

    if ((code & 0x8000) == 0x8000)
    {
        baseLocation = (uint8_t*)regs->GetSP();
    }
    else
    {
        baseLocation = (uint8_t*)*regs->pFP;
    }

    PTR_UIntNative regLocation = (PTR_UIntNative)baseLocation + (-8 + cfaOffset - regCount * 8);

    for (int i = 0; i < regCount; i++)
    {
        (&regs->pX0)[19 + i] = regLocation++;
    }

    regs->pFP = (PTR_UIntNative)(baseLocation + frameOffset);
    regs->pLR = (PTR_UIntNative)(baseLocation + frameOffset + 8);

    regs->SetSP((UIntNative)baseLocation + cfaOffset);

    regs->SetAddrOfIP(regs->pLR);
    regs->SetIP(*regs->GetAddrOfIP());

    return true;
}

#endif // TARGET_ARM64
