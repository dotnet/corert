// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#include "forward_declarations.h"

#ifdef FEATURE_RWX_MEMORY
#define WRITE_ACCESS_HOLDER_ARG                 , rh::util::WriteAccessHolder *pRWAccessHolder
#define WRITE_ACCESS_HOLDER_ARG_NULL_DEFAULT    , rh::util::WriteAccessHolder *pRWAccessHolder = NULL
#define PASS_WRITE_ACCESS_HOLDER_ARG            , pRWAccessHolder
#else // FEATURE_RWX_MEMORY
#define WRITE_ACCESS_HOLDER_ARG
#define WRITE_ACCESS_HOLDER_ARG_NULL_DEFAULT
#define PASS_WRITE_ACCESS_HOLDER_ARG
#endif // FEATURE_RWX_MEMORY

class AllocHeap
{
  public:
    AllocHeap();

#ifdef FEATURE_RWX_MEMORY
    // If pAccessMgr is non-NULL, it will be used to manage R/W access to the memory allocated.
    AllocHeap(UInt32 rwProtectType = PAGE_READWRITE,
              UInt32 roProtectType = 0, // 0 indicates "same as rwProtectType"
              rh::util::MemAccessMgr* pAccessMgr = NULL);
#endif // FEATURE_RWX_MEMORY

    bool Init();

    bool Init(UInt8 *    pbInitialMem,
              UIntNative cbInitialMemCommit,
              UIntNative cbInitialMemReserve,
              bool       fShouldFreeInitialMem);

    ~AllocHeap();

    // If AllocHeap was created with a MemAccessMgr, pRWAccessHolder must be non-NULL.
    // On return, the holder will permit R/W access to the allocated memory until it
    // is destructed.
    UInt8 * Alloc(UIntNative cbMem WRITE_ACCESS_HOLDER_ARG_NULL_DEFAULT);

    // If AllocHeap was created with a MemAccessMgr, pRWAccessHolder must be non-NULL.
    // On return, the holder will permit R/W access to the allocated memory until it
    // is destructed.
    UInt8 * AllocAligned(UIntNative cbMem,
                         UIntNative alignment
                         WRITE_ACCESS_HOLDER_ARG_NULL_DEFAULT);

    // Returns true if this AllocHeap owns the memory range [pvMem, pvMem+cbMem)
    bool Contains(void * pvMem,
                  UIntNative cbMem);

#ifdef FEATURE_RWX_MEMORY
    // Used with previously-allocated memory for which RW access is needed again.
    // Returns true on success. R/W access will be granted until the holder is
    // destructed.
    bool AcquireWriteAccess(void* pvMem,
                            UIntNative cbMem,
                            rh::util::WriteAccessHolder* pHolder);
#endif // FEATURE_RWX_MEMORY

  private:
    // Allocation Helpers
    UInt8* _Alloc(UIntNative cbMem, UIntNative alignment WRITE_ACCESS_HOLDER_ARG);
    bool _AllocNewBlock(UIntNative cbMem);
    UInt8* _AllocFromCurBlock(UIntNative cbMem, UIntNative alignment WRITE_ACCESS_HOLDER_ARG);
    bool _CommitFromCurBlock(UIntNative cbMem);

    // Access protection helpers
#ifdef FEATURE_RWX_MEMORY
    bool _AcquireWriteAccess(UInt8* pvMem, UIntNative cbMem, rh::util::WriteAccessHolder* pHolder);
#endif // FEATURE_RWX_MEMORY
    bool _UpdateMemPtrs(UInt8* pNextFree, UInt8* pFreeCommitEnd, UInt8* pFreeReserveEnd);
    bool _UpdateMemPtrs(UInt8* pNextFree, UInt8* pFreeCommitEnd);
    bool _UpdateMemPtrs(UInt8* pNextFree);
    bool _UseAccessManager() { return m_rwProtectType != m_roProtectType; }

    static const UIntNative s_minBlockSize = OS_PAGE_SIZE;

    typedef rh::util::MemRange Block;
    typedef DPTR(Block) PTR_Block;
    struct BlockListElem : public Block
    {
        BlockListElem(Block const & block)
            : Block(block)
            {}

        BlockListElem(UInt8 * pbMem, UIntNative  cbMem)
            : Block(pbMem, cbMem)
            {}

        Block       m_block;
        PTR_Block   m_pNext;
    };

    typedef SList<BlockListElem>    BlockList;
    BlockList                       m_blockList;

    UInt32                          m_rwProtectType; // READ/WRITE/EXECUTE/etc
    UInt32                          m_roProtectType; // What to do with fully allocated and initialized pages.

#ifdef FEATURE_RWX_MEMORY
    rh::util::MemAccessMgr*         m_pAccessMgr;
    rh::util::WriteAccessHolder     m_hCurPageRW;   // Used to hold RW access to the current allocation page
                                                    // Passed as pHint to MemAccessMgr::AcquireWriteAccess.
#endif // FEATURE_RWX_MEMORY
    UInt8 *                         m_pNextFree;
    UInt8 *                         m_pFreeCommitEnd;
    UInt8 *                         m_pFreeReserveEnd;

    UInt8 *                         m_pbInitialMem;
    bool                            m_fShouldFreeInitialMem;

    Crst                            m_lock;

    INDEBUG(bool                    m_fIsInit;)
};
typedef DPTR(AllocHeap) PTR_AllocHeap;

//-------------------------------------------------------------------------------------------------
void * __cdecl operator new(size_t n, AllocHeap * alloc);
void * __cdecl operator new[](size_t n, AllocHeap * alloc);

