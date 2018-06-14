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

namespace ILCompiler.PEWriter
{
    /// <summary>
    /// Ready-to-run PE builder combines copying the input MSIL PE executable with managed
    /// metadata and IL and adding new code and data representing the R2R JITted code and
    /// additional runtime structures (R2R header and tables).
    /// </summary>
    public class R2RPEBuilder : PEBuilder
    {
        /// <summary>
        /// PE reader representing the input MSIL PE file we're copying to the output composite PE file.
        /// </summary>
        private PEReader _peReader;
        
        /// <summary>
        /// Complete list of section names includes the sections present in the input MSIL file
        /// (.text, optionally .rsrc and .reloc) and extra questions injected during the R2R PE
        /// creation.
        /// </summary>
        private ImmutableArray<Section> _sections;

        /// <summary>
        /// Callback which is called to emit the data for each section.
        /// </summary>
        private Func<string, SectionLocation, BlobBuilder> _sectionSerializer;

        /// <summary>
        /// For each copied section, we store its initial and end RVA in the source PE file
        /// and the RVA difference between the old and new file. We use this table to relocate
        /// directory entries in the PE file header.
        /// </summary>
        private List<Tuple<int, int, int>> _sectionRvaDeltas;

        /// <summary>
        /// Constructor initializes the various control structures and combines the section list.
        /// </summary>
        /// <param name="peReader">Input MSIL PE file reader</param>
        /// <param name="sectionNames">Custom section names to add to the output PE</param>
        /// <param name="sectionSerializer">Callback for emission of data for the individual sections</param>
        public R2RPEBuilder(
            PEReader peReader,
            IEnumerable<Tuple<string, SectionCharacteristics>> sectionNames = null,
            Func<string, SectionLocation, BlobBuilder> sectionSerializer = null)
            : base(PEHeaderCopier.Copy(peReader.PEHeaders), deterministicIdProvider: null)
        {
            _peReader = peReader;
            _sectionSerializer = sectionSerializer;
            
            _sectionRvaDeltas = new List<Tuple<int, int, int>>();
            
            PEHeaders headers = peReader.PEHeaders;

            PEHeaderBuilder peHeaderBuilder = PEHeaderCopier.Copy(headers);

            ImmutableArray<Section>.Builder sectionListBuilder = ImmutableArray.CreateBuilder<Section>();

            foreach (SectionHeader sectionHeader in peReader.PEHeaders.SectionHeaders)
            {
                if (sectionHeader.Name == ".text")
                {
                    sectionListBuilder.Add(new Section(sectionHeader.Name, sectionHeader.SectionCharacteristics));
                }
            }

            if (sectionNames != null)
            {
                foreach (Tuple<string, SectionCharacteristics> nameCharPair in sectionNames)
                {
                    if (!peReader.PEHeaders.SectionHeaders.Any((header) => header.Name == nameCharPair.Item1))
                    {
                        sectionListBuilder.Add(new Section(nameCharPair.Item1, nameCharPair.Item2));
                    }
                }
            }

            foreach (SectionHeader sectionHeader in peReader.PEHeaders.SectionHeaders)
            {
                if (sectionHeader.Name != ".text")
                {
                    sectionListBuilder.Add(new Section(sectionHeader.Name, sectionHeader.SectionCharacteristics));
                }
            }
            
            _sections = sectionListBuilder.ToImmutable();
        }
        
        /// <summary>
        /// Copy all directory entries and the address of entry point, relocating them along the way.
        /// </summary>
        protected override PEDirectoriesBuilder GetDirectories()
        {
            PEDirectoriesBuilder builder = new PEDirectoriesBuilder();

            builder.AddressOfEntryPoint = RelocateRVA(_peReader.PEHeaders.PEHeader.AddressOfEntryPoint);
            builder.ExportTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.ExportTableDirectory);
            builder.ImportTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.ImportTableDirectory);
            builder.ResourceTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.ResourceTableDirectory);
            builder.ExceptionTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.ExceptionTableDirectory);
            // TODO - missing in PEDirectoriesBuilder
            // builder.CertificateTable = RelocateDirectoryEntry(peHeaders.PEHeader.CertificateTableDirectory);
            builder.BaseRelocationTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.BaseRelocationTableDirectory);
            builder.DebugTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.DebugTableDirectory);
            builder.CopyrightTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.CopyrightTableDirectory);
            builder.GlobalPointerTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.GlobalPointerTableDirectory);
            builder.ThreadLocalStorageTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.ThreadLocalStorageTableDirectory);
            builder.LoadConfigTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.LoadConfigTableDirectory);
            builder.BoundImportTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.BoundImportTableDirectory);
            builder.ImportAddressTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.ImportAddressTableDirectory);
            builder.DelayImportTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.DelayImportTableDirectory);
            builder.CorHeaderTable = RelocateDirectoryEntry(_peReader.PEHeaders.PEHeader.CorHeaderTableDirectory);
            return builder;
        }

        /// <summary>
        /// Relocate a single directory entry.
        /// </summary>
        /// <param name="entry">Directory entry to allocate</param>
        /// <returns>Relocated directory entry</returns>
        private DirectoryEntry RelocateDirectoryEntry(DirectoryEntry entry)
        {
            return new DirectoryEntry(RelocateRVA(entry.RelativeVirtualAddress), entry.Size);
        }
        
        /// <summary>
        /// Relocate a given RVA using the section offset table produced during section serialization.
        /// </summary>
        /// <param name="rva">RVA to relocate</param>
        /// <returns>Relocated RVA</returns>
        private int RelocateRVA(int rva)
        {
            if (rva == 0)
            {
                // Zero RVA is normally used as NULL
                return rva;
            }
            foreach (Tuple<int, int, int> rangeMap in _sectionRvaDeltas)
            {
                if (rva >= rangeMap.Item1 && rva < rangeMap.Item2)
                {
                    // We found the input section holding the RVA, apply its specific delt (output RVA - input RVA).
                    return rva + rangeMap.Item3;
                }
            }
            Debug.Assert(false, "RVA is not within any of the input sections - output PE may be inconsistent");
            return rva;
        }

        /// <summary>
        /// Provide an array of sections for the PEBuilder to use.
        /// </summary>
        protected override ImmutableArray<Section> CreateSections()
        {
            return _sections;
        }

        /// <summary>
        /// Output the section with a given name. For sections existent in the source MSIL PE file
        /// (.text, optionally .rsrc and .reloc), we first copy the content of the input MSIL PE file
        /// and then call the section serialization callback to emit the extra content after the input
        /// section content.
        /// </summary>
        /// <param name="name">Section name</param>
        /// <param name="location">RVA and file location where the section will be put</param>
        /// <returns>Blob builder representing the section data</returns>
        protected override BlobBuilder SerializeSection(string name, SectionLocation location)
        {
            BlobBuilder sectionDataBuilder = new BlobBuilder();
            int sectionIndex = _peReader.PEHeaders.SectionHeaders.Count() - 1;
            while (sectionIndex >= 0 && _peReader.PEHeaders.SectionHeaders[sectionIndex].Name != name)
            {
                sectionIndex--;
            }
            if (sectionIndex >= 0)
            {
                SectionHeader sectionHeader = _peReader.PEHeaders.SectionHeaders[sectionIndex];
                int sectionOffset = (_peReader.IsLoadedImage ? sectionHeader.VirtualAddress : sectionHeader.PointerToRawData);
                int rvaDelta = location.RelativeVirtualAddress - sectionHeader.VirtualAddress;
                
                _sectionRvaDeltas.Add(new Tuple<int, int, int>(
                    sectionHeader.VirtualAddress,
                    sectionHeader.VirtualAddress + Math.Max(sectionHeader.VirtualSize, sectionHeader.SizeOfRawData),
                    rvaDelta));
                
                unsafe
                {
                    int bytesToRead = Math.Min(sectionHeader.SizeOfRawData, sectionHeader.VirtualSize);
                    BlobReader inputSectionReader = _peReader.GetEntireImage()
                        .GetReader(sectionOffset, sectionHeader.SizeOfRawData);
                        
                    if (name == ".rsrc")
                    {
                        // There seems to be a bug in BlobBuilder - when we LinkSuffix to an empty blob builder,
                        // the blob data goes out of sync and WriteContentTo outputs garbage.
                        sectionDataBuilder = PEResourceHelper.Relocate(inputSectionReader, rvaDelta);
                    }
                    else
                    {
                        sectionDataBuilder.WriteBytes(inputSectionReader.StartPointer, bytesToRead);
                    }

                    if (sectionHeader.VirtualSize > bytesToRead)
                    {
                        // If the number of bytes read from the source PE file is less than the virtual size,
                        // zero pad to the end of virtual size before emitting extra section data
                        sectionDataBuilder.WriteBytes(0, sectionHeader.VirtualSize - bytesToRead);
                    }
                    location = new SectionLocation(
                        location.RelativeVirtualAddress + sectionDataBuilder.Count,
                        location.PointerToRawData + sectionDataBuilder.Count);
                }
            }
            if (_sectionSerializer != null)
            {
                BlobBuilder extraData = _sectionSerializer(name, location);
                if (sectionIndex < 0)
                {
                    // See above - there's a bug due to which LinkSuffix to an empty BlobBuilder screws up the blob content.
                    sectionDataBuilder = extraData;
                }
                else
                {
                    sectionDataBuilder.LinkSuffix(extraData);
                }
            }
            
            return sectionDataBuilder;
        }
    }

    /// <summary>
    /// When copying PE contents we may need to move the resource section, however its internal
    /// ResourceDataEntry records hold RVA's so they need to be relocated. Thankfully the resource
    /// data model is very simple so that we just traverse the structure using offset constants.
    /// </summary>
    unsafe sealed class PEResourceHelper
    {
        /// <summary>
        /// Field offsets in the resource directory table.
        /// </summary>
        private static class DirectoryTable
        {
            public const int Characteristics = 0x0;
            public const int TimeDateStamp = 0x04;
            public const int MajorVersion = 0x08;
            public const int MinorVersion = 0x0A;
            public const int NumberOfNameEntries = 0x0C;
            public const int NumberOfIDEntries = 0x0E;
            public const int Size = 0x10;
        }
        
        /// <summary>
        /// Field offsets in the resource directory entry.
        /// </summary>
        private static class DirectoryEntry
        {
            public const int NameOffsetOrID = 0x0;
            public const int DataOrSubdirectoryOffset = 0x4;
            public const int Size = 0x8;
        }

        /// <summary>
        /// When the 4-byte value at the offset DirectoryEntry.DataOrSubdirectoryOffset
        /// has 31-st bit set, it's a subdirectory table entry; when it's clear, it's a
        /// resource data entry.
        /// </summary>
        private const int EntryOffsetIsSubdirectory = unchecked((int)0x80000000u);
        
        /// <summary>
        /// Field offsets in the resource data entry.
        /// </summary>
        private static class DataEntry
        {
            public const int RVA = 0x0;
            public const int Size = 0x4;
            public const int Codepage = 0x8;
            public const int Reserved = 0xC;
        }
        
        /// <summary>
        /// Blob reader representing the input resource section.
        /// </summary>
        private BlobReader _reader;

        /// <summary>
        /// This BlobBuilder holds the relocated resource section after the ctor finishes.
        /// </summary>
        private BlobBuilder _builder;

        /// <summary>
        /// Relocation delta (the difference between input and output RVA of the resource section).
        /// </summary>
        private int _delta;

        /// <summary>
        /// Offsets within the resource section representing RVA's in the resource data entries
        /// that need relocating.
        /// </summary>
        private List<int> _offsetsOfRvasToRelocate;
        
        /// <summary>
        /// Public API receives the input resource section reader and the relocation delta
        /// and returns a blob builder representing the relocated resource section.
        /// </summary>
        /// <param name="reader">Blob reader representing the input resource section</param>
        /// <param name="delta">Relocation delta to apply (value to add to RVA's)</param>
        public static BlobBuilder Relocate(BlobReader reader, int delta)
        {
            return new PEResourceHelper(reader, delta)._builder;
        }
        
        /// <summary>
        /// Private constructor first traverses the internal graph of resource tables
        /// and collects offsets to RVA's that need relocation; after that we sort the list of
        /// offsets and do a linear copying pass patching the RVA cells with the updated values.
        /// </summary>
        /// <param name="reader">Blob reader representing the input resource section</param>
        /// <param name="delta">Relocation delta to apply (value to add to RVA's)</param>
        private PEResourceHelper(BlobReader reader, int delta)
        {
            _reader = reader;
            _builder = new BlobBuilder();
            _delta = delta;
            
            _offsetsOfRvasToRelocate = new List<int>();
            
            TraverseDirectoryTable(tableOffset: 0);

            _offsetsOfRvasToRelocate.Sort();
            int currentOffset = 0;
            
            _reader.Reset();
            foreach (int offsetOfRvaToRelocate in _offsetsOfRvasToRelocate)
            {
                int bytesToCopy = offsetOfRvaToRelocate - currentOffset;
                Debug.Assert(bytesToCopy >= 0);
                if (bytesToCopy > 0)
                {
                    _builder.WriteBytes(_reader.CurrentPointer, bytesToCopy);
                    _reader.Offset += bytesToCopy;
                    currentOffset += bytesToCopy;
                }
                int rva = _reader.ReadInt32();
                _builder.WriteInt32(rva + delta);
                currentOffset += sizeof(int);
            }
            if (_reader.RemainingBytes > 0)
            {
                _builder.WriteBytes(_reader.CurrentPointer, _reader.RemainingBytes);
            }
        }
        
        /// <summary>
        /// Traverse a single directory table at a given offset within the resource section.
        /// Please note the method might end up calling itself recursively through the call graph
        /// TraverseDirectoryTable -&gt; TraverseDirectoryEntry -&gt; TraverseDirectoryTable.
        /// Maximum depth is equal to depth of the table graph - today resources use 3.
        /// </summary>
        /// <param name="tableOffset">Offset of the resource directory table within the resource section</param>
        private void TraverseDirectoryTable(int tableOffset)
        {
            _reader.Offset = tableOffset + DirectoryTable.NumberOfNameEntries;
            int numberOfNameEntries = _reader.ReadInt16();
            int numberOfIDEntries = _reader.ReadInt16();
            int totalEntries = numberOfNameEntries + numberOfIDEntries;
            for (int entryIndex = 0; entryIndex < totalEntries; entryIndex++)
            {
                TraverseDirectoryEntry(tableOffset + DirectoryTable.Size + entryIndex * DirectoryEntry.Size);
            }
        }
        
        /// <summary>
        /// Traverse a single directory entry (name- and ID-based directory entries are processed
        /// the same way as we're not really interested in the entry identifier, just in the
        /// data / table pointers.
        /// </summary>
        /// <param name="entryOffset">Offset of the resource directory entry within the resource section</param>
        private void TraverseDirectoryEntry(int entryOffset)
        {
            _reader.Offset = entryOffset + DirectoryEntry.DataOrSubdirectoryOffset;
            int dataOrSubdirectoryOffset = _reader.ReadInt32();
            if ((dataOrSubdirectoryOffset & EntryOffsetIsSubdirectory) != 0)
            {
                // subdirectory offset
                TraverseDirectoryTable(dataOrSubdirectoryOffset & ~EntryOffsetIsSubdirectory);
            }
            else
            {
                // data entry offset
                _offsetsOfRvasToRelocate.Add(dataOrSubdirectoryOffset + DataEntry.RVA);
            }
        }
    }
    
    /// <summary>
    /// Simple helper for copying the various global values in the PE header.
    /// </summary>
    static class PEHeaderCopier
    {
        /// <summary>
        /// Copy PE headers into a PEHeaderBuilder used by PEBuilder.
        /// </summary>
        /// <param name="peHeaders">Headers to copy</param>
        public static PEHeaderBuilder Copy(PEHeaders peHeaders)
        {
            return new PEHeaderBuilder(
                machine: peHeaders.CoffHeader.Machine,
                sectionAlignment: peHeaders.PEHeader.SectionAlignment,
                fileAlignment: peHeaders.PEHeader.FileAlignment,
                imageBase: peHeaders.PEHeader.ImageBase,
                majorLinkerVersion: peHeaders.PEHeader.MajorLinkerVersion,
                minorLinkerVersion: peHeaders.PEHeader.MinorLinkerVersion,
                majorOperatingSystemVersion: peHeaders.PEHeader.MajorOperatingSystemVersion,
                minorOperatingSystemVersion: peHeaders.PEHeader.MinorOperatingSystemVersion,
                majorImageVersion: peHeaders.PEHeader.MajorImageVersion,
                minorImageVersion: peHeaders.PEHeader.MinorImageVersion,
                majorSubsystemVersion: peHeaders.PEHeader.MajorSubsystemVersion,
                minorSubsystemVersion: peHeaders.PEHeader.MinorSubsystemVersion,
                subsystem: peHeaders.PEHeader.Subsystem,
                dllCharacteristics: peHeaders.PEHeader.DllCharacteristics,
                imageCharacteristics: peHeaders.CoffHeader.Characteristics,
                sizeOfStackReserve: peHeaders.PEHeader.SizeOfStackReserve,
                sizeOfStackCommit: peHeaders.PEHeader.SizeOfStackCommit,
                sizeOfHeapReserve: peHeaders.PEHeader.SizeOfHeapReserve,
                sizeOfHeapCommit: peHeaders.PEHeader.SizeOfHeapCommit);
        }
    }
}
