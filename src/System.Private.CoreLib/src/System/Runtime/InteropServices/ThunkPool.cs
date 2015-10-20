// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//
// This is an implementation of a general purpose thunk pool manager. Each thunk consists of:
//      1- A thunk stub, typically consisting of a lea + jmp instructions (slightly different 
//         on ARM, but semantically equivalent)
//      2- A thunk common stub: the implementation of the common stub depends on
//         the usage scenario of the thunk
//      3- Thunk data: each thunk has two pointer-sized data values that can be stored.
//         The first data value is called the thunk's 'context', and the second value is
//         the thunk's jump target typically.
//
// Thunks are allocated by mapping a thunks template into memory. The template contains 2 sections,
// each section is a page-long (4096 bytes):
//      1- The first section has RX permissions, and contains the thunk stubs (lea's + jmp's), 
//         and the thunk common stubs. 
//      2- The second section has RW permissions and contains the thunks data (context + target).
//         The last pointer-sized block in this section is special: it stores the address of
//         the common stub that each thunk stub will jump to (the jump instruction in each thunk
//         jumps to the address stored in that block). Therefore, whenever a new thunks template
//         gets mapped into memory, the value of that last pointer cell in the data section is updated
//         to the common stub address passed in by the caller
//

namespace System.Runtime.InteropServices
{
    using System.Runtime.CompilerServices;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Internal.Runtime.Augments;

    public static class ThunkPool
    {
        private static class AsmCode
        {
            private const MethodImplOptions InternalCall = (MethodImplOptions)0x1000;

            [MethodImplAttribute(InternalCall)]
            [RuntimeImport("*", "Native_GetThunksBase")]
            public static extern IntPtr GetThunksBase();

            [MethodImplAttribute(InternalCall)]
            [RuntimeImport("*", "Native_GetNumThunksPerMapping")]
            public static extern int GetNumThunksPerMapping();

            [MethodImplAttribute(InternalCall)]
            [RuntimeImport("*", "Native_GetThunkSize")]
            public static extern int GetThunkSize();
        }

        private class ThunksTemplateMap
        {
            private IntPtr _thunkMapAddress;
            private int _numThunksUsed;
            private bool[] _usedFlags;

            internal ThunksTemplateMap(IntPtr thunkMapAddress)
            {
                _thunkMapAddress = thunkMapAddress;
                _usedFlags = new bool[AsmCode.GetNumThunksPerMapping()];
                _numThunksUsed = 0;
            }

            // Only safe to call under the ThunkPool's lock
            internal void FreeThunk(IntPtr thunkAddressToFree)
            {
                Debug.Assert(AsmCode.GetNumThunksPerMapping() == _usedFlags.Length);

                long thunkToFree = (long)thunkAddressToFree;
                long thunkMapAddress = (long)_thunkMapAddress;

                if (thunkToFree >= thunkMapAddress &&
                    thunkToFree < thunkMapAddress + AsmCode.GetNumThunksPerMapping() * AsmCode.GetThunkSize())
                {
                    Debug.Assert((thunkToFree - thunkMapAddress) % AsmCode.GetThunkSize() == 0);
                    int thunkIndex = (int)(thunkToFree - thunkMapAddress) / AsmCode.GetThunkSize();
                    Debug.Assert(thunkIndex >= 0 && thunkIndex < AsmCode.GetNumThunksPerMapping());
                    _usedFlags[thunkIndex] = false;
                    _numThunksUsed--;
                }
            }

            /// <summary>
            /// Get the thunk index of a thunk. Only safe to call under the ThunkPool's lock
            /// </summary>
            /// <param name="thunkAddress"></param>
            /// <param name="thunkIndex"></param>
            /// <returns>true if the thunk index was set</returns>
            internal bool TryGetThunkIndex(IntPtr thunkAddress, ref int thunkIndex)
            {
                Debug.Assert(AsmCode.GetNumThunksPerMapping() == _usedFlags.Length);

                long thunkToQuery = (long)thunkAddress;
                long thunkMapAddress = (long)_thunkMapAddress;
                if (thunkToQuery >= thunkMapAddress &&
                    thunkToQuery < thunkMapAddress + AsmCode.GetNumThunksPerMapping() * AsmCode.GetThunkSize())
                {
                    if ((thunkToQuery - thunkMapAddress) % AsmCode.GetThunkSize() != 0)
                    {
                        return false;
                    }
                    thunkIndex = (int)(thunkToQuery - thunkMapAddress) / AsmCode.GetThunkSize();
                    Debug.Assert(thunkIndex >= 0 && thunkIndex < AsmCode.GetNumThunksPerMapping());

                    // If thunk isn't in use, fail
                    if (!_usedFlags[thunkIndex])
                    {
                        // No other template block will have data
                        return false;
                    }

                    return true;
                }

                // Another template may have this thunk
                return false;
            }

            // Only safe to call under the ThunkPool's lock
            internal IntPtr GetNextThunk()
            {
                Debug.Assert(AsmCode.GetNumThunksPerMapping() == _usedFlags.Length);

                if (_numThunksUsed == AsmCode.GetNumThunksPerMapping())
                    return IntPtr.Zero;

                for (int i = 0; i < _usedFlags.Length; i++)
                {
                    if (!_usedFlags[i])
                    {
                        _usedFlags[i] = true;
                        _numThunksUsed++;
                        return (IntPtr)(_thunkMapAddress + (i * AsmCode.GetThunkSize()));
                    }
                }

                // We should not reach here
                Debug.Assert(false);
                return IntPtr.Zero;
            }
        }

        private const int PAGE_SIZE = 0x1000;                   // 4k
        private const int PAGE_SIZE_MASK = 0xFFF;
        private const int ALLOCATION_GRANULARITY = 0x10000;     // 64k
        private const int ALLOCATION_GRANULARITY_MASK = 0xFFFF;
        private const int NUM_THUNK_BLOCKS = ((ALLOCATION_GRANULARITY / 2) / PAGE_SIZE);

        private static IntPtr[] s_RecentlyMappedThunksBlock = new IntPtr[NUM_THUNK_BLOCKS];
        private static int s_RecentlyMappedThunksBlockIndex = NUM_THUNK_BLOCKS;

        private static object s_Lock = new object();
        private static IntPtr s_ThunksTemplate = IntPtr.Zero;
        private static LowLevelDictionary<IntPtr, LowLevelList<ThunksTemplateMap>> s_ThunkMaps = new LowLevelDictionary<IntPtr, LowLevelList<ThunksTemplateMap>>();

        // Helper functions to set/clear the lowest bit for ARM instruction pointers
        private static IntPtr ClearThumbBit(IntPtr value)
        {
#if ARM
            Debug.Assert((value.ToInt32() & 1) == 1);
            value = (IntPtr)(value - 1);
#endif
            return value;
        }
        private static IntPtr SetThumbBit(IntPtr value)
        {
#if ARM
            Debug.Assert((value.ToInt32() & 1) == 0);
            value = (IntPtr)(value + 1);
#endif
            return value;
        }

        public static void FreeThunk(IntPtr commonStubAddress, IntPtr thunkAddress)
        {
            thunkAddress = ClearThumbBit(thunkAddress);

            lock (s_Lock)
            {
                LowLevelList<ThunksTemplateMap> mappings;
                if (s_ThunkMaps.TryGetValue(commonStubAddress, out mappings))
                {
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        mappings[i].FreeThunk(thunkAddress);
                    }
                }
            }
        }

        public static bool TryGetThunkData(IntPtr commonStubAddress, IntPtr thunkAddress, out IntPtr context, out IntPtr target)
        {
            thunkAddress = ClearThumbBit(thunkAddress);
            context = IntPtr.Zero;
            target = IntPtr.Zero;

            lock (s_Lock)
            {
                LowLevelList<ThunksTemplateMap> mappings;
                if (s_ThunkMaps.TryGetValue(commonStubAddress, out mappings))
                {
                    int thunkIndex = 0;

                    for (int i = 0; i < mappings.Count; i++)
                    {
                        if (mappings[i].TryGetThunkIndex(thunkAddress, ref thunkIndex))
                        {
                            long thunkAddressValue = (long)thunkAddress;

                            // Compute the base address of the thunk's mapping
                            long currentThunkMapAddress = ((thunkAddressValue) & ~PAGE_SIZE_MASK);

                            unsafe
                            {
                                // Compute the address of the data block that corresponds to the current thunk
                                IntPtr* thunkData = (IntPtr*)(IntPtr)(currentThunkMapAddress + PAGE_SIZE + thunkIndex * 2 * IntPtr.Size);

                                // Pull out the thunk data
                                context = thunkData[0];
                                target = thunkData[1];
                            }

                            return true;
                        }
                    }
                }
            }

            // Not a thunk
            return false;
        }

        public unsafe static void SetThunkData(IntPtr thunkAddress, IntPtr context, IntPtr target)
        {
            long thunkAddressValue = (long)ClearThumbBit(thunkAddress);

            // Compute the base address of the thunk's mapping
            long currentThunkMapAddress = (thunkAddressValue & ~PAGE_SIZE_MASK);

            // Compute the thunk's index
            Debug.Assert((thunkAddressValue - currentThunkMapAddress) % AsmCode.GetThunkSize() == 0);
            int thunkIndex = (int)(thunkAddressValue - currentThunkMapAddress) / AsmCode.GetThunkSize();

            // Compute the address of the data block that corresponds to the current thunk
            IntPtr dataAddress = (IntPtr)(currentThunkMapAddress + PAGE_SIZE + thunkIndex * 2 * IntPtr.Size);

            // Update the data that will be used by the thunk that was allocated
            lock (s_Lock)
            {
                *((IntPtr*)(dataAddress)) = context;
                *((IntPtr*)(dataAddress + IntPtr.Size)) = target;
            }
        }

        //
        // Note: This method is expected to be called under lock
        //
        private unsafe static IntPtr AllocateThunksTemplateMapFromMapping(IntPtr thunkMap, IntPtr commonStubAddress, LowLevelList<ThunksTemplateMap> mappings)
        {
            // Update the last pointer value in the thunks data section with the value of the common stub address
            *((IntPtr*)(thunkMap + PAGE_SIZE * 2 - IntPtr.Size)) = commonStubAddress;
            Debug.Assert(*((IntPtr*)(thunkMap + PAGE_SIZE * 2 - IntPtr.Size)) != IntPtr.Zero);

            ThunksTemplateMap newMapping = new ThunksTemplateMap(thunkMap);
            mappings.Add(newMapping);

            // Always call GetNextThunk before returning the result, so that the ThunksTemplateMap can
            // correctly keep track of used/unused thunks
            IntPtr thunkStub = newMapping.GetNextThunk();
            Debug.Assert(thunkStub == thunkMap);    // First thunk always at the begining of the mapping

            return thunkStub;
        }

        //
        // Note: This method is expected to be called under lock
        //
        private static IntPtr GetThunkFromAllocatedPool(LowLevelList<ThunksTemplateMap> mappings, IntPtr commonStubAddress)
        {
            IntPtr result;

            for (int i = 0; i < mappings.Count; i++)
            {
                if ((result = mappings[i].GetNextThunk()) != IntPtr.Zero)
                {
                    return result;
                }
            }

            // Check the most recently mapped thunks block. Each mapping consists of multiple
            // thunk stubs pages, and multiple thunk data pages (typically 8 pages of each in a single mapping)
            if (s_RecentlyMappedThunksBlockIndex < NUM_THUNK_BLOCKS)
            {
                IntPtr nextThunkMapBlock = s_RecentlyMappedThunksBlock[s_RecentlyMappedThunksBlockIndex++];
#if DEBUG
                s_RecentlyMappedThunksBlock[s_RecentlyMappedThunksBlockIndex - 1] = IntPtr.Zero;
                Debug.Assert(nextThunkMapBlock != IntPtr.Zero);
#endif
                return AllocateThunksTemplateMapFromMapping(nextThunkMapBlock, commonStubAddress, mappings);
            }

            // No thunk available. Return 0 to indicate we need a new mapping
            return IntPtr.Zero;
        }

        public unsafe static IntPtr AllocateThunk(IntPtr commonStubAddress)
        {
            lock (s_Lock)
            {
                LowLevelList<ThunksTemplateMap> mappings;
                if (!s_ThunkMaps.TryGetValue(commonStubAddress, out mappings))
                    s_ThunkMaps[commonStubAddress] = mappings = new LowLevelList<ThunksTemplateMap>();

                IntPtr thunkStub = GetThunkFromAllocatedPool(mappings, commonStubAddress);

                if (thunkStub == IntPtr.Zero)
                {
                    // No available thunks, so we need a new mapping of the thunks template page

                    IntPtr thunkBase = AsmCode.GetThunksBase();
                    Debug.Assert(thunkBase != IntPtr.Zero);

                    IntPtr moduleHandle = RuntimeAugments.GetModuleFromTypeHandle(typeof(ThunkPool).TypeHandle);
                    Debug.Assert(moduleHandle != IntPtr.Zero);

                    int templateRva = (int)((long)thunkBase - (long)moduleHandle);
                    Debug.Assert(templateRva % ALLOCATION_GRANULARITY == 0);

                    IntPtr thunkMap = IntPtr.Zero;
                    if (s_ThunksTemplate == IntPtr.Zero)
                    {
                        // First, we use the thunks directly from the thunks template sections in the module until all
                        // thunks in that template are used up.
                        thunkMap = moduleHandle + templateRva;
                        s_ThunksTemplate = thunkMap;
                    }
                    else
                    {
                        // We've already used the thunks tempate in the module for some previous thunks, and we 
                        // cannot reuse it here. Now we need to create a new mapping of the thunks section in order to have 
                        // more thunks
                        thunkMap = RuntimeImports.RhAllocateThunksFromTemplate(moduleHandle, templateRva, NUM_THUNK_BLOCKS * PAGE_SIZE * 2);

                        if (thunkMap == IntPtr.Zero)
                        {
                            // We either ran out of memory and can't do anymore mappings of the thunks templates sections,
                            // or we are using the managed runtime services fallback, which doesn't provide the
                            // file mapping feature (ex: older version of mrt100.dll, or no mrt100.dll at all).

                            // The only option is for the caller to attempt and recycle unused thunks to be able to 
                            // find some free entries.
                            return IntPtr.Zero;
                        }
                    }
                    Debug.Assert(thunkMap != IntPtr.Zero && (long)thunkMap % ALLOCATION_GRANULARITY == 0);

                    // Each mapping consists of multiple blocks of thunk stubs/data pairs. Keep track of those
                    // so that we do not create a new mapping until all blocks in the sections we just mapped are consumed
                    for (int i = 0; i < NUM_THUNK_BLOCKS; i++)
                        s_RecentlyMappedThunksBlock[i] = thunkMap + (PAGE_SIZE * i * 2);
                    s_RecentlyMappedThunksBlockIndex = 1;

                    thunkStub = AllocateThunksTemplateMapFromMapping(thunkMap, commonStubAddress, mappings);
                }

                return SetThumbBit(thunkStub);
            }
        }

        public static int GetThunkSize() { return AsmCode.GetThunkSize(); }
    }
}
