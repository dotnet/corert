// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;

namespace Internal.Runtime
{
    //
    // Please keep the data structures in this file in sync with the native version at
    //  src/Native/Runtime/inc/ModuleHeaders.h
    //
    
    struct ReadyToRunHeaderConstants
    {
        public const uint Signature = 0x00525452; // 'RTR'

        public const ushort CurrentMajorVersion = 2;
        public const ushort CurrentMinorVersion = 1;
    }

#pragma warning disable 0169
    struct ReadyToRunHeader
    {
        UInt32 Signature;      // ReadyToRunHeaderConstants.Signature
        UInt16 MajorVersion;
        UInt16 MinorVersion;

        UInt32 Flags;

        UInt16 NumberOfSections;
        Byte EntrySize;
        Byte EntryType;

        // Array of sections follows.
    };
#pragma warning restore 0169

    //
    // ReadyToRunSectionType IDs are used by the runtime to look up specific global data sections
    // from each module linked into the final binary. New sections should be added at the bottom
    // of the enum and deprecated sections should not be removed to preserve ID stability.
    //
    // Eventually this will be reconciled with ReadyToRunSectionType from 
    // https://github.com/dotnet/coreclr/blob/master/src/inc/readytorun.h
    //
    enum ReadyToRunSectionType
    {
        StringTable                 = 200, // Unused
        GCStaticRegion              = 201,
        ThreadStaticRegion          = 202,
        InterfaceDispatchTable      = 203,
        ModuleManagerIndirection    = 204,
        EagerCctor                  = 205,
        FrozenObjectRegion          = 206,

        // Sections 300 - 399 are reserved for RhFindBlob backwards compatibility
        ReadonlyBlobRegionStart     = 300,
        ReadonlyBlobRegionEnd       = 399,
    }

    [Flags]
    enum ModuleInfoFlags : int
    {
        HasEndPointer               = 0x1,
    }
}
