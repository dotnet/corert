// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using Internal.Runtime;
using Internal.Runtime.Augments;

using Internal.NativeFormat;

namespace Internal.Runtime.TypeLoader
{
    public sealed partial class PermanentAllocatedMemoryBlobs
    {
        // Various functions in the type loader need to create permanent pointers for use by thread static lookup, or other purposes.

        private static PermanentlyAllocatedMemoryRegions_ThreadStaticFieldOffsets s_threadStaticFieldCookies = new PermanentlyAllocatedMemoryRegions_ThreadStaticFieldOffsets();
        private static PermanentlyAllocatedMemoryRegions_Uint_In_IntPtr s_uintCellValues = new PermanentlyAllocatedMemoryRegions_Uint_In_IntPtr();
        private static PermanentlyAllocatedMemoryRegions_IntPtr_In_IntPtr s_pointerIndirectionCellValues = new PermanentlyAllocatedMemoryRegions_IntPtr_In_IntPtr();

        private class PermanentlyAllocatedMemoryRegions_Uint_In_IntPtr
        {
            private LowLevelDictionary<uint, IntPtr> _allocatedBlocks = new LowLevelDictionary<uint, IntPtr>();
            private Lock _lock = new Lock();

            public unsafe IntPtr GetMemoryBlockForValue(uint value)
            {
                using (LockHolder.Hold(_lock))
                {
                    IntPtr result;
                    if (_allocatedBlocks.TryGetValue(value, out result))
                    {
                        return result;
                    }
                    result = MemoryHelpers.AllocateMemory(IntPtr.Size);
                    *(uint*)(result.ToPointer()) = value;
                    _allocatedBlocks.Add(value, result);
                    return result;
                }
            }
        }

        private class PermanentlyAllocatedMemoryRegions_IntPtr_In_IntPtr
        {
            private LowLevelDictionary<IntPtr, IntPtr> _allocatedBlocks = new LowLevelDictionary<IntPtr, IntPtr>();
            private Lock _lock = new Lock();

            public unsafe IntPtr GetMemoryBlockForValue(IntPtr value)
            {
                using (LockHolder.Hold(_lock))
                {
                    IntPtr result;
                    if (_allocatedBlocks.TryGetValue(value, out result))
                    {
                        return result;
                    }
                    result = MemoryHelpers.AllocateMemory(IntPtr.Size);
                    *(IntPtr*)(result.ToPointer()) = value;
                    _allocatedBlocks.Add(value, result);
                    return result;
                }
            }
        }

        public struct ThreadStaticFieldOffsets : IEquatable<ThreadStaticFieldOffsets>
        {
            public uint StartingOffsetInTlsBlock;    // Offset in the TLS block containing the thread static fields of a given type
            public uint FieldOffset;                 // Offset of a thread static field from the start of its containing type's TLS fields block
                                                     // (in other words, the address of a field is 'TLS block + StartingOffsetInTlsBlock + FieldOffset')

            public override int GetHashCode()
            {
                return (int)(StartingOffsetInTlsBlock ^ FieldOffset << 8);
            }

            public override bool Equals(object obj)
            {
                if (obj is ThreadStaticFieldOffsets)
                {
                    return Equals((ThreadStaticFieldOffsets)obj);
                }
                return false;
            }

            public bool Equals(ThreadStaticFieldOffsets other)
            {
                if (StartingOffsetInTlsBlock != other.StartingOffsetInTlsBlock)
                    return false;

                return FieldOffset == other.FieldOffset;
            }
        }

        private class PermanentlyAllocatedMemoryRegions_ThreadStaticFieldOffsets
        {
            private LowLevelDictionary<ThreadStaticFieldOffsets, IntPtr> _allocatedBlocks = new LowLevelDictionary<ThreadStaticFieldOffsets, IntPtr>();
            private Lock _lock = new Lock();

            public unsafe IntPtr GetMemoryBlockForValue(ThreadStaticFieldOffsets value)
            {
                using (LockHolder.Hold(_lock))
                {
                    IntPtr result;
                    if (_allocatedBlocks.TryGetValue(value, out result))
                    {
                        return result;
                    }
                    result = MemoryHelpers.AllocateMemory(sizeof(ThreadStaticFieldOffsets));
                    *(ThreadStaticFieldOffsets*)(result.ToPointer()) = value;
                    _allocatedBlocks.Add(value, result);
                    return result;
                }
            }
        }

        public static IntPtr GetPointerToUInt(uint value)
        {
            return s_uintCellValues.GetMemoryBlockForValue(value);
        }

        public static IntPtr GetPointerToIntPtr(IntPtr value)
        {
            return s_pointerIndirectionCellValues.GetMemoryBlockForValue(value);
        }

        public static IntPtr GetPointerToThreadStaticFieldOffsets(ThreadStaticFieldOffsets value)
        {
            return s_threadStaticFieldCookies.GetMemoryBlockForValue(value);
        }
    }
}
