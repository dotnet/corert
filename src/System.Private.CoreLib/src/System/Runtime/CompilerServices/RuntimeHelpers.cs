// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////
//
// RuntimeHelpers
//    This class defines a set of static methods that provide support for compilers.
//

using Internal.Runtime.Augments;
using System.Threading;

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

#if !FEATURE_SYNCTABLE
        private const int HASHCODE_BITS = 26;
        private const int MASK_HASHCODE = (1 << HASHCODE_BITS) - 1;
#endif

        [ThreadStatic]
        private static int t_hashSeed;

        internal static int GetNewHashCode()
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
#if FEATURE_SYNCTABLE
            return ObjectHeader.GetHashCode(o);
#else
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
#endif
        }

#if !FEATURE_SYNCTABLE
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

            return hash;
        }
#endif

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

        public static unsafe bool TryEnsureSufficientExecutionStack()
        {
            byte* limit = t_sufficientStackLimit;
            if (limit == null)
                limit = GetSufficientStackLimit();

            byte* currentStackPtr = (byte*)(&limit);
            return (currentStackPtr >= limit);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // Only called once per thread, no point in inlining.
        private static unsafe byte* GetSufficientStackLimit()
        {
            IntPtr lower, upper;
            RuntimeImports.RhGetCurrentThreadStackBounds(out lower, out upper);

            //
            // We consider half of the stack to be "sufficient."
            //
            return (t_sufficientStackLimit = (byte*)lower + (((byte*)upper - (byte*)lower) / 2));
        }
    }
}
