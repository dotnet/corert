// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

//#include "clrinternal.h"

#include <mutex>


/* A simple executable code heap.
 *
 * This heap does not dynamically grow, instead it stays a fixed size.  This is
 * important since we must report the exact bounds of each JIT manager to the
 * runtime.
 */
class ExecutableCodeHeap
{
public:
    ExecutableCodeHeap();

    /* Initialize the code heap for use.  Must be called before any other method.
     * Returns: True if successful, false on failure to reserve "size" space on
     *          the heap.
     */
    bool Init(size_t size);

    // Returns the base memory address this heap allocates.
    inline void *GetBase() const { return m_base; }

    // Returns the size (in bytes) of this heap.
    inline size_t GetSize() const { return m_limit - (size_t)m_base; }

    // Allocates a chunk of executable memory.
    void *AllocMemory_NoThrow(size_t size, DWORD alignment);

    /* Allocates a chunk of executable memory with a CodeHeader before it.
     * Returns the address of memory to write instructions to (that is,
     * this function returns the pointer AFTER the CodeHeader).  The code
     * header is placed at retval-ALIGN_UP(sizeof(CodeHeader), alignment).
     */
    void *AllocMemoryWithCodeHeader_NoThrow(size_t size, DWORD alignment);

    /* Allocates space for PData in the correct location (unwind data must
     * be located AFTER the code it referrs to, within a DWORD from it).
     */
    void *AllocPData(size_t size);

    /*Allocate space for EH info
    */
    void *AllocEHInfoRaw(size_t size);

private:
    void *AllocMemory(size_t size, DWORD alignment);
    bool CommitPages(size_t len);

private:
    void *m_base;               // The base address where we started allocating
    volatile size_t m_curr;     // The current "used" line of memory.
    volatile size_t m_commit;   // The committed memory line.
    volatile size_t m_limit;    // The limit of memory this heap can use (also the reserved line).

    std::mutex m_mutex;
    typedef std::lock_guard<std::mutex> MutexHolder;
};
