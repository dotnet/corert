// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"

#include "CodeHeap.h"
#include "JITCodeManager.h"

BYTE * ClrVirtualAllocWithinRange(const BYTE *pMinAddr, const BYTE *pMaxAddr, SIZE_T dwSize, DWORD flAllocationType, DWORD flProtect);

// Helper static fields we only want to look up once.
std::once_flag s_staticInit;

static void *s_mrtAddr = nullptr;
static void *s_bottomAddress = nullptr;
static void *s_topAddress = nullptr;
static DWORD s_pageSize = 0;
extern HMODULE s_hRuntime;

#if FEATURE_SINGLE_MODULE_RUNTIME
extern "C" void RhpNewArray();
#endif

void InitMemoryStatics()
{
    std::call_once(s_staticInit, []()
    {
        HMODULE module = s_hRuntime;
        if (module != NULL)
        {
#if FEATURE_SINGLE_MODULE_RUNTIME
            s_mrtAddr = &RhpNewArray;
#else
            s_mrtAddr = GetProcAddress(module, "RhpNewArray");
#endif
        }

        assert(s_mrtAddr != nullptr);

        SYSTEM_INFO sysInfo;
        GetSystemInfo(&sysInfo);
        s_bottomAddress = sysInfo.lpMinimumApplicationAddress;
        s_topAddress = sysInfo.lpMaximumApplicationAddress;
        s_pageSize = sysInfo.dwPageSize;
    });
}


ExecutableCodeHeap::ExecutableCodeHeap()
: m_base(0), m_curr(0), m_commit(0), m_limit(0)
{
}


bool ExecutableCodeHeap::Init(size_t size)
{
    assert(size > 0);

    InitMemoryStatics();

    // We must allocate code pages within an int32 of the runtime helpers, since the JIT only
    // emits call rel32 instructions.
    size = ALIGN_UP(size, s_pageSize);
    m_base = ClrVirtualAllocWithinRange((BYTE*)s_mrtAddr - INT_MAX / 2, (BYTE*)s_mrtAddr + INT_MAX / 2,
                                        size, MEM_RESERVE, PAGE_EXECUTE_READWRITE);

    m_curr = (size_t)m_base;
    m_commit = m_curr;
    m_limit = m_curr + size;

    return m_base != nullptr;
}

void *ExecutableCodeHeap::AllocMemory(size_t size, DWORD alignment)
{
    assert(size > 0);
    assert(alignment > 0);
    assert(m_curr != 0);

    // The location we will start allocating from.
    size_t curr = ALIGN_UP(m_curr, alignment);

    // Check that we haven't filled the heap.
    if (curr >= m_limit || size >= (m_limit - curr))
        return nullptr;

    // Commit pages, be sure to count the alignment change
    if (!CommitPages(size + (curr - m_curr)))
        return nullptr;

    void *result = (void*)curr;
    m_curr = m_curr + size;
    return result;
}

void *ExecutableCodeHeap::AllocPData(size_t size)
{
    size_t commit = 0;

    {
        // Try to alloc from our heap.
        MutexHolder lock(m_mutex);
        commit = m_commit;

        void *result = AllocMemory(size, 1);
        if (result != nullptr)
            return result;
    }

    // We are out of space.  Create a page of memory for PData within DWORD range.
    size = ALIGN_UP(size, s_pageSize);
    return ClrVirtualAllocWithinRange((BYTE*)commit, (BYTE*)commit + INT_MAX,
                                      size, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
}

void *ExecutableCodeHeap::AllocEHInfoRaw(size_t size)
{
    {
        // Try to alloc from our heap.
        MutexHolder lock(m_mutex);
        void *result = AllocMemory(size, 1);
        if (result != nullptr)
            return result;
    }

    // We are out of space try to create a page for storing EHInfo
    size = ALIGN_UP(size, s_pageSize);
    return ClrVirtualAllocWithinRange(NULL, NULL, size, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
}

void *ExecutableCodeHeap::AllocMemoryWithCodeHeader_NoThrow(size_t codeSize, DWORD alignment)
{
    assert(codeSize > 0);
    assert(alignment > 0);
    assert(m_curr != 0);

    MutexHolder lock(m_mutex);

    // The location we will start allocating from.  The header needs to be pointer aligned.
    size_t hdrAlloc = ALIGN_UP(m_curr, sizeof(void*));
    size_t hdrSize = sizeof(CodeHeader);

    // The code must be aligned correctly too.
    size_t codeAlloc = ALIGN_UP(hdrAlloc + hdrSize, alignment);
    size_t newHeapEnd = codeAlloc + codeSize;

    // Check that we haven't filled the heap.
    if (newHeapEnd >= m_limit)
        return nullptr;

    // Commit pages
    if (!CommitPages(newHeapEnd - m_curr))
        return nullptr;

    // Update heap
    m_curr = newHeapEnd;

    // Calculate return value.
    void *result = (void*)codeAlloc;
    assert(ALIGN_UP(result, alignment) == result);
    
    CodeHeader *header = new ((void*)hdrAlloc)CodeHeader(m_base, (DWORD)((BYTE*)result - (BYTE*)m_base));
    assert(ALIGN_UP(header, sizeof(void*)) == header);

    assert(result == header->GetCode());
    return result;
}

bool ExecutableCodeHeap::CommitPages(size_t size)
{
    // Do we need to commit anything?
    if (m_curr + size <= m_commit)
        return true;

    // Have we reserved enough memory to complete this request?
    size = ALIGN_UP(size, s_pageSize);
    if (m_commit + size > m_limit)
        return false;

    // Commit pages
    void *result = VirtualAlloc((LPVOID)m_commit, size, MEM_COMMIT, PAGE_EXECUTE_READWRITE);
    if (result == nullptr)
        return false;

    m_commit += size;
    return true;
}


#define TOP_MEMORY (s_topAddress)
#define BOT_MEMORY (s_bottomAddress)

#define VIRTUAL_ALLOC_RESERVE_GRANULARITY (64*1024)    // 0x10000  (64 KB)

BYTE * ClrVirtualAllocWithinRange(const BYTE *pMinAddr,
    const BYTE *pMaxAddr,
    SIZE_T dwSize,
    DWORD flAllocationType,
    DWORD flProtect)
{
    InitMemoryStatics();

    BYTE *pResult = NULL;
    //
    // First lets normalize the pMinAddr and pMaxAddr values
    //
    // If pMinAddr is NULL then set it to BOT_MEMORY
    if ((pMinAddr == 0) || (pMinAddr < (BYTE *)BOT_MEMORY))
    {
        pMinAddr = (BYTE *)BOT_MEMORY;
    }

    // If pMaxAddr is NULL then set it to TOP_MEMORY
    if ((pMaxAddr == 0) || (pMaxAddr >(BYTE *) TOP_MEMORY))
    {
        pMaxAddr = (BYTE *)TOP_MEMORY;
    }

    // If pMinAddr is BOT_MEMORY and pMaxAddr is TOP_MEMORY
    // then we can call ClrVirtualAlloc instead 
    if ((pMinAddr == (BYTE *)BOT_MEMORY) && (pMaxAddr == (BYTE *)TOP_MEMORY))
    {
        return (BYTE*)VirtualAlloc(NULL, dwSize, flAllocationType, flProtect);
    }

    // If pMaxAddr is not greater than pMinAddr we can not make an allocation
    if (dwSize == 0 || pMaxAddr <= pMinAddr)
    {
        return NULL;
    }

    // We will do one scan: [pMinAddr .. pMaxAddr]
    // Align to 64k. See docs for VirtualAllocEx and lpAddress and 64k alignment for reasons.
    BYTE *tryAddr = (BYTE *)ALIGN_UP((BYTE *)pMinAddr, VIRTUAL_ALLOC_RESERVE_GRANULARITY);

    // Now scan memory and try to find a free block of the size requested.
    while ((tryAddr + dwSize) <= (BYTE *)pMaxAddr)
    {
        MEMORY_BASIC_INFORMATION mbInfo;

        // Use VirtualQuery to find out if this address is MEM_FREE
        //
        if (!VirtualQuery((LPCVOID)tryAddr, &mbInfo, sizeof(mbInfo)))
            break;

        // Is there enough memory free from this start location?
        if ((mbInfo.State == MEM_FREE) && (mbInfo.RegionSize >= (SIZE_T)dwSize))
        {
            // Try reserving the memory using VirtualAlloc now
            pResult = (BYTE*)VirtualAlloc(tryAddr, dwSize, flAllocationType, flProtect);

            if (pResult != NULL)
            {
                return pResult;
            }

            // We could fail in a race.  Just move on to next region and continue trying
            tryAddr = tryAddr + VIRTUAL_ALLOC_RESERVE_GRANULARITY;
        }
        else
        {
            // Try another section of memory
            tryAddr = max(tryAddr + VIRTUAL_ALLOC_RESERVE_GRANULARITY,
                (BYTE*)mbInfo.BaseAddress + mbInfo.RegionSize);
        }
    }

    // Our tryAddr reached pMaxAddr
    return NULL;
}
