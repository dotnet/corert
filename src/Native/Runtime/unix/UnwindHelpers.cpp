// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "daccess.h"

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
#include <src/UnwindCursor.hpp>

using libunwind::Registers_x86_64;
using libunwind::LocalAddressSpace;
using libunwind::EHHeaderParser;
using libunwind::DwarfInstructions;
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

    return 0;
}

#endif // __APPLE__

bool DoTheStep(uintptr_t pc, UnwindInfoSections uwInfoSections, REGDISPLAY *regs)
{
#if defined(_TARGET_AMD64_)
    libunwind::UnwindCursor<LocalAddressSpace, Registers_x86_64> uc(_addressSpace);
#elif defined(_TARGET_ARM_)
    libunwind::UnwindCursor<LocalAddressSpace, Registers_arm> uc(_addressSpace);
#else
    #error "Unwinding is not implemented for this architecture yet."
#endif

    bool retVal = uc.getInfoFromDwarfSection(pc, uwInfoSections, 0 /* fdeSectionOffsetHint */);
    if (!retVal)
    {
        return false;
    }

    unw_proc_info_t procInfo;
    uc.getInfo(&procInfo);

    DwarfInstructions<LocalAddressSpace, REGDISPLAY> dwarfInst;

    int stepRet = dwarfInst.stepWithDwarf(_addressSpace, pc, procInfo.unwind_info, *regs);
    if (stepRet != UNW_STEP_SUCCESS)
    {
        return false;
    }

    regs->pIP = PTR_PCODE(regs->SP - sizeof(TADDR));

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

    dl_iterate_cb_data cb_data = {&uwInfoSections, pc };
    dl_iterate_phdr(LocateSectionsCallback, &cb_data);

#endif

    return uwInfoSections;
}

bool UnwindHelpers::StepFrame(REGDISPLAY *regs)
{
    uintptr_t pc = regs->GetIP();

    UnwindInfoSections uwInfoSections = LocateUnwindSections(pc);
    if (uwInfoSections.dwarf_section == NULL)
    {
        return false;
    }

    return DoTheStep(pc, uwInfoSections, regs);
}
