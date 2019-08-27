// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


public unsafe partial class ResourceData
{
    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGE_SECTION_HEADER
    {
        public unsafe void Read(Mu f)
        {
            byte[] bytes = Mu.Read(f, sizeof(IMAGE_SECTION_HEADER));
            fixed (byte* b = &bytes[0])
            {
                this = *(IMAGE_SECTION_HEADER*)b;
            }
        }

        public byte Name0;
        public byte Name1;
        public byte Name2;
        public byte Name3;
        public byte Name4;
        public byte Name5;
        public byte Name6;
        public byte Name7;
        public uint PhysicalAddressOrVirtualSize;
        public uint VirtualAddress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLinenumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public uint Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGE_RESOURCE_DIRECTORY
    {
        public unsafe void Read(Mu f)
        {
            byte[] bytes = Mu.Read(f, sizeof(IMAGE_RESOURCE_DIRECTORY));
            fixed (byte* b = &bytes[0])
            {
                this = *(IMAGE_RESOURCE_DIRECTORY*)b;
            }
        }

        public uint Characteristics;
        public uint TimeDateStamp;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ushort NumberOfNamedEntries;
        public ushort NumberOfIdEntries;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGE_RESOURCE_DIRECTORY_ENTRY
    {
        public unsafe void Read(Mu f)
        {
            byte[] bytes = Mu.Read(f, sizeof(IMAGE_RESOURCE_DIRECTORY_ENTRY));
            fixed (byte* b = &bytes[0])
            {
                this = *(IMAGE_RESOURCE_DIRECTORY_ENTRY*)b;
            }
        }

        public uint Name;
        public uint OffsetToData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGE_RESOURCE_DATA_ENTRY
    {
        public unsafe void Read(Mu f)
        {
            byte[] bytes = Mu.Read(f, sizeof(IMAGE_RESOURCE_DATA_ENTRY));
            fixed (byte* b = &bytes[0])
            {
                this = *(IMAGE_RESOURCE_DATA_ENTRY*)b;
            }
        }

        public uint OffsetToData;
        public uint Size;
        uint CodePage;
        uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IMAGE_FILE_HEADER
    {
        public ushort Machine;
        public ushort NumberOfSections;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public ushort Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IMAGE_DOS_HEADER
    {
        public unsafe void Read(Mu f)
        {
            byte[] bytes = Mu.Read(f, sizeof(IMAGE_DOS_HEADER));
            fixed (byte* b = &bytes[0])
            {
                this = *(IMAGE_DOS_HEADER*)b;
            }
        }

        public ushort e_magic;                     // Magic number
        ushort e_cblp;                      // Bytes on last page of file
        ushort e_cp;                        // Pages in file
        ushort e_crlc;                      // Relocations
        ushort e_cparhdr;                   // Size of header in paragraphs
        ushort e_minalloc;                  // Minimum extra paragraphs needed
        ushort e_maxalloc;                  // Maximum extra paragraphs needed
        ushort e_ss;                        // Initial (relative) SS value
        ushort e_sp;                        // Initial SP value
        ushort e_csum;                      // Checksum
        ushort e_ip;                        // Initial IP value
        ushort e_cs;                        // Initial (relative) CS value
        ushort e_lfarlc;                    // File address of relocation table
        ushort e_ovno;                      // Overlay number
        unsafe fixed ushort e_res[4];                    // Reserved words
        ushort e_oemid;                     // OEM identifier (for e_oeminfo)
        ushort e_oeminfo;                   // OEM information; e_oemid specific
        unsafe fixed ushort e_res2[10];                  // Reserved words
        public int e_lfanew;                    // File address of new exe header
    }

    [StructLayout(LayoutKind.Sequential)]
    private class ImageSectionHeaderObject
    {
        public IMAGE_SECTION_HEADER Data;
    }

}
