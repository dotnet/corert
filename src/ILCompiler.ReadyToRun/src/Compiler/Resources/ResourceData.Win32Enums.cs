// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public unsafe partial class ResourceData
{
    private enum IMAGE_DIRECTORY_ENTRY
    {
        EXPORT = 0,
        IMPORT = 1,
        RESOURCE = 2,
        EXCEPTION = 3,
        SECURITY = 4,
        BASERELOC = 5,
        DEBUG = 6,
        ARCHITECTURE = 7,
        GLOBALPTR = 8,
        TLS = 9,
        LOAD_CONFIG = 10,
        BOUND_IMPORT = 11,
        IAT = 12,
        DELAY_IMPORT = 13,
        COM_DESCRIPTOR = 14,
        NUMBEROF_DIRECTORY_ENTRIES = 16
    }

    private enum IMAGE_SCN : uint
    {
        MEM_READ = 0x40000000,  // Section is readable.
        CNT_INITIALIZED_DATA = 0x00000040,  // Section contains initialized data.
    }

    private const uint IMAGE_NT_SIGNATURE = 0x00004550;
    private const ushort IMAGE_DOS_SIGNATURE = 0x5A4D;

    enum IMAGE_FILE : uint
    {
        EXECUTABLE_IMAGE = 0x0002, // File is executable  (i.e. no unresolved external references).
        DLL = 0x2000  // File is a DLL.
    }

    enum IMAGE_NT_OPTIONAL_HDR : uint
    {
        _32_MAGIC = 0x10b,
        _64_MAGIC = 0x20b,
    }
}
