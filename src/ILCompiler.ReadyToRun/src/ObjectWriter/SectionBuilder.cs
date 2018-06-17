// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace ILCompiler.PEWriter
{
    /// <summary>
    /// Opaque key representing a single block.
    /// </summary>
    public struct BlockHandle : IEquatable<BlockHandle>, IEqualityComparer<BlockHandle>
    {
        /// <summary>
        /// Internal block handle.
        /// </summary>
        private int _value;

        /// <summary>
        /// Construct the block handle for a given key
        /// </summary>
        /// <param name="value">Key value to store in the block handle</param>
        public BlockHandle(int value)
        {
            _value = value;
        }

        /// <summary>
        /// True when the block handle is empty.
        /// </summary>
        public bool IsNull { get => (_value == default(int)); }

        /// <summary>
        /// Retrieve value of the block handle
        /// </summary>
        /// <returns></returns>
        public int GetValue()
        {
            return _value;
        }

        bool IEquatable<BlockHandle>.Equals(BlockHandle handle)
        {
            return _value == handle._value;
        }

        bool IEqualityComparer<BlockHandle>.Equals(BlockHandle handle1, BlockHandle handle2)
        {
            return handle1._value == handle2._value;
        }

        int IEqualityComparer<BlockHandle>.GetHashCode(BlockHandle handle)
        {
            return handle._value.GetHashCode();
        }
    }

    /// <summary>
    /// A single relocation entry within a block.
    /// </summary>
    public struct Relocation
    {
        /// <summary>
        /// Offset within the block content to relocate.
        /// </summary>
        public readonly int SourceOffset;

        /// <summary>
        /// Target block for the relocation
        /// </summary>
        public readonly BlockHandle Target;
        
        /// <summary>
        /// Offset within the target block
        /// </summary>
        public readonly int TargetOffset;

        /// <summary>
        /// Relocation type
        /// </summary>
        public readonly ushort Type;
        
        public Relocation(int sourceOffset, BlockHandle target, int targetOffset, ushort type)
        {
            SourceOffset = sourceOffset;
            Target = target;
            TargetOffset = targetOffset;
            Type = type;
        }
    }

    /// <summary>
    /// Block is the minimum section composition unit. The section builder accumulates blocks
    /// and splits them into sections and subsections. Subsequently, during section serialization,
    /// the blocks are enumerated in the proper order and used to actually lay out the sections.
    /// </summary>
    public class Block
    {
        /// <summary>
        /// Block handle (key)
        /// </summary>
        public BlockHandle Handle;

        /// <summary>
        /// Section in which the block resides
        /// </summary>
        public ushort SectionIndex;

        /// <summary>
        /// Subsection in which the block resides
        /// </summary>
        public byte SubsectionIndex;

        /// <summary>
        /// Block alignment and flags.
        /// </summary>
        public byte AlignmentAndFlags;
        
        /// <summary>
        /// Bottom 5 bits represent alignment encoded as dyadic logarithm of the actual
        /// alignment value, thus ranging from 2 &lt;&lt;0 thru 2 &lt;&lt; 31.
        /// </summary>
        public const byte AlignmentMask = 0x1F;
        
        /// <summary>
        /// True when the block contains file-level relocations.
        /// </summary>
        public const byte FlagContainsFileRelocations = 0x20;
        
        /// <summary>
        /// RVA is filled in during section serialization
        /// </summary>
        public int RVAWhenPlaced;

        /// <summary>
        /// Binary block content (also implies block length)
        /// </summary>
        public BlobBuilder Content;

        /// <summary>
        /// List of relocations for the block, may be null. If there are multiple relocations
        /// for a single block, they must be ordered by source offset.
        /// </summary>
        public Relocation[] Relocations;
    }

    /// <summary>
    /// Descriptive information for a single section
    /// </summary>
    public class SectionInfo
    {
        /// <summary>
        /// Index within the internal section table used by the section builder
        /// </summary>
        public readonly int Index;

        /// <summary>
        /// Section name
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Section characteristics
        /// </summary>
        public readonly SectionCharacteristics Characteristics;

        /// <summary>
        /// RVA gets filled in during section serialization.
        /// </summary>
        public int RVAWhenPlaced;

        /// <summary>
        /// Output file position gets filled in during section serialization.
        /// </summary>
        public int FilePosWhenPlaced;

        /// <summary>
        /// Construct a new session description object.
        /// </summary>
        /// <param name="index">Zero-based section index</param>
        /// <param name="name">Section name</param>
        /// <param name="characteristics">Section characteristics</param>
        public SectionInfo(int index, string name, SectionCharacteristics characteristics)
        {
            Index = index;
            Name = name;
            Characteristics = characteristics;
            RVAWhenPlaced = 0;
            FilePosWhenPlaced = 0;
        }
    }

    /// <summary>
    /// This class represents a single export symbol in the PE file.
    /// </summary>
    public class ExportSymbol
    {
        /// <summary>
        /// Symbol identifier
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// When placed into the export section, RVA of the symbol name gets updated.
        /// </summary>
        public int NameRVAWhenPlaced;

        /// <summary>
        /// Export symbol ordinal
        /// </summary>
        public readonly int Ordinal;

        /// <summary>
        /// Block containing the symbol
        /// </summary>
        public readonly Block Block;

        /// <summary>
        /// Offset of the export symbol within the block
        /// </summary>
        public readonly int Offset;
        
        /// <summary>
        /// Construct the export symbol instance filling in its arguments
        /// </summary>
        /// <param name="name">Export symbol identifier</param>
        /// <param name="ordinal">Ordinal ID of the export symbol</param>
        /// <param name="block">Block containing the symbol</param>
        /// <param name="offset">Offset of the export symbol within the block</param>
        public ExportSymbol(string name, int ordinal, Block block, int offset)
        {
            Name = name;
            Ordinal = ordinal;
            Block = block;
            Offset = offset;
        }
    }

    /// <summary>
    /// Section builder is capable of accumulating blocks, using them to lay out sections
    /// and relocate the produced executable according to the block relocation information.
    /// </summary>
    public class SectionBuilder
    {
        /// <summary>
        /// The top list is indexed by section index; each of its element contains a list
        /// of subsections within the particular section, and each subsection contains a list
        /// of blocks.
        /// </summary>
        List<List<List<Block>>> _sectionSubsectionBlockLists;

        /// <summary>
        /// Map from block handles to final block objects is used when applying relocations.
        /// </summary>
        Dictionary<BlockHandle, Block> _blockDictionary;

        /// <summary>
        /// List of sections defined in the builder
        /// </summary>
        List<SectionInfo> _sections;

        /// <summary>
        /// Symbols to export from the PE file.
        /// </summary>
        List<ExportSymbol> _exportSymbols;

        /// <summary>
        /// Export directory entry when available.
        /// </summary>
        DirectoryEntry _exportDirectoryEntry;

        /// <summary>
        /// Relocation directory extra size corresponds to extra file-level relocation entries.
        /// </summary>
        int _relocationDirectoryExtraSize;

        /// <summary>
        /// For PE files with exports, this is the "DLL name" string to store in the export directory table.
        /// </summary>
        string _dllNameForExportDirectoryTable;

        /// <summary>
        /// Construct an empty section builder without any sections or blocks.
        /// </summary>
        public SectionBuilder()
        {
            _sectionSubsectionBlockLists = new List<List<List<Block>>>();
            _blockDictionary = new Dictionary<BlockHandle, Block>();
            _sections = new List<SectionInfo>();
            _exportSymbols = new List<ExportSymbol>();
            _exportDirectoryEntry = default(DirectoryEntry);
            _relocationDirectoryExtraSize = 0;
        }

        /// <summary>
        /// Add a new section. Section names must be unique.
        /// </summary>
        /// <param name="name">Section name</param>
        /// <param name="characteristics">Section characteristics</param>
        /// <returns>Zero-based index of the added section</returns>
        public int AddSection(string name, SectionCharacteristics characteristics)
        {
            int sectionIndex = _sections.Count;
            Debug.Assert(FindSection(name) == null, "Duplicate section");
            _sections.Add(new SectionInfo(sectionIndex, name, characteristics));
            return sectionIndex;
        }

        /// <summary>
        /// Try to look up a pre-existing section in the builder; returns null if not found.
        /// </summary>
        public SectionInfo FindSection(string name)
        {
            return _sections.FirstOrDefault((sec) => sec.Name == name);
        }

        /// <summary>
        /// Attach an export symbol to the output PE file.
        /// </summary>
        /// <param name="name">Export symbol identifier</param>
        /// <param name="ordinal">Ordinal ID of the export symbol</param>
        /// <param name="block">Block containing the symbol</param>
        /// <param name="offset">Offset of the export symbol within the block</param>
        public void AddExportSymbol(string name, int ordinal, Block block, int offset)
        {
            _exportSymbols.Add(new ExportSymbol(
                name: name,
                ordinal: ordinal,
                block: block,
                offset: offset));
        }

        /// <summary>
        /// Record DLL name to emit in the export directory table.
        /// </summary>
        /// <param name="dllName">DLL name to emit</param>
        public void SetDllNameForExportDirectoryTable(string dllName)
        {
            _dllNameForExportDirectoryTable = dllName;
        }

        /// <summary>
        /// Add a block to a given section / subsection.
        /// </summary>
        /// <param name="block">Block to add</param>
        public void AddBlock(Block block)
        {
            if (block.Relocations != null)
            {
                // Check the presence of file-level relocations and set the appropriate AlignmentAndFlags bit.
                foreach (Relocation relocation in block.Relocations)
                {
                    if (Relocator.GetFileRelocationType(relocation.Type) != RelocType.IMAGE_REL_BASED_ABSOLUTE)
                    {
                        // Block has file-level relocations
                        block.AlignmentAndFlags |= Block.FlagContainsFileRelocations;
                        break;
                    }
                }
            }
            
            while (_sectionSubsectionBlockLists.Count <= block.SectionIndex)
            {
                _sectionSubsectionBlockLists.Add(new List<List<Block>>());
            }
            List<List<Block>> subsectionBlockLists = _sectionSubsectionBlockLists[block.SectionIndex];
            while (subsectionBlockLists.Count <= block.SubsectionIndex)
            {
                subsectionBlockLists.Add(new List<Block>());
            }
            List<Block> blockList = subsectionBlockLists[block.SubsectionIndex];
            blockList.Add(block);
            _blockDictionary.Add(block.Handle, block);
        }

        /// <summary>
        /// Get the list of sections that need to be emitted to the output PE file.
        /// </summary>
        public IEnumerable<(string SectionName, SectionCharacteristics Characteristics)> GetSections()
        {
            IEnumerable<(string SectionName, SectionCharacteristics Characteristics)> sectionList =
                _sections.Select((sec) => (SectionName: sec.Name, Characteristics: sec.Characteristics));
            if (_exportSymbols.Count != 0 && FindSection(".edata") == null)
            {
                sectionList = sectionList.Concat(new (string SectionName, SectionCharacteristics Characteristics)[]
                {
                    (SectionName: ".edata", Characteristics: SectionCharacteristics.ContainsInitializedData | SectionCharacteristics.MemRead)
                });
            }
            return sectionList;
        }

        /// <summary>
        /// Traverse blocks within a single section and use them to calculate final layout
        /// of the given section.
        /// </summary>
        /// <param name="name">Section to serialize</param>
        /// <param name="sectionLocation">Logical section address within the output PE file</param>
        /// <returns></returns>
        public BlobBuilder SerializeSection(string name, SectionLocation sectionLocation)
        {
            if (name == ".reloc")
            {
                return SerializeRelocationSection(sectionLocation);
            }
            
            if (name == ".edata")
            {
                return SerializeExportSection(sectionLocation);
            }

            // Locate logical section index by name
            int sectionIndex = _sections.Count - 1;
            while (sectionIndex >= 0 && _sections[sectionIndex].Name != name)
            {
                sectionIndex--;
            }

            BlobBuilder serializedSection = null;
            if (sectionIndex < 0)
            {
                // Section not available
                return null;
            }

            // Place the section
            _sections[sectionIndex].RVAWhenPlaced = sectionLocation.RelativeVirtualAddress;
            _sections[sectionIndex].FilePosWhenPlaced = sectionLocation.PointerToRawData;

            if (_sectionSubsectionBlockLists.Count < sectionIndex)
            {
                // Section is empty
                return null;
            }

            List<List<Block>> subsectionBlockLists = _sectionSubsectionBlockLists[sectionIndex];
            foreach (List<Block> blockList in subsectionBlockLists)
            {
                foreach (Block block in blockList)
                {
                    // Calculate alignment padding
                    int alignmentValue = 1 << (block.AlignmentAndFlags & Block.AlignmentMask);
                    int alignedRva = (sectionLocation.RelativeVirtualAddress + alignmentValue - 1) & -alignmentValue;
                    int padding = alignedRva - sectionLocation.RelativeVirtualAddress;
                    if (padding > 0)
                    {
                        if (serializedSection == null)
                        {
                            serializedSection = new BlobBuilder();
                        }
                        serializedSection.WriteBytes(0, padding);
                        sectionLocation = new SectionLocation(
                            sectionLocation.RelativeVirtualAddress + padding,
                            sectionLocation.PointerToRawData + padding);
                    }
                    block.RVAWhenPlaced = alignedRva;
                    int length = block.Content.Count;
                    if (serializedSection == null)
                    {
                        serializedSection = block.Content;
                    }
                    else
                    {
                        serializedSection.LinkSuffix(block.Content);
                    }
                    sectionLocation = new SectionLocation(
                        sectionLocation.RelativeVirtualAddress + length,
                        sectionLocation.PointerToRawData + length);
                }
            }
            return serializedSection;
        }

        /// <summary>
        /// Emit the .reloc section based on file relocation information in the individual blocks.
        /// We rely on the fact that the .reloc section is emitted last so that, by the time
        /// it's getting serialized, all other sections that may contain relocations have already
        /// been laid out.
        /// </summary>
        private BlobBuilder SerializeRelocationSection(SectionLocation sectionLocation)
        {
            // There are 12 bits for the relative offset
            const int RelocationTypeShift = 12;
            const int MaxRelativeOffsetInBlock = (1 << RelocationTypeShift) - 1;
            
            BlobBuilder builder = new BlobBuilder();
            int baseRVA = 0;
            List<ushort> offsetsAndTypes = null;
            // Traverse relocations in all sections in their RVA order
            // By now, all "normal" sections with relocations should already have been laid out
            foreach (SectionInfo sectionInfo in _sections.OrderBy((sec) => sec.RVAWhenPlaced))
            {
                if (_sectionSubsectionBlockLists.Count <= sectionInfo.Index)
                {
                    // No blocks in section
                    continue;
                }
                
                foreach (List<Block> blockList in _sectionSubsectionBlockLists[sectionInfo.Index])
                {
                    foreach (Block block in blockList)
                    {
                        if ((block.AlignmentAndFlags & Block.FlagContainsFileRelocations) != 0)
                        {
                            // Found block with file relocations
                            foreach (Relocation relocation in block.Relocations)
                            {
                                ushort fileRelocationType = Relocator.GetFileRelocationType(relocation.Type);
                                if (fileRelocationType != RelocType.IMAGE_REL_BASED_ABSOLUTE)
                                {
                                    int relocationRVA = block.RVAWhenPlaced + relocation.SourceOffset;
                                    if (offsetsAndTypes != null && relocationRVA - baseRVA > MaxRelativeOffsetInBlock)
                                    {
                                        // Need to flush relocation block as the current RVA is too far from base RVA
                                        FlushRelocationBlock(builder, baseRVA, offsetsAndTypes);
                                        offsetsAndTypes = null;
                                    }
                                    if (offsetsAndTypes == null)
                                    {
                                        // Create new relocation block
                                        baseRVA = relocationRVA;
                                        offsetsAndTypes = new List<ushort>();
                                    }
                                    ushort offsetAndType = (ushort)((fileRelocationType << RelocationTypeShift) | (relocationRVA - baseRVA));
                                    offsetsAndTypes.Add(offsetAndType);
                                }
                            }
                        }
                    }
                }
            }

            if (offsetsAndTypes != null)
            {
                FlushRelocationBlock(builder, baseRVA, offsetsAndTypes);
            }

            _relocationDirectoryExtraSize = builder.Count;

            return builder;
        }

        /// <summary>
        /// Serialize a block of relocations into the .reloc section.
        /// </summary>
        /// <param name="builder">Output blob builder to receive the serialized relocation block</param>
        /// <param name="baseRVA">Base RVA of the relocation block</param>
        /// <param name="offsetsAndTypes">16-bit entries encoding offset relative to the base RVA (low 12 bits) and relocation type (top 4 bite)</param>
        private static void FlushRelocationBlock(BlobBuilder builder, int baseRVA, List<ushort> offsetsAndTypes)
        {
            // First, emit the block header: 4 bytes starting RVA,
            builder.WriteInt32(baseRVA);
            // followed by the total block size comprising this header
            // and following 16-bit entries.
            builder.WriteInt32(4 + 4 + 2 * offsetsAndTypes.Count);
            // Now serialize out the entries
            foreach (ushort offsetAndType in offsetsAndTypes)
            {
                builder.WriteUInt16(offsetAndType);
            }
        }

        /// <summary>
        /// Serialize the export symbol table into the export section.
        /// </summary>
        /// <param name="location">RVA and file location of the .edata section</param>
        private BlobBuilder SerializeExportSection(SectionLocation sectionLocation)
        {
            _exportSymbols.Sort((es1, es2) => StringComparer.Ordinal.Compare(es1.Name, es2.Name));
            
            BlobBuilder builder = new BlobBuilder();

            int minOrdinal = int.MaxValue;
            int maxOrdinal = int.MinValue;

            // First, emit the name table and store the name RVA's for the individual export symbols
            // Also, record the ordinal range.
            foreach (ExportSymbol symbol in _exportSymbols)
            {
                symbol.NameRVAWhenPlaced = sectionLocation.RelativeVirtualAddress + builder.Count;
                builder.WriteUTF8(symbol.Name);
                builder.WriteByte(0);
                
                if (symbol.Ordinal < minOrdinal)
                {
                    minOrdinal = symbol.Ordinal;
                }
                if (symbol.Ordinal > maxOrdinal)
                {
                    maxOrdinal = symbol.Ordinal;
                }
            }

            // Emit the DLL name
            int dllNameRVA = sectionLocation.RelativeVirtualAddress + builder.Count;
            builder.WriteUTF8(_dllNameForExportDirectoryTable);
            builder.WriteByte(0);

            int[] addressTable = new int[maxOrdinal - minOrdinal + 1];

            // Emit the name pointer table; it should be alphabetically sorted.
            // Also, we can now fill in the export address table as we've detected its size
            // in the previous pass.
            int namePointerTableRVA = sectionLocation.RelativeVirtualAddress + builder.Count;
            foreach (ExportSymbol symbol in _exportSymbols)
            {
                builder.WriteInt32(symbol.NameRVAWhenPlaced);
                addressTable[symbol.Ordinal - minOrdinal] = symbol.Block.RVAWhenPlaced + symbol.Offset;
            }

            // Emit the ordinal table
            int ordinalTableRVA = sectionLocation.RelativeVirtualAddress + builder.Count;
            foreach (ExportSymbol symbol in _exportSymbols)
            {
                builder.WriteUInt16((ushort)(symbol.Ordinal - minOrdinal));
            }

            // Emit the address table
            int addressTableRVA = sectionLocation.RelativeVirtualAddress + builder.Count;
            foreach (int addressTableEntry in addressTable)
            {
                builder.WriteInt32(addressTableEntry);
            }
            
            // Emit the export directory table
            int exportDirectoryTableRVA = sectionLocation.RelativeVirtualAddress + builder.Count;
            // +0x00: reserved
            builder.WriteInt32(0);
            // +0x04: TODO: time/date stamp
            builder.WriteInt32(0);
            // +0x08: major version
            builder.WriteInt16(0);
            // +0x0A: minor version
            builder.WriteInt16(0);
            // +0x0C: DLL name RVA
            builder.WriteInt32(dllNameRVA);
            // +0x10: ordinal base
            builder.WriteInt32(minOrdinal);
            // +0x14: number of entries in the address table
            builder.WriteInt32(addressTable.Length);
            // +0x18: number of name pointers
            builder.WriteInt32(_exportSymbols.Count);
            // +0x1C: export address table RVA
            builder.WriteInt32(addressTableRVA);
            // +0x20: name pointer RVV
            builder.WriteInt32(namePointerTableRVA);
            // +0x24: ordinal table RVA
            builder.WriteInt32(ordinalTableRVA);
            int exportDirectorySize = sectionLocation.RelativeVirtualAddress + builder.Count - exportDirectoryTableRVA;

            _exportDirectoryEntry = new DirectoryEntry(relativeVirtualAddress: exportDirectoryTableRVA, size: exportDirectorySize);
            
            return builder;
        }

        /// <summary>
        /// Update the PE file directories. Currently this is used to update the export symbol table
        /// when export symbols have been added to the section builder.
        /// </summary>
        /// <param name="directoriesBuilder">PE directory builder to update</param>
        public void UpdateDirectories(PEDirectoriesBuilder directoriesBuilder)
        {
            if (_exportDirectoryEntry.Size != 0)
            {
                directoriesBuilder.ExportTable = _exportDirectoryEntry;
            }
            directoriesBuilder.BaseRelocationTable = new DirectoryEntry(
                directoriesBuilder.BaseRelocationTable.RelativeVirtualAddress,
                directoriesBuilder.BaseRelocationTable.Size + _relocationDirectoryExtraSize);
        }

        /// <summary>
        /// Relocate the produced PE file and output the result into a given stream.
        /// </summary>
        /// <param name="peFile">Blob builder representing the complete PE file</param>
        /// <param name="defaultImageBase">Default image load address</param>
        /// <param name="outputStream">Stream to receive the relocated PE file</param>
        public void RelocateOutputFile(BlobBuilder peFile, ulong defaultImageBase, Stream outputStream)
        {
            RelocationHelper relocationHelper = new RelocationHelper(outputStream, defaultImageBase, peFile);

            // Traverse relocations in all sections in their RVA order
            foreach (SectionInfo sectionInfo in _sections.OrderBy((sec) => sec.RVAWhenPlaced))
            {
                if (_sectionSubsectionBlockLists.Count < sectionInfo.Index)
                {
                    // Empty tail section
                    continue;
                }

                int rvaToFilePosDelta = sectionInfo.FilePosWhenPlaced - sectionInfo.RVAWhenPlaced;
                foreach (List<Block> blockList in _sectionSubsectionBlockLists[sectionInfo.Index])
                {
                    foreach (Block block in blockList)
                    {
                        if (block.Relocations != null)
                        {
                            foreach (Relocation relocation in block.Relocations)
                            {
                                // Process a single relocation
                                int relocationRVA = block.RVAWhenPlaced + relocation.SourceOffset;
                                int relocationFilePos = relocationRVA + rvaToFilePosDelta;

                                // Flush parts of PE file before the relocation to the output stream
                                relocationHelper.CopyToFilePosition(relocationFilePos);

                                // Look up relocation target
                                int targetRVA = relocation.TargetOffset;
                                if (!relocation.Target.IsNull)
                                {
                                    Block targetBlock = _blockDictionary[relocation.Target];
                                    targetRVA += targetBlock.RVAWhenPlaced;
                                }

                                relocationHelper.ProcessRelocation(relocation.Type, relocationRVA, targetRVA);
                            }
                        }
                    }
                }
            }

            // Flush remaining PE file blocks after the last relocation
            relocationHelper.CopyRestOfFile();
        }

        /// <summary>
        /// Helper used to copy the produced PE file from a BlobBuilder to an output stream,
        /// applying relocations along the way. It's mostly a linear copier that occasionally stops,
        /// patches a few bytes and then continues.
        /// </summary>
        private class RelocationHelper
        {
            /// <summary>
            /// Enumerator of blobs within the blob builder.
            /// </summary>
            private BlobBuilder.Blobs _peFileBlobs;

            /// <summary>
            /// Blob length is used at the end to verify that the relocated file hasn't changed length.
            /// </summary>
            private int _peFileLength;

            /// <summary>
            /// Backing array for the ArraySegment of the current blob.
            /// </summary>
            private byte[] _currentBlob;

            /// <summary>
            /// Current offset within the active blob.
            /// </summary>
            private int _blobOffset;

            /// <summary>
            /// Remaining number of bytes unprocessed in the active blob.
            /// </summary>
            private int _remainingLength;

            /// <summary>
            /// Preferred image load address is needed to properly fix up absolute relocation types.
            /// </summary>
            private ulong _defaultImageBase;

            /// <summary>
            /// Output stream to receive the relocated file
            /// </summary>
            private Stream _outputStream;

            /// <summary>
            /// Current position in the output file
            /// </summary>
            private int _outputFilePos;

            /// <summary>
            /// Relocation helper stores the output stream and initializes the PE blob builder enumerator.
            /// </summary>
            /// <param name="outputStream">Output stream for the relocated PE file</param>
            /// <param name="peFileBuilder">PE file blob builder</param>
            public RelocationHelper(Stream outputStream, ulong defaultImageBase, BlobBuilder peFileBuilder)
            {
                _outputStream = outputStream;
                _outputFilePos = 0;
                
                _defaultImageBase = defaultImageBase;

                _peFileLength = peFileBuilder.Count;
                _peFileBlobs = peFileBuilder.GetBlobs();
                FetchNextBlob();
            }

            /// <summary>
            /// Copy data from the PE file builder to the output stream, stopping at given file position.
            /// </summary>
            /// <param name="filePos">Output PE file position to stop at</param>
            public void CopyToFilePosition(int filePos)
            {
                CopyBytesToOutput(filePos - _outputFilePos);
            }

            /// <summary>
            /// Copy all unprocessed data (after the last relocation) into the output file
            /// without any further modifications.
            /// </summary>
            public void CopyRestOfFile()
            {
                do
                {
                    CopyBytesToOutput(_remainingLength);
                }
                while (TryFetchNextBlob());
                
                if (_outputFilePos != _peFileLength)
                {
                    // Input / output PE file length mismatch - internal error in the relocator
                    throw new BadImageFormatException();
                }
            }

            /// <summary>
            /// Process a single relocation by copying the required number of bytes into a
            /// buffer, applying the relocation and writing it to the output file.
            /// </summary>
            /// <param name="relocationType">Type of relocation to apply</param>
            /// <param name="sourceRVA">RVA representing the address to relocate</param>
            /// <param name="targetRVA">RVA representing the relocation target</param>
            public void ProcessRelocation(ushort relocationType, int sourceRVA, int targetRVA)
            {
                Relocator relocator = Relocator.GetRelocator(relocationType);
                int relocationLength = relocator.GetLength();
                byte[] relocationBuffer = new byte[relocationLength];
                CopyBytesToBuffer(relocationBuffer);

                // Apply the relocation
                relocator.Relocate(
                    bytes: relocationBuffer,
                    sourceRVA: sourceRVA,
                    targetRVA: targetRVA,
                    defaultImageBase: _defaultImageBase);

                // Write the relocated bytes to the output file
                _outputStream.Write(relocationBuffer, 0, relocationLength);
                _outputFilePos += relocationLength;
            }

            /// <summary>
            /// Read next blob from the PE blob builder. Throw exception of no more data is available
            /// (indicates an inconsistent PE file).
            /// </summary>
            private void FetchNextBlob()
            {
                if (!TryFetchNextBlob())
                {
                    throw new BadImageFormatException();
                }
            }

            /// <summary>
            /// Try to fetch next blob from the PE blob builder, return false on EOF.
            /// </summary>
            /// <returns>True when another blob was successfully fetched, false on EOF</returns>
            private bool TryFetchNextBlob()
            {
                if (!_peFileBlobs.MoveNext())
                {
                    return false;
                }

                ArraySegment<byte> blobContent = _peFileBlobs.Current.GetBytes();
                _currentBlob = blobContent.Array;
                _blobOffset = blobContent.Offset;
                _remainingLength = blobContent.Count;
                return true;
            }

            /// <summary>
            /// Copy a given number of bytes from the PE blob builder to the output stream.
            /// </summary>
            /// <param name="length">Number of bytes to copy</param>
            private void CopyBytesToOutput(int length)
            {
                Debug.Assert(length >= 0);

                while (length > 0)
                {
                    if (_remainingLength == 0)
                    {
                        FetchNextBlob();
                    }

                    int part = Math.Min(length, _remainingLength);
                    _outputStream.Write(_currentBlob, _blobOffset, part);
                    _outputFilePos += part;
                    _blobOffset += part;
                    _remainingLength -= part;
                    length -= part;
                }
            }

            /// <summary>
            /// Copy bytes from the PE blob builder to the given byte buffer;
            /// the number of bytes to copy equals the length of the buffer.
            /// </summary>
            /// <param name="buffer">Buffer to fill in from the blob builder</param>
            public void CopyBytesToBuffer(byte[] buffer)
            {
                int offset = 0;
                while (offset < buffer.Length)
                {
                    if (_remainingLength == 0)
                    {
                        FetchNextBlob();
                    }

                    int part = Math.Min(buffer.Length - offset, _remainingLength);
                    Array.Copy(
                        sourceArray: _currentBlob,
                        sourceIndex: _blobOffset,
                        destinationArray: buffer,
                        destinationIndex: offset,
                        length: part);

                    _blobOffset += part;
                    _remainingLength -= part;
                    offset += part;
                }
            }
        }
    }
    
    /// <summary>
    /// Section builder extensions for R2R PE builder.
    /// </summary>
    public static class SectionBuilderExtensions
    {
        /// <summary>
        /// Emit built sections using the R2R PE writer.
        /// </summary>
        /// <param name="builder">Section builder to emit</param>
        /// <param name="inputReader">Input MSIL reader</param>
        /// <param name="outputStream">Output stream for the final R2R PE file</param>
        public static void EmitR2R(this SectionBuilder builder, PEReader inputReader, Stream outputStream)
        {
            R2RPEBuilder r2rBuilder = new R2RPEBuilder(
                peReader: inputReader,
                sectionNames: builder.GetSections(),
                sectionSerializer: builder.SerializeSection,
                directoriesUpdater: builder.UpdateDirectories);

            BlobBuilder outputPeFile = new BlobBuilder();
            r2rBuilder.Serialize(outputPeFile);

            builder.RelocateOutputFile(outputPeFile, inputReader.PEHeaders.PEHeader.ImageBase, outputStream);
        }
    }
}
