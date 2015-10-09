// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Reflection
{
    [FlagsAttribute()]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum AssemblyNameFlags
    {
        None = 0x0000,
        // Flag used to indicate that an assembly ref contains the full public key, not the compressed token.
        // Must match afPublicKey in CorHdr.h.
        PublicKey = 0x0001,
        //ProcArchMask              = 0x00F0,     // Bits describing the processor architecture
        // Accessible via AssemblyName.ProcessorArchitecture
        //EnableJITcompileOptimizer = 0x4000, 
        //EnableJITcompileTracking  = 0x8000, 
        Retargetable = 0x0100,
        //ContentType             = 0x0E00, // Bits describing the ContentType are accessible via AssemblyName.ContentType
    }
}
