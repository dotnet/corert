// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Internal.TypeSystem;

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
        }

        private TargetDetails _target;
        private ArrayBuilder<Relocation> _relocs;
        private ArrayBuilder<byte> _data;
        internal int Alignment;
        internal ArrayBuilder<ISymbolNode> DefinedSymbols;

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
            ObjectNode.ObjectData returnData = new ObjectNode.ObjectData(_data.ToArray(),
                                                                         _relocs.ToArray(),
                                                                         Alignment,
                                                                         DefinedSymbols.ToArray());

            return returnData;
        }
    }
}
