// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.Augments;
using System.Diagnostics;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime
{
    public static class TypeLoaderExports
    {
        [RuntimeExport("GetThreadStaticsForDynamicType")]
        public static IntPtr GetThreadStaticsForDynamicType(int index)
        {
            IntPtr result = RuntimeImports.RhGetThreadLocalStorageForDynamicType(index, 0, 0);
            if (result != IntPtr.Zero)
                return result;

            int numTlsCells;
            int tlsStorageSize = RuntimeAugments.TypeLoaderCallbacks.GetThreadStaticsSizeForDynamicType(index, out numTlsCells);
            result = RuntimeImports.RhGetThreadLocalStorageForDynamicType(index, tlsStorageSize, numTlsCells);

            if (result == IntPtr.Zero)
                throw new OutOfMemoryException();

            return result;
        }

        [RuntimeExport("ActivatorCreateInstanceAny")]
        public static unsafe void ActivatorCreateInstanceAny(ref object ptrToData, IntPtr pEETypePtr)
        {
            EETypePtr pEEType = new EETypePtr(pEETypePtr);

            if (pEEType.IsValueType)
            {
                // Nothing else to do for value types.
                return;
            }

            // For reference types, we need to:
            //  1- Allocate the new object
            //  2- Call its default ctor
            //  3- Update ptrToData to point to that newly allocated object
            ptrToData = RuntimeImports.RhNewObject(pEEType);

            Entry entry = LookupInCache(s_cache, pEETypePtr, pEETypePtr);
            if (entry == null)
            {
                entry = CacheMiss(pEETypePtr, pEETypePtr, SignatureKind.DefaultConstructor);
            }
            RawCalliHelper.Call(entry.Result, ptrToData);
        }

        //
        // Generic lookup cache
        //

        private class Entry
        {
            public IntPtr Context;
            public IntPtr Signature;
            public IntPtr Result;
            public IntPtr AuxResult;
            public Entry Next;
        }

        // Initialize the cache eagerly to avoid null checks.
        // Use array with just single element to make this pay-for-play. The actual cache will be allocated only 
        // once the lazy lookups are actually needed.
        private static Entry[] s_cache;

        private static Lock s_lock;
        private static GCHandle s_previousCache;
        private volatile static IntPtr[] s_resolutionFunctionPointers;
        private static int s_nextResolutionFunctionPointerIndex;

        internal static void Initialize()
        {
            s_cache = new Entry[1];
            s_resolutionFunctionPointers = new IntPtr[4];
            s_nextResolutionFunctionPointerIndex = (int)SignatureKind.Count;
        }

        [RuntimeExport("GenericLookup")]
        public static IntPtr GenericLookup(IntPtr context, IntPtr signature)
        {
            Entry entry = LookupInCache(s_cache, context, signature);
            if (entry == null)
            {
                entry = CacheMiss(context, signature);
            }
            return entry.Result;
        }

        [RuntimeExport("GenericLookupAndCallCtor")]
        public static void GenericLookupAndCallCtor(Object arg, IntPtr context, IntPtr signature)
        {
            Entry entry = LookupInCache(s_cache, context, signature);
            if (entry == null)
            {
                entry = CacheMiss(context, signature);
            }
            RawCalliHelper.Call(entry.Result, arg);
        }

        [RuntimeExport("GenericLookupAndAllocObject")]
        public static Object GenericLookupAndAllocObject(IntPtr context, IntPtr signature)
        {
            Entry entry = LookupInCache(s_cache, context, signature);
            if (entry == null)
            {
                entry = CacheMiss(context, signature);
            }
            return RawCalliHelper.Call<Object>(entry.Result, entry.AuxResult);
        }

        [RuntimeExport("GenericLookupAndAllocArray")]
        public static Object GenericLookupAndAllocArray(IntPtr context, IntPtr arg, IntPtr signature)
        {
            Entry entry = LookupInCache(s_cache, context, signature);
            if (entry == null)
            {
                entry = CacheMiss(context, signature);
            }
            return RawCalliHelper.Call<Object>(entry.Result, entry.AuxResult, arg);
        }

        [RuntimeExport("GenericLookupAndCheckArrayElemType")]
        public static void GenericLookupAndCheckArrayElemType(IntPtr context, object arg, IntPtr signature)
        {
            Entry entry = LookupInCache(s_cache, context, signature);
            if (entry == null)
            {
                entry = CacheMiss(context, signature);
            }
            RawCalliHelper.Call(entry.Result, entry.AuxResult, arg);
        }

        [RuntimeExport("GenericLookupAndCast")]
        public static Object GenericLookupAndCast(Object arg, IntPtr context, IntPtr signature)
        {
            Entry entry = LookupInCache(s_cache, context, signature);
            if (entry == null)
            {
                entry = CacheMiss(context, signature);
            }
            return RawCalliHelper.Call<Object>(entry.Result, arg, entry.AuxResult);
        }

        [RuntimeExport("UpdateTypeFloatingDictionary")]
        public static IntPtr UpdateTypeFloatingDictionary(IntPtr eetypePtr, IntPtr dictionaryPtr)
        {
            // No caching needed. Update is in-place, and happens once per dictionary
            return RuntimeAugments.TypeLoaderCallbacks.UpdateFloatingDictionary(eetypePtr, dictionaryPtr);
        }

        [RuntimeExport("UpdateMethodFloatingDictionary")]
        public static IntPtr UpdateMethodFloatingDictionary(IntPtr dictionaryPtr)
        {
            // No caching needed. Update is in-place, and happens once per dictionary
            return RuntimeAugments.TypeLoaderCallbacks.UpdateFloatingDictionary(dictionaryPtr, dictionaryPtr);
        }

        public static unsafe IntPtr GetDelegateThunk(object delegateObj, int whichThunk)
        {
            Entry entry = LookupInCache(s_cache, delegateObj.m_pEEType, new IntPtr(whichThunk));
            if (entry == null)
            {
                entry = CacheMiss(delegateObj.m_pEEType, new IntPtr(whichThunk), SignatureKind.GenericDelegateThunk, delegateObj);
            }
            return entry.Result;
        }

        public static unsafe IntPtr GVMLookupForSlot(object obj, RuntimeMethodHandle slot)
        {
            Entry entry = LookupInCache(s_cache, obj.m_pEEType, *(IntPtr*)&slot);
            if (entry == null)
            {
                entry = CacheMiss(obj.m_pEEType, *(IntPtr*)&slot, SignatureKind.GenericVirtualMethod);
            }
            return entry.Result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IntPtr OpenInstanceMethodLookup(IntPtr openResolver, object obj)
        {
            Entry entry = LookupInCache(s_cache, obj.m_pEEType, openResolver);
            if (entry == null)
            {
                entry = CacheMiss(obj.m_pEEType, openResolver, SignatureKind.OpenInstanceResolver, obj);
            }
            return entry.Result;
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static Entry LookupInCache(Entry[] cache, IntPtr context, IntPtr signature)
        {
            int key = ((context.GetHashCode() >> 4) ^ signature.GetHashCode()) & (cache.Length - 1);
            Entry entry = cache[key];
            while (entry != null)
            {
                if (entry.Context == context && entry.Signature == signature)
                    break;
                entry = entry.Next;
            }
            return entry;
        }

        private enum SignatureKind
        {
            GenericDictionary,
            GenericVirtualMethod,
            OpenInstanceResolver,
            DefaultConstructor,
            GenericDelegateThunk,
            Count
        }

        internal static int RegisterResolutionFunction(IntPtr resolutionFunction)
        {
            if (s_lock == null)
                Interlocked.CompareExchange(ref s_lock, new Lock(), null);

            s_lock.Acquire();
            try
            {
                int newResolutionFunctionId = s_nextResolutionFunctionPointerIndex;
                IntPtr[] resolutionFunctionPointers = null;
                if (newResolutionFunctionId < s_resolutionFunctionPointers.Length)
                {
                    resolutionFunctionPointers = s_resolutionFunctionPointers;
                }
                else
                {
                    resolutionFunctionPointers = new IntPtr[s_resolutionFunctionPointers.Length * 2];
                    Array.Copy(s_resolutionFunctionPointers, resolutionFunctionPointers, s_resolutionFunctionPointers.Length);
                    s_resolutionFunctionPointers = resolutionFunctionPointers;
                }
                Volatile.Write(ref s_resolutionFunctionPointers[newResolutionFunctionId], resolutionFunction);
                s_nextResolutionFunctionPointerIndex++;
                return newResolutionFunctionId;
            }
            finally
            {
                s_lock.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IntPtr RuntimeCacheLookupInCache(IntPtr context, IntPtr signature, int registeredResolutionFunction, object contextObject, out IntPtr auxResult)
        {
            Entry entry = LookupInCache(s_cache, context, signature);
            if (entry == null)
            {
                entry = CacheMiss(context, signature, (SignatureKind)registeredResolutionFunction, contextObject);
            }
            auxResult = entry.AuxResult;
            return entry.Result;
        }

        private static unsafe Entry CacheMiss(IntPtr context, IntPtr signature, SignatureKind signatureKind = SignatureKind.GenericDictionary, object contextObject = null)
        {
            IntPtr result = IntPtr.Zero, auxResult = IntPtr.Zero;
            bool previouslyCached = false;

            //
            // Try to find the entry in the previous version of the cache that is kept alive by weak reference
            //
            if (s_previousCache.IsAllocated)
            {
                Entry[] previousCache = (Entry[])s_previousCache.Target;
                if (previousCache != null)
                {
                    Entry previousEntry = LookupInCache(previousCache, context, signature);
                    if (previousEntry != null)
                    {
                        result = previousEntry.Result;
                        auxResult = previousEntry.AuxResult;
                        previouslyCached = true;
                    }
                }
            }

            //
            // Call into the type loader to compute the target
            //
            if (!previouslyCached)
            {
                switch (signatureKind)
                {
                    case SignatureKind.GenericDictionary:
                        result = RuntimeAugments.TypeLoaderCallbacks.GenericLookupFromContextAndSignature(context, signature, out auxResult);
                        break;
                    case SignatureKind.GenericVirtualMethod:
                        result = Internal.Runtime.CompilerServices.GenericVirtualMethodSupport.GVMLookupForSlot(new RuntimeTypeHandle(new EETypePtr(context)), *(RuntimeMethodHandle*)&signature);
                        break;
                    case SignatureKind.OpenInstanceResolver:
                        result = Internal.Runtime.CompilerServices.OpenMethodResolver.ResolveMethodWorker(signature, contextObject);
                        break;
                    case SignatureKind.DefaultConstructor:
                        {
                            result = RuntimeAugments.TypeLoaderCallbacks.TryGetDefaultConstructorForType(new RuntimeTypeHandle(new EETypePtr(context)));
                            if (result == IntPtr.Zero)
                                result = RuntimeAugments.GetFallbackDefaultConstructor();
                        }
                        break;
                    case SignatureKind.GenericDelegateThunk:
                        result = RuntimeAugments.TypeLoaderCallbacks.GetDelegateThunk((Delegate)contextObject, (int)signature);
                        break;
                    default:
                        result = RawCalliHelper.Call<IntPtr>(s_resolutionFunctionPointers[(int)signatureKind], context, signature, contextObject, out auxResult);
                        break;
                }
            }

            //
            // Update the cache under the lock
            //
            if (s_lock == null)
                Interlocked.CompareExchange(ref s_lock, new Lock(), null);

            s_lock.Acquire();
            try
            {
                // Avoid duplicate entries
                Entry existingEntry = LookupInCache(s_cache, context, signature);
                if (existingEntry != null)
                    return existingEntry;

                // Resize cache as necessary
                Entry[] cache = ResizeCacheForNewEntryAsNecessary();

                int key = ((context.GetHashCode() >> 4) ^ signature.GetHashCode()) & (cache.Length - 1);

                Entry newEntry = new Entry() { Context = context, Signature = signature, Result = result, AuxResult = auxResult, Next = cache[key] };
                cache[key] = newEntry;
                return newEntry;
            }
            finally
            {
                s_lock.Release();
            }
        }

        //
        // Parameters and state used by generic lookup cache resizing algorithm
        //

        private const int InitialCacheSize = 128; // MUST BE A POWER OF TWO
        private const int DefaultCacheSize = 1024;
        private const int MaximumCacheSize = 128 * 1024;

        private static long s_tickCountOfLastOverflow;
        private static int s_entries;
        private static bool s_roundRobinFlushing;

        private static Entry[] ResizeCacheForNewEntryAsNecessary()
        {
            Entry[] cache = s_cache;

            if (cache.Length < InitialCacheSize)
            {
                // Start with small cache size so that the cache entries used by startup one-time only initialization will get flushed soon
                return s_cache = new Entry[InitialCacheSize];
            }

            int entries = s_entries++;

            // If the cache has spare space, we are done
            if (2 * entries < cache.Length)
            {
                if (s_roundRobinFlushing)
                {
                    cache[2 * entries] = null;
                    cache[2 * entries + 1] = null;
                }
                return cache;
            }

            //
            // Now, we have cache that is overflowing with the stuff. We need to decide whether to resize it or start flushing the old entries instead
            //

            // Start over counting the entries
            s_entries = 0;

            // See how long it has been since the last time the cache was overflowing
            long tickCount = Environment.TickCount64;
            long tickCountSinceLastOverflow = tickCount - s_tickCountOfLastOverflow;
            s_tickCountOfLastOverflow = tickCount;

            bool shrinkCache = false;
            bool growCache = false;

            if (cache.Length < DefaultCacheSize)
            {
                // If the cache have not reached the default size, just grow it without thinking about it much
                growCache = true;
            }
            else
            {
                if (tickCountSinceLastOverflow < cache.Length / 128)
                {
                    // If the fill rate of the cache is faster than ~0.01ms per entry, grow it
                    if (cache.Length < MaximumCacheSize)
                        growCache = true;
                }
                else
                if (tickCountSinceLastOverflow > cache.Length * 16)
                {
                    // If the fill rate of the cache is slower than 16ms per entry, shrink it
                    if (cache.Length > DefaultCacheSize)
                        shrinkCache = true;
                }
                // Otherwise, keep the current size and just keep flushing the entries round robin
            }

            if (growCache || shrinkCache)
            {
                s_roundRobinFlushing = false;

                // Keep the reference to the old cache in a weak handle. We will try to use to avoid
                // hitting the type loader until GC collects it.
                if (s_previousCache.IsAllocated)
                {
                    s_previousCache.Target = cache;
                }
                else
                {
                    s_previousCache = GCHandle.Alloc(cache, GCHandleType.Weak);
                }

                return s_cache = new Entry[shrinkCache ? (cache.Length / 2) : (cache.Length * 2)];
            }
            else
            {
                s_roundRobinFlushing = true;
                return cache;
            }
        }
    }

    [System.Runtime.InteropServices.McgIntrinsicsAttribute]
    internal class RawCalliHelper
    {
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static T Call<T>(System.IntPtr pfn, IntPtr arg)
        {
            return default(T);
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static void Call(System.IntPtr pfn, Object arg)
        {
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static T Call<T>(System.IntPtr pfn, IntPtr arg1, IntPtr arg2)
        {
            return default(T);
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static T Call<T>(System.IntPtr pfn, IntPtr arg1, IntPtr arg2, object arg3, out IntPtr arg4)
        {
            arg4 = IntPtr.Zero;
            return default(T);
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static void Call(System.IntPtr pfn, IntPtr arg1, Object arg2)
        {
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static T Call<T>(System.IntPtr pfn, Object arg1, IntPtr arg2)
        {
            return default(T);
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static T Call<T>(IntPtr pfn, string[] arg0)
        {
            return default(T);
        }
    }
}
