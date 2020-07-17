// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime;

// Disable: Filter expression is a constant. We know. We just can't do an unfiltered catch.
#pragma warning disable 7095

namespace System.Runtime
{
    internal static unsafe partial class EH
    {
        internal struct RhEHClauseWasm
        {
            internal uint _tryStartOffset;
            internal EHClauseIterator.RhEHClauseKindWasm _clauseKind;
            internal uint _tryEndOffset;
            internal uint _typeSymbol;
            internal byte* _handlerAddress;
            internal byte* _filterAddress;

            public bool TryStartsAt(uint idxTryLandingStart)
            {
                return idxTryLandingStart == _tryStartOffset;
            }

            public bool ContainsCodeOffset(uint idxTryLandingStart)
            {
                return ((idxTryLandingStart >= _tryStartOffset) &&
                        (idxTryLandingStart < _tryEndOffset));
            }
        }

        // TODO: temporary to try things out, when working look to see how to refactor with FindFirstPassHandler
        private static bool FindFirstPassHandlerWasm(object exception, uint idxStart, uint idxCurrentBlockStart /* the start IL idx of the current block for the landing pad, will use in place of PC */, 
            void* shadowStack, ref EHClauseIterator clauseIter, out uint tryRegionIdx, out byte* pHandler)
        {
            pHandler = (byte*)0;
            tryRegionIdx = MaxTryRegionIdx;
            uint lastTryStart = 0, lastTryEnd = 0;
            RhEHClauseWasm ehClause = new RhEHClauseWasm();
            for (uint curIdx = 0; clauseIter.Next(ref ehClause); curIdx++)
            {
                // 
                // Skip to the starting try region.  This is used by collided unwinds and rethrows to pickup where
                // the previous dispatch left off.
                //
                if (idxStart != MaxTryRegionIdx)
                {
                    if (curIdx <= idxStart)
                    {
                        lastTryStart = ehClause._tryStartOffset;
                        lastTryEnd = ehClause._tryEndOffset;
                        continue;
                    }

                    // Now, we continue skipping while the try region is identical to the one that invoked the 
                    // previous dispatch.
                    if ((ehClause._tryStartOffset == lastTryStart) && (ehClause._tryEndOffset == lastTryEnd))
                    {
                        continue;
                    }

                    // We are done skipping. This is required to handle empty finally block markers that are used
                    // to separate runs of different try blocks with same native code offsets.
                    idxStart = MaxTryRegionIdx;
                }

                EHClauseIterator.RhEHClauseKindWasm clauseKind = ehClause._clauseKind;
                if (((clauseKind != EHClauseIterator.RhEHClauseKindWasm.RH_EH_CLAUSE_TYPED) &&
                     (clauseKind != EHClauseIterator.RhEHClauseKindWasm.RH_EH_CLAUSE_FILTER))
                    || !ehClause.ContainsCodeOffset(idxCurrentBlockStart))
                {
                    continue;
                }

                // Found a containing clause. Because of the order of the clauses, we know this is the
                // most containing.
                if (clauseKind == EHClauseIterator.RhEHClauseKindWasm.RH_EH_CLAUSE_TYPED)
                {
                    if (ShouldTypedClauseCatchThisException(exception, (EEType*)ehClause._typeSymbol))
                    {
                        pHandler = ehClause._handlerAddress;
                        tryRegionIdx = curIdx;
                        return true;
                    }
                }
                else
                {
                    tryRegionIdx = 0;
                    bool shouldInvokeHandler = InternalCalls.RhpCallFilterFunclet(exception, ehClause._filterAddress, shadowStack);
                    if (shouldInvokeHandler)
                    {
                        pHandler = ehClause._handlerAddress;
                        tryRegionIdx = curIdx;
                        return true;
                    }
                }
            }

            return false;
        }

        private static void InvokeSecondPassWasm(uint idxStart, uint idxTryLandingStart, ref EHClauseIterator clauseIter, uint idxLimit, void* shadowStack)
        {
            uint lastTryStart = 0, lastTryEnd = 0;
            // Search the clauses for one that contains the current offset.
            RhEHClauseWasm ehClause = new RhEHClauseWasm();
            for (uint curIdx = 0; clauseIter.Next(ref ehClause) && curIdx < idxLimit; curIdx++)
            {
                // 
                // Skip to the starting try region.  This is used by collided unwinds and rethrows to pickup where
                // the previous dispatch left off.
                //
                if (idxStart != MaxTryRegionIdx)
                {
                    if (curIdx <= idxStart)
                    {
                        lastTryStart = ehClause._tryStartOffset;
                        lastTryEnd = ehClause._tryEndOffset;
                        continue;
                    }

                    // Now, we continue skipping while the try region is identical to the one that invoked the 
                    // previous dispatch.
                    if ((ehClause._tryStartOffset == lastTryStart) && (ehClause._tryEndOffset == lastTryEnd))
                        continue;

                    // We are done skipping. This is required to handle empty finally block markers that are used
                    // to separate runs of different try blocks with same native code offsets.
                    idxStart = MaxTryRegionIdx;
                }

                EHClauseIterator.RhEHClauseKindWasm clauseKind = ehClause._clauseKind;

                if ((clauseKind != EHClauseIterator.RhEHClauseKindWasm.RH_EH_CLAUSE_FAULT)
                    || !ehClause.TryStartsAt(idxTryLandingStart))
                {
                    continue;
                }

                // Found a containing clause. Because of the order of the clauses, we know this is the
                // most containing.

                // N.B. -- We need to suppress GC "in-between" calls to finallys in this loop because we do
                // not have the correct next-execution point live on the stack and, therefore, may cause a GC
                // hole if we allow a GC between invocation of finally funclets (i.e. after one has returned
                // here to the dispatcher, but before the next one is invoked).  Once they are running, it's 
                // fine for them to trigger a GC, obviously.
                // 
                // As a result, RhpCallFinallyFunclet will set this state in the runtime upon return from the
                // funclet, and we need to reset it if/when we fall out of the loop and we know that the 
                // method will no longer get any more GC callbacks.

                byte* pFinallyHandler = ehClause._handlerAddress;

                InternalCalls.RhpCallFinallyFunclet(pFinallyHandler, shadowStack);
            }
        }
    } // static class EH
}
