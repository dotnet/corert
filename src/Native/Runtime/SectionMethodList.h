// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

class PageEntry
{
    UInt32  m_uPageEntry;

    static UInt32 const MAX_METHOD_INDEX                    = 0x7FFFFFFF;   // reserve one bit for a flag
    static UInt32 const MAX_METHOD_INDEX_SMALLENTRIES       = 0x7FFF;       // reserve one bit for a flag
    static UInt32 const METHOD_INDEX_SHIFT_AMOUNT           = 1;            // shift by one for flag
    static UInt32 const METHOD_STARTS_ON_PREV_PAGE_FLAG     = 0x00000001;   // used in page list entries

public:
    PageEntry(UInt32 uPageEntry);

    bool IsCoveredByOneMethod();
    UInt32 GetMethodIndex();
};

#ifndef RHDUMP
struct ModuleHeader;
typedef DPTR(ModuleHeader) PTR_ModuleHeader;
#endif // RHDUMP

class SectionMethodList
{
    UInt32  m_uFlags;

    PTR_UInt8 m_pbPageList;
    PTR_UInt8 m_pbMethodList;
    PTR_UInt8 m_pbGCInfoList;
    PTR_UInt8 m_pbGCInfoBlob;
    PTR_UInt8 m_pbEHInfoList;
    PTR_UInt8 m_pbEHInfoBlob;

#ifdef _DEBUG
    UInt32 m_uPageListCountDEBUG;
    UInt32 m_uMethodListCountDEBUG;
#endif // _DEBUG

    PageEntry GetPageListEntry(UInt32 idx);
    UInt32 GetMethodPageOffset(UInt32 idxMethod);

    UInt32 SectionOffsetToPageNumber(UInt32 uSectionOffset);
    UInt32 SectionOffsetToPageOffset(UInt32 uSectionOffset);

    // A subset of these flags match those that come from the module header, written by the binder. This set must be kept
    // in sync with the definitions in ModuleHeader::ModuleHeaderFlags
    enum SectionMethodListFlags
    {
        SmallPageListEntriesFlag    = 0x00000001,   // if set, 2-byte page list entries, 4-byte otherwise
        SmallGCInfoListEntriesFlag  = 0x00000002,   // if set, 2-byte gc info list entries, 4-byte otherwise
        SmallEHInfoListEntriesFlag  = 0x00000004,   // if set, 2-byte EH info list entries, 4-byte otherwise
    };

    static UInt32 const SECTION_METHOD_LIST_PAGE_SIZE       = 1024;
    static UInt32 const METHOD_ALIGNMENT_IN_BYTES           = 4;

public:
    SectionMethodList();

#ifndef RHDUMP
    bool    Init(ModuleHeader * pHdr);
#endif // RHDUMP
    bool    Init(UInt32 uFlags, UInt32 numMethods, UInt8 * pbCodeMapInfo, UInt8 * pbEHInfo);
    void    GetMethodInfo(UInt32 uSectionOffset, UInt32 * puMethodIndex, 
                          UInt32 * puMethodStartSectionOffset, UInt32 * puMethodSize);

    UInt32    GetGCInfoOffset(UInt32 uMethodIndex);
    PTR_UInt8 GetGCInfo(UInt32 uMethodIndex);
    PTR_VOID  GetEHInfo(UInt32 uMethodIndex);
    PTR_UInt8 GetDeltaShortcutTablePtr();

#ifdef _DEBUG
    UInt32 GetNumMethodsDEBUG();
#endif // _DEBUG
};
