// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime;
using System.Runtime.InteropServices;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    [McgIntrinsics]
    public static partial class StartupCodeHelpers
    {
        /// <summary>
        /// Initial module array allocation used when adding modules dynamically.
        /// </summary>
        private const int InitialModuleCount = 8;

        /// <summary>
        /// Table of logical modules. Only the first s_moduleCount elements of the array are in use.
        /// </summary>
        private static TypeManagerHandle[] s_modules;

        /// <summary>
        /// Number of valid elements in the logical module table.
        /// </summary>
        private static int s_moduleCount;

        [UnmanagedCallersOnly(EntryPoint = "InitializeModules", CallingConvention = CallingConvention.Cdecl)]
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
            if (s_modules != null)
            {
                for (int i = 0; i < modules.Length; i++)
                {
                    AddModule(modules[i]);
                }
            }
            else
            {
                s_modules = modules;
                s_moduleCount = modules.Length;
            }

            // These two loops look funny but it's important to initialize the global tables before running
            // the first class constructor to prevent them calling into another uninitialized module
            for (int i = 0; i < modules.Length; i++)
            {
                InitializeEagerClassConstructorsForModule(modules[i]);
            }
        }

        /// <summary>
        /// Return the number of registered logical modules; optionally copy them into an array.
        /// </summary>
        /// <param name="outputModules">Array to copy logical modules to, null = only return logical module count</param>
        internal static int GetLoadedModules(TypeManagerHandle[] outputModules)
        {
            if (outputModules != null)
            {
                int copyLimit = (s_moduleCount < outputModules.Length ? s_moduleCount : outputModules.Length);
                for (int copyIndex = 0; copyIndex < copyLimit; copyIndex++)
                {
                    outputModules[copyIndex] = s_modules[copyIndex];
                }
            }
            return s_moduleCount;
        }

        private static void AddModule(TypeManagerHandle newModuleHandle)
        {
            if (s_modules == null || s_moduleCount >= s_modules.Length)
            {
                // Reallocate logical module array
                int newModuleLength = 2 * s_moduleCount;
                if (newModuleLength < InitialModuleCount)
                {
                    newModuleLength = InitialModuleCount;
                }

                TypeManagerHandle[] newModules = new TypeManagerHandle[newModuleLength];
                for (int copyIndex = 0; copyIndex < s_moduleCount; copyIndex++)
                {
                    newModules[copyIndex] = s_modules[copyIndex];
                }
                s_modules = newModules;
            }
            
            s_modules[s_moduleCount] = newModuleHandle;
            s_moduleCount++;
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
                {
                    modules[moduleIndex] = RuntimeImports.RhpCreateTypeManager(osModule, pModuleHeaders[i], pClasslibFunctions, nClasslibFunctions);
                    moduleIndex++;
                }
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

            // Initialize Mrt import address tables
            IntPtr mrtImportSection = RuntimeImports.RhGetModuleSection(typeManager, ReadyToRunSectionType.ImportAddressTables, out length);
            if (mrtImportSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                InitializeImports(mrtImportSection, length);
            }

#if !PROJECTN
            // Initialize statics if any are present
            IntPtr staticsSection = RuntimeImports.RhGetModuleSection(typeManager, ReadyToRunSectionType.GCStaticRegion, out length);
            if (staticsSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                InitializeStatics(staticsSection, length);
            }
#endif

            // Initialize frozen object segment for the module with GC present
            IntPtr frozenObjectSection = RuntimeImports.RhGetModuleSection(typeManager, ReadyToRunSectionType.FrozenObjectRegion, out length);
            if (frozenObjectSection != IntPtr.Zero)
            {
                Debug.Assert(length % IntPtr.Size == 0);
                InitializeModuleFrozenObjectSegment(frozenObjectSection, length);
            }
        }

        private static unsafe void InitializeModuleFrozenObjectSegment(IntPtr segmentStart, int length)
        {
            if (RuntimeImports.RhpRegisterFrozenSegment(segmentStart, (IntPtr)length) == IntPtr.Zero)
            {
                // This should only happen if we ran out of memory.
                RuntimeExceptionHelpers.FailFast("Failed to register frozen object segment for the module.");
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

        [StructLayout(LayoutKind.Sequential)]
        unsafe struct MrtExportsV1
        {
            public int ExportsVersion; // Currently only version 1 is supported
            public int SymbolsCount;
            public int FirstDataItemAsRelativePointer; // Index 1
        }

        [StructLayout(LayoutKind.Sequential)]
        unsafe struct MrtImportsV1
        {
            public int ImportVersion; // Currently only version 1 is supported
            public int ImportCount; // Count of imports
            public MrtExportsV1** ExportTable; // Pointer to pointer to Export table
            public IntPtr FirstImportEntry;
        }

        private static unsafe void InitializeImports(IntPtr importsRegionStart, int length)
        {
            IntPtr importsRegionEnd = (IntPtr)((byte*)importsRegionStart + length);

            for (MrtImportsV1** importTablePtr = (MrtImportsV1**)importsRegionStart; importTablePtr < (MrtImportsV1**)importsRegionEnd; importTablePtr++)
            {
                MrtImportsV1* importTable = *importTablePtr;
                if (importTable->ImportVersion != 1)
                    RuntimeExceptionHelpers.FailFast("Mrt Import table version");

                MrtExportsV1* exportTable = *importTable->ExportTable;
                if (exportTable->ExportsVersion != 1)
                    RuntimeExceptionHelpers.FailFast("Mrt Export table version");

                if (importTable->ImportCount < 0)
                {
                    RuntimeExceptionHelpers.FailFast("Mrt Import Count");
                }

                int* firstExport = &exportTable->FirstDataItemAsRelativePointer;
                IntPtr* firstImport = &importTable->FirstImportEntry;
                for (int import = 0; import < importTable->ImportCount; import++)
                {
                    // Get 1 based ordinal from import table
                    int importOrdinal = (int)firstImport[import];

                    if ((importOrdinal < 1) || (importOrdinal > exportTable->SymbolsCount))
                        RuntimeExceptionHelpers.FailFast("Mrt import ordinal");

                    // Get entry in export table
                    int* exportTableEntry = &firstExport[importOrdinal - 1];

                    // Get pointer from export table
                    int relativeOffsetFromExportTableEntry = *exportTableEntry;
                    byte* actualPointer = ((byte*)exportTableEntry) + relativeOffsetFromExportTableEntry + sizeof(int);

                    // Update import table with imported value
                    firstImport[import] = new IntPtr(actualPointer);
                }
            }
        }

#if !PROJECTN
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
#endif // !PROJECTN
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct TypeManagerSlot
    {
        public TypeManagerHandle TypeManager;
        public int ModuleIndex;
    }
}
