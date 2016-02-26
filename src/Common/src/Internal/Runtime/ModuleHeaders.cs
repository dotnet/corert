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
    
    //
    // ModuleHeaderSection IDs are used by the runtime to look up specific global data sections
    // from each module linked into the final binary. New sections should be added at the bottom
    // of the enum and deprecated sections should not be removed to preserve ID stability.
    //
    // Eventually this will be reconciled with ReadyToRunSectionType from 
    // https://github.com/dotnet/coreclr/blob/master/src/inc/readytorun.h
    //
    enum ModuleHeaderSection
    {
        StringTable                 = 200,
        GCStaticRegion              = 201,
        ThreadStaticRegion          = 202,
        InterfaceDispatchTable      = 203,
        ModuleIndirectionCell       = 204,
        EagerCctor                  = 205,
    }

    [Flags]
    enum ModuleInfoFlags : int
    {
        HasEndPointer               = 0x1,
    }
}
