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
    private void ReadResourceData()
    {
        byte[] peFileData = _initialFileData;

        uint onewexe;
        IMAGE_DOS_HEADER oldexe = new IMAGE_DOS_HEADER();

        Mu inpfh = new Mu(peFileData);
        inpfh.FilePos = 0;
        oldexe.Read(inpfh);
        if (oldexe.e_magic != IMAGE_DOS_SIGNATURE)
            throw new Exception("Invalid Exe Signature");

        onewexe = (uint)oldexe.e_lfanew;
        if (onewexe == 0)
            throw new Exception("Bad Exe Format");

        IMAGE_NT_HEADERS32 Old = new IMAGE_NT_HEADERS32();

        //
        // Position file to start of NT header and read the image header.
        //

        Mu.MoveFilePos(inpfh, onewexe);
        Old.Read(inpfh);

        //
        // If the file is not an NT image, then return an error.
        //

        if (Old.Signature != IMAGE_NT_SIGNATURE)
        {
            throw new ArgumentException("inpfh not NT image");
        }

        //
        // If the file is not an executable or a dll, then return an error.
        //

        if ((Old.FileHeader.Characteristics & (uint)IMAGE_FILE.EXECUTABLE_IMAGE) == 0 &&
            (Old.FileHeader.Characteristics & (uint)IMAGE_FILE.DLL) == 0)
        {
            throw new ArgumentException("inpfh not executable or dll");
        }

        //
        // Call the proper function dependent on the machine type.
        //

        if (Old.OptionalHeader.Magic == (uint)IMAGE_NT_OPTIONAL_HDR._64_MAGIC)
        {
            PEReadResource<IMAGE_NT_HEADERS64>(inpfh, onewexe);
        }
        else if (Old.OptionalHeader.Magic == (uint)IMAGE_NT_OPTIONAL_HDR._32_MAGIC)
        {
            PEReadResource<IMAGE_NT_HEADERS32>(inpfh, onewexe);
        }
        else
        {
            throw new ArgumentException("Bad exe format");
        }

    }

    void PEReadResource<NT_HEADER_TYPE>(
        Mu inpfh,
        uint cbOldexe
        ) where NT_HEADER_TYPE : unmanaged, I_NT_HEADER_TYPE
    {

        NT_HEADER_TYPE Old = new NT_HEADER_TYPE();         /* original header */

        Mu.MoveFilePos(inpfh, cbOldexe);
        Old.Read(inpfh);

        uint ibObjTab = (uint)(cbOldexe + sizeof(NT_HEADER_TYPE));

        /* Read section table */
        ImageSectionHeaderObject[] pObjtblOld = new ImageSectionHeaderObject[Old.GetFileHeaderNumberOfSections()];
        Mu.MoveFilePos(inpfh, ibObjTab);
        for (uint iSection = 0; iSection < Old.GetFileHeaderNumberOfSections(); iSection++)
        {
            ImageSectionHeaderObject oldSection = new ImageSectionHeaderObject();
            pObjtblOld[iSection] = oldSection;
            oldSection.Data.Read(inpfh);
        }

        Debug.WriteLine("Old section table: {0:x8} bytes",
                 Old.GetFileHeaderNumberOfSections() * sizeof(IMAGE_SECTION_HEADER));

        Mu laidOutImage = new Mu((int)Old.GetOptionalHeaderSizeOfImage());
        foreach (var section in pObjtblOld)
        {
            inpfh.FilePosUnsigned = section.Data.PointerToRawData;
            byte[] sectionData = Mu.Read(inpfh, section.Data.PhysicalAddressOrVirtualSize);

            laidOutImage.FilePosUnsigned = section.Data.VirtualAddress;
            Mu.Write(laidOutImage, sectionData);
        }

        // At this point, the image is basically laid out.
        if (Old.GetDataDirectorySize(IMAGE_DIRECTORY_ENTRY.RESOURCE) > 0)
        {
            // If we have a resource section, read it from laidOutImage
            ReadResources(laidOutImage, Old.GetDataDirectoryVirtualAddress(IMAGE_DIRECTORY_ENTRY.RESOURCE));
        }
    }

    void ReadResources(Mu laidOutImage, uint resourceDirectoryRVA)
    {
        laidOutImage.FilePosUnsigned = resourceDirectoryRVA;

        Mu resourceReader = new Mu(Mu.Read(laidOutImage, laidOutImage.FileSize - laidOutImage.FilePos));

        DoResourceDirectoryRead(resourceReader, 0, ProcessOuterResource);
        return;

        void ProcessOuterResource(object typeName, uint offset, bool isTypeDictionaryEntry)
        {
            if (!isTypeDictionaryEntry)
                throw new ArgumentException();

            DoResourceDirectoryRead(resourceReader, offset, ProcessNameList);
            return;

            void ProcessNameList(object name, uint offsetOfLanguageList, bool isNameListDictionaryEntry)
            {
                if (!isNameListDictionaryEntry)
                    throw new ArgumentException();

                DoResourceDirectoryRead(resourceReader, offsetOfLanguageList, ProcessLanguageList);
                return;

                void ProcessLanguageList(object languageName, uint offsetOfLanguageListEntry, bool isLanguageListEntryIsDictionaryEntry)
                {
                    if (languageName is string)
                        throw new ArgumentException();

                    if (isLanguageListEntryIsDictionaryEntry)
                        throw new ArgumentException();

                    IMAGE_RESOURCE_DATA_ENTRY resourceData = new IMAGE_RESOURCE_DATA_ENTRY();
                    resourceReader.FilePosUnsigned = offsetOfLanguageListEntry;
                    resourceData.Read(resourceReader);

                    // The actual resource data offset is relative to the start address of the file
                    laidOutImage.FilePosUnsigned = resourceData.OffsetToData;
                    byte[] data = Mu.Read(laidOutImage, resourceData.Size);

                    AddResource(typeName, name, (ushort)languageName, data);
                }
            }
        }
    }

    void DoResourceDirectoryRead(Mu resourceReaderExternal, uint startOffset, Action<object, uint, bool> entry)
    {
        // Create a copy of the Mu, so that we don't allow the delegate to affect its state
        Mu resourceReader = new Mu(resourceReaderExternal);
        IMAGE_RESOURCE_DIRECTORY directory = new IMAGE_RESOURCE_DIRECTORY();
        resourceReader.FilePosUnsigned = startOffset;
        directory.Read(resourceReader);
        for (uint i = 0; i < directory.NumberOfNamedEntries + directory.NumberOfIdEntries; i++)
        {
            IMAGE_RESOURCE_DIRECTORY_ENTRY directoryEntry = new IMAGE_RESOURCE_DIRECTORY_ENTRY();
            directoryEntry.Read(resourceReader);

            object name;
            if ((directoryEntry.Name & 0x80000000) != 0)
            {
                int oldPosition = resourceReader.FilePos;

                // This is a named entry, read the string
                uint nameOffset = directoryEntry.Name & ~0x80000000;

                resourceReader.FilePosUnsigned = nameOffset;
                ushort stringLen = Mu.ReadUInt16(resourceReader);
                char[] newStringData = new char[stringLen];
                for (int iStr = 0; iStr < stringLen; iStr++)
                {
                    newStringData[iStr] = (char)Mu.ReadUInt16(resourceReader);
                }
                name = new string(newStringData);
                // And then reset back to read more things.
                resourceReader.FilePos = oldPosition;
            }
            else
            {
                name = checked((ushort)directoryEntry.Name);
            }

            uint offset = directoryEntry.OffsetToData;
            bool isDirectory = false;
            if ((offset & 0x80000000) != 0)
            {
                offset &= ~0x80000000;
                isDirectory = true;
            }

            entry(name, offset, isDirectory);
        }
    }
}
