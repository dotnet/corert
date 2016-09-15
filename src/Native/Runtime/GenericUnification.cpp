// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
#include "slist.h"
#include "holder.h"
#include "Crst.h"
#include "rhbinder.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "GenericUnification.h"
#include "eetype.h"

bool GenericUnificationHashtable::GrowTable(UInt32 minSize)
{
    // immediately give up for ridiculously large sizes
    if (minSize >= 1024 * 1024 * 1024 / sizeof(Entry *))
        return false;

    UInt32 newSize = max(1024, m_tableSize);
    while (newSize < minSize)
        newSize *= 2;

    // newSize should be a power of 2 as we always double the size of the table
    // we rely on this property, otherwise masking off bits to index wouldn't work
    ASSERT((newSize & (newSize - 1)) == 0);

    Entry ** newTable = new (nothrow) Entry*[newSize];
    if (newTable == nullptr)
        return false;

    memset(newTable, 0, sizeof(newTable[0])*newSize);
    UInt32 newHashMask = newSize - 1;

    for (UInt32 i = 0; i < m_tableSize; i++)
    {
        Entry * pNextEntry;
        for (Entry *pEntry = m_table[i]; pEntry != nullptr; pEntry = pNextEntry)
        {
            pNextEntry = pEntry->m_nextInHash;

            GenericUnificationDesc * pDesc = pEntry->m_desc;
            UInt32 hashCode = pDesc->m_hashCode & newHashMask;

            pEntry->m_nextInHash = newTable[hashCode];
            newTable[hashCode] = pEntry;
        }
    }

    if (m_table != nullptr)
        delete[] m_table;

    m_table = newTable;
    m_tableSize = newSize;
    m_hashMask = newHashMask;

    return true;
}


// this generic type or method is not a duplicate - enter it into the hash table
bool GenericUnificationHashtable::EnterDesc(GenericUnificationDesc *pDesc, UIntTarget *pIndirCells)
{
    if (m_tableSize < m_entryCount)
    {
        if (!GrowTable(m_entryCount))
            return false;
    }

    m_entryCount++;
    
    UInt32 hashCode = pDesc->m_hashCode & m_hashMask;

    Entry *pEntry = new (nothrow) Entry(m_table[hashCode], pDesc, pIndirCells);
    if (pEntry == nullptr)
        return false;
    m_table[hashCode] = pEntry;

    if (pDesc->m_flags & GUF_GC_STATICS)
    {
        UInt32 indirCellIndex = pDesc->GetIndirCellIndex(GUF_GC_STATICS);
        UInt8 *pGcStaticData = (UInt8 *)pIndirCells[indirCellIndex];
        StaticGcDesc *pGcStaticsDesc = (StaticGcDesc *)pIndirCells[indirCellIndex + 1];
        if (!GetRuntimeInstance()->AddDynamicGcStatics(pGcStaticData, pGcStaticsDesc))
            return false;
    }

    if (pDesc->m_flags & GUF_THREAD_STATICS)
    {
        UInt32 indirCellIndex = pDesc->GetIndirCellIndex(GUF_THREAD_STATICS);
        UInt32 tlsIndex = *(UInt32 *)pIndirCells[indirCellIndex];

        // replace the pointer to the tls index by the tls index itself
        // this is done so code referencing the tls index does not have to do an additional indirection
        pIndirCells[indirCellIndex] = tlsIndex;
        
        UInt32 tlsOffset = (UInt32)pIndirCells[indirCellIndex + 1];
        StaticGcDesc *pGcStaticsDesc = (StaticGcDesc *)pIndirCells[indirCellIndex + 2];
        if (!GetRuntimeInstance()->AddDynamicThreadStaticGcData(tlsIndex, tlsOffset, pGcStaticsDesc))
            return false;
    }

    return true;
}


// we have found a duplicate - copy the indirection cells from the winner over those from the loser
void GenericUnificationHashtable::CopyIndirCells(Entry *pWinnerEntry, GenericUnificationDesc *pLoserDesc, UIntTarget *pLoserIndirCells)
{
    GenericUnificationDesc *pWinnerDesc = pWinnerEntry->m_desc;
    UIntTarget *pWinnerIndirCells = pWinnerEntry->m_indirCells;

    ASSERT(pWinnerDesc->m_flags == pLoserDesc->m_flags);

    UInt32 winnerIndirCellCount = pWinnerDesc->GetIndirCellCount();
    UInt32 loserIndirCellCount = pLoserDesc->GetIndirCellCount();

    if (pWinnerDesc->m_flags & GUF_THREAD_STATICS)
    {
        // the cells for the thread static index and thread static offset should be 
        // copied always because 0 can be a valid value
        UInt32 threadStaticIndirCellIndex = pWinnerDesc->GetIndirCellIndex(GUF_THREAD_STATICS);
        pLoserIndirCells[threadStaticIndirCellIndex] = pWinnerIndirCells[threadStaticIndirCellIndex];
        pLoserIndirCells[threadStaticIndirCellIndex + 1] = pWinnerIndirCells[threadStaticIndirCellIndex + 1];
    }

    ASSERT(winnerIndirCellCount == loserIndirCellCount);
    for (UInt32 i = 0; i < winnerIndirCellCount; i++)
    {
        if (i < loserIndirCellCount)
        {
            // pointers to method bodies can be null if the body was not present
            if (pWinnerIndirCells[i] != 0)
            {
                // don't overwrite the loser's cells with null
                pLoserIndirCells[i] = pWinnerIndirCells[i];
            }
            else if (pLoserIndirCells[i] != 0)
            {
                // overwrite winner's null with a loser's non-null
                // so that later losers get the unified value
                pWinnerIndirCells[i] = pLoserIndirCells[i];
            }
        }
    }
}


// unify one generic type or method
bool GenericUnificationHashtable::UnifyDesc(GenericUnificationDesc *pDesc, UIntTarget *pIndirCells)
{
    UInt32 hashCode = pDesc->m_hashCode & m_hashMask;

    for (Entry *pEntry = m_table[hashCode]; pEntry != nullptr; pEntry = pEntry->m_nextInHash)
    {
        if (pEntry->m_desc->Equals(pDesc))
        {
            CopyIndirCells(pEntry, pDesc, pIndirCells);

#if defined(GENERIC_UNIFICATION_STATS)
            m_duplicateCount++;
#endif

            return true;
        }
    }
    return EnterDesc(pDesc, pIndirCells);
}

#if defined(GENERIC_UNIFICATION_STATS)
#ifdef _X86_
static UInt64 GetTicks()
{
    _asm rdtsc
}
#else
#define GetTicks    PalGetTickCount
#endif
#endif

// unify an array of descriptors describing a parallel array of indirection cells
bool GenericUnificationHashtable::UnifyDescs(GenericUnificationDesc *descs, UInt32 descCount, UIntTarget *pIndirCells, UInt32 indirCellCount)
{
    UNREFERENCED_PARAMETER(indirCellCount);
    ASSERT(descCount < 128 * 1024 * 1024);
    if (m_tableSize < descCount)
    {
        if (!GrowTable(descCount))
            return false;
    }

#if defined(GENERIC_UNIFICATION_STATS)
    m_indirCellCount += indirCellCount;
    UInt64 startTicks = GetTicks();
#endif

    UInt32 indirCellIndex = 0;
    for (UInt32 i = 0; i < descCount; i++)
    {
        ASSERT(indirCellIndex <= indirCellCount);
        ASSERT(descs[i].GetIndirCellCount() <= indirCellCount - indirCellIndex);
        if (!UnifyDesc(&descs[i], &pIndirCells[indirCellIndex]))
            return false;
        indirCellIndex += descs[i].GetIndirCellCount();
    }

#if defined(GENERIC_UNIFICATION_STATS)
    UInt64 elapsedTicks = GetTicks() - startTicks;
    m_elapsedTicks += elapsedTicks;
#endif

    return true;
}


bool GenericUnificationDesc::Equals(GenericUnificationDesc *that)
{
    if (this->m_hashCode != that->m_hashCode)
        return false;

    if (this->m_openType.GetValue() != that->m_openType.GetValue())
        return false;

    if (this->GetOrdinal() != that->GetOrdinal())
        return false;

    return this->m_genericComposition->Equals(that->m_genericComposition);
}


bool GenericComposition::Equals(GenericComposition *that)
{
    if (this->m_arity != that->m_arity)
        return false;

    EETypeRef *thisArgList = this->GetArguments();
    EETypeRef *thatArgList = that->GetArguments();

    for (unsigned i = 0; i < m_arity; i++)
    {
        EEType *thisArg = thisArgList[i].GetValue();
        EEType *thatArg = thatArgList[i].GetValue();

        if (thisArg == thatArg)
            continue;

        if (thisArg == CANON_EETYPE || thatArg == CANON_EETYPE)
            return false;

        if (!thisArg->IsEquivalentTo(thatArg))
            return false;
    }

    return true;
}
