// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace ILCompiler.Win32Resources
{
    public unsafe partial class ResourceData
    {
        private interface I_NT_HEADER_TYPE
        {
            void Read(Mu f);
            void Write(Mu f);
            void SetOptionalHeaderSizeOfImage(uint size);
            uint GetDataDirectorySize(IMAGE_DIRECTORY_ENTRY entry); // Should work something like this.OptionalHeader.DataDirectory[entry].Size
            void SetDataDirectorySize(IMAGE_DIRECTORY_ENTRY entry, uint newSize);  // Should work something like this.OptionalHeader.DataDirectory[entry].Size
            uint GetDataDirectoryVirtualAddress(IMAGE_DIRECTORY_ENTRY entry);  // Should work something like this.OptionalHeader.DataDirectory[entry].VirtualAddress
            void SetDataDirectoryVirtualAddress(IMAGE_DIRECTORY_ENTRY entry, uint newAddress);  // Should work something like this.OptionalHeader.DataDirectory[entry].VirtualAddress
            uint GetOptionalHeaderSizeOfHeaders(); // Should work something like this OptionalHeader.SizeOfHeaders
            void SetOptionalHeaderSizeOfHeaders(uint size); // Should work something like this OptionalHeader.SizeOfHeaders
            uint GetOptionalHeaderFileAlignment(); // OptionalHeader.FileAlignment
            uint GetOptionalHeaderSectionAlignment(); // OptionalHeader.SectionAlignment
            uint GetOptionalHeaderSizeOfImage(); // OptionalHeader.SizeOfImage;
            uint GetOptionalHeaderSizeOfInitializedData(); // OptionalHeader.SizeOfInitializedData
            void SetOptionalHeaderSizeOfInitializedData(uint size); // OptionalHeader.SizeOfInitializedData
            uint GetFileHeaderNumberOfSections(); // Like ->FileHeader.NumberOfSections
            uint GetFileHeaderPointerToSymbolTable(); // FileHeader.PointerToSymbolTable
            void SetFileHeaderPointerToSymbolTable(uint pointer); // FileHeader.PointerToSymbolTable
            void IncrementFileHeaderNumberOfSections(); // Like ->FileHeader.NumberOfSections++
            void DecrementFileHeaderNumberOfSections(); // Like ->FileHeader.NumberOfSections--
            bool Is64Bit();
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_OPTIONAL_HEADER32
        {
            public ushort Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public uint SizeOfCode;
            public uint SizeOfInitializedData;
            public uint SizeOfUninitializedData;
            public uint AddressOfEntryPoint;
            public uint BaseOfCode;
            public uint BaseOfData;

            //
            // NT additional fields.
            //

            public uint ImageBase;
            public uint SectionAlignment;
            public uint FileAlignment;
            public ushort MajorOperatingSystemVersion;
            public ushort MinorOperatingSystemVersion;
            public ushort MajorImageVersion;
            public ushort MinorImageVersion;
            public ushort MajorSubsystemVersion;
            public ushort MinorSubsystemVersion;
            public uint Win32VersionValue;
            public uint SizeOfImage;
            public uint SizeOfHeaders;
            public uint CheckSum;
            public ushort Subsystem;
            public ushort DllCharacteristics;
            public uint SizeOfStackReserve;
            public uint SizeOfStackCommit;
            public uint SizeOfHeapReserve;
            public uint SizeOfHeapCommit;
            public uint LoaderFlags;
            public uint NumberOfRvaAndSizes;
            public unsafe fixed uint DataDirectory[32];
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_OPTIONAL_HEADER64
        {
            public ushort Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public uint SizeOfCode;
            public uint SizeOfInitializedData;
            public uint SizeOfUninitializedData;
            public uint AddressOfEntryPoint;
            public uint BaseOfCode;
            public ulong ImageBase;
            public uint SectionAlignment;
            public uint FileAlignment;
            public ushort MajorOperatingSystemVersion;
            public ushort MinorOperatingSystemVersion;
            public ushort MajorImageVersion;
            public ushort MinorImageVersion;
            public ushort MajorSubsystemVersion;
            public ushort MinorSubsystemVersion;
            public uint Win32VersionValue;
            public uint SizeOfImage;
            public uint SizeOfHeaders;
            public uint CheckSum;
            public ushort Subsystem;
            public ushort DllCharacteristics;
            public ulong SizeOfStackReserve;
            public ulong SizeOfStackCommit;
            public ulong SizeOfHeapReserve;
            public ulong SizeOfHeapCommit;
            public uint LoaderFlags;
            public uint NumberOfRvaAndSizes;
            public unsafe fixed uint DataDirectory[32];
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_NT_HEADERS64 : I_NT_HEADER_TYPE
        {
            public uint Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER64 OptionalHeader;

            public bool Is64Bit() => true;
            public unsafe void Read(Mu f)
            {
                byte[] bytes = Mu.Read(f, sizeof(IMAGE_NT_HEADERS64));
                fixed (byte* b = &bytes[0])
                {
                    this = *(IMAGE_NT_HEADERS64*)b;
                }
            }
            public unsafe void Write(Mu f)
            {
                byte[] bytes = new byte[sizeof(IMAGE_NT_HEADERS64)];
                fixed (byte* b = &bytes[0])
                {
                    *(IMAGE_NT_HEADERS64*)b = this;
                }

                Mu.Write(f, bytes);
            }
            public void DecrementFileHeaderNumberOfSections()
            {
                FileHeader.NumberOfSections--;
            }
            public void IncrementFileHeaderNumberOfSections()
            {
                FileHeader.NumberOfSections++;
            }

            public unsafe uint GetDataDirectorySize(IMAGE_DIRECTORY_ENTRY entry) => OptionalHeader.DataDirectory[(((int)entry) * 2) + 1];
            public unsafe uint GetDataDirectoryVirtualAddress(IMAGE_DIRECTORY_ENTRY entry) => OptionalHeader.DataDirectory[(((int)entry) * 2)];
            public uint GetFileHeaderNumberOfSections() => FileHeader.NumberOfSections;
            public uint GetFileHeaderPointerToSymbolTable() => FileHeader.PointerToSymbolTable;
            public uint GetOptionalHeaderFileAlignment() => OptionalHeader.FileAlignment;
            public uint GetOptionalHeaderSectionAlignment() => OptionalHeader.SectionAlignment;
            public uint GetOptionalHeaderSizeOfHeaders() => OptionalHeader.SizeOfHeaders;
            public uint GetOptionalHeaderSizeOfImage() => OptionalHeader.SizeOfImage;
            public uint GetOptionalHeaderSizeOfInitializedData() => OptionalHeader.SizeOfInitializedData;
            public unsafe void SetDataDirectorySize(IMAGE_DIRECTORY_ENTRY entry, uint newSize)
            { OptionalHeader.DataDirectory[(((int)entry) * 2) + 1] = newSize; }
            public unsafe void SetDataDirectoryVirtualAddress(IMAGE_DIRECTORY_ENTRY entry, uint newAddress)
            { OptionalHeader.DataDirectory[(((int)entry) * 2)] = newAddress; }
            public void SetFileHeaderPointerToSymbolTable(uint pointer)
            { FileHeader.PointerToSymbolTable = pointer; }
            public void SetOptionalHeaderSizeOfHeaders(uint size)
            { OptionalHeader.SizeOfHeaders = size; }
            public void SetOptionalHeaderSizeOfImage(uint size)
            { OptionalHeader.SizeOfImage = size; }
            public void SetOptionalHeaderSizeOfInitializedData(uint size)
            { OptionalHeader.SizeOfInitializedData = size; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_NT_HEADERS32 : I_NT_HEADER_TYPE
        {
            public uint Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER32 OptionalHeader;

            public bool Is64Bit() => false;
            public unsafe void Read(Mu f)
            {
                byte[] bytes = Mu.Read(f, sizeof(IMAGE_NT_HEADERS32));
                fixed (byte* b = &bytes[0])
                {
                    this = *(IMAGE_NT_HEADERS32*)b;
                }
            }
            public unsafe void Write(Mu f)
            {
                byte[] bytes = new byte[sizeof(IMAGE_NT_HEADERS32)];
                fixed (byte* b = &bytes[0])
                {
                    *(IMAGE_NT_HEADERS32*)b = this;
                }

                Mu.Write(f, bytes);
            }
            public void DecrementFileHeaderNumberOfSections()
            {
                FileHeader.NumberOfSections--;
            }
            public void IncrementFileHeaderNumberOfSections()
            {
                FileHeader.NumberOfSections++;
            }

            public uint GetDataDirectorySize(IMAGE_DIRECTORY_ENTRY entry) => OptionalHeader.DataDirectory[(((int)entry) * 2) + 1];
            public uint GetDataDirectoryVirtualAddress(IMAGE_DIRECTORY_ENTRY entry) => OptionalHeader.DataDirectory[(((int)entry) * 2)];
            public uint GetFileHeaderNumberOfSections() => FileHeader.NumberOfSections;
            public uint GetFileHeaderPointerToSymbolTable() => FileHeader.PointerToSymbolTable;
            public uint GetOptionalHeaderFileAlignment() => OptionalHeader.FileAlignment;
            public uint GetOptionalHeaderSectionAlignment() => OptionalHeader.SectionAlignment;
            public uint GetOptionalHeaderSizeOfHeaders() => OptionalHeader.SizeOfHeaders;
            public uint GetOptionalHeaderSizeOfImage() => OptionalHeader.SizeOfImage;
            public uint GetOptionalHeaderSizeOfInitializedData() => OptionalHeader.SizeOfInitializedData;
            public void SetDataDirectorySize(IMAGE_DIRECTORY_ENTRY entry, uint newSize)
            { OptionalHeader.DataDirectory[(((int)entry) * 2) + 1] = newSize; }
            public void SetDataDirectoryVirtualAddress(IMAGE_DIRECTORY_ENTRY entry, uint newAddress)
            { OptionalHeader.DataDirectory[(((int)entry) * 2)] = newAddress; }
            public void SetFileHeaderPointerToSymbolTable(uint pointer)
            { FileHeader.PointerToSymbolTable = pointer; }
            public void SetOptionalHeaderSizeOfHeaders(uint size)
            { OptionalHeader.SizeOfHeaders = size; }
            public void SetOptionalHeaderSizeOfImage(uint size)
            { OptionalHeader.SizeOfImage = size; }
            public void SetOptionalHeaderSizeOfInitializedData(uint size)
            { OptionalHeader.SizeOfInitializedData = size; }
        }
    }
}
