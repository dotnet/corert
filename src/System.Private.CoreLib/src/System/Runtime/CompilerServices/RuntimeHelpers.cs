// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////
//
// RuntimeHelpers
//    This class defines a set of static methods that provide support for compilers.
//

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Internal.Runtime.Augments;

namespace System.Runtime.CompilerServices
{
    public static class RuntimeHelpers
    {
        [Intrinsic]
        public static extern void InitializeArray(Array array, RuntimeFieldHandle fldHandle);

        public static void RunClassConstructor(RuntimeTypeHandle type)
        {
            if (type.IsNull)
                throw new ArgumentException(SR.InvalidOperation_HandleIsNotInitialized);

            IntPtr pStaticClassConstructionContext = RuntimeAugments.Callbacks.TryGetStaticClassConstructionContext(type);
            if (pStaticClassConstructionContext == IntPtr.Zero)
                return;
            unsafe
            {
                ClassConstructorRunner.EnsureClassConstructorRun((StaticClassConstructionContext*)pStaticClassConstructionContext);
            }
        }

        public static Object GetObjectValue(Object obj)
        {
            if (obj == null)
                return null;

            EETypePtr eeType = obj.EETypePtr;
            if ((!eeType.IsValueType) || eeType.IsPrimitive)
                return obj;

            return RuntimeImports.RhMemberwiseClone(obj);
        }

        private const int HASHCODE_BITS = 26;
        private const int MASK_HASHCODE = (1 << (int)HASHCODE_BITS) - 1;

        [ThreadStatic]
        private static int t_hashSeed;

        private static int GetNewHashCode()
        {
            int multiplier = Environment.CurrentManagedThreadId * 4 + 5;
            // Every thread has its own generator for hash codes so that we won't get into a situation
            // where two threads consistently give out the same hash codes.
            // Choice of multiplier guarantees period of 2**32 - see Knuth Vol 2 p16 (3.2.1.2 Theorem A).
            t_hashSeed = t_hashSeed * multiplier + 1;
            return t_hashSeed;
        }

        public static unsafe int GetHashCode(Object o)
        {
            if (o == null)
                return 0;

            fixed (IntPtr* pEEType = &o.m_pEEType)
            {
                int* pSyncBlockIndex = (int*)((byte*)pEEType - 4); // skipping exactly 4 bytes for the SyncTableEntry (exactly 4 bytes not a pointer size).
                int hash = *pSyncBlockIndex & MASK_HASHCODE;

                if (hash == 0)
                    return MakeHashCode(o, pSyncBlockIndex);
                else
                    return hash;
            }
        }

        private static unsafe int MakeHashCode(Object o, int* pSyncBlockIndex)
        {
            int hash = GetNewHashCode() & MASK_HASHCODE;

            if (hash == 0)
                hash = 1;

            while (true)
            {
                int oldIndex = Volatile.Read(ref *pSyncBlockIndex);

                int currentHash = oldIndex & MASK_HASHCODE;
                if (currentHash != 0)
                {
                    // Someone else set the hash code.
                    hash = currentHash;
                    break;
                }

                int newIndex = oldIndex | hash;

                if (Interlocked.CompareExchange(ref *pSyncBlockIndex, newIndex, oldIndex) == oldIndex)
                    break;
                // If we get here someone else modified the header.  They may have set the hash code, or maybe some
                // other bits.  Let's try again.
            }

            return (int)hash;
        }

        public static int OffsetToStringData
        {
            get
            {
                // Number of bytes from the address pointed to by a reference to
                // a String to the first 16-bit character in the String.  
                // This property allows C#'s fixed statement to work on Strings.
                return String.FIRST_CHAR_OFFSET;
            }
        }

        // unchecked cast, performs no dynamic type checking
        [Intrinsic]
        internal static extern T UncheckedCast<T>(Object value) where T : class;

        [ThreadStatic]
        private static unsafe byte* t_sufficientStackLimit;

        public static unsafe void EnsureSufficientExecutionStack()
        {
            byte* limit = t_sufficientStackLimit;
            if (limit == null)
                limit = GetSufficientStackLimit();

            byte* currentStackPtr = (byte*)(&limit);
            if (currentStackPtr < limit)
                throw new InsufficientExecutionStackException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // Only called once per thread, no point in inlining.
        private static unsafe byte* GetSufficientStackLimit()
        {
            //
            // We rely on the fact that Windows allocates a thread's stack in a single allocation.  Thus we can call VirtualQuery on any 
            // address on the stack, and the returned info will give the extents of the entire stack.
            //
            // We need to first ensure that the current stack page has been written to, so that it has the same attributes all higher stack pages.
            // This way info.RegionSize will include the whole stack written so far.
            //
            Interop.mincore.MEMORY_BASIC_INFORMATION info = new Interop.mincore.MEMORY_BASIC_INFORMATION();
            Volatile.Write(ref info.BaseAddress, IntPtr.Zero); // Extra paranoid write, to avoid optimizations.

            Interop.mincore.VirtualQuery((IntPtr)(&info), out info, (UIntPtr)(uint)sizeof(Interop.mincore.MEMORY_BASIC_INFORMATION));

            byte* lower = (byte*)info.AllocationBase;
            byte* upper = (byte*)info.BaseAddress + info.RegionSize.ToUInt64();

            //
            // We consider half of the stack to be "sufficient."
            //
            t_sufficientStackLimit = lower + ((upper - lower) / 2);
            return t_sufficientStackLimit;
        }
    }
}
