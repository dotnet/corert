// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Runtime
{
    internal unsafe struct EHClauseIterator
    {
        private uint _totalClauses;
        private byte *_currentPtr;
        private int _currentClause;

        private static uint DecodeUnsigned(ref byte* stream)
        {
            uint value = 0;

            uint val = *stream;
            if ((val & 1) == 0)
            {
                value = (val >> 1);
                stream += 1;
            }
            else if ((val & 2) == 0)
            {
                value = (val >> 2) |
                        (((uint)*(stream + 1)) << 6);
                stream += 2;
            }
            else if ((val & 4) == 0)
            {
                value = (val >> 3) |
                        (((uint)*(stream + 1)) << 5) |
                        (((uint)*(stream + 2)) << 13);
                stream += 3;
            }
            else if ((val & 8) == 0)
            {
                value = (val >> 4) |
                        (((uint)*(stream + 1)) << 4) |
                        (((uint)*(stream + 2)) << 12) |
                        (((uint)*(stream + 3)) << 20);
                stream += 4;
            }
            else if ((val & 16) == 0)
            {
                stream += 1;
                value = ReadUInt32(ref stream);
            }

            // TODO : deleted all the error handling
            return value;
        }

        private static uint ReadUInt32(ref byte* stream)
        {
            uint result = *(uint*)(stream); // Assumes little endian and unaligned access
            stream += 4;
            return result;
        }

        uint GetUnsigned()
        {
            uint value;
            value = DecodeUnsigned(ref _currentPtr);
            return value;
        }

        internal void InitFromEhInfo(byte* ehInfoStart, byte* ehInfoEnd, int idxStart)
        {
            _currentPtr = ehInfoStart;
            _currentClause = 0;
            _totalClauses = GetUnsigned();
        }

        // TODO : copied from EH
        internal enum RhEHClauseKindWasm
        {
            RH_EH_CLAUSE_TYPED = 0,
            RH_EH_CLAUSE_FAULT = 1,
            RH_EH_CLAUSE_FILTER = 2,
            RH_EH_CLAUSE_UNUSED = 3,
        }

        internal bool Next(ref EH.RhEHClauseWasm pEHClause)
        {
            if (_currentClause >= _totalClauses) return false;

            _currentClause++;
            pEHClause._tryStartOffset = GetUnsigned();
            uint tryLengthAndKind = GetUnsigned();
            pEHClause._clauseKind = (RhEHClauseKindWasm)(tryLengthAndKind & 3);
            pEHClause._tryEndOffset = (tryLengthAndKind >> 2) + pEHClause._tryStartOffset;
            switch (pEHClause._clauseKind)
            {
                case RhEHClauseKindWasm.RH_EH_CLAUSE_TYPED:

                    AlignToSymbol();
                    pEHClause._typeSymbol = ReadUInt32(ref _currentPtr);
                    pEHClause._handlerAddress = (byte *)ReadUInt32(ref _currentPtr);
                    break;
                case RhEHClauseKindWasm.RH_EH_CLAUSE_FAULT:
                    AlignToSymbol();
                    pEHClause._handlerAddress = (byte*)ReadUInt32(ref _currentPtr);
                    break;
                case RhEHClauseKindWasm.RH_EH_CLAUSE_FILTER:
                    AlignToSymbol();
                    pEHClause._handlerAddress = (byte*)ReadUInt32(ref _currentPtr);
                    pEHClause._filterAddress = (byte*)ReadUInt32(ref _currentPtr);
                    break;
            }
            return true;
        }

        private void AlignToSymbol()
        {
            _currentPtr = (byte *)(((uint)_currentPtr + 3) & ~(3));
        }
    }
}
