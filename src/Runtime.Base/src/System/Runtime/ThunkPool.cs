// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
// Available thunks are tracked using a linked list. The first cell in the data block of each thunk is
// used as the nodes of the linked list. The cell will point to the data block of the next available thunk, 
// if one is available, or point to null. When thunks are freed, they are added to the begining of the list.
// 

using System.Diagnostics;
using Internal.Runtime;

// Convenient typecasting for IntPtr to use with arithmetic operations
#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else
using nint = System.Int32;
using nuint = System.UInt32;
#endif


namespace System.Runtime
{
    internal static class Constants
    {
        public const uint PageSize = 0x1000;                   // 4k
        public const uint AllocationGranularity = 0x10000;     // 64k
        public const nuint PageSizeMask = 0xFFF;
        public const nuint AllocationGranularityMask = 0xFFFF;

        public static int ThunkSize = InternalCalls.RhpGetThunkSize();
        public static int NumThunksPerBlock = InternalCalls.RhpGetNumThunksPerBlock();
        public static int NumThunkBlocksPerMapping = InternalCalls.RhpGetNumThunkBlocksPerMapping();
    }

    internal class ThunksHeap
    {
        class AllocatedBlock
        {
            internal IntPtr _blockBaseAddress;
            internal AllocatedBlock _nextBlock;
        }

        private IntPtr _commonStubAddress;
        private IntPtr _nextAvailableThunkPtr;
        private IntPtr _lastThunkPtr;

        private AllocatedBlock _allocatedBlocks;

        // Helper functions to set/clear the lowest bit for ARM instruction pointers
        private static IntPtr ClearThumbBit(IntPtr value)
        {
#if ARM
            Debug.Assert(((nuint)value & 1) == 1);
            value = (IntPtr)((nuint)value - 1);
#endif
            return value;
        }
        private static IntPtr SetThumbBit(IntPtr value)
        {
#if ARM
            Debug.Assert(((nuint)value & 1) == 0);
            value = (IntPtr)((nuint)value + 1);
#endif
            return value;
        }

        private unsafe ThunksHeap(IntPtr commonStubAddress)
        {
            _commonStubAddress = commonStubAddress;

            _allocatedBlocks = new AllocatedBlock();

            InternalCalls.RhpAcquireThunkPoolLock();

            IntPtr thunksBlock = ThunkBlocks.GetNewThunksBlock();

            InternalCalls.RhpReleaseThunkPoolLock();

            if (thunksBlock != IntPtr.Zero)
            {
                // Update the last pointer value in the thunks data section with the value of the common stub address
                *((IntPtr*)(thunksBlock + (int)(Constants.PageSize * 2 - IntPtr.Size))) = commonStubAddress;
                Debug.Assert(*((IntPtr*)(thunksBlock + (int)(Constants.PageSize * 2 - IntPtr.Size))) != IntPtr.Zero);

                // Set the head and end of the linked list
                _nextAvailableThunkPtr = thunksBlock + (int)Constants.PageSize;
                _lastThunkPtr = _nextAvailableThunkPtr + 2 * IntPtr.Size * (Constants.NumThunksPerBlock - 1);

                _allocatedBlocks._blockBaseAddress = thunksBlock;
            }
        }

        public unsafe static ThunksHeap CreateThunksHeap(IntPtr commonStubAddress)
        {
            try
            {
                ThunksHeap newHeap = new ThunksHeap(commonStubAddress);

                if (newHeap._nextAvailableThunkPtr != IntPtr.Zero)
                    return newHeap;
            }
            catch { }

            return null;
        }

        // TODO: Feature
        // public static ThunksHeap DestroyThunksHeap(ThunksHeap heapToDestroy)
        // {
        // }

        //
        // Note: Expected to be called under lock
        //
        private unsafe bool ExpandHeap()
        {
            AllocatedBlock newBlockInfo;

            try
            {
                newBlockInfo = new AllocatedBlock();
            }
            catch
            {
                return false;
            }

            IntPtr newBlockAddress = ThunkBlocks.GetNewThunksBlock();

            if (newBlockAddress != IntPtr.Zero)
            {
                // Update the last pointer value in the thunks data section with the value of the common stub address
                *((IntPtr*)(newBlockAddress + (int)(Constants.PageSize * 2 - IntPtr.Size))) = _commonStubAddress;
                Debug.Assert(*((IntPtr*)(newBlockAddress + (int)(Constants.PageSize * 2 - IntPtr.Size))) != IntPtr.Zero);

                // Link the last entry in the old list to the first entry in the new list
                *((IntPtr*)_lastThunkPtr) = newBlockAddress + (int)Constants.PageSize;

                // Update the pointer to the last entry in the list
                _lastThunkPtr = newBlockAddress + (int)Constants.PageSize + 2 * IntPtr.Size * (Constants.NumThunksPerBlock - 1);

                newBlockInfo._blockBaseAddress = newBlockAddress;
                newBlockInfo._nextBlock = _allocatedBlocks;

                _allocatedBlocks = newBlockInfo;

                return true;
            }

            return false;
        }

        public unsafe IntPtr AllocateThunk()
        {
            // TODO: optimize the implementation and make it lock-free
            // or at least change it to a per-heap lock instead of a global lock.

            Debug.Assert(_nextAvailableThunkPtr != IntPtr.Zero);

            InternalCalls.RhpAcquireThunkPoolLock();

            IntPtr nextAvailableThunkPtr = _nextAvailableThunkPtr;
            IntPtr nextNextAvailableThunkPtr = *((IntPtr*)(nextAvailableThunkPtr));

            if (nextNextAvailableThunkPtr == IntPtr.Zero)
            {
                if (!ExpandHeap())
                {
                    InternalCalls.RhpReleaseThunkPoolLock();
                    return IntPtr.Zero;
                }

                nextAvailableThunkPtr = _nextAvailableThunkPtr;
                nextNextAvailableThunkPtr = *((IntPtr*)(nextAvailableThunkPtr));
                Debug.Assert(nextNextAvailableThunkPtr != IntPtr.Zero);
            }

            _nextAvailableThunkPtr = nextNextAvailableThunkPtr;

            InternalCalls.RhpReleaseThunkPoolLock();

            Debug.Assert(nextAvailableThunkPtr != IntPtr.Zero);

#if DEBUG
            // Reset debug flag indicating the thunk is now in use
            *((IntPtr*)(nextAvailableThunkPtr + IntPtr.Size)) = IntPtr.Zero;
#endif
            int thunkIndex = (int)(((nuint)nextAvailableThunkPtr) - ((nuint)nextAvailableThunkPtr & ~Constants.PageSizeMask));
            Debug.Assert((thunkIndex % (2 * IntPtr.Size)) == 0);
            thunkIndex = thunkIndex / (2 * IntPtr.Size);

            IntPtr thunksBlockBaseAddress = new IntPtr((void*)(((nuint)nextAvailableThunkPtr & ~Constants.PageSizeMask) - Constants.PageSize));
            IntPtr thunkAddress = thunksBlockBaseAddress + thunkIndex * Constants.ThunkSize;

            return SetThumbBit(thunkAddress);
        }

        public unsafe void FreeThunk(IntPtr thunkAddress)
        {
            // TODO: optimize the implementation and make it lock-free
            // or at least change it to a per-heap lock instead of a global lock.

            IntPtr dataAddress = TryGetThunkDataAddress(thunkAddress);
            if (dataAddress == IntPtr.Zero)
                EH.FallbackFailFast(RhFailFastReason.InternalError, null);

#if DEBUG
            if (!IsThunkInHeap(thunkAddress))
                EH.FallbackFailFast(RhFailFastReason.InternalError, null);

            // Debug flag indicating the thunk is no longer used
            *((IntPtr*)(dataAddress + IntPtr.Size)) = new IntPtr(-1);
#endif

            InternalCalls.RhpAcquireThunkPoolLock();

            *((IntPtr*)(dataAddress)) = _nextAvailableThunkPtr;
            _nextAvailableThunkPtr = dataAddress;

            InternalCalls.RhpReleaseThunkPoolLock();
        }

        private bool IsThunkInHeap(IntPtr thunkAddress)
        {
            nuint thunkAddressValue = (nuint)ClearThumbBit(thunkAddress);

            AllocatedBlock currentBlock = _allocatedBlocks;

            while (currentBlock != null)
            {
                if (thunkAddressValue >= (nuint)currentBlock._blockBaseAddress &&
                    thunkAddressValue < (nuint)currentBlock._blockBaseAddress + (nuint)(Constants.NumThunksPerBlock * Constants.ThunkSize))
                {
                    return true;
                }

                currentBlock = currentBlock._nextBlock;
            }

            return false;
        }

        private IntPtr TryGetThunkDataAddress(IntPtr thunkAddress)
        {
            nuint thunkAddressValue = (nuint)ClearThumbBit(thunkAddress);

            // Compute the base address of the thunk's mapping
            nuint currentThunkMapAddress = thunkAddressValue & ~Constants.PageSizeMask;

            // Make sure the thunk address is valid by checking alignment
            if ((thunkAddressValue - currentThunkMapAddress) % (nuint)Constants.ThunkSize != 0)
                return IntPtr.Zero;

            // Compute the thunk's index
            int thunkIndex = (int)(thunkAddressValue - currentThunkMapAddress) / Constants.ThunkSize;

            // Compute the address of the data block that corresponds to the current thunk
            return (IntPtr)(currentThunkMapAddress + (nuint)(Constants.PageSize + thunkIndex * 2 * IntPtr.Size));
        }

        /// <summary>
        /// This method retrieves the two data fields for a thunk.
        /// Caution: No checks are made to verify that the thunk address is that of a 
        /// valid thunk in use. The caller of this API is responsible for providing a valid
        /// address of a thunk that was not previously freed.
        /// </summary>
        /// <returns>True if the thunk's data was successfully retrieved.</returns>
        public unsafe bool TryGetThunkData(IntPtr thunkAddress, out IntPtr context, out IntPtr target)
        {
            context = IntPtr.Zero;
            target = IntPtr.Zero;

            IntPtr dataAddress = TryGetThunkDataAddress(thunkAddress);
            if (dataAddress == IntPtr.Zero)
                return false;

            if (!IsThunkInHeap(thunkAddress))
                return false;

            // Update the data that will be used by the thunk that was allocated
            context = *((IntPtr*)(dataAddress));
            target = *((IntPtr*)(dataAddress + IntPtr.Size));

            return true;
        }

        /// <summary>
        /// This method sets the two data fields for a thunk.
        /// Caution: No checks are made to verify that the thunk address is that of a 
        /// valid thunk in use. The caller of this API is responsible for providing a valid
        /// address of a thunk that was not previously freed.
        /// </summary>
        /// <returns>True if the thunk's data was successfully set.</returns>
        public unsafe void SetThunkData(IntPtr thunkAddress, IntPtr context, IntPtr target)
        {
            IntPtr dataAddress = TryGetThunkDataAddress(thunkAddress);
            if (dataAddress == IntPtr.Zero)
                EH.FallbackFailFast(RhFailFastReason.InternalError, null);

#if DEBUG
            if (!IsThunkInHeap(thunkAddress))
                EH.FallbackFailFast(RhFailFastReason.InternalError, null);
#endif

            // Update the data that will be used by the thunk that was allocated
            *((IntPtr*)(dataAddress)) = context;
            *((IntPtr*)(dataAddress + IntPtr.Size)) = target;
        }
    }

    internal class ThunkBlocks
    {
        private static IntPtr[] s_currentlyMappedThunkBlocks = new IntPtr[Constants.NumThunkBlocksPerMapping];
        private static int s_currentlyMappedThunkBlocksIndex = Constants.NumThunkBlocksPerMapping;        
        private static IntPtr s_thunksTemplate;

        private static IntPtr s_thunksModuleBaseAddress;
        private static int s_thunksTemplateRva;

        public unsafe static IntPtr GetNewThunksBlock()
        {
            IntPtr nextThunkMapBlock;

            // Check the most recently mapped thunks block. Each mapping consists of multiple
            // thunk stubs pages, and multiple thunk data pages (typically 8 pages of each in a single mapping)
            if (s_currentlyMappedThunkBlocksIndex < Constants.NumThunkBlocksPerMapping)
            {
                nextThunkMapBlock = s_currentlyMappedThunkBlocks[s_currentlyMappedThunkBlocksIndex++];
#if DEBUG
                s_currentlyMappedThunkBlocks[s_currentlyMappedThunkBlocksIndex - 1] = IntPtr.Zero;
                Debug.Assert(nextThunkMapBlock != IntPtr.Zero);
#endif
            }
            else
            {
                if (s_thunksTemplate == IntPtr.Zero)
                {
                    // First, we use the thunks directly from the thunks template sections in the module until all
                    // thunks in that template are used up.
                    s_thunksTemplate = nextThunkMapBlock = InternalCalls.RhpGetThunksBase();
                }
                else
                {
                    // Compute and cache the current module's base address and the RVA of the thunks template

                    if (s_thunksModuleBaseAddress == IntPtr.Zero)
                    {
                        EEType* pInstanceType = (new ThunkBlocks()).EEType;
                        s_thunksModuleBaseAddress = InternalCalls.RhGetModuleFromPointer(pInstanceType);

                        IntPtr thunkBase = InternalCalls.RhpGetThunksBase();
                        Debug.Assert(thunkBase != IntPtr.Zero);

                        s_thunksTemplateRva = (int)(((nuint)thunkBase) - ((nuint)s_thunksModuleBaseAddress));
                        Debug.Assert(s_thunksTemplateRva % (int)Constants.AllocationGranularity == 0);
                    }

                    // We've already used the thunks tempate in the module for some previous thunks, and we 
                    // cannot reuse it here. Now we need to create a new mapping of the thunks section in order to have 
                    // more thunks
                    nextThunkMapBlock = InternalCalls.RhAllocateThunksFromTemplate(
                        s_thunksModuleBaseAddress,
                        s_thunksTemplateRva,
                        (int)(Constants.NumThunkBlocksPerMapping * Constants.PageSize * 2));

                    if (nextThunkMapBlock == IntPtr.Zero)
                    {
                        // We either ran out of memory and can't do anymore mappings of the thunks templates sections,
                        // or we are using the managed runtime services fallback, which doesn't provide the
                        // file mapping feature (ex: older version of mrt100.dll, or no mrt100.dll at all).

                        // The only option is for the caller to attempt and recycle unused thunks to be able to 
                        // find some free entries.

                        InternalCalls.RhpReleaseThunkPoolLock();

                        return IntPtr.Zero;
                    }
                }

                // Each mapping consists of multiple blocks of thunk stubs/data pairs. Keep track of those
                // so that we do not create a new mapping until all blocks in the sections we just mapped are consumed
                for (int i = 0; i < Constants.NumThunkBlocksPerMapping; i++)
                    s_currentlyMappedThunkBlocks[i] = nextThunkMapBlock + (int)(Constants.PageSize * i * 2);
                s_currentlyMappedThunkBlocksIndex = 1;
            }

            Debug.Assert(nextThunkMapBlock != IntPtr.Zero);

            // Setup the thunks in the new block as a linked list of thunks.
            // Use the first data field of the thunk to build the linked list.
            int numThunksPerBlock = Constants.NumThunksPerBlock;
            IntPtr dataAddress = nextThunkMapBlock + (int)Constants.PageSize;
            for (int i = 0; i < numThunksPerBlock; i++)
            {
                Debug.Assert(dataAddress == nextThunkMapBlock + (int)(Constants.PageSize + i * 2 * IntPtr.Size));

                if (i == (numThunksPerBlock - 1))
                    *((IntPtr*)(dataAddress)) = IntPtr.Zero;
                else
                    *((IntPtr*)(dataAddress)) = dataAddress + 2 * IntPtr.Size;

#if DEBUG
                // Debug flag in the second data cell indicating the thunk is not used
                *((IntPtr*)(dataAddress + IntPtr.Size)) = new IntPtr(-1);
#endif

                dataAddress += 2 * IntPtr.Size;
            }

            return nextThunkMapBlock;
        }

        // TODO: [Feature] Keep track of mapped sections and free them if we need to.
        // public unsafe static void FreeThunksBlock()
        // {
        // }
    }
}