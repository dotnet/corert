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

namespace ILCompiler.Win32Resources
{
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
            private uint CodePage;
            private uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_FILE_HEADER
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
        private struct IMAGE_DOS_HEADER
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
            private ushort e_cblp;                      // Bytes on last page of file
            private ushort e_cp;                        // Pages in file
            private ushort e_crlc;                      // Relocations
            private ushort e_cparhdr;                   // Size of header in paragraphs
            private ushort e_minalloc;                  // Minimum extra paragraphs needed
            private ushort e_maxalloc;                  // Maximum extra paragraphs needed
            private ushort e_ss;                        // Initial (relative) SS value
            private ushort e_sp;                        // Initial SP value
            private ushort e_csum;                      // Checksum
            private ushort e_ip;                        // Initial IP value
            private ushort e_cs;                        // Initial (relative) CS value
            private ushort e_lfarlc;                    // File address of relocation table
            private ushort e_ovno;                      // Overlay number
            private unsafe fixed ushort e_res[4];                    // Reserved words
            private ushort e_oemid;                     // OEM identifier (for e_oeminfo)
            private ushort e_oeminfo;                   // OEM information; e_oemid specific
            private unsafe fixed ushort e_res2[10];                  // Reserved words
            public int e_lfanew;                    // File address of new exe header
        }

        [StructLayout(LayoutKind.Sequential)]
        private class ImageSectionHeaderObject
        {
            public IMAGE_SECTION_HEADER Data;
        }
    }
}
