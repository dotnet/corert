// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public struct ObjectDataBuilder
    {
        public ObjectDataBuilder(NodeFactory factory)
        {
            _target = factory.Target;
            _data = new ArrayBuilder<byte>();
            _relocs = new ArrayBuilder<Relocation>();
            Alignment = 1;
            DefinedSymbols = new ArrayBuilder<ISymbolNode>();
#if DEBUG
            _numReservations = 0;
#endif
        }

        private TargetDetails _target;
        private ArrayBuilder<Relocation> _relocs;
        private ArrayBuilder<byte> _data;
        internal int Alignment;
        internal ArrayBuilder<ISymbolNode> DefinedSymbols;

#if DEBUG
        private int _numReservations;
#endif

        public int CountBytes
        {
            get
            {
                return _data.Count;
            }
        }

        public void RequireAlignment(int align)
        {
            Alignment = Math.Max(align, Alignment);
        }

        public void RequirePointerAlignment()
        {
            RequireAlignment(_target.PointerSize);
        }

        public void EmitByte(byte emit)
        {
            _data.Add(emit);
        }

        public void EmitShort(short emit)
        {
            EmitByte((byte)(emit & 0xFF));
            EmitByte((byte)((emit >> 8) & 0xFF));
        }

        public void EmitInt(int emit)
        {
            EmitByte((byte)(emit & 0xFF));
            EmitByte((byte)((emit >> 8) & 0xFF));
            EmitByte((byte)((emit >> 16) & 0xFF));
            EmitByte((byte)((emit >> 24) & 0xFF));
        }

        public void EmitLong(long emit)
        {
            EmitByte((byte)(emit & 0xFF));
            EmitByte((byte)((emit >> 8) & 0xFF));
            EmitByte((byte)((emit >> 16) & 0xFF));
            EmitByte((byte)((emit >> 24) & 0xFF));
            EmitByte((byte)((emit >> 32) & 0xFF));
            EmitByte((byte)((emit >> 40) & 0xFF));
            EmitByte((byte)((emit >> 48) & 0xFF));
            EmitByte((byte)((emit >> 56) & 0xFF));
        }

        public void EmitBytes(byte[] bytes)
        {
            _data.Append(bytes);
        }

        public void EmitZeroPointer()
        {
            _data.ZeroExtend(_target.PointerSize);
        }

        public void EmitZeros(int numBytes)
        {
            _data.ZeroExtend(numBytes);
        }

        private Reservation GetReservationTicket(int size)
        {
#if DEBUG
            _numReservations++;
#endif
            Reservation ticket = (Reservation)_data.Count;
            _data.ZeroExtend(size);
            return ticket;
        }

        private int ReturnReservationTicket(Reservation reservation)
        {
#if DEBUG
            Debug.Assert(_numReservations > 0);
            _numReservations--;
#endif
            return (int)reservation;
        }

        public Reservation ReserveByte()
        {
            return GetReservationTicket(1);
        }

        public void EmitByte(Reservation reservation, byte emit)
        {
            int offset = ReturnReservationTicket(reservation);
            _data[offset] = emit;
        }

        public Reservation ReserveShort()
        {
            return GetReservationTicket(2);
        }

        public void EmitShort(Reservation reservation, short emit)
        {
            int offset = ReturnReservationTicket(reservation);
            _data[offset] = (byte)(emit & 0xFF);
            _data[offset + 1] = (byte)((emit >> 8) & 0xFF);
        }

        public Reservation ReserveInt()
        {
            return GetReservationTicket(4);
        }

        public void EmitInt(Reservation reservation, int emit)
        {
            int offset = ReturnReservationTicket(reservation);
            _data[offset] = (byte)(emit & 0xFF);
            _data[offset + 1] = (byte)((emit >> 8) & 0xFF);
            _data[offset + 2] = (byte)((emit >> 16) & 0xFF);
            _data[offset + 3] = (byte)((emit >> 24) & 0xFF);
        }

        public void AddRelocAtOffset(ISymbolNode symbol, RelocType relocType, int offset, int delta = 0)
        {
            Relocation symbolReloc = new Relocation();
            symbolReloc.Target = symbol;
            symbolReloc.RelocType = relocType;
            symbolReloc.Offset = offset;
            symbolReloc.Delta = delta;
            _relocs.Add(symbolReloc);
        }

        public void EmitReloc(ISymbolNode symbol, RelocType relocType, int delta = 0)
        {
            AddRelocAtOffset(symbol, relocType, _data.Count, delta);

            // And add space for the reloc
            switch (relocType)
            {
                case RelocType.IMAGE_REL_BASED_REL32:
                    EmitInt(0);
                    break;
                case RelocType.IMAGE_REL_BASED_DIR64:
                    EmitLong(0);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void EmitPointerReloc(ISymbolNode symbol, int delta = 0)
        {
            if (_target.PointerSize == 8)
            {
                EmitReloc(symbol, RelocType.IMAGE_REL_BASED_DIR64, delta);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public ObjectNode.ObjectData ToObjectData()
        {
#if DEBUG
            Debug.Assert(_numReservations == 0);
#endif

            ObjectNode.ObjectData returnData = new ObjectNode.ObjectData(_data.ToArray(),
                                                                         _relocs.ToArray(),
                                                                         Alignment,
                                                                         DefinedSymbols.ToArray());

            return returnData;
        }

        public enum Reservation { }
    }
}
