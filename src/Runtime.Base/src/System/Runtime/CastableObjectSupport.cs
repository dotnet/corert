using System;
using Internal.Runtime;
using System.Runtime.CompilerServices;

namespace System.Runtime
{
    [System.Runtime.CompilerServices.EagerStaticClassConstructionAttribute]
    static class CastableObjectSupport
    {
        private static object s_castFailCanary = new object();

        internal interface ICastableObject
        // TODO!! BEGIN REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
            : ICastable
        // TODO!! END REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
        {
            // This is called if casting this object to the given interface type would otherwise fail. Casting
            // here means the IL isinst and castclass instructions in the case where they are given an interface
            // type as the target type.
            //
            // A return value of non-null indicates the cast is valid.
            // The return value (if non-null) must be an object instance that implements the specified interface.
            //
            // If null is returned when this is called as part of a castclass then the usual InvalidCastException
            // will be thrown unless an alternate exception is assigned to the castError output parameter. This
            // parameter is ignored on successful casts or during the evaluation of an isinst (which returns null
            // rather than throwing on error).
            //
            // The results of this call are cached
            //
            // The results of this call should be semantically  invariant for the same object, interface type pair. 
            // That is because this is the only guard placed before an interface invocation at runtime. It is possible
            // that this call may occur more than once for a given pair, and it is possible that the results of multiple calls
            // may remain in use over time.
            object CastToInterface(EETypePtr interfaceType, bool produceCastErrorException, out Exception castError);
        }

        internal struct CastableObjectCacheEntry
        {
            public unsafe EEType *Type;
            public object InstanceObjectForType;
        }

        internal class CastableObject
        {
            public CastableObjectCacheEntry[] Cache;
        }

        // cache must be a size which is a power of two.
        internal static unsafe object CastableTargetLookup(CastableObjectCacheEntry[] cache, EEType* interfaceType)
        {
            uint cacheMask = (uint)cache.Length - 1;
            uint bucket = interfaceType->HashCode & cacheMask;
            uint curbucket = bucket;

            // hash algorithm is open addressing with linear probing

            while (curbucket < cache.Length)
            {
                if (cache[curbucket].Type == interfaceType)
                    return cache[curbucket].InstanceObjectForType;
                if (cache[curbucket].Type == null)
                    return null;
                curbucket++;
            }

            // Handle wrap-around case
            curbucket = 0;
            while (curbucket < bucket)
            {
                if (cache[curbucket].Type == interfaceType)
                    return cache[curbucket].InstanceObjectForType;
                if (cache[curbucket].Type == null)
                    return null;
                curbucket++;
            }

            return null;
        }

        internal unsafe static int GetCachePopulation(CastableObjectCacheEntry[] cache)
        {
            int population = 0;
            for (int i = 0; i < cache.Length; i++)
            {
                if (cache[i].Type != null)
                    population++;
            }

            return population;
        }

        internal unsafe static void AddToExistingCache(CastableObjectCacheEntry[] cache, EEType* interfaceType, object objectForType)
        {
            uint cacheMask = (uint)cache.Length - 1;
            uint bucket = interfaceType->HashCode & cacheMask;
            uint curbucket = bucket;

            // hash algorithm is open addressing with linear probing

            while (curbucket < cache.Length)
            {
                if (cache[curbucket].Type == null)
                {
                    cache[curbucket].Type = interfaceType;
                    cache[curbucket].InstanceObjectForType = objectForType;
                    return;
                }
                curbucket++;
            }

            // Handle wrap-around case
            curbucket = 0;
            while (curbucket < bucket)
            {
                if (cache[curbucket].Type == null)
                {
                    cache[curbucket].Type = interfaceType;
                    cache[curbucket].InstanceObjectForType = objectForType;
                    return;
                }
                curbucket++;
            }

            EH.FallbackFailFast(RhFailFastReason.InternalError, null);
            return;
        }


        /// <summary>
        /// Add the results of a CastableObject call to the cache if possible. (OOM errors may cause caching failure. An OOM is specified not
        /// to introduce new failure points though.)
        /// </summary>
        internal unsafe static void AddToCastableCache(ICastableObject castableObject, EEType* interfaceType, object objectForType)
        {
            CastableObjectCacheEntry[] cache = Unsafe.As<CastableObject>(castableObject).Cache;
            bool setNewCache = false;

            // If there is no cache, allocate one
            if (cache == null)
            {
                try
                {
                    cache = new CastableObjectCacheEntry[8];
                }
                catch (OutOfMemoryException)
                {
                    // Failed to allocate a cache.  That is fine, simply return.
                    return;
                }

                setNewCache = true;
            }

            // Expand old cache if it isn't big enough.
            if (GetCachePopulation(cache) > (cache.Length / 2))
            {
                setNewCache = true;
                CastableObjectCacheEntry[] oldCache = cache;
                try
                {
                    cache = new CastableObjectCacheEntry[oldCache.Length * 2];
                }
                catch (OutOfMemoryException)
                {
                    // Failed to allocate a bigger cache.  That is fine, keep the old one.
                }

                for (int i = 0; i < oldCache.Length; i++)
                {
                    if (oldCache[i].Type != null)
                    {
                        AddToExistingCache(cache, oldCache[i].Type, oldCache[i].InstanceObjectForType);
                    }
                }
            }

            AddToExistingCache(cache, interfaceType, objectForType);

            if (setNewCache)
            {
                Unsafe.As<CastableObject>(castableObject).Cache = cache;
            }

            return;
        }

        internal static unsafe object GetCastableTargetIfPossible(ICastableObject castableObject, EEType *interfaceType, bool produceException, ref Exception exception)
        {
            CastableObjectCacheEntry[] cache = Unsafe.As<CastableObject>(castableObject).Cache;

            object targetObjectInitial = null;

            if (cache != null)
            {
                targetObjectInitial = CastableTargetLookup(cache, interfaceType);
                if (targetObjectInitial != null)
                {
                    if (targetObjectInitial != s_castFailCanary)
                        return targetObjectInitial;
                    else if (!produceException)
                        return null;
                }
            }

            // Call into the object to determine if the runtime can perform the cast. This will return null if it fails.
            object targetObject = castableObject.CastToInterface(new EETypePtr(new IntPtr(interfaceType)), produceException, out exception);

            // If the target object is null, and that result has already been cached, just return null now. 
            // Otherwise, we need to store the canary in the cache so future failing "is" checks can be fast
            if (targetObject == null)
            {
                if (targetObjectInitial != null)
                    return null;
                else
                    targetObject = s_castFailCanary;
            }

            InternalCalls.RhpAcquireCastCacheLock();
            // Assuming we reach here, we should attempt to add the newly discovered targetObject to the per-object cache

            // First, check to see if something is already there

            // we may have replaced the cache object since the earlier acquisition in this method. Re-acquire the cache object
            // here.
            cache = Unsafe.As<CastableObject>(castableObject).Cache;
            object targetObjectInCache = null;

            if (cache != null)
                targetObjectInCache = CastableTargetLookup(cache, interfaceType);

            if (targetObjectInCache == null)
            {
                // If the target object still isn't in the cache by this point, add it now
                AddToCastableCache(castableObject, interfaceType, targetObject);
                targetObjectInCache = targetObject;
            }
            InternalCalls.RhpReleaseCastCacheLock();

            if (targetObjectInCache != s_castFailCanary)
                return targetObjectInCache;
            else
                return null;
        }

        [RuntimeExport("RhpCastableObjectResolve")]
        unsafe private static IntPtr RhpCastableObjectResolve(IntPtr callerTransitionBlockParam, IntPtr pCell)
        {
            IntPtr locationOfThisPointer = callerTransitionBlockParam + TransitionBlock.GetThisOffset();
            object pObject = Unsafe.As<IntPtr, Object>(ref *(IntPtr*)locationOfThisPointer);

            DispatchCellInfo cellInfo;
            InternalCalls.RhpGetDispatchCellInfo(pCell, out cellInfo);
            if (cellInfo.CellType != DispatchCellType.InterfaceAndSlot)
            {
                // Dispatch cell used for castable object resolve is not InterfaceAndSlot. This should not be possible
                // as all metadata based cells should have been converted to interface and slot cells by this time.
                EH.FallbackFailFast(RhFailFastReason.InternalError, null);
                return IntPtr.Zero;
            }

            EEType* pInterfaceType = cellInfo.InterfaceType.ToPointer();

            Exception e = null;
            object targetObject = GetCastableTargetIfPossible((ICastableObject)pObject, pInterfaceType, false, ref e);
            if (targetObject == null)
                EH.FailFastViaClasslib(RhFailFastReason.InternalError, null, pObject.EEType->GetAssociatedModuleAddress());

            Unsafe.As<IntPtr, Object>(ref *(IntPtr*)locationOfThisPointer) = targetObject;

            InternalCalls.RhpSetTLSDispatchCell(pCell);
            return InternalCalls.RhpGetTailCallTLSDispatchCell();
        }
    }
}
