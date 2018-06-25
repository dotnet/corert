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

    internal struct ReadyToRunHeaderConstants
    {
        public const uint Signature = 0x00525452; // 'RTR'

        public const ushort CurrentMajorVersion = 2;
        public const ushort CurrentMinorVersion = 1;
    }

#pragma warning disable 0169
    internal struct ReadyToRunHeader
    {
        private uint Signature;      // ReadyToRunHeaderConstants.Signature
        private ushort MajorVersion;
        private ushort MinorVersion;

        private uint Flags;

        private ushort NumberOfSections;
        private byte EntrySize;
        private byte EntryType;

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
    public enum ReadyToRunSectionType
    {
        StringTable = 200, // Unused
        GCStaticRegion = 201,
        ThreadStaticRegion = 202,
        InterfaceDispatchTable = 203,
        TypeManagerIndirection = 204,
        EagerCctor = 205,
        FrozenObjectRegion = 206,
        GCStaticDesc = 207,
        ThreadStaticOffsetRegion = 208,
        ThreadStaticGCDescRegion = 209,
        ThreadStaticIndex = 210,
        LoopHijackFlag = 211,
        ImportAddressTables = 212,

        // Sections 300 - 399 are reserved for RhFindBlob backwards compatibility
        ReadonlyBlobRegionStart = 300,
        ReadonlyBlobRegionEnd = 399,
    }

    [Flags]
    internal enum ModuleInfoFlags : int
    {
        HasEndPointer = 0x1,
    }
}
