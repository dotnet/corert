#pragma once

class GenericUnificationHashtable
{
    struct Entry
    {
        Entry *                     m_nextInHash;
        GenericUnificationDesc  *   m_desc;
        UIntTarget              *   m_indirCells;

        Entry(Entry *next, GenericUnificationDesc *desc, UIntTarget *indirCells) : m_nextInHash(next), m_desc(desc), m_indirCells(indirCells)
        {
        }
    };

    Entry   **  m_table;            // table of hash buckets
    UInt32      m_tableSize;        // size of the table - always a power of two
    UInt32      m_hashMask;         // mask to AND hash code with
    UInt32      m_entryCount;       // number of entries in table
#ifdef _DEBUG
#define GENERIC_UNIFICATION_STATS
#endif
#if defined(GENERIC_UNIFICATION_STATS)
    UInt32      m_duplicateCount;   // number of duplicate generic unification descs found so far
    UInt32      m_indirCellCount;   // number of indirection cells found so far
    UInt64      m_elapsedTicks;     // number of "ticks" spent so far - either clock cycles (x86) or milliseconds
#endif

    // grow the size of the hash table to at least minSize
    bool GrowTable(UInt32 minSize);

    // unify one generic type or method
    bool UnifyDesc(GenericUnificationDesc *pDesc, UIntTarget *pIndirCells);

    // this generic type or method is not a duplicate - enter it into the hash table
    bool EnterDesc(GenericUnificationDesc *pDesc, UIntTarget *pIndirCells);

    // we have found a duplicate - copy the indirection cells from the winner over those from the loser
    void CopyIndirCells(Entry *pWinnerEntry, GenericUnificationDesc *pLoserDesc, UIntTarget *pLoserIndirCells);

public:
    GenericUnificationHashtable() : m_table(nullptr), m_tableSize(0), m_hashMask(0), m_entryCount(0)
#if defined(GENERIC_UNIFICATION_STATS)
        , m_duplicateCount(0), m_indirCellCount(0), m_elapsedTicks(0)
#endif
    {
    }

    // unify an array of descriptors describing a parallel array of indirection cells
    bool UnifyDescs(GenericUnificationDesc *descs, UInt32 descCount, UIntTarget *pIndirCells, UInt32 indirCellCount);
};