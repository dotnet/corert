// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "rhassert.h"

#include "CommonMacros.inl"

#include "rhbinder.h"
#include "SectionMethodList.h"



#ifndef DACCESS_COMPILE

SectionMethodList::SectionMethodList() : 
    m_uFlags(0),
    m_pbPageList(NULL),
    m_pbMethodList(NULL),
    m_pbGCInfoList(NULL),
    m_pbGCInfoBlob(NULL),
    m_pbEHInfoList(NULL),
    m_pbEHInfoBlob(NULL)
#ifdef _DEBUG
    , m_uPageListCountDEBUG(0)
    , m_uMethodListCountDEBUG(0)
#endif // _DEBUG
{
}

#ifndef RHDUMP
bool SectionMethodList::Init(ModuleHeader * pHdr)
{
    // Initialize our flags with the subset from the ModuleHeader that translate directly.
    // This gets us the entry size for the page list, GC info list, and EH info list.
    UInt32 uFlags = pHdr->Flags & ModuleHeader::FlagsMatchingSMLFlagsMask;
    return Init(uFlags, pHdr->CountOfMethods, pHdr->GetCodeMapInfo(), pHdr->GetEHInfo());
}
#endif // RHDUMP

bool SectionMethodList::Init(UInt32 uFlags, UInt32 numMethods, UInt8 * pbCodeMapInfo, UInt8 * pbEHInfo)
{
    m_uFlags = uFlags;

    // Locate the page list.
    UInt8 * pbEncodedData = pbCodeMapInfo;

    // @TODO: we could move the count of pages to the MethodHeader, too, and avoid this page read.
    UInt32 uNumPages = *(UInt32*)pbEncodedData;

#ifdef _DEBUG
    m_uPageListCountDEBUG = uNumPages;
#endif // _DEBUG

    UInt32 uSizeofEntry = sizeof(UInt32);
    pbEncodedData += sizeof(UInt32);
    m_pbPageList = pbEncodedData;

    if (m_uFlags & SmallPageListEntriesFlag)
    {
        uSizeofEntry = sizeof(UInt16);
    }

    m_pbMethodList = pbEncodedData + (uNumPages * uSizeofEntry);

    UInt32 uMethodListCount = numMethods + 2;   // include the 'fake method' entry and the sentinel entry
#ifdef _DEBUG
    m_uMethodListCountDEBUG = uMethodListCount;
#endif // _DEBUG

    // Locate the GC info list, which is just past the method list.
    m_pbGCInfoList = (UInt8 *) ALIGN_UP(m_pbMethodList + (uMethodListCount * sizeof(UInt8)), sizeof(UInt16));

    UInt32 gcInfoListEntrySize = (m_uFlags & SmallGCInfoListEntriesFlag) ? 2 : 4;
    UInt32 ehInfoListEntrySize = (m_uFlags & SmallEHInfoListEntriesFlag) ? 2 : 4;

    // Locate the EH info list, which is just past the GC info list.
    m_pbEHInfoList          = m_pbGCInfoList + (numMethods * gcInfoListEntrySize);

    // Locate the GC info blob, which is just past the EH info list.
    // At the start of the gc info blob is the delta shortcut table, which we need to skip.
    m_pbGCInfoBlob          = m_pbEHInfoList + (numMethods * ehInfoListEntrySize) + 
                                ModuleHeader::DELTA_SHORTCUT_TABLE_SIZE;

    // Locate the EH info blob
    m_pbEHInfoBlob = pbEHInfo;

    return true;
}

PTR_UInt8 SectionMethodList::GetDeltaShortcutTablePtr()
{
    return m_pbGCInfoBlob - ModuleHeader::DELTA_SHORTCUT_TABLE_SIZE;
}

#endif // !DACCESS_COMPILE

UInt32  SectionMethodList::GetGCInfoOffset(UInt32 uMethodIndex)
{
    // The gc info offset array is a parallel array to the method list
    ASSERT(uMethodIndex < m_uMethodListCountDEBUG); 

    if (m_uFlags & SmallGCInfoListEntriesFlag)
    {
        return (dac_cast<PTR_UInt16>(m_pbGCInfoList))[uMethodIndex];
    }

    return (dac_cast<PTR_UInt32>(m_pbGCInfoList))[uMethodIndex];
}

PTR_UInt8 SectionMethodList::GetGCInfo(UInt32 uMethodIndex)
{
    return m_pbGCInfoBlob + GetGCInfoOffset(uMethodIndex);
}

PTR_VOID SectionMethodList::GetEHInfo(UInt32 uMethodIndex)
{
    // The EH info offset array is a parallel array to the method list.
    ASSERT(uMethodIndex < m_uMethodListCountDEBUG); 

    // Some methods do not have EH info. These are marked with an offset of -1.
    // @TODO: consider using a sentinel EHInfo that contains zero clauses to reduce the path length in here.
    if (m_uFlags & SmallEHInfoListEntriesFlag)
    {
        UInt16 offset = (dac_cast<PTR_UInt16>(m_pbEHInfoList))[uMethodIndex];
        
        if (offset != 0xFFFF)
        {
            return m_pbEHInfoBlob + offset;
        }            
    }
    else
    {
        UInt32 offset = (dac_cast<PTR_UInt32>(m_pbEHInfoList))[uMethodIndex];

        if (offset != 0xFFFFFFFF)
        {
            return m_pbEHInfoBlob + offset;
        }            
    }

    return NULL;
}

PageEntry SectionMethodList::GetPageListEntry(UInt32 idx)
{
    ASSERT(idx < m_uPageListCountDEBUG);

    if (m_uFlags & SmallPageListEntriesFlag)
        return  PageEntry(dac_cast<ArrayDPTR(UInt16)>(m_pbPageList)[idx]);

    return PageEntry((dac_cast<PTR_UInt32>(m_pbPageList))[idx]);
}

void SectionMethodList::GetMethodInfo(UInt32 uSectionOffset, UInt32 * puMethodIndex, 
                                      UInt32 * puMethodStartSectionOffset, UInt32 * puMethodSize)
{
    UInt32 uPageNumber = SectionOffsetToPageNumber(uSectionOffset);
    UInt32 uPageOffset = SectionOffsetToPageOffset(uSectionOffset);

    PageEntry page = GetPageListEntry(uPageNumber);

    UInt32 idxCurMethod = page.GetMethodIndex();

    if (page.IsCoveredByOneMethod() ||
        (uPageOffset < GetMethodPageOffset(idxCurMethod)))
    {
        // This page is covered completely by a method that started on a previous page.  The index in this
        // entry is the index of the method following the spanning method, so the correct method index is
        // one less than the index in the entry.
        // 
        // OR
        //
        // The current page offset falls before the page offset of the first method that begins on the page.
        // Therefore, we must look for the last method on the previous page.  The correct method index is 
        // one less than the index in the entry.

        idxCurMethod = idxCurMethod - 1;

        // Now search for the first prior page which isn't completely covered by a method. This will be the 
        // page on which this method starts.

        for (;;)
        {
            // Since we think the method starts on a previous page, we must not be at page 0 already.
            ASSERT(uPageNumber > 0);
            uPageNumber--;

            page = GetPageListEntry(uPageNumber);
            if (!page.IsCoveredByOneMethod())
                break;
        }

    }
    else
    {
        // This works because we always have an extra page at the end of the pageList which holds an index of 
        // methodCount, additionally, for pages which are spanning they contain the method index of the method 
        // following the spanning method.
        UInt32 idxMaxMethod = GetPageListEntry(uPageNumber + 1).GetMethodIndex() - 1;

        // At this point, we know the method is one of the set [idxCurMethod, idxMaxMethod].

        // Linear search -- @TODO: also implement binary search if the number of methods to scan is large.
        for (; idxCurMethod < idxMaxMethod; idxCurMethod++)
        {
            if (uPageOffset < GetMethodPageOffset(idxCurMethod + 1))
            {
                break;
            }
        }
    }

    *puMethodIndex              = idxCurMethod;
    *puMethodStartSectionOffset = (uPageNumber * SECTION_METHOD_LIST_PAGE_SIZE) + GetMethodPageOffset(idxCurMethod);

    if (puMethodSize != NULL)
    {
        //
        // Find the page that the next method starts on...
        //
        UInt32 idxNextMethod = idxCurMethod + 1;

        UInt32 idxNextPage = uPageNumber + 1;
        PageEntry nextPage = GetPageListEntry(idxNextPage);
        UInt32 uEndPageNumber;

        if (nextPage.GetMethodIndex() == idxNextMethod)
        {
            // The current method extends up to and possibly beyond the boundary between this page and the next.

            // If it covers the entire next page, keep going until we find the end.
            while (nextPage.IsCoveredByOneMethod())
            {
                idxNextPage++;
                nextPage = GetPageListEntry(idxNextPage);
            }

            uEndPageNumber = idxNextPage;
        }
        else
        {
            // The current method ends on the page it starts on.
            uEndPageNumber = uPageNumber;
        }

        UInt32 uMethodEndSectionOffset = (uEndPageNumber * SECTION_METHOD_LIST_PAGE_SIZE) + 
                                         GetMethodPageOffset(idxNextMethod);
        ASSERT(uMethodEndSectionOffset > *puMethodStartSectionOffset);

        *puMethodSize = uMethodEndSectionOffset - *puMethodStartSectionOffset;
    }
}

UInt32 SectionMethodList::GetMethodPageOffset(UInt32 idxMethod)
{
    ASSERT(idxMethod < m_uMethodListCountDEBUG);

    return (m_pbMethodList[idxMethod] * METHOD_ALIGNMENT_IN_BYTES);
}

// returns the section page number from the byte offset within a section
UInt32 SectionMethodList::SectionOffsetToPageNumber(UInt32 uSectionOffset)
{
    return uSectionOffset / SECTION_METHOD_LIST_PAGE_SIZE;
}

// returns the byte offset within a page from the byte offset within a section
UInt32 SectionMethodList::SectionOffsetToPageOffset(UInt32 uSectionOffset)
{
    return uSectionOffset % SECTION_METHOD_LIST_PAGE_SIZE;
}

#ifdef _DEBUG
UInt32 SectionMethodList::GetNumMethodsDEBUG()
{
    ASSERT(m_uMethodListCountDEBUG > 0);
    return (m_uMethodListCountDEBUG - 2); // -1 to account for the 'dummy method' that fills up the last page
                                          // -1 to account for the sentinel entry at the end
}
#endif // _DEBUG

PageEntry::PageEntry(UInt32 uPageEntry) : 
    m_uPageEntry(uPageEntry)
{
}

bool PageEntry::IsCoveredByOneMethod()
{
    // If the method index is the index of a method starting on some previous page, then
    // this page is completely covered by that one method.
    return m_uPageEntry & METHOD_STARTS_ON_PREV_PAGE_FLAG;
}

// There are two meanings for this method index:
//   -- if (!IsCoveredByOneMethod()) { This is the index of the first method that begins on that page. }
//   -- else                         { This is the index of the method that follows the one covering this page. }
UInt32 PageEntry::GetMethodIndex()
{
    return m_uPageEntry >> METHOD_INDEX_SHIFT_AMOUNT;
}
