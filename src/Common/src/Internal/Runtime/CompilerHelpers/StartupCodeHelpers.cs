// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    [McgIntrinsics]
    internal static partial class StartupCodeHelpers
    {
        public static IntPtr[] Modules
        {
            get; private set;
        }

        [NativeCallable(EntryPoint = "InitializeModules", CallingConvention = CallingConvention.Cdecl)]
        internal static void InitializeModules(IntPtr moduleHeaders, int count)
        {
            IntPtr[] modules = CreateModuleManagers(moduleHeaders, count);

            for (int i = 0; i < modules.Length; i++)
            {
                InitializeGlobalTablesForModule(modules[i]);
            }

            // We are now at a stage where we can use GC statics - publish the list of modules
            // so that the eager constructors can access it.
            Modules = modules;

            // These two loops look funny but it's important to initialize the global tables before running
            // the first class constructor to prevent them calling into another uninitialized module
            for (int i = 0; i < modules.Length; i++)
            {
                InitializeEagerClassConstructorsForModule(modules[i]);
            }
        }

        private static unsafe IntPtr[] CreateModuleManagers(IntPtr moduleHeaders, int count)
        {
            // Count the number of modules so we can allocate an array to hold the ModuleManager objects.
            // At this stage of startup, complex collection classes will not work.
            int moduleCount = 0;
            for (int i = 0; i < count; i++)
            {
                // The null pointers are sentinel values and padding inserted as side-effect of
                // the section merging. (The global static constructors section used by C++ has 
                // them too.)
                if (((IntPtr *)moduleHeaders)[i] != IntPtr.Zero)
                    moduleCount++;
            }

            IntPtr[] modules = new IntPtr[moduleCount];
            int moduleIndex = 0;
            for (int i = 0; i < count; i++)
            {
                if (((IntPtr *)moduleHeaders)[i] != IntPtr.Zero)
                    modules[moduleIndex++] = CreateModuleManager(((IntPtr *)moduleHeaders)[i]);
            }

            return modules;
        }

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
            IntPtr* section = (IntPtr*)GetModuleSection(moduleManager, ReadyToRunSectionType.ModuleManagerIndirection, out length);
            *section = moduleManager;

            // Initialize statics if any are present
            IntPtr staticsSection = GetModuleSection(moduleManager, ReadyToRunSectionType.GCStaticRegion, out length);
            if (staticsSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                InitializeStatics(staticsSection, length);
            }

            // Initialize frozen object segment with GC present
            IntPtr frozenObjectSection = GetModuleSection(moduleManager, ReadyToRunSectionType.FrozenObjectRegion, out length);
            if (frozenObjectSection != IntPtr.Zero)
            {
                InitializeFrozenObjectSegment(frozenObjectSection, length);
            }
        }

        private static unsafe void InitializeFrozenObjectSegment(IntPtr segmentStart, int length)
        {
            if (!RuntimeImports.RhpRegisterFrozenSegment(segmentStart, length))
            {
                // This should only happen if we ran out of memory.
                Environment.FailFast("Failed to register frozen object segment.");
            }
        }

        private static unsafe void InitializeEagerClassConstructorsForModule(IntPtr moduleManager)
        {
            int length;

            // Run eager class constructors if any are present
            IntPtr eagerClassConstructorSection = GetModuleSection(moduleManager, ReadyToRunSectionType.EagerCctor, out length);
            if (eagerClassConstructorSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                RunEagerClassConstructors(eagerClassConstructorSection, length);
            }
        }
        
        private static void Call(System.IntPtr pfn)
        {
        }

        private static unsafe void RunEagerClassConstructors(IntPtr cctorTableStart, int length)
        {
            IntPtr cctorTableEnd = (IntPtr)((byte*)cctorTableStart + length);

            for (IntPtr* tab = (IntPtr*)cctorTableStart; tab < (IntPtr*)cctorTableEnd; tab++)
            {
                Call(*tab);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int CStrLen(byte* str)
        {
            int len = 0;
            for (; str[len] != 0; len++) { }
            return len;
        }

        [RuntimeImport(".", "RhpGetModuleSection")]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern IntPtr GetModuleSection(IntPtr module, ReadyToRunSectionType section, out int length);

        [RuntimeImport(".", "RhpCreateModuleManager")]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static unsafe extern IntPtr CreateModuleManager(IntPtr moduleHeader);
    }
}
