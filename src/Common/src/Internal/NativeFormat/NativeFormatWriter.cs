// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

// Managed mirror of NativeFormatWriter.h/.cpp
namespace Internal.NativeFormat
{
#if NATIVEFORMAT_PUBLICWRITER
    public
#else
    internal
#endif
    abstract class Vertex
    {
        internal int _offset = NotPlaced;
        internal int _iteration = -1; // Iteration that the offset is valid for

        internal const int NotPlaced = -1;
        internal const int Placed = -2;
        internal const int Unified = -3;

        public Vertex()
        {
        }

        internal abstract void Save(NativeWriter writer);

        public int VertexOffset
        {
            get
            {
                Debug.Assert(_offset >= 0);
                return _offset;
            }
        }
    }

#if NATIVEFORMAT_PUBLICWRITER
    public
#else
    internal
#endif
    class Section
    {
        internal List<Vertex> _items = new List<Vertex>();
        internal Dictionary<Vertex, Vertex> _placedMap = new Dictionary<Vertex, Vertex>();

        public Section()
        {
        }

        public Vertex Place(Vertex vertex)
        {
            if (vertex._offset == Vertex.Unified)
            {
                Vertex placedVertex;
                if (_placedMap.TryGetValue(vertex, out placedVertex))
                    return placedVertex;

                placedVertex = new PlacedVertex(vertex);
                _placedMap.Add(vertex, placedVertex);
                vertex = placedVertex;
            }

            Debug.Assert(vertex._offset == Vertex.NotPlaced);
            vertex._offset = Vertex.Placed;
            _items.Add(vertex);

            return vertex;
        }
    }

#if NATIVEFORMAT_PUBLICWRITER
    public
#else
    internal
#endif
    class NativeWriter
    {
        List<Section> _sections = new List<Section>();

        enum SavePhase
        {
            Initial,
            Shrinking,
            Growing
        }


        int _iteration = 0;
        SavePhase _phase; // Current save phase
        int _offsetAdjustment; // Cumulative offset adjustment compared to previous iteration
        int _paddingSize; // How much padding was used

        Dictionary<Vertex, Vertex> _unifier = new Dictionary<Vertex, Vertex>();

        NativePrimitiveEncoder _encoder = new NativePrimitiveEncoder();

#if NATIVEFORMAT_COMPRESSION
        struct Tentative
        {
            internal Vertex Vertex;
            internal int PreviousOffset;
        }

        // State used by compression
        List<Tentative> _tentativelyWritten = new List<Tentative>(); // Tentatively written Vertices.
        int _compressionDepth = 0;
#endif

        public NativeWriter()
        {
            _encoder.Init();
        }

        public Section NewSection()
        {
            Section section = new Section();
            _sections.Add(section);
            return section;
        }

        public void WriteByte(byte b) { _encoder.WriteByte(b); }
        public void WriteUInt8(byte value) { _encoder.WriteUInt8(value); }
        public void WriteUInt16(ushort value) { _encoder.WriteUInt16(value); }
        public void WriteUInt32(uint value) { _encoder.WriteUInt32(value); }
        public void WriteUInt64(ulong value) { _encoder.WriteUInt64(value); }
        public void WriteUnsigned(uint d) { _encoder.WriteUnsigned(d); }
        public void WriteSigned(int i) { _encoder.WriteSigned(i); }
        public void WriteUnsignedLong(ulong i) { _encoder.WriteUnsignedLong(i); }
        public void WriteSignedLong(long i) { _encoder.WriteSignedLong(i); }
        public void WriteFloat(float value) { _encoder.WriteFloat(value); }
        public void WriteDouble(double value) { _encoder.WriteDouble(value); }

        public void WritePad(int size)
        {
            while (size > 0)
            {
                _encoder.WriteByte(0);
                size--;
            }
        }

        public bool IsGrowing()
        {
            return _phase == SavePhase.Growing;
        }

        public void UpdateOffsetAdjustment(int offsetDelta)
        {
            switch (_phase)
            {
                case SavePhase.Shrinking:
                    _offsetAdjustment = Math.Min(_offsetAdjustment, offsetDelta);
                    break;
                case SavePhase.Growing:
                    _offsetAdjustment = Math.Max(_offsetAdjustment, offsetDelta);
                    break;
                default:
                    break;
            }
        }

        public void RollbackTo(int offset)
        {
            _encoder.RollbackTo(offset);
        }

        public void RollbackTo(int offset, int offsetAdjustment)
        {
            _offsetAdjustment = offsetAdjustment;
            RollbackTo(offset);
        }

        public void PatchByteAt(int offset, byte value)
        {
            _encoder.PatchByteAt(offset, value);
        }

        // Swallow exceptions if invalid encoding is detected.
        // This is the price we have to pay for using UTF8. Thing like High Surrogate Start Char - '\ud800'
        // can be expressed in UTF-16 (which is the format used to store ECMA metadata), but don't have
        // a representation in UTF-8.
        private static Encoding _stringEncoding = new UTF8Encoding(false, false);

        public void WriteString(string s)
        {
            // The actual bytes are only necessary for the final version during the growing plase
            if (IsGrowing())
            {
                byte[] bytes = _stringEncoding.GetBytes(s);

                _encoder.WriteUnsigned((uint)bytes.Length);
                for (int i = 0; i < bytes.Length; i++)
                    _encoder.WriteByte(bytes[i]);
            }
            else
            {
                int byteCount = _stringEncoding.GetByteCount(s);
                _encoder.WriteUnsigned((uint)byteCount);
                WritePad(byteCount);
            }
        }

        public void WriteRelativeOffset(Vertex val)
        {
            if (val._iteration == -1)
            {
                // If the offsets are not determined yet, use the maximum possible encoding
                _encoder.WriteSigned(0x7FFFFFFF);
                return;
            }

            int offset = val._offset;

            // If the offset was not update in this iteration yet, adjust it by delta we have accumulated in this iteration so far.
            // This adjustment allows the offsets to converge faster.
            if (val._iteration < _iteration)
                offset += _offsetAdjustment;

            _encoder.WriteSigned(offset - GetCurrentOffset());
        }

        public int GetCurrentOffset()
        {
            return _encoder.Size;
        }

        public int GetNumberOfIterations()
        {
            return _iteration;
        }

        public int GetPaddingSize()
        {
            return _paddingSize;
        }

        public void Save(Stream stream)
        {
            _encoder.Clear();

            _phase = SavePhase.Initial;
            foreach (var section in _sections) foreach (var vertex in section._items)
            {
                vertex._offset = GetCurrentOffset();
                vertex._iteration = _iteration;
                vertex.Save(this);

#if NATIVEFORMAT_COMPRESSION
                // Ensure that the compressor state is fully flushed
                Debug.Assert(_TentativelyWritten.Count == 0);
                Debug.Assert(_compressionDepth == 0);
#endif
            }

            // Aggresive phase that only allows offsets to shrink.
            _phase = SavePhase.Shrinking;
            for (; ; )
            {
                _iteration++;
                _encoder.Clear();

                _offsetAdjustment = 0;

                foreach (var section in _sections) foreach (var vertex in section._items)
                {
                    int currentOffset = GetCurrentOffset();

                    // Only allow the offsets to shrink.
                    _offsetAdjustment = Math.Min(_offsetAdjustment, currentOffset - vertex._offset);

                    vertex._offset += _offsetAdjustment;

                    if (vertex._offset < currentOffset)
                    {
                        // It is possible for the encoding of relative offsets to grow during some iterations.
                        // Ignore this growth because of it should disappear during next iteration.
                        RollbackTo(vertex._offset);
                    }
                    Debug.Assert(vertex._offset == GetCurrentOffset());

                    vertex._iteration = _iteration;

                    vertex.Save(this);

#if NATIVEFORMAT_COMPRESSION
                    // Ensure that the compressor state is fully flushed
                    Debug.Assert(_tentativelyWritten.Count == 0);
                    Debug.Assert(_compressionDepth == 0);
#endif
                }

                // We are not able to shrink anymore. We cannot just return here. It is possible that we have rolledback
                // above because of we shrinked too much.
                if (_offsetAdjustment == 0)
                    break;

                // Limit number of shrinking interations. This limit is meant to be hit in corner cases only.
                if (_iteration > 10)
                    break;
            }

            // Conservative phase that only allows the offsets to grow. It is guaranteed to converge.
            _phase = SavePhase.Growing;
            for (; ; )
            {
                _iteration++;
                _encoder.Clear();

                _offsetAdjustment = 0;
                _paddingSize = 0;

                foreach (var section in _sections) foreach (var vertex in section._items)
                {
                    int currentOffset = GetCurrentOffset();

                    // Only allow the offsets to grow.
                    _offsetAdjustment = Math.Max(_offsetAdjustment, currentOffset - vertex._offset);

                    vertex._offset += _offsetAdjustment;

                    if (vertex._offset > currentOffset)
                    {
                        // Padding
                        int padding = vertex._offset - currentOffset;
                        _paddingSize += padding;
                        WritePad(padding);
                    }
                    Debug.Assert(vertex._offset == GetCurrentOffset());

                    vertex._iteration = _iteration;

                    vertex.Save(this);

#if NATIVEFORMAT_COMPRESSION
                    // Ensure that the compressor state is fully flushed
                    Debug.Assert(_tentativelyWritten.Count == 0);
                    Debug.Assert(_compressionDepth == 0);
#endif
                }

                if (_offsetAdjustment == 0)
                {
                    _encoder.Save(stream);
                    return;
                }
            }
        }

#if NATIVEFORMAT_COMPRESSION
        // TODO:
#else
        struct TypeSignatureCompressor
        {
            TypeSignatureCompressor(NativeWriter pWriter) { }
            void Pack(Vertex vertex) { }
        }
#endif

        T Unify<T>(T vertex) where T : Vertex
        {
            Vertex existing;
            if (_unifier.TryGetValue(vertex, out existing))
                return (T)existing;

            Debug.Assert(vertex._offset == Vertex.NotPlaced);
            vertex._offset = Vertex.Unified;
            _unifier.Add(vertex, vertex);

            return vertex;
        }

        public Vertex GetUnsignedConstant(uint value)
        {
            UnsignedConstant vertex = new UnsignedConstant(value);
            return Unify(vertex);
        }

        public Vertex GetTuple(Vertex item1, Vertex item2)
        {
            Tuple vertex = new Tuple(item1, item2);
            return Unify(vertex);
        }

        public Vertex GetTuple(Vertex item1, Vertex item2, Vertex item3)
        {
            Tuple vertex = new Tuple(item1, item2, item3);
            return Unify(vertex);
        }
    }

    class PlacedVertex : Vertex
    {
        Vertex _unified;

        public PlacedVertex(Vertex unified)
        {
            _unified = unified;
        }

        internal override void Save(NativeWriter writer)
        {
            _unified.Save(writer);
        }
    }

    class UnsignedConstant : Vertex
    {
        uint _value;

        public UnsignedConstant(uint value)
        {
            _value = value;
        }

        internal override void Save(NativeWriter writer)
        {
            writer.WriteUnsigned(_value);
        }

        public override int GetHashCode()
        {
            return 6659 + ((int)_value) * 19;
        }
        public override bool Equals(object other)
        {
            if (!(other is UnsignedConstant))
                return false;

            UnsignedConstant p = (UnsignedConstant)other;
            if (_value != p._value) return false;
            return true;
        }
    }

    class Tuple : Vertex
    {
        private Vertex _item1;
        private Vertex _item2;
        private Vertex _item3;

        public Tuple(Vertex item1, Vertex item2, Vertex item3 = null)
        {
            _item1 = item1;
            _item2 = item2;
            _item3 = item3;
        }

        internal override void Save(NativeWriter writer)
        {
            _item1.Save(writer);
            _item2.Save(writer);
            if (_item3 != null)
                _item3.Save(writer);
        }

        public override int GetHashCode()
        {
            int hash = _item1.GetHashCode() * 93481 + _item2.GetHashCode() + 3492;
            if (_item3 != null)
                hash += (hash << 7) + _item3.GetHashCode() * 34987 + 213;
            return hash;
        }

        public override bool Equals(object obj)
        {
            Tuple other = obj as Tuple;
            if (other == null)
                return false;

            return Object.Equals(_item1, other._item1) &&
                Object.Equals(_item2, other._item2) &&
                Object.Equals(_item3, other._item3);
        }
    }

#if NATIVEFORMAT_PUBLICWRITER
    public
#else
    internal
#endif
    class VertexHashtable : Vertex
    {
        struct Entry
        {
            public Entry(uint hashcode, Vertex vertex)
            {
                Offset = 0;
                Hashcode = hashcode;
                Vertex = vertex;
            }

            public int Offset;

            public uint Hashcode;
            public Vertex Vertex;

            public static int Comparison(Entry a, Entry b)
            {
                return (int)(a.Hashcode /*& mask*/) - (int)(b.Hashcode /*& mask*/); 
            }
        }

        List<Entry> _Entries;

        // How many entries to target per bucket. Higher fill factor means smaller size, but worse runtime perf.
        int _nFillFactor;

        // Number of buckets choosen for the table. Must be power of two. 0 means that the table is still open for mutation.
        uint _nBuckets;

        // Current size of index entry
        int _entryIndexSize; // 0 - uint8, 1 - uint16, 2 - uint32

        public const int DefaultFillFactor = 13;

        public VertexHashtable(int fillFactor = DefaultFillFactor)
        {
            _Entries = new List<Entry>();
            _nFillFactor = fillFactor;
            _nBuckets = 0;
            _entryIndexSize = 0;
        }

        public void Append(uint hashcode, Vertex element)
        {
            // The table needs to be open for mutation
            Debug.Assert(_nBuckets == 0);

            _Entries.Add(new Entry(hashcode, element));
        }

        // Returns 1 + log2(x) rounded up, 0 iff x == 0
        static int HighestBit(uint x)
        {
            int ret = 0;
            while (x != 0)
            {
                x >>= 1;
                ret++;
            }
            return ret;
        }

        // Helper method to back patch entry index in the bucket table
        static void PatchEntryIndex(NativeWriter writer, int patchOffset, int entryIndexSize, int entryIndex)
        {
            if (entryIndexSize == 0)
            {
                writer.PatchByteAt(patchOffset, (byte)entryIndex);
            }
            else if (entryIndexSize == 1)
            {
                writer.PatchByteAt(patchOffset, (byte)entryIndex);
                writer.PatchByteAt(patchOffset + 1, (byte)(entryIndex >> 8));
            }
            else
            {
                writer.PatchByteAt(patchOffset, (byte)entryIndex);
                writer.PatchByteAt(patchOffset + 1, (byte)(entryIndex >> 8));
                writer.PatchByteAt(patchOffset + 2, (byte)(entryIndex >> 16));
                writer.PatchByteAt(patchOffset + 3, (byte)(entryIndex >> 24));
            }
        }

        void ComputeLayout()
        {
            uint bucketsEstimate = (uint)(_Entries.Count / _nFillFactor);

            // Round number of buckets up to the power of two
            _nBuckets = (uint)(1 << HighestBit(bucketsEstimate));

            // Lowest byte of the hashcode is used for lookup within the bucket. Keep it sorted too so that
            // we can use the ordering to terminate the lookup prematurely.
            uint mask = ((_nBuckets - 1) << 8) | 0xFF;

            // sort it by hashcode
            _Entries.Sort(
                    (a, b) =>
                    {
                        return (int)(a.Hashcode & mask) - (int)(b.Hashcode & mask);
                    }
                );

            // Start with maximum size entries
            _entryIndexSize = 2;
        }

        internal override void Save(NativeWriter writer)
        {
            // Compute the layout of the table if we have not done it yet
            if (_nBuckets == 0)
                ComputeLayout();

            int nEntries = _Entries.Count;
            int startOffset = writer.GetCurrentOffset();
            uint bucketMask = (_nBuckets - 1);

            // Lowest two bits are entry index size, the rest is log2 number of buckets 
            int numberOfBucketsShift = HighestBit(_nBuckets) - 1;
            writer.WriteByte((byte)((numberOfBucketsShift << 2) | _entryIndexSize));

            int bucketsOffset = writer.GetCurrentOffset();

            writer.WritePad((int)((_nBuckets + 1) << _entryIndexSize));

            // For faster lookup at runtime, we store the first entry index even though it is redundant (the 
            // value can be inferred from number of buckets)
            PatchEntryIndex(writer, bucketsOffset, _entryIndexSize, writer.GetCurrentOffset() - bucketsOffset);

            int iEntry = 0;

            for (int iBucket = 0; iBucket < _nBuckets; iBucket++)
            {
                while (iEntry < nEntries)
                {
                    if (((_Entries[iEntry].Hashcode >> 8) & bucketMask) != iBucket)
                        break;

                    Entry curEntry = _Entries[iEntry];

                    int currentOffset = writer.GetCurrentOffset();
                    writer.UpdateOffsetAdjustment(currentOffset - curEntry.Offset);
                    curEntry.Offset = currentOffset;
                    _Entries[iEntry] = curEntry;

                    writer.WriteByte((byte)curEntry.Hashcode);
                    writer.WriteRelativeOffset(curEntry.Vertex);

                    iEntry++;
                }

                int patchOffset = bucketsOffset + ((iBucket + 1) << _entryIndexSize);

                PatchEntryIndex(writer, patchOffset, _entryIndexSize, writer.GetCurrentOffset() - bucketsOffset);
            }
            Debug.Assert(iEntry == nEntries);

            int maxIndexEntry = (writer.GetCurrentOffset() - bucketsOffset);
            int newEntryIndexSize = 0;
            if (maxIndexEntry > 0xFF)
            {
                newEntryIndexSize++;
                if (maxIndexEntry > 0xFFFF)
                    newEntryIndexSize++;
            }

            if (writer.IsGrowing())
            {
                if (newEntryIndexSize > _entryIndexSize)
                {
                    // Ensure that the table will be redone with new entry index size
                    writer.UpdateOffsetAdjustment(1);

                    _entryIndexSize = newEntryIndexSize;
                }
            }
            else
            {
                if (newEntryIndexSize < _entryIndexSize)
                {
                    // Ensure that the table will be redone with new entry index size
                    writer.UpdateOffsetAdjustment(-1);

                    _entryIndexSize = newEntryIndexSize;
                }
            }
        }
    }
}
