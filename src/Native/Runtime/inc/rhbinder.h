// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This header contains binder-generated data structures that the runtime consumes.
//
#ifndef RHDUMP_TARGET_NEUTRAL
#include "TargetPtrs.h"
#endif
#ifndef RHDUMP
#include "WellKnownMethods.h"
#endif
#if !defined(RHDUMP) || defined(RHDUMP_TARGET_NEUTRAL)
//
// Region Relative Addresses (RRAs)
//
// Now that RH code can be emitted as a regular object file which can be linked with arbitrary native code,
// the module header (or any RH code or data) no longer has access to the OS module handle for which it is a
// part. As a result it's not a good idea to encode pointers as RVAs (relative virtual addresses) in the
// header or other RH metadata since without the OS module handle we have no mechanism to derive a VA (virtual
// address) from these RVAs.
//
// It's still desirable to utilize relative addresses since this saves space on 64-bit machines. So instead of
// RVAs we introduce the concept of RRAs (Region Relative Addresses). These are 32-bit offsets from the start
// of one of several "regions" defined by the Redhawk ModuleHeader. See the RegionTypes enum below for the
// currently defined regions. These are all contiguous regions of memory emitted by the binder (e.g. the text
// section which contains all RH method code).
//
// To recover a VA from an RRA you simply add the base VA of the correct region to the RRA. One weakness of
// the current mechanism is that there's no strong type checking to ensure that you use the correct region to
// interpret a given RRA. Possibly something could be done with templates here. For now we have a relatively
// small set of RRAs and as much as possible I've tried to abstract access to these via macros and other
// techniques to limit the places where mistakes can be made. If this turns out to be a bug farm we can
// revisit the issue.
//

#ifdef RHDUMP
// Always use RVAs
typedef UInt32 RegionPtr;
#else
typedef TgtPTR_UInt8 RegionPtr; 
#endif

struct ModuleHeader
{
    // A subset of these flags match those that we need in the SectionMethodList at runtime. This set must be kept
    // in sync with the definitions in SectionMethodList::SectionMethodListFlags
    enum ModuleHeaderFlags
    {
        SmallPageListEntriesFlag    = 0x00000001,   // if set, 2-byte page list entries, 4-byte otherwise
        SmallGCInfoListEntriesFlag  = 0x00000002,   // if set, 2-byte gc info list entries, 4-byte otherwise
        SmallEHInfoListEntriesFlag  = 0x00000004,   // if set, 2-byte EH info list entries, 4-byte otherwise
        FlagsMatchingSMLFlagsMask   = 0x00000007,   // Mask for flags that match those in SectionMethodList at runtime
        UsesClrEHFlag               = 0x00000008,   // set if module expects CLR EH model, clear if module expects RH EH model.
        StandaloneExe               = 0x00000010,   // this module represents the only (non-runtime) module in the process
    };

    enum ModuleHeaderConstants : UInt32
    {
        CURRENT_VERSION             = 3,            // Version of the module header protocol. Increment on
                                                    // breaking changes
        DELTA_SHORTCUT_TABLE_SIZE   = 16,
        MAX_REGIONS                 = 8,            // Max number of regions described by the Regions array
        MAX_WELL_KNOWN_METHODS      = 8,            // Max number of methods described by the WellKnownMethods array 
        MAX_EXTRA_WELL_KNOWN_METHODS= 8,            // Max number of methods described by the ExtraWellKnownMethods array 
        NULL_RRA                    = 0xffffffff,   // NULL value for region relative addresses (0 is often a
                                                    // legal RRA)
    };

    // The region types defined so far. Each module has at most one of each of these regions.
    enum RegionTypes
    {
        TEXT_REGION                 = 0,            // Code
        DATA_REGION                 = 1,            // Read/write data
        RDATA_REGION                = 2,            // Read-only data
        IAT_REGION                  = 3,            // Import Address Table
    };

    UInt32  Version;
    UInt32  Flags;                      // Various flags passed from the binder to the runtime (See ModuleHeaderFlags below).
    UInt32  CountOfMethods;             // Count of method bodies in this module.  This count is used by the SectionMethodList as the size of its various arrays
    UInt32  RraCodeMapInfo;             // RRA to SectionMethodList, includes ip-to-method map and method gc info
    UInt32  RraStaticsGCDataSection;    // RRA to region containing GC statics
    UInt32  RraStaticsGCInfo;           // RRA to GC info for module statics (an array of StaticGcDesc structs)
    UInt32  RraThreadStaticsGCInfo;     // RRA to GC info for module thread statics (an array of StaticGcDesc structs)
#ifdef FEATURE_CACHED_INTERFACE_DISPATCH
    UInt32  RraInterfaceDispatchCells;  // RRA to array of cache data structures used to dispatch interface calls
    UInt32  CountInterfaceDispatchCells;// Number of elements in above array
#endif
    UInt32  RraFrozenObjects;           // RRA to the image's string literals (used for object ref verification of frozen strings)
    UInt32  SizeFrozenObjects;          // size, in bytes, of string literals
    UInt32  RraEHInfo;                  // RRA to the EH info, which is past the variable length GC info.
    UInt32  SizeEHTypeTable;            // The EH info starts with a table of types used by the clauses, this is the size (in bytes) of that table.
    UInt32  RraSystemObjectEEType;      // RRA to the IAT entry for the classlib's System.Object EEType. Zero if this is the classlib itself.
    UInt32  RraUnwindInfoBlob;          // RRA to blob used for unwind infos that are referenced by the method GC info
    UInt32  RraCallsiteInfoBlob;        // RRA to blob used for callsite GC root strings that are referenced by the method GC info
    UInt32  SizeStubCode;               // size, in bytes, of stub code at the end of the TEXT_REGION. See ZapImage::SaveModuleHeader for details.
    UInt32  RraReadOnlyBlobs;           // RRA to list of read-only opaque data blobs
    UInt32  SizeReadOnlyBlobs;          // size, in bytes, of the read-only data blobs above
    UInt32  RraNativeInitFunctions;     // RRA to table of function pointers for initialization functions from linked in native code
    UInt32  CountNativeInitFunctions;   // count of the number of entries in the table above
    // info for loop hijacking {
    UInt32  RraLoopIndirCells;          // RRA to start of loop hijacking indirection cells
    UInt32  RraLoopIndirCellChunkBitmap;// RRA to a bitmap which tracks redirected loop hijack indirection cell chunks
    UInt32  RraLoopRedirTargets;        // RRA to start of code block which implements the redirected targets for hijacking loops
    UInt32  RraLoopTargets;             // RRA to start of compressed info describing the original loop targets (prior to redirection)
    UInt32  CountOfLoopIndirCells;      // count of loop hijacking indirection cells
    // } // end info for loop hijacking
    UInt32  RraDispatchMapLookupTable;  // RRA of table of pointers to DispatchMaps

    UInt32  WellKnownMethods[MAX_WELL_KNOWN_METHODS];   // Array of methods with well known semantics defined
                                                        // in this module

    // These two arrays, RegionSize and RegionPtr are parallel arrays.  They are not simply an array of 
    // structs because that would waste space on 64-bit due to the natural alignment requirement of the 
    // pointers.
    UInt32          RegionSize[MAX_REGIONS];    // sizes of each region in the module
    RegionPtr       RegionPtr[MAX_REGIONS];     // Base addresses for the RRAs above

    TgtPTR_UInt32   PointerToTlsIndex;  // Pointer to TLS index if this module uses thread statics (cannot be
                                        // RRA because it's fixed up by the OS loader)
    UInt32          TlsStartOffset;     // Offset into TLS section at which this module's thread statics begin

#ifdef FEATURE_PROFILING
    UInt32          RraProfilingEntries;        // RRA to start of profile info
    UInt32          CountOfProfilingEntries;    // count of profile info records
#endif // FEATURE_PROFILING

    UInt32          RraArrayBaseEEType;       // RRA to the classlib's array base type EEType (usually System.Array), zero if this is not the classlib

#ifdef FEATURE_CUSTOM_IMPORTS
    UInt32          RraCustomImportDescriptors;      // RRA to an array of CustomImportDescriptors
    UInt32          CountCustomImportDescriptors;    // count of entries in the above array
#endif // FEATURE_CUSTOM_IMPORTS

    UInt32          RraGenericUnificationDescs;
    UInt32          CountOfGenericUnificationDescs;

    UInt32          RraGenericUnificationIndirCells;
    UInt32          CountOfGenericUnificationIndirCells;

    UInt32          RraColdToHotMappingInfo;

    UInt32  ExtraWellKnownMethods[MAX_EXTRA_WELL_KNOWN_METHODS];   // Array of methods with well known semantics defined
                                                                   // in this module

    // Macro to generate an inline accessor for RRA-based fields.
#ifdef RHDUMP
#define DEFINE_GET_ACCESSOR(_field, _region)\
    inline UInt64 Get##_field() { return Rra##_field == NULL_RRA ? NULL : RegionPtr[_region] + Rra##_field; }
#else
#define DEFINE_GET_ACCESSOR(_field, _region)\
    inline PTR_UInt8 Get##_field() { return Rra##_field == NULL_RRA ? NULL : RegionPtr[_region] + Rra##_field; }
#endif

    // Similar macro to DEFINE_GET_ACCESSOR that handles data that is read-write normally but read-only if the
    // module is in standalone exe mode.
#ifdef RHDUMP
#define DEFINE_GET_ACCESSOR_RO_OR_RW_DATA(_field)\
    inline UInt64 Get##_field() { return Rra##_field == NULL_RRA ? NULL : RegionPtr[(Flags & StandaloneExe) ? RDATA_REGION : DATA_REGION] + Rra##_field; }
#else
#define DEFINE_GET_ACCESSOR_RO_OR_RW_DATA(_field)\
    inline PTR_UInt8 Get##_field() { return Rra##_field == NULL_RRA ? NULL : RegionPtr[(Flags & StandaloneExe) ? RDATA_REGION : DATA_REGION] + Rra##_field; }
#endif

    DEFINE_GET_ACCESSOR(SystemObjectEEType,         IAT_REGION);

    DEFINE_GET_ACCESSOR(CodeMapInfo,                RDATA_REGION);
    DEFINE_GET_ACCESSOR(StaticsGCInfo,              RDATA_REGION);
    DEFINE_GET_ACCESSOR(ThreadStaticsGCInfo,        RDATA_REGION);
    DEFINE_GET_ACCESSOR(EHInfo,                     RDATA_REGION);
    DEFINE_GET_ACCESSOR(UnwindInfoBlob,             RDATA_REGION);
    DEFINE_GET_ACCESSOR(CallsiteInfoBlob,           RDATA_REGION);

    DEFINE_GET_ACCESSOR(StaticsGCDataSection,       DATA_REGION);
#ifdef FEATURE_CACHED_INTERFACE_DISPATCH
    DEFINE_GET_ACCESSOR(InterfaceDispatchCells,     DATA_REGION);
#endif
    DEFINE_GET_ACCESSOR(FrozenObjects,             DATA_REGION);

    DEFINE_GET_ACCESSOR(LoopIndirCells,             DATA_REGION);
    DEFINE_GET_ACCESSOR(LoopIndirCellChunkBitmap,   DATA_REGION);
    DEFINE_GET_ACCESSOR(LoopRedirTargets,           TEXT_REGION);
    DEFINE_GET_ACCESSOR(LoopTargets,                RDATA_REGION);

    DEFINE_GET_ACCESSOR(DispatchMapLookupTable,     RDATA_REGION);

#ifdef FEATURE_PROFILING
    DEFINE_GET_ACCESSOR(ProfilingEntries,           DATA_REGION);
#endif // FEATURE_PROFILING

    DEFINE_GET_ACCESSOR(ReadOnlyBlobs,              RDATA_REGION);

    DEFINE_GET_ACCESSOR(NativeInitFunctions,        RDATA_REGION);

#ifdef FEATURE_CUSTOM_IMPORTS
    DEFINE_GET_ACCESSOR(CustomImportDescriptors,    RDATA_REGION);
#endif // FEATURE_CUSTOM_IMPORTS

    DEFINE_GET_ACCESSOR(GenericUnificationDescs,    RDATA_REGION);
    DEFINE_GET_ACCESSOR(GenericUnificationIndirCells,DATA_REGION);

    DEFINE_GET_ACCESSOR(ColdToHotMappingInfo,       RDATA_REGION);

#ifndef RHDUMP
    // Macro to generate an inline accessor for well known methods (these are all TEXT-based RRAs since they
    // point to code).
#define DEFINE_WELL_KNOWN_METHOD(_name)                                                                                     \
    inline PTR_VOID Get_##_name()                                                                                           \
    {                                                                                                                       \
        unsigned int index = (unsigned int)WKM_##_name;                                                                     \
        if (index >= MAX_WELL_KNOWN_METHODS)                                                                                \
        {                                                                                                                   \
            index = index - MAX_WELL_KNOWN_METHODS;                                                                         \
            return ExtraWellKnownMethods[index] == NULL_RRA ? NULL : RegionPtr[TEXT_REGION] + ExtraWellKnownMethods[index]; \
        }                                                                                                                   \
        else                                                                                                                \
        {                                                                                                                   \
            return WellKnownMethods[index] == NULL_RRA ? NULL : RegionPtr[TEXT_REGION] + WellKnownMethods[index];           \
        }                                                                                                                   \
    }
#include "WellKnownMethodList.h"
#undef DEFINE_WELL_KNOWN_METHOD
#endif // !RHDUMP
};
#ifndef RHDUMP
typedef DPTR(ModuleHeader) PTR_ModuleHeader;
#endif // !RHDUMP

class GcPollInfo
{
public:

#ifndef RHDUMP
    static const UInt32 indirCellsPerBitmapBit  = 64 / POINTER_SIZE;    // one cache line per bit
#endif // !RHDUMP

    static const UInt32 cbChunkCommonCode_X64   = 17;
    static const UInt32 cbChunkCommonCode_X86   = 16;
    static const UInt32 cbChunkCommonCode_ARM   = 32;
#ifdef _TARGET_ARM_
    // on ARM, the index of the indirection cell can be computed
    // from the pointer to the indirection cell left in R12, 
    // thus we need only one entry point on ARM,
    // thus entries take no space, and you can have as many as you want
    static const UInt32 cbEntry                 = 0;
    static const UInt32 cbBundleCommonCode      = 0;
    static const UInt32 entriesPerBundle        = 0x7fffffff;
    static const UInt32 bundlesPerChunk         = 0x7fffffff;
    static const UInt32 entriesPerChunk         = 0x7fffffff;
#else
    static const UInt32 cbEntry                 = 4;    // push imm8 / jmp rel8
    static const UInt32 cbBundleCommonCode      = 5;    // jmp rel32

    static const UInt32 entriesPerSubBundlePos  = 32;   // for the half with forward jumps
    static const UInt32 entriesPerSubBundleNeg  = 30;   // for the half with negative jumps
    static const UInt32 entriesPerBundle        = entriesPerSubBundlePos + entriesPerSubBundleNeg;
    static const UInt32 bundlesPerChunk         = 4;
    static const UInt32 entriesPerChunk         = bundlesPerChunk * entriesPerBundle;
#endif

    static const UInt32 cbFullBundle            = cbBundleCommonCode + 
                                                  (entriesPerBundle * cbEntry);

#ifndef RHDUMP
    static UInt32 EntryIndexToStubOffset(UInt32 entryIndex)
    {
# if defined(_TARGET_ARM_)
        return EntryIndexToStubOffset(entryIndex, cbChunkCommonCode_ARM);
# elif defined(_TARGET_AMD64_)
        return EntryIndexToStubOffset(entryIndex, cbChunkCommonCode_X64);
# else
        return EntryIndexToStubOffset(entryIndex, cbChunkCommonCode_X86);
# endif
    }
#endif

    static UInt32 EntryIndexToStubOffset(UInt32 entryIndex, UInt32 cbChunkCommonCode)
    {
# if defined(_TARGET_ARM_)
        UNREFERENCED_PARAMETER(entryIndex);
        UNREFERENCED_PARAMETER(cbChunkCommonCode);

        return 0;
# else
        UInt32 cbFullChunk              = cbChunkCommonCode + 
                                          (bundlesPerChunk * cbBundleCommonCode) +
                                          (entriesPerChunk * cbEntry);

        UInt32 numFullChunks             = entryIndex / entriesPerChunk;
        UInt32 numEntriesInLastChunk     = entryIndex - (numFullChunks * entriesPerChunk);

        UInt32 numFullBundles            = numEntriesInLastChunk / entriesPerBundle;
        UInt32 numEntriesInLastBundle    = numEntriesInLastChunk - (numFullBundles * entriesPerBundle);

        UInt32 offset                    = (numFullChunks * cbFullChunk) +
                                          cbChunkCommonCode + 
                                          (numFullBundles * cbFullBundle) +
                                          (numEntriesInLastBundle * cbEntry);

        if (numEntriesInLastBundle >= entriesPerSubBundlePos)
            offset += cbBundleCommonCode;

        return offset;
# endif
    }
};
#endif // !defined(RHDUMP) || defined(RHDUMP_TARGET_NEUTRAL)
#if       !defined(RHDUMP) || !defined(RHDUMP_TARGET_NEUTRAL)



struct StaticGcDesc
{
    struct GCSeries
    {
        UInt32 m_size;
        UInt32 m_startOffset;
    };

    UInt32   m_numSeries;
    GCSeries m_series[1];

    UInt32 GetSize()
    {
        return (UInt32)(offsetof(StaticGcDesc, m_series) + (m_numSeries * sizeof(GCSeries)));
    }
    
#ifdef DACCESS_COMPILE
    static UInt32 DacSize(TADDR addr);
#endif
};

#ifdef RHDUMP
typedef StaticGcDesc * PTR_StaticGcDesc;
typedef StaticGcDesc::GCSeries * PTR_StaticGcDescGCSeries;
#else
typedef SPTR(StaticGcDesc) PTR_StaticGcDesc;
typedef DPTR(StaticGcDesc::GCSeries) PTR_StaticGcDescGCSeries;
#endif

class EEType;

#ifdef FEATURE_CACHED_INTERFACE_DISPATCH

enum class DispatchCellType
{
    InterfaceAndSlot = 0x0,
    MetadataToken = 0x1,
    VTableOffset = 0x2,
};

struct DispatchCellInfo
{
    DispatchCellType CellType;
    EEType *InterfaceType = nullptr;
    UInt16 InterfaceSlot = 0;
    UInt8 HasCache = 0;
    UInt32 MetadataToken = 0;
    UInt32 VTableOffset = 0;
};

struct InterfaceDispatchCacheHeader
{
private:
    enum Flags
    {
        CH_TypeAndSlotIndex = 0x0,
        CH_MetadataToken = 0x1,
        CH_Mask = 0x3,
        CH_Shift = 0x2,
    };

public:
    void Initialize(EEType *pInterfaceType, UInt16 interfaceSlot, UInt32 metadataToken)
    {
        if (pInterfaceType != nullptr)
        {
            ASSERT(metadataToken == 0);
            m_pInterfaceType = pInterfaceType;
            m_slotIndexOrMetadataTokenEncoded = CH_TypeAndSlotIndex | (((UInt32)interfaceSlot) << CH_Shift);
        }
        else
        {
            ASSERT(pInterfaceType == nullptr);
            ASSERT(interfaceSlot == 0);
            m_pInterfaceType = nullptr;
            m_slotIndexOrMetadataTokenEncoded = CH_MetadataToken | (metadataToken << CH_Shift);
        }
    }

    void Initialize(const DispatchCellInfo *pCellInfo)
    {
        ASSERT((pCellInfo->CellType == DispatchCellType::InterfaceAndSlot) ||
               (pCellInfo->CellType == DispatchCellType::MetadataToken));
        if (pCellInfo->CellType == DispatchCellType::InterfaceAndSlot)
        {
            ASSERT(pCellInfo->MetadataToken == 0);
            Initialize(pCellInfo->InterfaceType, pCellInfo->InterfaceSlot, 0);
        }
        else
        {
            ASSERT(pCellInfo->CellType == DispatchCellType::MetadataToken);
            ASSERT(pCellInfo->InterfaceType == nullptr);
            Initialize(nullptr, 0, pCellInfo->MetadataToken);
        }
    }

    DispatchCellInfo GetDispatchCellInfo()
    {
        DispatchCellInfo cellInfo;
        
        if ((m_slotIndexOrMetadataTokenEncoded & CH_Mask) == CH_TypeAndSlotIndex)
        {
            cellInfo.InterfaceType = m_pInterfaceType;
            cellInfo.InterfaceSlot = (UInt16)(m_slotIndexOrMetadataTokenEncoded >> CH_Shift);
            cellInfo.CellType = DispatchCellType::InterfaceAndSlot;
        }
        else
        {
            cellInfo.MetadataToken = m_slotIndexOrMetadataTokenEncoded >> CH_Shift;
            cellInfo.CellType = DispatchCellType::MetadataToken;
        }
        cellInfo.HasCache = 1;
        return cellInfo;
    }

private:
    EEType *    m_pInterfaceType;   // EEType of interface to dispatch on
    UInt32      m_slotIndexOrMetadataTokenEncoded;
};

// One of these is allocated per interface call site. It holds the stub to call, data to pass to that stub
// (cache information) and the interface contract, i.e. the interface type and slot being called.
struct InterfaceDispatchCell
{
    // The first two fields must remain together and at the beginning of the structure. This is due to the
    // synchronization requirements of the code that updates these at runtime and the instructions generated
    // by the binder for interface call sites.
    UIntTarget      m_pStub;    // Call this code to execute the interface dispatch
    volatile UIntTarget m_pCache;   // Context used by the stub above (one or both of the low two bits are set
                                    // for initial dispatch, and if not set, using this as a cache pointer or 
                                    // as a vtable offset.)
                                    //
                                    // In addition, there is a Slot/Flag use of this field. DispatchCells are
                                    // emitted as a group, and the final one in the group (identified by m_pStub
                                    // having the null value) will have a Slot field is the low 16 bits of the
                                    // m_pCache field, and in the second lowest 16 bits, a Flags field. For the interface
                                    // case Flags shall be 0, and for the metadata token case, Flags shall be 1.

    //
    // Keep these in sync with the managed copy in src\Common\src\Internal\Runtime\InterfaceCachePointerType.cs
    //
    enum Flags
    {
        // The low 2 bits of the m_pCache pointer are treated specially so that we can avoid the need for 
        // extra fields on this type.
        // OR if the m_pCache value is less than 0x1000 then this it is a vtable offset and should be used as such
        IDC_CachePointerIsInterfaceRelativePointer = 0x3,
        IDC_CachePointerIsIndirectedInterfaceRelativePointer = 0x2,
        IDC_CachePointerIsInterfacePointerOrMetadataToken = 0x1, // Metadata token is a 30 bit number in this case. 
                                                                 // Tokens are required to have at least one of their upper 20 bits set
                                                                 // But they are not required by this part of the system to follow any specific
                                                                 // token format
        IDC_CachePointerPointsAtCache = 0x0,
        IDC_CachePointerMask = 0x3,
        IDC_CachePointerMaskShift = 0x2,
        IDC_MaxVTableOffsetPlusOne = 0x1000,
    };

#if !defined(RHDUMP) && !defined(BINDER)
    DispatchCellInfo GetDispatchCellInfo()
    {
        // Capture m_pCache into a local for safe access (this is a volatile read of a value that may be
        // modified on another thread while this function is executing.)
        UIntTarget cachePointerValue = m_pCache;
        DispatchCellInfo cellInfo;

        if ((cachePointerValue < IDC_MaxVTableOffsetPlusOne) && ((cachePointerValue & IDC_CachePointerMask) == IDC_CachePointerPointsAtCache))
        {
            cellInfo.VTableOffset = (UInt32)cachePointerValue;
            cellInfo.CellType = DispatchCellType::VTableOffset;
            cellInfo.HasCache = 1;
            return cellInfo;
        }

        // If there is a real cache pointer, grab the data from there.
        if ((cachePointerValue & IDC_CachePointerMask) == IDC_CachePointerPointsAtCache)
        {
            return ((InterfaceDispatchCacheHeader*)cachePointerValue)->GetDispatchCellInfo();
        }

        // Otherwise, walk to cell with Flags and Slot field

        // The slot number/flags for a dispatch cell is encoded once per run of DispatchCells
        // The run is terminated by having an dispatch cell with a null stub pointer.
        const InterfaceDispatchCell *currentCell = this;
        while (currentCell->m_pStub != 0)
        {
            currentCell = currentCell + 1;
        } 
        UIntTarget cachePointerValueFlags = currentCell->m_pCache;

        DispatchCellType cellType = (DispatchCellType)(cachePointerValueFlags >> 16);
        cellInfo.CellType = cellType;

        if (cellType == DispatchCellType::InterfaceAndSlot)
        {
            cellInfo.InterfaceSlot = (UInt16)cachePointerValueFlags;

            switch (cachePointerValue & IDC_CachePointerMask)
            {
            case IDC_CachePointerIsInterfacePointerOrMetadataToken:
                cellInfo.InterfaceType = (EEType*)(cachePointerValue & ~IDC_CachePointerMask);
                break;

            case IDC_CachePointerIsInterfaceRelativePointer:
            case IDC_CachePointerIsIndirectedInterfaceRelativePointer:
                {
                    UIntTarget interfacePointerValue = (UIntTarget)&m_pCache + (Int32)cachePointerValue;
                    interfacePointerValue &= ~IDC_CachePointerMask;
                    if ((cachePointerValue & IDC_CachePointerMask) == IDC_CachePointerIsInterfaceRelativePointer)
                    {
                        cellInfo.InterfaceType = (EEType*)interfacePointerValue;
                    }
                    else
                    {
                        cellInfo.InterfaceType = *(EEType**)interfacePointerValue;
                    }
                }
                break;
            }
        }
        else
        {
            cellInfo.MetadataToken = (UInt32)(cachePointerValue >> IDC_CachePointerMaskShift);
        }

        return cellInfo;
    }

    static bool IsCache(UIntTarget value)
    {
        if (((value & IDC_CachePointerMask) != 0) || (value < IDC_MaxVTableOffsetPlusOne))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    InterfaceDispatchCacheHeader* GetCache() const
    {
        // Capture m_pCache into a local for safe access (this is a volatile read of a value that may be
        // modified on another thread while this function is executing.)
        UIntTarget cachePointerValue = m_pCache;
        if (IsCache(cachePointerValue))
        {
            return (InterfaceDispatchCacheHeader*)cachePointerValue;
        }
        else
        {
            return 0;
        }
    }
#endif // !RHDUMP && !BINDER
};

#endif // FEATURE_CACHED_INTERFACE_DISPATCH

#ifdef _TARGET_ARM_
// Note for ARM: try and keep the flags in the low 16-bits, since they're not easy to load into a register in
// a single instruction within our stubs.
enum PInvokeTransitionFrameFlags
{
    // NOTE: Keep in sync with ndp\FxCore\CoreRT\src\Native\Runtime\arm\AsmMacros.h

    // NOTE: The order in which registers get pushed in the PInvokeTransitionFrame's m_PreservedRegs list has 
    //       to match the order of these flags (that's also the order in which they are read in StackFrameIterator.cpp

    // standard preserved registers
    PTFF_SAVE_R4        = 0x00000001,
    PTFF_SAVE_R5        = 0x00000002,
    PTFF_SAVE_R6        = 0x00000004,
    PTFF_SAVE_R7        = 0x00000008,   // should never be used, we require FP frames for methods with 
                                        // pinvoke and it is saved into the frame pointer field instead
    PTFF_SAVE_R8        = 0x00000010,
    PTFF_SAVE_R9        = 0x00000020,
    PTFF_SAVE_R10       = 0x00000040,
    PTFF_SAVE_SP        = 0x00000100,   // Used for 'coop pinvokes' in runtime helper routines.  Methods with
                                        // PInvokes are required to have a frame pointers, but methods which
                                        // call runtime helpers are not.  Therefore, methods that call runtime
                                        // helpers may need SP to seed the stackwalk.

    // scratch registers
    PTFF_SAVE_R0        = 0x00000200,
    PTFF_SAVE_R1        = 0x00000400,
    PTFF_SAVE_R2        = 0x00000800,
    PTFF_SAVE_R3        = 0x00001000,
    PTFF_SAVE_LR        = 0x00002000,   // this is useful for the case of loop hijacking where we need both
                                        // a return address pointing into the hijacked method and that method's
                                        // lr register, which may hold a gc pointer

    PTFF_R0_IS_GCREF    = 0x00004000,   // used by hijack handler to report return value of hijacked method
    PTFF_R0_IS_BYREF    = 0x00008000,   // used by hijack handler to report return value of hijacked method

    PTFF_THREAD_ABORT   = 0x00010000,   // indicates that ThreadAbortException should be thrown when returning from the transition
};
#elif defined(_TARGET_ARM64_)
enum PInvokeTransitionFrameFlags : UInt64
{
    // NOTE: Keep in sync with ndp\FxCore\CoreRT\src\Native\Runtime\arm64\AsmMacros.h

    // NOTE: The order in which registers get pushed in the PInvokeTransitionFrame's m_PreservedRegs list has 
    //       to match the order of these flags (that's also the order in which they are read in StackFrameIterator.cpp

    // standard preserved registers
    PTFF_SAVE_X19       = 0x0000000000000001,
    PTFF_SAVE_X20       = 0x0000000000000002,
    PTFF_SAVE_X21       = 0x0000000000000004,
    PTFF_SAVE_X22       = 0x0000000000000008,
    PTFF_SAVE_X23       = 0x0000000000000010,
    PTFF_SAVE_X24       = 0x0000000000000020,
    PTFF_SAVE_X25       = 0x0000000000000040,
    PTFF_SAVE_X26       = 0x0000000000000080,
    PTFF_SAVE_X27       = 0x0000000000000100,
    PTFF_SAVE_X28       = 0x0000000000000200,

    PTFF_SAVE_SP        = 0x0000000000000400,   // Used for 'coop pinvokes' in runtime helper routines.  Methods with
                                                // PInvokes are required to have a frame pointers, but methods which
                                                // call runtime helpers are not.  Therefore, methods that call runtime
                                                // helpers may need SP to seed the stackwalk.

    // Scratch registers
    PTFF_SAVE_X0        = 0x0000000000000800,
    PTFF_SAVE_X1        = 0x0000000000001000,
    PTFF_SAVE_X2        = 0x0000000000002000,
    PTFF_SAVE_X3        = 0x0000000000004000,
    PTFF_SAVE_X4        = 0x0000000000008000,
    PTFF_SAVE_X5        = 0x0000000000010000,
    PTFF_SAVE_X6        = 0x0000000000020000,
    PTFF_SAVE_X7        = 0x0000000000040000,
    PTFF_SAVE_X8        = 0x0000000000080000,
    PTFF_SAVE_X9        = 0x0000000000100000,
    PTFF_SAVE_X10       = 0x0000000000200000,
    PTFF_SAVE_X11       = 0x0000000000400000,
    PTFF_SAVE_X12       = 0x0000000000800000,
    PTFF_SAVE_X13       = 0x0000000001000000,
    PTFF_SAVE_X14       = 0x0000000002000000,
    PTFF_SAVE_X15       = 0x0000000004000000,
    PTFF_SAVE_X16       = 0x0000000008000000,
    PTFF_SAVE_X17       = 0x0000000010000000,
    PTFF_SAVE_X18       = 0x0000000020000000,

    PTFF_SAVE_FP        = 0x0000000040000000,   // should never be used, we require FP frames for methods with 
                                                // pinvoke and it is saved into the frame pointer field instead

    PTFF_SAVE_LR        = 0x0000000080000000,   // this is useful for the case of loop hijacking where we need both
                                                // a return address pointing into the hijacked method and that method's
                                                // lr register, which may hold a gc pointer

    // used by hijack handler to report return value of hijacked method
    PTFF_X0_IS_GCREF    = 0x0000000100000000,
    PTFF_X0_IS_BYREF    = 0x0000000200000000,
    PTFF_X1_IS_GCREF    = 0x0000000400000000,
    PTFF_X1_IS_BYREF    = 0x0000000800000000,

    PTFF_THREAD_ABORT   = 0x0000001000000000,   // indicates that ThreadAbortException should be thrown when returning from the transition
};

// TODO: Consider moving the PInvokeTransitionFrameFlags definition to a separate file to simplify header dependencies
#ifdef ICODEMANAGER_INCLUDED
// Verify that we can use bitwise shifts to convert from GCRefKind to PInvokeTransitionFrameFlags and back
C_ASSERT(PTFF_X0_IS_GCREF == ((UInt64)GCRK_Object << 32));
C_ASSERT(PTFF_X0_IS_BYREF == ((UInt64)GCRK_Byref << 32));
C_ASSERT(PTFF_X1_IS_GCREF == ((UInt64)GCRK_Scalar_Obj << 32));
C_ASSERT(PTFF_X1_IS_BYREF == ((UInt64)GCRK_Scalar_Byref << 32));

inline UInt64 ReturnKindToTransitionFrameFlags(GCRefKind returnKind)
{
    if (returnKind == GCRK_Scalar)
        return 0;

    return PTFF_SAVE_X0 | PTFF_SAVE_X1 | ((UInt64)returnKind << 32);
}

inline GCRefKind TransitionFrameFlagsToReturnKind(UInt64 transFrameFlags)
{
    GCRefKind returnKind = (GCRefKind)((transFrameFlags & (PTFF_X0_IS_GCREF | PTFF_X0_IS_BYREF | PTFF_X1_IS_GCREF | PTFF_X1_IS_BYREF)) >> 32);
    ASSERT((returnKind == GCRK_Scalar) || ((transFrameFlags & PTFF_SAVE_X0) && (transFrameFlags & PTFF_SAVE_X1)));
    return returnKind;
}
#endif // ICODEMANAGER_INCLUDED
#else // _TARGET_ARM_
enum PInvokeTransitionFrameFlags
{
    // NOTE: Keep in sync with ndp\FxCore\CoreRT\src\Native\Runtime\[amd64|i386]\AsmMacros.inc

    // NOTE: The order in which registers get pushed in the PInvokeTransitionFrame's m_PreservedRegs list has 
    //       to match the order of these flags (that's also the order in which they are read in StackFrameIterator.cpp

    // standard preserved registers
    PTFF_SAVE_RBX       = 0x00000001,
    PTFF_SAVE_RSI       = 0x00000002,
    PTFF_SAVE_RDI       = 0x00000004,
    PTFF_SAVE_RBP       = 0x00000008,   // should never be used, we require RBP frames for methods with 
                                        // pinvoke and it is saved into the frame pointer field instead
    PTFF_SAVE_R12       = 0x00000010,
    PTFF_SAVE_R13       = 0x00000020,
    PTFF_SAVE_R14       = 0x00000040,
    PTFF_SAVE_R15       = 0x00000080,

    PTFF_SAVE_RSP       = 0x00008000,   // Used for 'coop pinvokes' in runtime helper routines.  Methods with
                                        // PInvokes are required to have a frame pointers, but methods which
                                        // call runtime helpers are not.  Therefore, methods that call runtime
                                        // helpers may need RSP to seed the stackwalk.
                                        //
                                        // NOTE: despite the fact that this flag's bit is out of order, it is
                                        // still expected to be saved here after the preserved registers and
                                        // before the scratch registers
    PTFF_SAVE_RAX       = 0x00000100,
    PTFF_SAVE_RCX       = 0x00000200,
    PTFF_SAVE_RDX       = 0x00000400,
    PTFF_SAVE_R8        = 0x00000800,
    PTFF_SAVE_R9        = 0x00001000,
    PTFF_SAVE_R10       = 0x00002000,
    PTFF_SAVE_R11       = 0x00004000,

    PTFF_RAX_IS_GCREF   = 0x00010000,   // used by hijack handler to report return value of hijacked method
    PTFF_RAX_IS_BYREF   = 0x00020000,   // used by hijack handler to report return value of hijacked method

    PTFF_THREAD_ABORT   = 0x00040000,   // indicates that ThreadAbortException should be thrown when returning from the transition
};
#endif // _TARGET_ARM_

#pragma warning(push)
#pragma warning(disable:4200) // nonstandard extension used: zero-sized array in struct/union
class Thread;
#if defined(USE_PORTABLE_HELPERS)
//the members of this structure are currently unused except m_pThread and exist only to allow compilation
//of StackFrameIterator their values are not currently being filled in and will require significant rework
//in order to satisfy the runtime requirements of StackFrameIterator
struct PInvokeTransitionFrame
{
    void*       m_RIP;
    Thread*     m_pThread;  // unused by stack crawler, this is so GetThread is only called once per method
                            // can be an invalid pointer in universal transition cases (which never need to call GetThread)
    uint32_t    m_Flags;    // PInvokeTransitionFrameFlags
};
#else // USE_PORTABLE_HELPERS
struct PInvokeTransitionFrame
{
#ifdef _TARGET_ARM_
    TgtPTR_Void     m_ChainPointer; // R11, used by OS to walk stack quickly
#endif
#ifdef _TARGET_ARM64_
    // On arm64, the FP and LR registers are pushed in that order when setting up frames
    TgtPTR_Void     m_FramePointer;
    TgtPTR_Void     m_RIP;
#else
    TgtPTR_Void     m_RIP;
    TgtPTR_Void     m_FramePointer;
#endif
    TgtPTR_Thread   m_pThread;  // unused by stack crawler, this is so GetThread is only called once per method
                                // can be an invalid pointer in universal transition cases (which never need to call GetThread)
#ifdef _TARGET_ARM64_
    UInt64          m_Flags;  // PInvokeTransitionFrameFlags
#else   
    UInt32          m_Flags;  // PInvokeTransitionFrameFlags
#endif       
    UIntTarget      m_PreservedRegs[];
};
#endif // USE_PORTABLE_HELPERS
#pragma warning(pop)

#ifdef _TARGET_AMD64_
// RBX, RSI, RDI, R12, R13, R14, R15, RAX, RSP
#define PInvokeTransitionFrame_SaveRegs_count 9
#elif defined(_TARGET_X86_)
// RBX, RSI, RDI, RAX, RSP
#define PInvokeTransitionFrame_SaveRegs_count 5
#elif defined(_TARGET_ARM_)
#ifdef PROJECTN
// R4-R6,R8-R10, R0, SP
#define PInvokeTransitionFrame_SaveRegs_count 8
#else // PROJECTN
// R4-R10, R0, SP
#define PInvokeTransitionFrame_SaveRegs_count 9
#endif // PROJECTN
#endif
#define PInvokeTransitionFrame_MAX_SIZE (sizeof(PInvokeTransitionFrame) + (POINTER_SIZE * PInvokeTransitionFrame_SaveRegs_count))

#ifdef _TARGET_AMD64_
#define OFFSETOF__Thread__m_pTransitionFrame 0x40
#elif defined(_TARGET_ARM64_)
#define OFFSETOF__Thread__m_pTransitionFrame 0x40
#elif defined(_TARGET_X86_)
#define OFFSETOF__Thread__m_pTransitionFrame 0x2c
#elif defined(_TARGET_ARM_)
#define OFFSETOF__Thread__m_pTransitionFrame 0x2c
#endif

#ifdef RHDUMP
typedef EEType * PTR_EEType;
typedef PTR_EEType * PTR_PTR_EEType;
#else
typedef DPTR(EEType) PTR_EEType;
typedef DPTR(PTR_EEType) PTR_PTR_EEType;
#endif

struct EETypeRef
{
    union
    {
        EEType *    pEEType;
        EEType **   ppEEType;
        UInt8 *     rawPtr;
        UIntTarget  rawTargetPtr; // x86_amd64: keeps union big enough for target-platform pointer
    };

    static const size_t DOUBLE_INDIR_FLAG = 1;

    PTR_EEType GetValue()
    {
        if (dac_cast<TADDR>(rawTargetPtr) & DOUBLE_INDIR_FLAG)
            return *dac_cast<PTR_PTR_EEType>(rawTargetPtr - DOUBLE_INDIR_FLAG);
        else
            return dac_cast<PTR_EEType>(rawTargetPtr);
    }
};

// Generic type parameter variance type (these are allowed only on generic interfaces or delegates). The type
// values must correspond to those defined in the CLR as CorGenericParamAttr (see corhdr.h).
enum GenericVarianceType : UInt8
{
    GVT_NonVariant = 0,
    GVT_Covariant = 1,
    GVT_Contravariant = 2,
    GVT_ArrayCovariant = 0x20,
};

// Blobs are opaque data passed from the compiler, through the binder and into the native image. At runtime we
// provide a simple API to retrieve these blobs (they're keyed by a simple integer ID). Blobs are passed to
// the binder from the compiler and stored in native images by the binder in a sequential stream, each blob
// having the following header.
struct BlobHeader
{
    UInt32 m_flags;  // Flags describing the blob (used by the binder only at the moment)
    UInt32 m_id;     // Unique identifier of the blob (used to access the blob at runtime)
                     // also used by BlobTypeFieldPreInit to identify (at bind time) which field to pre-init.
    UInt32 m_size;   // Size of the individual blob excluding this header (DWORD aligned)
};

// Structure used in the runtime initialization of deferred static class constructors. Deferred here means
// executed during normal code execution just prior to a static field on the type being accessed (as opposed
// to eager cctors, which are run at module load time). This is the fixed portion of the context structure,
// class libraries can add their own fields to the end.
struct StaticClassConstructionContext
{
    // Pointer to the code for the static class constructor method. This is initialized by the
    // binder/runtime.
    TgtPTR_Void m_cctorMethodAddress;

    // Initialization state of the class. This is initialized to 0. Every time managed code checks the
    // cctor state the runtime will call the classlibrary's CheckStaticClassConstruction with this context
    // structure unless initialized == 1. This check is specific to allow the classlibrary to store more
    // than a binary state for each cctor if it so desires.
    Int32       m_initialized;
};

#endif // !defined(RHDUMP) || !defined(RHDUMP_TARGET_NEUTRAL)

#ifdef FEATURE_CUSTOM_IMPORTS
struct CustomImportDescriptor
{
    UInt32  RvaEATAddr;  // RVA of the indirection cell of the address of the EAT for that module
    UInt32  RvaIAT;      // RVA of IAT array for that module
    UInt32  CountIAT;    // Count of entries in the above array
};
#endif // FEATURE_CUSTOM_IMPORTS

enum RhEHClauseKind
{
    RH_EH_CLAUSE_TYPED              = 0,
    RH_EH_CLAUSE_FAULT              = 1,
    RH_EH_CLAUSE_FILTER             = 2,
    RH_EH_CLAUSE_UNUSED             = 3
};

#define RH_EH_CLAUSE_TYPED_INDIRECT RH_EH_CLAUSE_UNUSED 

#ifndef RHDUMP
// as System::__Canon is not exported by the SharedLibrary.dll, it is represented by a special "pointer" for generic unification
#ifdef BINDER
static const UIntTarget CANON_EETYPE = 42;
#else
static const EEType * CANON_EETYPE = (EEType *)42;
#endif
#endif

#ifndef RHDUMP
// flags describing what a generic unification descriptor (below) describes or contains
enum GenericUnificationFlags
{
    GUF_IS_METHOD       = 0x01,         // GUD represents a method, not a type
    GUF_EETYPE          = 0x02,         // GUD has an indirection cell for the eetype itself
    GUF_DICT            = 0x04,         // GUD has an indirection cell for the dictionary
    GUF_GC_STATICS      = 0x08,         // GUD has 2 indirection cells for the gc statics and their gc desc
    GUF_NONGC_STATICS   = 0x10,         // GUD has an indirection cell for the non gc statics
    GUF_THREAD_STATICS  = 0x20,         // GUD has 3 indirection cells for the tls index, the tls offset and the tls gc desc
    GUF_METHOD_BODIES   = 0x40,         // GUD has indirection cells for method bodies
    GUF_UNBOXING_STUBS  = 0x80,         // GUD has indirection cells for unboxing/instantiating stubs
};

class GenericComposition;

// describes a generic type or method for the purpose of generic unification
struct GenericUnificationDesc
{
    UInt32              m_hashCode;                     // hash code of the type or method
    UInt32              m_flags : 8;                    // GenericUnificationFlags (above)
    UInt32              m_indirCellCountOrOrdinal : 24; // # indir cells used or method ordinal
#ifdef BINDER
    UIntTarget          m_openType;                     // ref to open type
    UIntTarget          m_genericComposition;           // ref to generic composition
                                                        // (including type args of the enclosing type)
#else
    EETypeRef           m_openType;                     // ref to open type
    GenericComposition *m_genericComposition;           // ref to generic composition
                                                        // (including type args of the enclosing type)
#endif // BINDER

    inline UInt32 GetIndirCellIndex(GenericUnificationFlags flags)
    {
#ifdef BINDER
        assert((m_flags & flags) != 0);
#endif // BINDER
        UInt32 indirCellIndex = 0;

        if (flags == GUF_EETYPE)
            return indirCellIndex;
        if (m_flags & GUF_EETYPE)
            indirCellIndex += 1;

        if (flags == GUF_DICT)
            return indirCellIndex;
        if (m_flags & GUF_DICT)
            indirCellIndex += 1;

        if (flags == GUF_GC_STATICS)
            return indirCellIndex;
        if (m_flags & GUF_GC_STATICS)
            indirCellIndex += 2;

        if (flags == GUF_NONGC_STATICS)
            return indirCellIndex;
        if (m_flags & GUF_NONGC_STATICS)
            indirCellIndex += 1;

        if (flags == GUF_THREAD_STATICS)
            return indirCellIndex;
        if (m_flags & GUF_THREAD_STATICS)
            indirCellIndex += 3;

        if (flags == GUF_METHOD_BODIES)
            return indirCellIndex;

#ifdef BINDER
        // not legal to have unboxing stubs without method bodies
        assert((m_flags & (GUF_METHOD_BODIES| GUF_UNBOXING_STUBS)) == (GUF_METHOD_BODIES | GUF_UNBOXING_STUBS));
#endif // BINDER
        if (flags & GUF_UNBOXING_STUBS)
        {
            // the remainining indirection cells should be for method bodies and instantiating/unboxing stubs
            // where each method has an associated instantiating/unboxing stub
            UInt32 remainingIndirCellCount = m_indirCellCountOrOrdinal - indirCellIndex;
            // thus the number of remaining indirection cells should be divisible by 2
            assert(remainingIndirCellCount % 2 == 0);
            // the method bodies come first, followed by the unboxing stubs
            return indirCellIndex + remainingIndirCellCount/2;
        }

#ifdef BINDER
        assert(!"bad GUF flag parameter");
#endif // BINDER
        return indirCellIndex;
    }

#ifdef BINDER
    inline void SetIndirCellCount(UInt32 indirCellCount)
    {
        // generic unification descs for methods always have 1 indirection cell
        assert(!(m_flags & GUF_IS_METHOD));
        m_indirCellCountOrOrdinal = indirCellCount;
        assert(m_indirCellCountOrOrdinal == indirCellCount);
    }

    inline void SetOrdinal(UInt32 ordinal)
    {
        assert(m_flags & GUF_IS_METHOD);
        m_indirCellCountOrOrdinal = ordinal;
        assert(m_indirCellCountOrOrdinal == ordinal);
    }
#endif // !BINDER

    inline UInt32 GetIndirCellCount()
    {
        // generic unification descs for methods always have 1 indirection cell
        return (m_flags & GUF_IS_METHOD) ? 1 : m_indirCellCountOrOrdinal;
    }

    inline UInt32 GetOrdinal()
    {
        // For methods, we need additional identification, for types, we don't
        // However, we need to make sure no type can match a method, so for
        // types we return a value that would be never legal for a method
        return (m_flags & GUF_IS_METHOD) ? m_indirCellCountOrOrdinal : ~0;
    }

    bool Equals(GenericUnificationDesc *that);
};


// mapping of cold code blocks to the corresponding hot entry point RVA
// format is a as follows:
// -------------------
// | subSectionCount |     # of subsections, where each subsection has a run of hot bodies
// -------------------     followed by a run of cold bodies
// | hotMethodCount  |     # of hot bodies in subsection
// | coldMethodCount |     # of cold bodies in subsection
// -------------------
// ... possibly repeated on ARM
// -------------------
// | hotRVA #1       |     RVA of the hot entry point corresponding to the 1st cold body
// | hotRVA #2       |     RVA of the hot entry point corresponding to the 2nd cold body
// ... one entry for each cold body containing the corresponding hot entry point

// number of hot and cold bodies in a subsection of code
// in x86 and x64 there's only one subsection, on ARM there may be several
// for large modules with > 16 MB of code
struct SubSectionDesc
{
    UInt32          hotMethodCount;
    UInt32          coldMethodCount;
};

// this is the structure describing the cold to hot mapping info
struct ColdToHotMapping
{
    UInt32          subSectionCount;
    SubSectionDesc  subSection[/*subSectionCount*/1];
    //  UINT32   hotRVAofColdMethod[/*coldMethodCount*/];
};
#endif
