// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////
//
// RuntimeHelpers
//    This class defines a set of static methods that provide support for compilers.
//

using Internal.Reflection.Augments;
using Internal.Reflection.Core.NonPortable;
using Internal.Runtime.Augments;
using System.Runtime;
using System.Runtime.Serialization;
using System.Threading;

namespace System.Runtime.CompilerServices
{
    public static class RuntimeHelpers
    {
        [Intrinsic]
        public static void InitializeArray(Array array, RuntimeFieldHandle fldHandle)
        {
            // We only support this intrinsic when it occurs within a well-defined IL sequence.
            // If a call to this method occurs within the recognized sequence, codegen must expand the IL sequence completely.
            // For any other purpose, the API is currently unsupported.
            // https://github.com/dotnet/corert/issues/364
            throw new PlatformNotSupportedException();
        }

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

        public static void RunModuleConstructor(ModuleHandle module)
        {
            if (module.AssociatedModule == null)
                throw new ArgumentException(SR.InvalidOperation_HandleIsNotInitialized);

            ReflectionAugments.ReflectionCoreCallbacks.RunModuleConstructor(module.AssociatedModule);
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

        public new static bool Equals(Object obj1, Object obj2)
        {
            if (obj1 == obj2)
                return true;

            if ((obj1 == null) || (obj2 == null))
                return false;

            // If it's not a value class, don't compare by value
            if (!obj1.EETypePtr.IsValueType)
                return false;

            // Make sure they are the same type.
            if (obj1.EETypePtr != obj2.EETypePtr)
                return false;

            return RuntimeImports.RhCompareObjectContentsAndPadding(obj1, obj2);
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

            // Compute the limit used by EnsureSufficientExecutionStack and cache it on the thread. This minimum
            // stack size should be sufficient to allow a typical non-recursive call chain to execute, including
            // potential exception handling and garbage collection.

#if BIT64
            const int MinExecutionStackSize = 128 * 1024;
#else
            const int MinExecutionStackSize = 64 * 1024;
#endif

            byte* limit = (((byte*)upper - (byte*)lower > MinExecutionStackSize)) ?
                ((byte*)lower + MinExecutionStackSize) : ((byte*)upper);

            return (t_sufficientStackLimit = limit);
        }

        [Intrinsic]
        public static bool IsReferenceOrContainsReferences<T>()
        {
            var pEEType = EETypePtr.EETypePtrOf<T>();
            return !pEEType.IsValueType || pEEType.HasPointers;
        }

        // Constrained Execution Regions APIs are NOP's because we do not support CERs in .NET Core at all.
        public static void ProbeForSufficientStack() { }
        public static void PrepareConstrainedRegions() { }
        public static void PrepareConstrainedRegionsNoOP() { }
        public static void PrepareMethod(RuntimeMethodHandle method) { }
        public static void PrepareMethod(RuntimeMethodHandle method, RuntimeTypeHandle[] instantiation) { }
        public static void PrepareContractedDelegate(Delegate d) { }
        public static void PrepareDelegate(Delegate d)
        {
            if (d == null)
                throw new ArgumentNullException(nameof(d));
        }

        public static void ExecuteCodeWithGuaranteedCleanup(TryCode code, CleanupCode backoutCode, Object userData)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));
            if (backoutCode == null)
                throw new ArgumentNullException(nameof(backoutCode));

            bool exceptionThrown = false;

            try
            {
                code(userData);
            }
            catch
            {
                exceptionThrown = true;
                throw;
            }
            finally
            {
                backoutCode(userData, exceptionThrown);
            }
        }

        public delegate void TryCode(Object userData);
        public delegate void CleanupCode(Object userData, bool exceptionThrown);

        public static object GetUninitializedObject(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type), SR.ArgumentNull_Type);
            }
            
            if(!type.IsRuntimeImplemented())
            {
                throw new SerializationException(SR.Format(SR.Serialization_InvalidType, type.ToString()));
            }

            if (type.IsArray || type.IsByRef || type.IsPointer)
            {
                throw new ArgumentException(SR.Argument_InvalidValue);
            }

            if (type.ContainsGenericParameters)
            {
                throw new MemberAccessException(SR.Acc_CreateGeneric);
            }

            if (type.IsCOMObject)
            {
                throw new NotSupportedException(SR.NotSupported_ManagedActivation);
            }

            EETypePtr eeTypePtr = type.TypeHandle.ToEETypePtr();

            if (eeTypePtr == EETypePtr.EETypePtrOf<string>())
            {
                throw new ArgumentException(SR.Argument_NoUninitializedStrings);
            }

            if (eeTypePtr.IsAbstract)
            {
                throw new MemberAccessException(SR.Acc_CreateAbst);
            }

            if (eeTypePtr.IsByRefLike)
            {
                throw new NotSupportedException(SR.NotSupported_ByRefLike);
            }

            if (eeTypePtr.IsNullable)
            {
                return GetUninitializedObject(ReflectionCoreNonPortable.GetRuntimeTypeForEEType(eeTypePtr.NullableType));
            }

            // Triggering the .cctor here is slightly different than desktop/CoreCLR, which 
            // decide based on BeforeFieldInit, but we don't want to include BeforeFieldInit
            // in EEType just for this API to behave slightly differently.
            RunClassConstructor(type.TypeHandle);

            return RuntimeImports.RhNewObject(eeTypePtr);
        }
    }
}
