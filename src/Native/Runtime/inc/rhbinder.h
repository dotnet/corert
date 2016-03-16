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
        CURRENT_VERSION             = 1,            // Version of the module header protocol. Increment on
                                                    // breaking changes
        DELTA_SHORTCUT_TABLE_SIZE   = 16,
        MAX_REGIONS                 = 8,            // Max number of regions described by the Regions array
        MAX_WELL_KNOWN_METHODS      = 8,            // Max number of methods described by the WellKnownMethods array
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
    UInt32  RraGidsWithGcRootsList;     // RRA to head of list of GenericInstanceDescs which report GC roots
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
    UInt32  RraGenericInstances;        // RRA to the list of regular generic instances contained in the module
    UInt32  CountGenericInstances;      // count of generic instances in the above list
    UInt32  RraGcRootGenericInstances;  // RRA to the list of generic instances with GC roots to report contained in the module
    UInt32  CountGcRootGenericInstances;// count of generic instances in the above list
    UInt32  RraVariantGenericInstances; // RRA to the list of generic instances with variant type parameters contained in the module
    UInt32  CountVariantGenericInstances; // count of generic instances in the above list
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

    UInt32  RraArrayBaseEEType;       // RRA to the classlib's array base type EEType (usually System.Array), zero if this is not the classlib

#ifdef FEATURE_CUSTOM_IMPORTS
    UInt32          RraCustomImportDescriptors;      // RRA to an array of CustomImportDescriptors
    UInt32          CountCustomImportDescriptors;    // count of entries in the above array
#endif // FEATURE_CUSTOM_IMPORTS

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
    DEFINE_GET_ACCESSOR_RO_OR_RW_DATA(GidsWithGcRootsList);
    DEFINE_GET_ACCESSOR(EHInfo,                     RDATA_REGION);
    DEFINE_GET_ACCESSOR(UnwindInfoBlob,             RDATA_REGION);
    DEFINE_GET_ACCESSOR(CallsiteInfoBlob,           RDATA_REGION);

    DEFINE_GET_ACCESSOR(StaticsGCDataSection,       DATA_REGION);
#ifdef FEATURE_CACHED_INTERFACE_DISPATCH
    DEFINE_GET_ACCESSOR(InterfaceDispatchCells,     DATA_REGION);
#endif
    DEFINE_GET_ACCESSOR(FrozenObjects,             DATA_REGION);
    DEFINE_GET_ACCESSOR_RO_OR_RW_DATA(GenericInstances);
    DEFINE_GET_ACCESSOR_RO_OR_RW_DATA(GcRootGenericInstances);
    DEFINE_GET_ACCESSOR_RO_OR_RW_DATA(VariantGenericInstances);

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

#ifndef RHDUMP
    // Macro to generate an inline accessor for well known methods (these are all TEXT-based RRAs since they
    // point to code).
#define DEFINE_WELL_KNOWN_METHOD(_name)                                 \
    inline PTR_VOID Get_##_name()                                       \
    {                                                                   \
        return WellKnownMethods[WKM_##_name] == NULL_RRA ? NULL : RegionPtr[TEXT_REGION] + WellKnownMethods[WKM_##_name]; \
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

struct InterfaceDispatchCacheHeader
{
    EEType *    m_pInterfaceType;   // EEType of interface to dispatch on
    UInt16      m_slotIndex;        // Which slot on the interface should be dispatched on.
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
                                    // for initial dispatch, and if not set, using this as a cache pointer.)

    enum Flags
    {
        // The low 2 bits of the m_pCache pointer are treated specially so that we can avoid the need for 
        // extra fields on this type.
        IDC_CachePointerIsInterfaceRelativePointer = 0x3,
        IDC_CachePointerIsIndirectedInterfaceRelativePointer = 0x2,
        IDC_CachePointerIsInterfacePointer = 0x1,
        IDC_CachePointerPointsAtCache = 0x0,
        IDC_CachePointerMask = 0x3,
    };

#if !defined(RHDUMP) && !defined(BINDER)
    EEType * GetInterfaceType() const
    {
        // Capture m_pCache into a local for safe access (this is a volatile read of a value that may be
        // modified on another thread while this function is executing.)
        UIntTarget cachePointerValue = m_pCache;
        switch (cachePointerValue & IDC_CachePointerMask)
        {
        case IDC_CachePointerPointsAtCache:
            return ((InterfaceDispatchCacheHeader*)cachePointerValue)->m_pInterfaceType;
        case IDC_CachePointerIsInterfacePointer:
            return (EEType*)(cachePointerValue & ~IDC_CachePointerMask);
        case IDC_CachePointerIsInterfaceRelativePointer:
        case IDC_CachePointerIsIndirectedInterfaceRelativePointer:
            {
                UIntTarget interfacePointerValue = (UIntTarget)&m_pCache + cachePointerValue;
                interfacePointerValue &= ~IDC_CachePointerMask;
                if ((cachePointerValue & IDC_CachePointerMask) == IDC_CachePointerIsInterfaceRelativePointer)
                {
                    return (EEType*)interfacePointerValue;
                }
                else
                {
                    return *(EEType**)interfacePointerValue;
                }
            }
        }
        return nullptr;
    }

    static bool IsCache(UIntTarget value)
    {
        if ((value & IDC_CachePointerMask) != 0)
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

    UInt16 GetSlotNumber() const
    {
        // Only call GetCache once, subsequent calls are not garaunteed to return equal results
        InterfaceDispatchCacheHeader* cache = GetCache();

        // If we have a cache, use it instead as its faster to access
        if (cache != nullptr)
        {
            return cache->m_slotIndex;
        }

        // The slot number for an interface dispatch cell is encoded once per run of InterfaceDispatchCells
        // The run is terminated by having an interface dispatch cell with a null stub pointer.
        const InterfaceDispatchCell *currentCell = this;
        while (currentCell->m_pStub != 0)
        {
            currentCell = currentCell + 1;
        } 

        return (UInt16)currentCell->m_pCache;
    }
#endif // !RHDUMP && !BINDER
};

#endif // FEATURE_CACHED_INTERFACE_DISPATCH

#ifdef _TARGET_ARM_
// Note for ARM: try and keep the flags in the low 16-bits, since they're not easy to load into a register in
// a single instruction within our stubs.
enum PInvokeTransitionFrameFlags
{
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
};
#else // _TARGET_ARM_
enum PInvokeTransitionFrameFlags
{
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
};
#endif // _TARGET_ARM_

#pragma warning(push)
#pragma warning(disable:4200) // nonstandard extension used: zero-sized array in struct/union
class Thread;
struct PInvokeTransitionFrame
{
#ifdef _TARGET_ARM_
    TgtPTR_Void     m_ChainPointer; // R11, used by OS to walk stack quickly
#endif
    TgtPTR_Void     m_RIP;
    TgtPTR_Void     m_FramePointer;
    TgtPTR_Thread   m_pThread;  // unused by stack crawler, this is so GetThread is only called once per method
                                // can be an invalid pointer in universal transition cases (which never need to call GetThread)
    UInt32          m_dwFlags;  // PInvokeTransitionFrameFlags
#ifdef _TARGET_AMD64_
    UInt32          m_dwAlignPad2;
#endif
    UIntTarget      m_PreservedRegs[];
};
#pragma warning(pop)

#ifdef _TARGET_AMD64_
// RBX, RSI, RDI, R12, R13, R14, R15, RAX, RSP
#define PInvokeTransitionFrame_SaveRegs_count 9
#elif defined(_TARGET_X86_)
// RBX, RSI, RDI, RAX, RSP
#define PInvokeTransitionFrame_SaveRegs_count 5
#elif defined(_TARGET_ARM_)
// R4-R6,R8-R10, R0, SP
#define PInvokeTransitionFrame_SaveRegs_count 8
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

// The GenericInstanceDesc structure holds additional type information associated with generic EETypes. The
// amount of data can potentially get quite large but many of the items can be omitted for most types.
// Therefore, rather than representing the data as a regular C++ struct with fixed fields, we include one
// fixed field, a bitmask indicating what sort of data is encoded, and then encode only the required data in a
// packed form.
//
// While this is straightforward enough, we have a lot of fields that now all require accessor methods (get
// offset of value, get value, set value) and the offset calculations, though simple, are messy and easy to
// get wrong. In light of this we use a script to write the accessor code. See
// rh\tools\WriteOptionalFieldsCode.pl for the script and rh\src\inc\GenericInstanceDescFields.src for the
// definitions of the fields that it takes as input.

struct GenericInstanceDesc
{
#include "GenericInstanceDescFields.h"

#ifdef DACCESS_COMPILE
    static UInt32 DacSize(TADDR addr);
#endif

    UInt32 GetHashCode()
    {
        UInt32 hash = 0;
        const UInt32 HASH_MULT = 1220703125; // 5**13
        hash ^= (UInt32)dac_cast<TADDR>(this->GetGenericTypeDef().GetValue());
        for (UInt32 i = 0; i < this->GetArity(); i++)
        {
            hash *= HASH_MULT;
            hash ^= (UInt32)dac_cast<TADDR>(this->GetParameterType(i).GetValue());
        }
        return hash;
    }
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

// Structure used to store offsets information of thread static fields, and mainly used
// by Reflection to get the address of that field in the TLS block
struct ThreadStaticFieldOffsets
{
    UInt32 StartingOffsetInTlsBlock;    // Offset in the TLS block containing the thread static fields of a given type
    UInt32 FieldOffset;                 // Offset of a thread static field from the start of its containing type's TLS fields block
                                        // (in other words, the address of a field is 'TLS block + StartingOffsetInTlsBlock + FieldOffset')
};
