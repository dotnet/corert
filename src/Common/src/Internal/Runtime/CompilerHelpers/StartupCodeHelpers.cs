// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.InteropServices;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    [McgIntrinsics]
    public static partial class StartupCodeHelpers
    {
        public static IntPtr[] OSModules
        {
            get; private set;
        }

        public static TypeManagerHandle[] Modules
        {
            get; private set;
        }

        [NativeCallable(EntryPoint = "InitializeModules", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe void InitializeModules(IntPtr osModule, IntPtr* pModuleHeaders, int count, IntPtr* pClasslibFunctions, int nClasslibFunctions)
        {
            RuntimeImports.RhpRegisterOsModule(osModule);
            TypeManagerHandle[] modules = CreateTypeManagers(osModule, pModuleHeaders, count, pClasslibFunctions, nClasslibFunctions);

            for (int i = 0; i < modules.Length; i++)
            {
                InitializeGlobalTablesForModule(modules[i], i);
            }

            // We are now at a stage where we can use GC statics - publish the list of modules
            // so that the eager constructors can access it.
            Modules = modules;
            OSModules = new IntPtr[] { osModule };

            // These two loops look funny but it's important to initialize the global tables before running
            // the first class constructor to prevent them calling into another uninitialized module
            for (int i = 0; i < modules.Length; i++)
            {
                InitializeEagerClassConstructorsForModule(modules[i]);
            }
        }

        private static unsafe TypeManagerHandle[] CreateTypeManagers(IntPtr osModule, IntPtr* pModuleHeaders, int count, IntPtr* pClasslibFunctions, int nClasslibFunctions)
        {
            // Count the number of modules so we can allocate an array to hold the TypeManager objects.
            // At this stage of startup, complex collection classes will not work.
            int moduleCount = 0;
            for (int i = 0; i < count; i++)
            {
                // The null pointers are sentinel values and padding inserted as side-effect of
                // the section merging. (The global static constructors section used by C++ has 
                // them too.)
                if (pModuleHeaders[i] != IntPtr.Zero)
                    moduleCount++;
            }

            TypeManagerHandle[] modules = new TypeManagerHandle[moduleCount];
            int moduleIndex = 0;
            for (int i = 0; i < count; i++)
            {
                if (pModuleHeaders[i] != IntPtr.Zero)
                    modules[moduleIndex++] = RuntimeImports.RhpCreateTypeManager(osModule, pModuleHeaders[i], pClasslibFunctions, nClasslibFunctions);
            }

            return modules;
        }

        /// <summary>
        /// Each managed module linked into the final binary may have its own global tables for strings,
        /// statics, etc that need initializing. InitializeGlobalTables walks through the modules
        /// and offers each a chance to initialize its global tables.
        /// </summary>
        private static unsafe void InitializeGlobalTablesForModule(TypeManagerHandle typeManager, int moduleIndex)
        {
            // Configure the module indirection cell with the newly created TypeManager. This allows EETypes to find
            // their interface dispatch map tables.
            int length;
            TypeManagerSlot* section = (TypeManagerSlot*)RuntimeImports.RhGetModuleSection(typeManager, ReadyToRunSectionType.TypeManagerIndirection, out length);
            section->TypeManager = typeManager;
            section->ModuleIndex = moduleIndex;

#if CORERT
            // Initialize statics if any are present
            IntPtr staticsSection = RuntimeImports.RhGetModuleSection(typeManager, ReadyToRunSectionType.GCStaticRegion, out length);
            if (staticsSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                InitializeStatics(staticsSection, length);
            }
#endif

            // Initialize frozen object segment with GC present
            IntPtr frozenObjectSection = RuntimeImports.RhGetModuleSection(typeManager, ReadyToRunSectionType.FrozenObjectRegion, out length);
            if (frozenObjectSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                InitializeFrozenObjectSegment(frozenObjectSection, length);
            }
        }

        private static unsafe void InitializeFrozenObjectSegment(IntPtr segmentStart, int length)
        {
            if (!RuntimeImports.RhpRegisterFrozenSegment(segmentStart, length))
            {
                // This should only happen if we ran out of memory.
                RuntimeExceptionHelpers.FailFast("Failed to register frozen object segment.");
            }
        }

        private static unsafe void InitializeEagerClassConstructorsForModule(TypeManagerHandle typeManager)
        {
            int length;

            // Run eager class constructors if any are present
            IntPtr eagerClassConstructorSection = RuntimeImports.RhGetModuleSection(typeManager, ReadyToRunSectionType.EagerCctor, out length);
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

#if CORERT
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
                long blockAddr = (*pBlock).ToInt64();
                if ((blockAddr & GCStaticRegionConstants.Uninitialized) == GCStaticRegionConstants.Uninitialized)
                {
                    object obj = RuntimeImports.RhNewObject(new EETypePtr(new IntPtr(blockAddr & ~GCStaticRegionConstants.Mask)));

                    if ((blockAddr & GCStaticRegionConstants.HasPreInitializedData) == GCStaticRegionConstants.HasPreInitializedData)
                    {
                        // The next pointer is preinitialized data blob that contains preinitialized static GC fields,
                        // which are pointer relocs to GC objects in frozen segment. 
                        // It actually has all GC fields including non-preinitialized fields and we simply copy over the
                        // entire blob to this object, overwriting everything. 
                        IntPtr pPreInitDataAddr = *(pBlock + 1);
                        RuntimeImports.RhBulkMoveWithWriteBarrier(ref obj.GetRawData(), ref *(byte *)pPreInitDataAddr, obj.GetRawDataSize());
                    }

                    *pBlock = RuntimeImports.RhHandleAlloc(obj, GCHandleType.Normal);
                }
            }
        }
#endif // CORERT
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct TypeManagerSlot
    {
        public TypeManagerHandle TypeManager;
        public Int32 ModuleIndex;
    }
}
