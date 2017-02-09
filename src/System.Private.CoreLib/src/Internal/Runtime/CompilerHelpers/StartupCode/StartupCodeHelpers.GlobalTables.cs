// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Internal.NativeFormat;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    public partial class StartupCodeHelpers
    {
        /// <summary>
        /// Each managed module linked into the final binary may have its own global tables for strings,
        /// statics, etc that need initializing. InitializeGlobalTables walks through the modules
        /// and offers each a chance to initialize its global tables.
        /// </summary>
        private static unsafe void InitializeGlobalTablesForModule(IntPtr moduleManager)
        {
            // Configure the module indirection cell with the newly created ModuleManager. This allows EETypes to find
            // their interface dispatch map tables.
            int length;
            IntPtr* section = (IntPtr*)RuntimeImports.GetModuleSection(moduleManager, (int)ReadyToRunSectionType.ModuleManagerIndirection, out length);
            *section = moduleManager;

            // Initialize statics if any are present
            IntPtr staticsSection = RuntimeImports.GetModuleSection(moduleManager, (int)ReadyToRunSectionType.GCStaticRegion, out length);
            if (staticsSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                InitializeStatics(staticsSection, length);
            }

            // Initialize frozen object segment with GC present
            IntPtr frozenObjectSection = RuntimeImports.GetModuleSection(moduleManager, (int)ReadyToRunSectionType.FrozenObjectRegion, out length);
            if (frozenObjectSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                InitializeFrozenObjectSegment(frozenObjectSection, length);
            }
        }

        private static unsafe void InitializeStatics(IntPtr gcStaticRegionStart, int length)
        {
            IntPtr gcStaticRegionEnd = (IntPtr)((byte*)gcStaticRegionStart + length);
            for (IntPtr* block = (IntPtr*)gcStaticRegionStart; block < (IntPtr*)gcStaticRegionEnd; block++)
            {
                // Gc Static regions can be shared by modules linked together during compilation. To ensure each
                // is initialized once, the static region pointer is stored with lowest bit set in the image.
                // The first time we initialize the static region its pointer is replaced with an object reference
                // whose lowest bit is no longer set.
                IntPtr* pBlock = (IntPtr*)*block;
                if (((*pBlock).ToInt64() & 0x1L) == 1)
                {
                    object obj = RuntimeImports.RhNewObject(new EETypePtr(new IntPtr((*pBlock).ToInt64() & ~0x1L)));
                    *pBlock = RuntimeImports.RhHandleAlloc(obj, GCHandleType.Normal);
                }
            }
        }
    }
}
