// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "daccess.h"
#include "rhassert.h"

#define UNW_STEP_SUCCESS 1
#define UNW_STEP_END     0

#ifdef __APPLE__
#include <mach-o/getsect.h>
#endif

#include <regdisplay.h>
#include "UnwindHelpers.h"

// libunwind headers
#include <libunwind.h>
#include <src/config.h>
#include <src/Registers.hpp>
#include <src/AddressSpace.hpp>
#if defined(_TARGET_ARM_)
#include <src/libunwind_ext.h>
#endif
#include <src/UnwindCursor.hpp>

#if defined(_TARGET_AMD64_)
using libunwind::Registers_x86_64;
#elif defined(_TARGET_ARM_)
using libunwind::Registers_arm;
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

#ifdef __APPLE__

struct dyld_unwind_sections
{
    const struct mach_header*   mh;
    const void*                 dwarf_section;
    uintptr_t                   dwarf_section_length;
    const void*                 compact_unwind_section;
    uintptr_t                   compact_unwind_section_length;
};

#else // __APPLE__

#if defined(_TARGET_AMD64_)
// Passed to the callback function called by dl_iterate_phdr
struct dl_iterate_cb_data
{
    UnwindInfoSections *sects;
    uintptr_t targetAddr;
};

// Callback called by dl_iterate_phdr. Locates unwind info sections for the target
// address.
static int LocateSectionsCallback(struct dl_phdr_info *info, size_t size, void *data)
{
    // info is a pointer to a structure containing information about the shared object
    dl_iterate_cb_data* cbdata = static_cast<dl_iterate_cb_data*>(data);
    uintptr_t addrOfInterest = (uintptr_t)cbdata->targetAddr;

    size_t object_length;
    bool found_obj = false;
    bool found_hdr = false;

    // If the base address of the SO is past the address we care about, move on.
    if (info->dlpi_addr > addrOfInterest)
    {
        return 0;
    }

    // Iterate through the program headers for this SO
    for (ElfW(Half) i = 0; i < info->dlpi_phnum; i++)
    {
        const ElfW(Phdr) *phdr = &info->dlpi_phdr[i];

        if (phdr->p_type == PT_LOAD)
        {
            // This is a loadable entry. Loader loads all segments of this type.

            uintptr_t begin = info->dlpi_addr + phdr->p_vaddr;
            uintptr_t end = begin + phdr->p_memsz;

            if (addrOfInterest >= begin && addrOfInterest < end)
            {
                cbdata->sects->dso_base = begin;
                object_length = phdr->p_memsz;
                found_obj = true;
            }
        }
        else if (phdr->p_type == PT_GNU_EH_FRAME)
        {
            // This element specifies the location and size of the exception handling 
            // information as defined by the .eh_frame_hdr section.

            EHHeaderParser<LocalAddressSpace>::EHHeaderInfo hdrInfo;

            uintptr_t eh_frame_hdr_start = info->dlpi_addr + phdr->p_vaddr;
            cbdata->sects->dwarf_index_section = eh_frame_hdr_start;
            cbdata->sects->dwarf_index_section_length = phdr->p_memsz;

            EHHeaderParser<LocalAddressSpace> ehp;
            ehp.decodeEHHdr(_addressSpace, eh_frame_hdr_start, phdr->p_memsz, hdrInfo);

            cbdata->sects->dwarf_section = hdrInfo.eh_frame_ptr;
            found_hdr = true;
        }
    }

    bool found = found_obj && found_hdr;
    return static_cast<int>(found);
}
#endif

#endif // __APPLE__

#ifdef _TARGET_AMD64_

// Shim that implements methods required by libunwind over REGDISPLAY
struct Registers_REGDISPLAY : REGDISPLAY
{
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

#endif // _TARGET_AMD64_
#if defined(_TARGET_ARM_)

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

#endif // _TARGET_ARM_

bool DoTheStep(uintptr_t pc, UnwindInfoSections uwInfoSections, REGDISPLAY *regs)
{
#if defined(_TARGET_AMD64_)
    libunwind::UnwindCursor<LocalAddressSpace, Registers_x86_64> uc(_addressSpace);
#elif defined(_TARGET_ARM_)
    libunwind::UnwindCursor<LocalAddressSpace, Registers_arm_rt> uc(_addressSpace, regs);
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

    DwarfInstructions<LocalAddressSpace, Registers_REGDISPLAY> dwarfInst;

    int stepRet = dwarfInst.stepWithDwarf(_addressSpace, pc, procInfo.unwind_info, *(Registers_REGDISPLAY*)regs);
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

UnwindInfoSections LocateUnwindSections(uintptr_t pc)
{
    UnwindInfoSections uwInfoSections;

#ifdef __APPLE__
    // On macOS, we can use a dyld function from libSystem in order
    // to find the unwind sections.

    libunwind::dyld_unwind_sections dyldInfo;

  if (libunwind::_dyld_find_unwind_sections((void *)pc, &dyldInfo))
    {
        uwInfoSections.dso_base                      = (uintptr_t)dyldInfo.mh;

        uwInfoSections.dwarf_section                 = (uintptr_t)dyldInfo.dwarf_section;
        uwInfoSections.dwarf_section_length          = dyldInfo.dwarf_section_length;

        uwInfoSections.compact_unwind_section        = (uintptr_t)dyldInfo.compact_unwind_section;
        uwInfoSections.compact_unwind_section_length = dyldInfo.compact_unwind_section_length;
    }
#else // __APPLE__

#if _LIBUNWIND_SUPPORT_DWARF_UNWIND
    dl_iterate_cb_data cb_data = {&uwInfoSections, pc };
    dl_iterate_phdr(LocateSectionsCallback, &cb_data);
#else
    PORTABILITY_ASSERT("LocateUnwindSections");
#endif

#endif

    return uwInfoSections;
}

bool UnwindHelpers::StepFrame(REGDISPLAY *regs)
{
#if _LIBUNWIND_SUPPORT_DWARF_UNWIND
    uintptr_t pc = regs->GetIP();
    UnwindInfoSections uwInfoSections = LocateUnwindSections(pc);
    if (uwInfoSections.dwarf_section == NULL)
    {
        return false;
    }
    return DoTheStep(pc, uwInfoSections, regs);
#elif defined(_LIBUNWIND_ARM_EHABI)
    // unwind section is located later for ARM
    // pc will be taked from regs parameter
    UnwindInfoSections uwInfoSections;
    return DoTheStep(0, uwInfoSections, regs);
#else
    PORTABILITY_ASSERT("StepFrame");
#endif
}
