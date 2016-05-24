// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
//
// Implementation for RCWs
//
// NOTE:
//   These source code are being published to InternalAPIs and consumed by RH builds
//   Use PublishInteropAPI.bat to keep the InternalAPI copies in sync
// ---------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text;
using System.Runtime;
using System.Diagnostics.Contracts;
using Internal.NativeFormat;

namespace System
{
    /// <summary>
    /// Helper class to finalize this RCW object
    /// When we have managed class deriving from native class, if it has a finalizer, the finalizer won't
    /// have a call to base __ComObject because compiler doesn't see it. Also, developer can call
    /// SuppressFinalize and by pass the finalizer altogether without knowing that they've by passed the
    /// finalizer of the base __ComObject.
    /// The solution here is to simply rely on another object to do the finalization.
    /// Note that we can't do this in the ComCallableObject's finalizer (we don't have it now) because the
    /// the CCW would have shorter life time than the actual managed object so it might've destroyed the base
    /// RCW before managed class is gone.
    /// </summary>
    internal class RCWFinalizer
    {
        private __ComObject m_comObject;

        internal RCWFinalizer(__ComObject comObject)
        {
            m_comObject = comObject;
        }

        ~RCWFinalizer()
        {
            m_comObject.Cleanup(disposing: false);
        }
    }

    /// <summary>
    /// This is the weakly-typed RCW and also base class of all strongly-typed RCWs
    /// NOTE: Managed debugger depends on type name: "System.__ComObject"
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    public unsafe class __ComObject : ICastable
    {
        #region Private variables

        /// <summary>
        /// RCW Identity interface pointer + context
        /// This supports all the cross-apartment marshalling
        /// </summary>
        ContextBoundInterfacePointer m_baseIUnknown;

        /// <summary>
        /// Base IUnknown of this RCW. When we QI we'll be using this IUnknown
        /// Note that this is not necessarily the identity IUnknown, which is why I name it "Base" IUnknown
        /// If this is default(IntPtr), this RCW is not initialized yet
        /// </summary>
        internal IntPtr BaseIUnknown_UnsafeNoAddRef { get { return m_baseIUnknown.ComPointer_UnsafeNoAddRef; } }

        /// <summary>
        /// Internal RCW ref count
        /// NOTE: this is different from ref count on the underlying COM object
        /// Each time when you marshal a native pointer into a RCW, the RCW gets one AddRef
        /// Typically you don't need to release as garbage collector will automatically take care of it
        /// But you can call Marshal.ReleaseComObject if you want explicit release it as early as possible
        /// </summary>
        int m_refCount;

        /// <summary>
        /// Flags of this __ComObject. See ComObjectFlags for the possible value
        /// </summary>
        ComObjectFlags m_flags;

        /// <summary>
        /// A reference to CCW
        /// This makes sure the lifetime of the CCW and this RCW is tied together in aggregation scenario
        /// </summary>
        ComCallableObject m_outer;

        /// <summary>
        /// Saved identity IUnknown vtbl at creation time
        /// This is mostly used as a way to diagnose what the underlying COM object really is (if the vtbl
        /// is still there, of course) in case the COM object was destroyed due to an extra release
        /// </summary>
        IntPtr m_savedIUnknownVtbl;

        internal IntPtr SavedIUnknownVtbl { get { return m_savedIUnknownVtbl; } }

        /// <summary>
        /// Fixed array of cached interfaces that are lock-free
        /// @TODO: Make this a struct instead of an array object
        /// NOTE: Managed Debugger depends on field name "m_cachedInterfaces" and field type:SimpleComInterfaceCacheItem
        /// Update managed debugger whenever field name/field type is changed.
        /// See CordbObjectValue::GetInterfaceData in debug\dbi\values.cpp
        /// </summary>
        internal SimpleComInterfaceCacheItem[] m_cachedInterfaces;
        internal const int FIXED_CACHE_SIZE = 8;

        /// <summary>
        /// Growable additional cached interfaces.  Access this via AcquireAdditionalCacheExclusive/ForRead
        /// NOTE: Managed Debugger depends on field name: "m_additionalCachedInterfaces_dontAccessDirectly" and field type: AdditionalComInterfaceCacheContext
        /// Update managed debugger whenever field name/field type is changed.
        /// See CordbObjectValue::GetInterfaceData in debug\dbi\values.cpp
        /// </summary>
        private AdditionalComInterfaceCacheContext[] m_additionalCachedInterfaces_dontAccessDirectly;

        // if m_additionalCachedInterfaces_dontAccessDirectly == CacheLocked, the cache is being updated and
        // cannot be read or written from another thread.  We do this instead of using a "real" lock, to save space.
        private static readonly AdditionalComInterfaceCacheContext[] CacheLocked = new AdditionalComInterfaceCacheContext[0];

        /// <summary>
        /// Finalizer helper object that does cleanup.
        /// See RCWFinalizer class for more details.
        /// </summary>
        private RCWFinalizer m_finalizer;

        #endregion

        #region Debugging Private Variables

#if DEBUG
        /// <summary>
        /// Runtime class name of this WinRT __ComObject. This is helpful when you want to understand why
        /// you get back a __ComObject instead of a strongly-typed RCW
        /// </summary>
        internal string m_runtimeClassName;

        /// <summary>
        /// sequential allocation ID of this COM object
        /// useful when you are debugging bugs where the program's behavior is deterministic
        /// </summary>
        internal uint m_allocationId;

        /// <summary>
        /// Next allocation ID
        /// Typed as int to make sure InterlockedExchange.Add is happy
        /// </summary>
        internal static int s_nextAllocationId;
#endif

        /// <summary>
        /// Return allocation ID in debug build
        /// INTERNAL only - not in public contract
        /// </summary>
        public uint AllocationId
        {
            get
            {
#if DEBUG
                return m_allocationId;
#else
                return 0xffffffff;
#endif
            }
        }

        #endregion

        /// <summary>
        /// Gets/sets the outer CCW
        /// Only used in aggregation scenarios
        /// We only set the outer CCW during creation of managed object that derives from native
        /// </summary>
        internal ComCallableObject Outer
        {
            get
            {
                return m_outer;
            }
            set
            {
                m_outer = value;
            }
        }

        private AdditionalComInterfaceCacheContext[] AcquireAdditionalCacheExclusive()
        {
            AdditionalComInterfaceCacheContext[] additionalCache;

            SpinWait spin = new SpinWait();

            while ((additionalCache = Interlocked.Exchange(ref m_additionalCachedInterfaces_dontAccessDirectly, CacheLocked)) == CacheLocked)
                spin.SpinOnce();

            return additionalCache;
        }

        private void ReleaseAdditionalCacheExclusive(AdditionalComInterfaceCacheContext[] contexts)
        {
            Debug.Assert(m_additionalCachedInterfaces_dontAccessDirectly == CacheLocked);
            Volatile.Write(ref m_additionalCachedInterfaces_dontAccessDirectly, contexts);
        }

        private AdditionalComInterfaceCacheContext[] AcquireAdditionalCacheForRead()
        {
            SpinWait spin = new SpinWait();
            AdditionalComInterfaceCacheContext[] additionalCache;

            while ((additionalCache = Volatile.Read(ref m_additionalCachedInterfaces_dontAccessDirectly)) == CacheLocked)
                spin.SpinOnce();

            return additionalCache;
        }

        /// <returns>True is added, false if duplication found</returns>
        private bool AddToAdditionalCache(ContextCookie contextCookie, RuntimeTypeHandle interfaceType, IntPtr pComPtr, object adapter, bool checkDup)
        {
            var additionalCache = AcquireAdditionalCacheExclusive();

            bool added = false;

            try
            {
                //
                // Try to find this context
                //
                int firstFree = -1;

                if (additionalCache != null)
                {
                    for (int i = 0; i < additionalCache.Length; i++)
                    {
                        if (additionalCache[i] == null)
                        {
                            if (firstFree == -1)
                            {
                                firstFree = i;
                            }
                        }
                        else if (additionalCache[i].context.ContextCookie.Equals(contextCookie))
                        {
                            return additionalCache[i].Add(interfaceType, pComPtr, adapter, checkDup);
                        }
                    }
                }

                //
                // This is a new context.
                //
                if (firstFree == -1)
                {
                    //
                    // Need a bigger array
                    //
                    AdditionalComInterfaceCacheContext[] newCache;

                    if (additionalCache != null)
                    {
                        newCache = new AdditionalComInterfaceCacheContext[additionalCache.Length + 1];
                        Array.Copy(additionalCache, newCache, additionalCache.Length);
                        firstFree = additionalCache.Length;
                    }
                    else
                    {
                        newCache = new AdditionalComInterfaceCacheContext[1];
                        firstFree = 0;
                    }

                    additionalCache = newCache;
                }

                var newContext = new AdditionalComInterfaceCacheContext(contextCookie);
                added = newContext.Add(interfaceType, pComPtr, adapter, checkDup);
                Volatile.Write(ref additionalCache[firstFree], newContext);
            }
            finally
            {
                ReleaseAdditionalCacheExclusive(additionalCache);
            }

            return added;
        }

        #region Constructor and Finalizer

        /// <summary>
        /// Default constructor for RCW 'new' code path
        /// This only initialize __ComObject to a default, non-usable state
        /// Please use Attach to initialize the RCW
        /// Aggregation requires this to be a two-step process in order to access 'this' pointer
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public __ComObject()
        {
            __InitToDefaultState();
        }

        /// <summary>
        /// Attaching ctor of __ComObject for RCW marshalling code path in order to create a weakly typed RCW
        /// Initialize and Attach to a existing Com Object
        /// if pBaseIUnknown is default(IntPtr), does initialization only
        /// </summary>
        /// <param name="pBaseIUnknown">Base IUnknown*. Could be Zero</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal __ComObject(IntPtr pBaseIUnknown, RuntimeTypeHandle classType)
        {
            this.__AttachingCtor(pBaseIUnknown, classType);
        }

        /// <summary>
        /// Attaching ctor used by CreateComObjectInternal. Always used following RhNewObject call therefore the
        /// default constructor is not ran. Other code should call __Attach/__AttachAndRelease (which assumes
        /// default constructor has ran)
        /// </summary>
        /// <remarks>
        /// 'int' return value type is a dummy here, it's used because of a limitation on our AddrOf/Call support
        /// </remarks>
        internal static int AttachingCtor(__ComObject comObject, IntPtr pBaseIUnknown, RuntimeTypeHandle classType)
        {
            comObject.__AttachingCtor(pBaseIUnknown, classType);
            return 0;
        }

        /// <summary>
        /// Attaching ctor used by CreateComObjectInternal. Always used following RhNewObject call therefore the
        /// default constructor is not ran. Other code should call __Attach/__AttachAndRelease (which assumes
        /// default constructor has ran)
        /// </summary>
        private void __AttachingCtor(IntPtr pBaseIUnknown, RuntimeTypeHandle classType)
        {
            __InitToDefaultState();

            if (pBaseIUnknown != default(IntPtr))
                __Attach(pBaseIUnknown, classType);
        }

        /// <summary>
        /// This method updates the flags to represent the right GCPressureRange that is filtered by the MCG by reading the [Windows.Foundation.Metadata.GcPressureAttribute].
        /// By default all the __ComObject get the default GCPressure.
        /// This method, also calls the GC.AddMemoryPressure(); with the right mappings for different ranges created in GCMemoryPressureConstants
        /// </summary>
        /// <param name="gcMemoryPressureRange"></param>
        private void AddGCMemoryPressure(GCPressureRange gcMemoryPressureRange)
        {
#if ENABLE_WINRT
            switch (gcMemoryPressureRange)
            {
                case GCPressureRange.WinRT_Default:
                    m_flags |= ComObjectFlags.GCPressure_Set;
                    break;
                case GCPressureRange.WinRT_Low:
                    m_flags |= (ComObjectFlags.GCPressureWinRT_Low | ComObjectFlags.GCPressure_Set);
                    break;
                case GCPressureRange.WinRT_Medium:
                    m_flags |= (ComObjectFlags.GCPressureWinRT_Medium | ComObjectFlags.GCPressure_Set);
                    break;
                case GCPressureRange.WinRT_High:
                    m_flags |= (ComObjectFlags.GCPressureWinRT_High | ComObjectFlags.GCPressure_Set);
                    break;
                default:
                    Debug.Assert(false, "Incorrect GCPressure value");
                    return;
            }

            Debug.Assert(IsGCPressureSet);

            GC.AddMemoryPressure(GCMemoryPressure);
#endif
        }

        /// <summary>
        /// This method updates the flags to represent the right GCPressureRange that is filtered by the MCG by reading the [Windows.Foundation.Metadata.GcPressureAttribute].
        /// By default all the __ComObject get the default GCPressure.
        /// This method, also calls the GC.AddMemoryPressure(); with the right mappings for different ranges created in GCMemoryPressureConstants
        /// </summary>
        /// <param name="gcMemoryPressureRange"></param>
        private void UpdateComMarshalingType(ComMarshalingType marshallingType)
        {
            switch (marshallingType)
            {
                case ComMarshalingType.Inhibit:
                    m_flags |= ComObjectFlags.MarshalingBehavior_Inhibit;
                    break;
                case ComMarshalingType.Free:
                    m_flags |= ComObjectFlags.MarshalingBehavior_Free;
                    break;
                case ComMarshalingType.Standard:
                    m_flags |= ComObjectFlags.MarshalingBehavior_Standard;
                    break;
            }
        }

        private ComMarshalingType MarshalingType
        {
            get
            {
                switch (m_flags & ComObjectFlags.MarshalingBehavior_Mask)
                {
                    case ComObjectFlags.MarshalingBehavior_Inhibit:
                        return ComMarshalingType.Inhibit;

                    case ComObjectFlags.MarshalingBehavior_Free:
                        return ComMarshalingType.Free;

                    case ComObjectFlags.MarshalingBehavior_Standard:
                        return ComMarshalingType.Standard;

                    default:
                        return ComMarshalingType.Unknown;
                }
            }
        }

        private bool IsGCPressureSet
        {
            get
            {
                return ((m_flags & ComObjectFlags.GCPressure_Set) != 0);
            }
        }

        /// <summary>
        /// This property creates the mapping between the GCPressure ranges to the actual memory pressure in bytes per RCW.
        /// The different mapping ranges are defined in GCMemoryPressureConstants
        /// </summary>
#if ENABLE_WINRT
        private int GCMemoryPressure
        {
            get
            {
                Contract.Assert(IsGCPressureSet, "GCPressureRange.Unknown");

                switch (m_flags & ComObjectFlags.GCPressureWinRT_Mask)
                {
                    case ComObjectFlags.GCPressureWinRT_Low: return GCMemoryPressureConstants.GC_PRESSURE_WINRT_LOW;

                    case ComObjectFlags.GCPressureWinRT_Medium: return GCMemoryPressureConstants.GC_PRESSURE_WINRT_MEDIUM;

                    case ComObjectFlags.GCPressureWinRT_High: return GCMemoryPressureConstants.GC_PRESSURE_WINRT_HIGH;

                    default: return GCMemoryPressureConstants.GC_PRESSURE_DEFAULT;
                }
            }
        }
#endif

        /// <summary>
        /// Initialize RCW to default state
        /// </summary>
        private void __InitToDefaultState()
        {
            m_flags = ComObjectFlags.None;
            m_refCount = 1;
            m_cachedInterfaces = new SimpleComInterfaceCacheItem[FIXED_CACHE_SIZE];
#if DEBUG
            m_allocationId = (uint)Interlocked.Add(ref s_nextAllocationId, 1);
#endif
        }

        private unsafe IntPtr GetVtbl(IntPtr pUnk)
        {
            return new IntPtr((*(void**)pUnk));
        }

        /// <summary>
        /// Attach this RCW to a IUnknown *
        /// NOTE: This function is not CLS-compliant but we'll only call this from C# code.
        /// The '__' prefix is added to avoid name conflict in sub classes
        /// </summary>
        /// <remarks>
        /// Should only be called from RCW 'new' code path
        /// </remarks>
        /// <param name="pBaseIUnknown">IUnknown *. Should never be Zero</param>
        private void __Attach(IntPtr pBaseIUnknown)
        {
            __Attach(pBaseIUnknown, this.GetTypeHandle());
        }

        /// <summary>
        /// Attach this RCW to a IUnknown *
        /// NOTE: This function is not CLS-compliant but we'll only call this from C# code.
        /// The '__' prefix is added to avoid name conflict in sub classes
        /// </summary>
        /// <param name="pBaseIUnknown">IUnknown *. Should never be Zero</param>
        private void __Attach(IntPtr pBaseIUnknown, RuntimeTypeHandle classType)
        {
            Debug.Assert(pBaseIUnknown != default(IntPtr));

            //
            // Read information from classType and apply on the RCW
            //
            if (!classType.IsNull())
            {
                GCPressureRange gcPressureRange = classType.GetGCPressureRange();
                if (gcPressureRange != GCPressureRange.None)
                    AddGCMemoryPressure(gcPressureRange);

                UpdateComMarshalingType(classType.GetMarshalingType());
            }

            // Save the IUnknown vtbl for debugging in case the object has been incorrectly destroyed
            m_savedIUnknownVtbl = GetVtbl(pBaseIUnknown);

            m_baseIUnknown.Initialize(pBaseIUnknown, MarshalingType);

#if ENABLE_WINRT
            IntPtr pJupiterObj =
                McgMarshal.ComQueryInterfaceNoThrow(pBaseIUnknown, ref Interop.COM.IID_IJupiterObject);

            if (pJupiterObj != default(IntPtr))
            {
                m_flags |= ComObjectFlags.IsJupiterObject;

                m_cachedInterfaces[0].Assign(pJupiterObj, InternalTypes.IJupiterObject);
                RCWWalker.OnJupiterRCWCreated(this);

                //
                // If this COM object is aggregated, don't keep a ref count on IJupiterObject
                // Otherwise this would keep the CCW alive and therefore keeping this RCW alive, forming
                // a cycle
                //
                if (IsAggregated)
                    McgMarshal.ComRelease(pJupiterObj);

                pJupiterObj = default(IntPtr);
            }
#endif
            // Insert self into global cache, assuming pBaseIUnknown *is* the identity
            if (!ComObjectCache.Add(pBaseIUnknown, this))
            {
                // Add failed - this means somebody else beat us in creating the RCW
                // We need to make this RCW a duplicate RCW
                m_flags |= ComObjectFlags.IsDuplicate;
            }

            if (IsJupiterObject)
            {
                RCWWalker.AfterJupiterRCWCreated(this);
            }

            // Register for finalization of this RCW
            m_finalizer = new RCWFinalizer(this);

            if (InteropEventProvider.IsEnabled())
                InteropEventProvider.Log.TaskRCWCreation(
                    (long)InteropExtensions.GetObjectID(this),
                    this.GetTypeHandle().GetRawValue().ToInt64(),
#if ENABLE_WINRT
                    McgComHelpers.GetRuntimeClassName(this),
#else
                    null,
#endif
                    (long)ContextCookie.pCookie,
                    (long)m_flags);
        }

        /// <summary>
        /// Attach RCW to the returned interface pointer from the factory and release the extra release
        /// Potentially we could optimize RCW to "swallow" the extra reference and avoid an extra pair of
        /// AddRef & Release
        /// </summary>
        [CLSCompliant(false)]
        public void __AttachAndRelease(IntPtr pBaseIUnknown)
        {
            try
            {
                if (pBaseIUnknown != default(IntPtr))
                {
                    __Attach(pBaseIUnknown);
                }
            }
            finally
            {
                McgMarshal.ComSafeRelease(pBaseIUnknown);
            }
        }

#endregion

#region Properties

        /// <summary>
        /// Whether this __ComObject represents a Jupiter UI object that implements IJupiterObject for life
        /// time tracking purposes
        /// </summary>
        internal bool IsJupiterObject
        {
            [GCCallback]
            get
            {
                return (m_flags & ComObjectFlags.IsJupiterObject) != 0;
            }
        }

        /// <summary>
        /// Whether this RCW is used as a baseclass of a managed class. For example, MyButton: Button
        /// </summary>
        internal bool ExtendsComObject
        {
            get
            {
                return (m_flags & ComObjectFlags.ExtendsComObject) != 0;
            }

            set
            {
                //
                // NOTE: This isn't thread safe, but you are only supposed to call this from the constructor
                // anyway from MCG inside a ctor
                //
                if (value)
                    m_flags |= ComObjectFlags.ExtendsComObject;
                else
                    m_flags &= (~ComObjectFlags.ExtendsComObject);
            }
        }

        /// <summary>
        /// Whether this RCW/underlying COM object is being aggregated.
        /// Note that ExtendsComObject is not necessarily the same as aggregation. It just that this is true
        /// in .NET Native (but not true in desktop CLR, where extends a COM object could mean either
        /// aggregation or containment, depending on whether the underlying COM objects supports it)
        /// </summary>
        internal bool IsAggregated
        {
            get
            {
                // In .NET Native - extending a COM base object means aggregation
                return ExtendsComObject;
            }
        }

#endregion

#region Jupiter Lifetime

        /// <remarks>
        /// WARNING: This function might be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal unsafe __com_IJupiterObject* GetIJupiterObject_NoAddRef()
        {
            Debug.Assert(IsJupiterObject);
            RuntimeTypeHandle interfaceType;
            Debug.Assert(m_cachedInterfaces[0].TryGetType(out interfaceType) && interfaceType.IsIJupiterObject());

            // Slot 0 is always IJupiterObject*
            return (__com_IJupiterObject*)m_cachedInterfaces[0].GetPtr().ToPointer();
        }

#endregion

#region Lifetime Management

        /// <summary>
        /// AddRef on the RCW
        /// See m_refCount for more details
        /// </summary>
        internal int AddRef()
        {
            int newRefCount = Threading.Interlocked.Increment(ref m_refCount);

            if (InteropEventProvider.IsEnabled())
                InteropEventProvider.Log.TaskRCWRefCountInc((long)InteropExtensions.GetObjectID(this), newRefCount);

            return newRefCount;
        }

        /// <summary>
        /// Release on the RCW
        /// See m_refCount for more details
        /// </summary>
        internal int Release()
        {
            int newRefCount = Threading.Interlocked.Decrement(ref m_refCount);

            if (InteropEventProvider.IsEnabled())
                InteropEventProvider.Log.TaskRCWRefCountDec((long)InteropExtensions.GetObjectID(this), newRefCount);

            if (newRefCount == 0)
            {
                Cleanup(disposing: true);
            }

            return newRefCount;
        }

        /// <summary>
        /// Completely release the RCW by setting m_refCount to 0
        /// </summary>
        internal void FinalReleaseSelf()
        {
            int prevCount = Threading.Interlocked.Exchange(ref m_refCount, 0);

            if (prevCount > 0)
            {
                Cleanup(disposing: true);
            }
        }

        /// <summary>
        /// Returns the current ref count
        /// </summary>
        internal int PeekRefCount()
        {
            return m_refCount;
        }

        internal void Cleanup(bool disposing)
        {
            //
            // If the RCW hasn't been initialized yet or has already cleaned by ReleaseComObject - skip
            //
            if (m_baseIUnknown.IsDisposed)
            {
                return;
            }

            RCWFinalizer rcwFinalizer = Interlocked.Exchange(ref m_finalizer, null);

            //
            // Another thread is attempting to clean up this __ComObject instance and we lost the race
            //
            if (rcwFinalizer == null)
            {
                return;
            }

            //
            // If the cleanup is not being performed by RCWFinalizer on the finalizer thread then suppress the 
            // finalization of RCWFinalizer since we don't need it
            //
            if (disposing)
            {
                GC.SuppressFinalize(rcwFinalizer);
            }

            if (InteropEventProvider.IsEnabled())
                InteropEventProvider.Log.TaskRCWFinalization((long)InteropExtensions.GetObjectID(this), this.m_refCount);

            //
            // Remove self from cache if this RCW is not a duplicate RCW
            // Duplicate RCW is not stored in the cache
            //
            if (!IsDuplicate)
                ComObjectCache.Remove(m_baseIUnknown.ComPointer_UnsafeNoAddRef, this);

            //
            // Check if we're in the right context for our base IUnknown
            //
            ContextEntry baseContext = m_baseIUnknown.ContextEntry;
            bool inBaseContext = IsFreeThreaded || baseContext.IsCurrent;

            //
            // We didn't AddRef on cached interfaces if this is an aggregated COM object
            // So don't release either
            //
            if (!IsAggregated)
            {
                //
                // For Jupiter objects, start with index 1 because we need the IJupiterObject* to call
                // BeforeRelease
                //
                int startIndex = 0;

                if (IsJupiterObject)
                {
                    RuntimeTypeHandle zeroSlotInterfaceType;
                    Debug.Assert(m_cachedInterfaces[0].TryGetType(out zeroSlotInterfaceType) && zeroSlotInterfaceType.IsIJupiterObject());
                    startIndex = 1;
                }

                //
                // Disposing simple fixed cache
                //
                for (int i = startIndex; i < FIXED_CACHE_SIZE; ++i)
                {
                    IntPtr ptr;
                    if (m_cachedInterfaces[i].TryGetPtr(out ptr))
                    {
                        if (IsJupiterObject)
                            RCWWalker.BeforeRelease(this);

                        if (inBaseContext)
                            McgMarshal.ComRelease(ptr);
                        else
                            baseContext.EnqueueDelayedRelease(ptr);
                    }
                }

                //
                // Disposing additional cache
                //
                AdditionalComInterfaceCacheContext[] cacheContext = AcquireAdditionalCacheForRead();
                if (cacheContext != null)
                {
                    for (int i = 0; i < cacheContext.Length; i++)
                    {
                        var cache = cacheContext[i];
                        if (cache == null) continue;

                        bool isCacheContextCurrent = cache.context.IsCurrent;

                        foreach (var cacheEntry in cache.items)
                        {
                            if (IsJupiterObject)
                                RCWWalker.BeforeRelease(this);

                            if (isCacheContextCurrent)
                                McgMarshal.ComRelease(cacheEntry.ptr);
                            else
                                cache.context.EnqueueDelayedRelease(cacheEntry.ptr);
                        }
                    }
                }
            }

            //
            // Dispose self
            //
            if (IsJupiterObject)
                RCWWalker.BeforeRelease(this);

            m_baseIUnknown.Dispose(inBaseContext);

            //
            // Last step
            // Dispose IJupiterObject*
            //
            if (IsJupiterObject && !IsAggregated)
            {
                RuntimeTypeHandle interfaceType;
                Debug.Assert(m_cachedInterfaces[0].TryGetType(out interfaceType) && interfaceType.IsIJupiterObject());

                RCWWalker.BeforeRelease(this);

                if (inBaseContext)
                    McgMarshal.ComRelease(m_cachedInterfaces[0].GetPtr());
                else
                    baseContext.EnqueueDelayedRelease(m_cachedInterfaces[0].GetPtr());
            }

            if (IsAggregated)
            {
                //
                // Release the extra AddRef that we did when we create the aggregated CCW
                // This makes sure the CCW is released is nobody else is holding on it and delay the cleanup
                // if there is anybody holding on to it until the final release.
                // For example, Jupiter object's release are posted to the STA thread, which means their
                // final release won't get called until the STA thread process them, and this would
                // create a problem if we clean up CCW in RCW finalization and the jupiter object's
                // final release touch the CCW (such as as Release or QI on ICCW).
                //
                m_outer.Release();
            }

#if ENABLE_WINRT
            if (IsGCPressureSet)
                GC.RemoveMemoryPressure(GCMemoryPressure);
#endif
        }

        internal void RemoveInterfacesForContext(ContextCookie currentContext)
        {
            Debug.Assert(currentContext.IsCurrent && !currentContext.IsDefault);

            //
            // Only clean up if this object is context bound, which could be either
            // 1) context-bound and not free threaded
            // 2) is a jupiter object (which is be free-threaded but considered STA)
            //
            if (IsFreeThreaded && !IsJupiterObject)
                return;

            if (m_baseIUnknown.ContextCookie.Equals(currentContext))
            {
                // We cannot use this object any more, as calls to IUnknown will fail.
                FinalReleaseSelf();
            }
            else
            {
                // We know that the base IUnknown is not in this context; therefore nothing in the
                // "simple" cache is in this context.  But we may have marshaled interfaces into this context,
                // and stored them in the "additional" cache.  Remove and release those interfaces now.
                AdditionalComInterfaceCacheContext[] cache = AcquireAdditionalCacheExclusive();

                try
                {
                    if (cache != null)
                    {
                        for (int i = 0; i < cache.Length; i++)
                        {
                            AdditionalComInterfaceCacheContext cacheContext = cache[i];

                            if (cacheContext != null &&
                                cacheContext.context.ContextCookie.Equals(currentContext))
                            {
                                // Remove the context from the cache . Note that there might be
                                // active readers using cache[i] and it's up to the reader to check
                                // if cache[i] is null
                                cache[i] = null;

                                if (!IsAggregated)
                                {
                                    // Release all interfaces in the context
                                    foreach (var cacheEntry in cacheContext.items)
                                    {
                                        if (IsJupiterObject)
                                            RCWWalker.BeforeRelease(this);

                                        McgMarshal.ComRelease(cacheEntry.ptr);
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    ReleaseAdditionalCacheExclusive(cache);
                }
            }
        }

#endregion

#region Properties

        /// <summary>
        /// Whether the RCW is free-threaded
        /// <summary>
        internal bool IsFreeThreaded
        {
            get
            {
                return m_baseIUnknown.IsFreeThreaded;
            }
        }

        /// <summary>
        /// Whether the RCW is a duplicate RCW that is not saved in cache
        /// </summary>
        internal bool IsDuplicate
        {
            get
            {
                return (m_flags & ComObjectFlags.IsDuplicate) != 0;
            }
        }

        /// <summary>
        /// Returns the context cookie where this RCW is created
        /// </summary>
        internal ContextCookie ContextCookie
        {
            get
            {
                return m_baseIUnknown.ContextCookie;
            }
        }

        internal ComObjectFlags Flags
        {
            get
            {
                return m_flags;
            }
        }

        #endregion

        #region QueryInterface
        /// <summary>
        /// QueryInterface for the specified IID and returns a Non-AddRefed COM interface pointer for the
        /// the interface you've specified. The returned interface pointer is always callable from current
        /// context
        /// NOTE: This version uses RuntimeTypeHandle and is much faster than GUID version
        /// </summary>
        /// <returns>A non-AddRef-ed interface pointer that is callable under current context</returns>
        private int QueryInterface_NoAddRef(
            RuntimeTypeHandle interfaceType,
            bool cacheOnly,
            out IntPtr pComPtr)
        {
#if !RHTESTCL
            // Throw if the underlying object is already disposed.
            if (m_baseIUnknown.IsDisposed)
            {
                throw new InvalidComObjectException(SR.Excep_InvalidComObject_NoRCW_Wrapper);
            }
#endif

            ContextCookie currentCookie = ContextCookie.Default;

            //
            // Do we have an existing cached interface in the simple cache that matches
            //

            // For free-threaded RCWs we don't care about context
            bool matchContext = m_baseIUnknown.IsFreeThreaded;

            if (!matchContext)
            {
                // In most cases WinRT objects are free-threaded, so we'll usually skip the context cookie
                // check
                // If we did came here, initialize the currentCookie for use later and check whether the
                // coookie matches
                currentCookie = ContextCookie.Current;
                matchContext = currentCookie.Equals(m_baseIUnknown.ContextCookie);
            }

            if (matchContext)
            {
                //
                // Search for simple fixed locking cache where context always match
                // NOTE: it is important to use Length instead of the constant because the compiler would
                // eliminate the range check (for the most part) when we are using Length
                //
                for (int i = 0; i < m_cachedInterfaces.Length; ++i)
                {
                    //
                    // Check whether this is the same COM interface as cached
                    //
                    if(m_cachedInterfaces[i].TryReadCachedNativeInterface(interfaceType, out pComPtr))
                    {
                        return Interop.COM.S_OK;
                    }
                }
            }

            //
            // No match found in the simple interface cache
            // Proceed to the slow path only if we want to do the actual cache look-up.
            //
            return QueryInterface_NoAddRef_Slow(interfaceType, ref currentCookie, cacheOnly, out pComPtr);
        }

        /// <summary>
        /// QueryInterface for the specified IID and returns a Non-AddRefed COM interface pointer for the
        /// the interface you've specified. The returned interface pointer is always callable from current
        /// context
        /// NOTE: This version uses RuntimeTypeHandle and is much faster than GUID version
        /// </summary>
        /// <returns>A non-AddRef-ed interface pointer that is callable under current context</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IntPtr QueryInterface_NoAddRef_Internal(
            RuntimeTypeHandle interfaceType,
            bool cacheOnly = false,
            bool throwOnQueryInterfaceFailure = true)
        {

#if !RHTESTCL
            // Throw if the underlying object is already disposed.
            if (m_baseIUnknown.IsDisposed)
            {
                throw new InvalidComObjectException(SR.Excep_InvalidComObject_NoRCW_Wrapper);
            }
#endif
            bool matchContext = m_baseIUnknown.IsFreeThreaded || m_baseIUnknown.ContextCookie.Equals(ContextCookie.Current);
            if (matchContext)
            {
                //
                // Search for simple fixed locking cache where context always match
                // NOTE: it is important to use Length instead of the constant because the compiler would
                // eliminate the range check (for the most part) when we are using Length
                //
                int i = 0;
                do
                {
                    IntPtr cachedComPtr;
                    if (m_cachedInterfaces[i].TryReadCachedNativeInterface(interfaceType, out cachedComPtr))
                    {
                        return cachedComPtr;
                    }
                } while(++i < m_cachedInterfaces.Length);
            }

            IntPtr pComPtr;
            ContextCookie currentCookie = ContextCookie.Current;
            //
            // No match found in the simple interface cache
            // Proceed to the slow path only if we want to do the actual cache look-up.
            //
            int hr = QueryInterface_NoAddRef_Slow(interfaceType, ref currentCookie, cacheOnly, out pComPtr);
            if (throwOnQueryInterfaceFailure && pComPtr == default(IntPtr))
            {
                throw CreateInvalidCastExceptionForFailedQI(interfaceType, hr);
            }
            return pComPtr;
        }

        /// <summary>
        /// Slow path of QueryInterface that does not look at any cache.
        /// NOTE: MethodImpl(NoInlining) is necessary becauase Bartok is trying to be "helpful" by inlining
        /// these calls while in other cases it does not inline when it should.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int QueryInterface_NoAddRef_SlowNoCacheLookup(
            RuntimeTypeHandle interfaceType,
            ContextCookie currentCookie,
            out IntPtr pComPtr)
        {

#if ENABLE_WINRT
            // Make sure cookie is initialized
            Debug.Assert(!currentCookie.IsDefault);
#endif

            // Before we QI, we need to make sure we always QI in the right context by retrieving
            // the right IUnknown under current context
            // NOTE: This IUnknown* is AddRef-ed
            //
            if (m_baseIUnknown.IsFreeThreaded || m_baseIUnknown.ContextCookie.Equals(currentCookie))
            {
                //
                // We are in the right context - we can use the IUnknown directly
                //
                return QueryInterfaceAndInsertToCache_NoAddRef(
                    m_baseIUnknown.ComPointer_UnsafeNoAddRef,
                    interfaceType,
                    currentCookie,
                    out pComPtr);
            }
            else
            {
                //
                // Not in the right context - we need to get the right IUnknown through marshalling
                //
                IntPtr pIUnknown = default(IntPtr);

                try
                {
                    pIUnknown = m_baseIUnknown.GetIUnknownForCurrContext(currentCookie);

                    return QueryInterfaceAndInsertToCache_NoAddRef(
                        pIUnknown,
                        interfaceType,
                        currentCookie,
                        out pComPtr);
                }
                finally
                {
                    if (pIUnknown != default(IntPtr))
                    {
                        McgMarshal.ComRelease(pIUnknown);
                    }
                }
            }
        }

        /// <summary>
        /// Slow path of QueryInterface that looks up additional interface cache and does a QueryInterface if
        /// no match can be found in the cache
        /// NOTE: MethodImpl(NoInlining) is necessary becauase Bartok is trying to be "helpful" by inlining
        /// these calls while in other cases it does not inline when it should.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int QueryInterface_NoAddRef_Slow(
            RuntimeTypeHandle interfaceType,
            ref ContextCookie currentCookie,
            bool cacheOnly,
            out IntPtr pComPtr)
        {
            // Make sure cookie is initialized
            if (currentCookie.IsDefault)
                currentCookie = ContextCookie.Current;

            if (TryGetInterfacePointerFromAdditionalCache_NoAddRef(interfaceType, out pComPtr, currentCookie))
            {
                //
                // We've found a match in the additional interface cache
                //
                return 0;
            }

            if (!cacheOnly)
            {
                return QueryInterface_NoAddRef_SlowNoCacheLookup(interfaceType, currentCookie, out pComPtr);
            }

            pComPtr = default(IntPtr);
            return 0;
        }

        /// <summary>
        /// Do a QueryInterface and insert the returned pointer to the cache
        /// Return the QI-ed interface pointer as a result - no need to release
        /// </summary>
        private int QueryInterfaceAndInsertToCache_NoAddRef(
            IntPtr pIUnknown,
            RuntimeTypeHandle interfaceType,
            ContextCookie currentCookie,
            out IntPtr pComPtr)
        {
            int hr = 0;
            //
            // QI the underlying COM object and insert into cache
            // Cache will assume it is already add-refed, so no need to release
            //
            Guid intfGuid = interfaceType.GetInterfaceGuid();
            pComPtr = McgMarshal.ComQueryInterfaceNoThrow(pIUnknown, ref intfGuid, out hr);
            IntPtr pTempComPtr = pComPtr;

            try
            {
                if (pComPtr == default(IntPtr))
                {
                    if (InteropEventProvider.IsEnabled())
                        InteropEventProvider.Log.TaskRCWQueryInterfaceFailure(
                            (long)InteropExtensions.GetObjectID(this), (long)ContextCookie.pCookie,
                            intfGuid, hr);

                    return hr;
                }

                //
                // Cache the result and zero out pComItf if we want to transfer the ref count
                //
                InsertIntoCache(interfaceType, currentCookie, ref pTempComPtr, false);
                return 0;
            }
            finally
            {
                McgMarshal.ComSafeRelease(pTempComPtr);
            }
        }

        private InvalidCastException CreateInvalidCastExceptionForFailedQI(RuntimeTypeHandle interfaceType, int hr)
        {
#if RHTESTCL
            throw new InvalidCastException();
#elif ENABLE_WINRT
            string comObjectDisplayName = this.GetType().TypeHandle.GetDisplayName();
            string interfaceDisplayName = interfaceType.GetDisplayName();

            if (comObjectDisplayName == null)
            {
                comObjectDisplayName = "System.__ComObject";
            }

            if (interfaceDisplayName == null)
            {
                interfaceDisplayName = SR.MissingMetadataType;
            }

            if (hr == Interop.COM.E_NOINTERFACE && interfaceType.IsWinRTInterface())
            {
                // If this is a WinRT secenario and the failure is E_NOINTERFACE then display the standard
                // InvalidCastException as most developers are not interested in IID's or HRESULTS
                return new InvalidCastException(String.Format(SR.InvalidCast_WinRT, comObjectDisplayName, interfaceDisplayName));
            }
            else
            {
                string errorMessage = ExternalInterop.GetMessage(hr);

                if(errorMessage == null)
                {
                    errorMessage = String.Format("({0} 0x{1:X})", SR.Excep_FromHResult, hr);
                }
                else
                {
                    errorMessage = String.Format("{0} ({1} 0x{2:X})", errorMessage, SR.Excep_FromHResult, hr);
                }

                return new InvalidCastException(String.Format(
                    SR.InvalidCast_Com,
                    comObjectDisplayName,
                    interfaceDisplayName,
                    interfaceType.GetInterfaceGuid().ToString("B").ToUpper(),
                    errorMessage));
            }
#else // !ENABLE_WINRT
            string errorMessage = String.Format("({0} 0x{1:X})", SR.Excep_FromHResult, hr);
            string interfaceDisplayName = interfaceType.GetDisplayName();

            return new InvalidCastException(String.Format(
                   SR.InvalidCast_Com,
                   "__ComObject",
                   interfaceDisplayName,
                   interfaceType.GetInterfaceGuid().ToString("B").ToUpper(),
                   errorMessage));

#endif
        }

#endregion
#region Cache Management

        /// <summary>
        /// Insert COM interface pointer into our cache. The cache will NOT do a AddRef and will transfer
        /// the ref count ownership to itself
        /// Note: this function might introduce duplicates in the cache, but we don't really care
        /// </summary>
        internal void InsertIntoCache(
            RuntimeTypeHandle interfaceType,
            ContextCookie cookie,
            ref IntPtr pComPtr,
            bool checkDup)
        {
            Debug.Assert(cookie.IsCurrent);

            bool cachedInSimpleCache = false;

            //
            // Instantiate the dynamic adapter object, if needed
            //
            ComInterfaceDynamicAdapter adapter = null;

            if (interfaceType.HasDynamicAdapterClass())
            {
                adapter = (ComInterfaceDynamicAdapter)InteropExtensions.RuntimeNewObject(interfaceType.GetDynamicAdapterClassType());
                adapter.Initialize(this);
            }
            else if (m_baseIUnknown.IsFreeThreaded || cookie.Equals(m_baseIUnknown.ContextCookie))
            {
                //
                // Search for a match, or free slots in interface cache only when the context matches
                //
                for (int i = 0; i < FIXED_CACHE_SIZE; ++i)
                {
                    if (m_cachedInterfaces[i].Assign(pComPtr, interfaceType))
                    {
                        cachedInSimpleCache = true;
                        break;
                    }
                    else if (checkDup)
                    {
                        if (m_cachedInterfaces[i].IsMatchingEntry(pComPtr, interfaceType)) // found exact match, no need to store
                        {
                            return; // If duplicate found, skipping clear pComPtr and RCWWalker.AfterAddRef
                        }
                    }
                }
            }

            if (!cachedInSimpleCache)
            {
                if (!AddToAdditionalCache(cookie, interfaceType, pComPtr, adapter, checkDup))
                {
                    return; // If duplicate found, skipping clear pComPtr and RCWWalker.AfterAddRef
                }
            }

            if (!IsAggregated)
            {
                //
                // "Swallow" the ref count and transfer it into our cache if this is not aggregation
                // Optionally call out to jupiter to tell them we've "done an AddRef"
                //
                pComPtr = default(IntPtr);

                if (IsJupiterObject)
                    RCWWalker.AfterAddRef(this);
            }
            else
            {
                //
                // Otherwise, this COM object is aggregated
                // We can't add ref on the interface pointer because this would keep the CCW
                // alive which would keep this RCW alive, forming a cycle.
                // NOTE: Since in aggregation it is invalid to maintain a tear-off's
                // lifetime separately (due to the outer IUnknown delegation), we can safely
                // keep this interface pointer cached without a AddRef
                //
            }
        }

        /// <summary>
        /// Look up additional interface cache that are context-aware, growing cache which requires locking
        /// </summary>
        private bool TryGetInterfacePointerFromAdditionalCache_NoAddRef(RuntimeTypeHandle interfaceType, out IntPtr pComPtr, ContextCookie currentCookie)
        {
            //
            // Search for additional growable interface cache
            //
            AdditionalComInterfaceCacheContext[] cacheContext = AcquireAdditionalCacheForRead();
            if (cacheContext != null)
            {
                for (int i = 0; i < cacheContext.Length; i++)
                {
                    var cache = cacheContext[i];
                    if (cache == null) continue;

                    if (cache.context.ContextCookie.Equals(currentCookie))
                    {
                        foreach (var item in cache.items)
                        {
                            if (item.typeHandle.Equals(interfaceType))
                            {
                                pComPtr = item.ptr;
                                return true;
                            }
                        }
                    }
                }
            }

            pComPtr = default(IntPtr);
            return false;
        }

#endregion

#region ICastable implementation for weakly typed RCWs

        /// <summary>
        /// ================================================================================================
        /// COMMENTS from ICastable.IsInstanceOfInterface
        ///
        /// This is called if casting this object to the given interface type would otherwise fail. Casting
        /// here means the IL isinst and castclass instructions in the case where they are given an interface
        /// type as the target type.
        ///
        /// A return value of true indicates the cast is valid.
        ///
        /// If false is returned when this is called as part of a castclass then the usual InvalidCastException
        /// will be thrown unless an alternate exception is assigned to the castError output parameter. This
        /// parameter is ignored on successful casts or during the evaluation of an isinst (which returns null
        /// rather than throwing on error).
        ///
        /// No exception should be thrown from this method (it will cause unpredictable effects, including the
        /// possibility of an immediate failfast).
        ///
        /// The results of this call are not cached, so it is advisable to provide a performant implementation.
        ///
        /// The results of this call should be invariant for the same class, interface type pair. That is
        /// because this is the only guard placed before an interface invocation at runtime. If a type decides
        /// it no longer wants to implement a given interface it has no way to synchronize with callers that
        /// have already cached this relationship and can invoke directly via the interface pointer.
        /// ================================================================================================
        ///
        /// If this function is called, this means we are being casted with a non-supported interface.
        /// This means:
        /// 1. The object is a weakly-typed RCW __ComObject
        /// 2. The object is a strongly-typed RCW __ComObject derived type, but might support more interface
        /// than what its metadata has specified
        ///
        /// In this case, we perform a QueryInterface to see if we really support that interface
        /// </summary>
        /// <param name="interfaceType">The interface being casted to</param>
        /// <param name="castError">More specific cast failure other than the default InvalidCastException
        /// prepared by the runtime</param>
        /// <returns>True means it is supported. False no. </returns>
        bool ICastable.IsInstanceOfInterface(RuntimeTypeHandle interfaceType, out Exception castError)
        {
            castError = null;
            IntPtr pComPtr;
            try
            {
                pComPtr = QueryInterface_NoAddRef_Internal(interfaceType, /* cacheOnly= */ true, /* throwOnQueryInterfaceFailure= */ false);
                if (pComPtr != default(IntPtr))
                    return true;

                //
                // This is typeHandle for ICollection<KeyValuePair<>> which could be
                // Dictionary or List<KeyValuePair<>>
                //
                RuntimeTypeHandle secondTypeHandle;
                RuntimeTypeHandle firstTypeHandle;
                if (McgModuleManager.TryGetTypeHandleForICollecton(interfaceType, out firstTypeHandle, out secondTypeHandle))
                {
                    if (!firstTypeHandle.IsNull() || !secondTypeHandle.IsNull())
                    {
                        RuntimeTypeHandle resolvedTypeHandle;
                        return TryQITypeForICollection(firstTypeHandle, secondTypeHandle, out resolvedTypeHandle);
                    }
                }

                //
                // any data for interfaceType
                //
                if (!interfaceType.HasInterfaceData())
                    return false;

                //
                // QI for that interfce
                //
                int hr = QueryInterface_NoAddRef(interfaceType, /* cacheOnly= */ false, out pComPtr);
                if (pComPtr != default(IntPtr))
                    return true;

                //
                // Is there a dynamic adapter for the interface?
                //
                if (interfaceType.HasDynamicAdapterClass() && GetDynamicAdapterInternal(interfaceType, default(RuntimeTypeHandle)) != null)
                    return true;

                castError = CreateInvalidCastExceptionForFailedQI(interfaceType, hr);
                return false;
            }
            catch (Exception ex)
            {
                // We are not allowed to leak exception out from here
                // Instead, set castError to the exception being thrown
                castError = ex;
            }

            return false;
        }

        /// <summary>
        ///
        /// ================================================================================================
        /// COMMENTS from ICastable.GetImplType
        ///
        /// This is called as part of the interface dispatch mechanism when the dispatcher logic cannot find
        /// the given interface type in the interface map of this object.
        ///
        /// It allows the implementor to return an alternate class type which does implement the interface. The
        /// interface lookup shall be performed again on this type (failure to find the interface this time
        /// resulting in a fail fast) and the corresponding implemented method on that class called instead.
        ///
        /// Naturally, since the call is dispatched to a method on a class which does not match the type of the
        /// this pointer, extreme care must be taken in the implementation of the interface methods of this
        /// surrogate type.
        ///
        /// No exception should be thrown from this method (it will cause unpredictable effects, including the
        /// possibility of an immediate failfast).
        ///
        /// There is no error path defined here. By construction all interface dispatches will already have
        /// been verified via the castclass/isinst mechanism (and thus a call to IsInstanceOfInterface above)
        /// so this method is expected to succeed in all cases. The contract for interface dispatch does not
        /// include any errors from the infrastructure, of which this is a part.
        ///
        /// The results of this lookup are cached so computation of the result is not as perf-sensitive as
        /// IsInstanceOfInterface.
        /// ==========================================================================================
        ///
        /// If we are here, it means we've previously succeeded in ICastable.IsInstanceOfInterface, and
        /// we need to return the correct stub class that implement the interface so that RH knows how to
        /// dispatch the call, for example:
        ///
        /// class IFoo_StubClass: __ComObject, IFoo
        /// {
        ///     public IFoo.Bar()
        ///     {
        ///         // Interop code for IFoo.Bar goes here
        ///     }
        /// }
        ///
        /// Note that the stub class in this case needs to be compatible in terms of object layout with
        /// 'this', and the most obvious way to get that is to derive from __ComObject
        /// </summary>
        /// <param name="interfaceType">The interface type we need to dispatch</param>
        /// <returns>The stub class where RH dispatch interface calls to</returns>
        RuntimeTypeHandle ICastable.GetImplType(RuntimeTypeHandle interfaceType)
        {
            RuntimeTypeHandle dispatchClassType = interfaceType.GetDispatchClassType();
            if (!dispatchClassType.IsInvalid())
                return dispatchClassType;

            // ICollection<T>/IReadOnlyCollection<T> case
            RuntimeTypeHandle firstICollectionType;
            RuntimeTypeHandle secondICollectionType;
            if (McgModuleManager.TryGetTypeHandleForICollecton(interfaceType, out firstICollectionType, out secondICollectionType))
            {
                RuntimeTypeHandle resolvedICollectionType;
                if (!secondICollectionType.IsNull())
                {
                    // if _ComObject doesn't support QI for first ICollectionType and second ICollectionType,
                    // return interfaceType, so later,we will have invalidcast exception
                    if (!TryQITypeForICollection(firstICollectionType, secondICollectionType, out resolvedICollectionType))
                        return interfaceType;
                }
                else
                {
                    resolvedICollectionType = firstICollectionType;
                }

                dispatchClassType = resolvedICollectionType.GetDispatchClassType();
                if (!dispatchClassType.IsInvalid())
                    return dispatchClassType;
            }

#if !RHTESTCL
            // RCW is discarded for this interface type
            Environment.FailFast(McgTypeHelpers.GetDiagnosticMessageForMissingType(interfaceType));
#else
            Environment.FailFast("RCW is discarded.");
#endif
            // Never hit
            return default(RuntimeTypeHandle);
        }

#endregion

        //
        // Get the dynamic adapter object associated with this COM object for the given interface.
        // If the first typeInfo fails, try the second one. If both fails to get a dynamic adapter
        // this function throws an InvalidCastException
        //
        internal unsafe object GetDynamicAdapter(RuntimeTypeHandle requestedType, RuntimeTypeHandle targetType)
        {
            object result = GetDynamicAdapterInternal(requestedType, targetType);
            if (result == null)
            {
                //
                // We did not find a suitable dynamic adapter for the type. Throw InvalidCastException
                //
                throw new System.InvalidCastException();
            }

            return result;
        }

        /// <summary>
        /// This method resolves the Type for ICollection<KeyValuePair<>> which can't be 
        /// determined statically. 
        /// </summary>
        /// <param name="firstTypeHandle">Type for the Dictionary\ReadOnlyDictionary.</param>
        /// <param name="secondaryTypeHandle">Type for the List or ReadOnlyList.</param>
        /// <param name="resolvedTypeHandle">Type for ICollection<KeyValuePair<>> determined at runtime.</param>
        /// <returns>Success or failure of resolution.</returns>
        private bool TryQITypeForICollection(RuntimeTypeHandle firstTypeHandle, RuntimeTypeHandle secondaryTypeHandle, out RuntimeTypeHandle resolvedTypeHandle)
        {
            IntPtr interfacePtr;

            // In case __ComObject can be type casted to both IDictionary and IList<KeyValuePair<>>
            // we give IDictionary the preference. first Type point to the RuntimeTypeHandle for IDictionary.

            // We first check in the cache for both.
            // We then check for IDictionary first and IList later.

            // In case none of them succeeds we return false with resolvedTypeHandle set to null.
            resolvedTypeHandle = default(RuntimeTypeHandle);

            // We first check the cache for the two interfaces if this does not succeed we then actually
            // go to the query interface check.
            if (!firstTypeHandle.IsNull())
            {
                interfacePtr = QueryInterface_NoAddRef_Internal(firstTypeHandle, /* cacheOnly= */ true, /* throwOnQueryInterfaceFailure= */ false);
                if (interfacePtr != default(IntPtr))
                {
                    resolvedTypeHandle = firstTypeHandle;
                    return true;
                }
            }

            if (!secondaryTypeHandle.IsNull())
            {
                interfacePtr = QueryInterface_NoAddRef_Internal(secondaryTypeHandle, /* cacheOnly= */ true, /* throwOnQueryInterfaceFailure= */ false);
                if (interfacePtr != default(IntPtr))
                {
                    resolvedTypeHandle = secondaryTypeHandle;
                    return true;
                }
            }

            ContextCookie currentCookie = ContextCookie.Current;
            if (!firstTypeHandle.IsNull())
            {
                QueryInterface_NoAddRef_SlowNoCacheLookup(firstTypeHandle, currentCookie, out interfacePtr);
                if (interfacePtr != default(IntPtr))
                {
                    resolvedTypeHandle = firstTypeHandle;
                    return true;
                }
            }

            if (!secondaryTypeHandle.IsNull())
            {
                QueryInterface_NoAddRef_SlowNoCacheLookup(secondaryTypeHandle, currentCookie, out interfacePtr);
                if (interfacePtr != default(IntPtr))
                {
                    resolvedTypeHandle = secondaryTypeHandle;
                    return true;
                }
            }

            return false;
        }

        private unsafe object GetDynamicAdapterUsingQICache(RuntimeTypeHandle requestedType, AdditionalComInterfaceCacheContext[] cacheContext)
        {
            //
            // Fast path: make a first pass through the cache to find an exact match we've already QI'd for.
            //
            if (cacheContext != null)
            {
                for (int i = 0; i < cacheContext.Length; i++)
                {
                    var cache = cacheContext[i];
                    if (cache == null) continue;

                    foreach (AdditionalComInterfaceCacheItem existingType in cache.items)
                    {
                        if (existingType.typeHandle.Equals(requestedType))
                            return existingType.dynamicAdapter;
                    }
                }
            }

            //
            // We may not have QI'd for this interface yet.  Do so now, in case the object directly supports
            // the requested interface.  If we find it, call ourselves again so our fast path will pick it up.
            //
            if (QueryInterface_NoAddRef_Internal(requestedType, /* cacheOnly= */ false, /* throwOnQueryInterfaceFailure= */ false) != default(IntPtr))
                return GetDynamicAdapterInternal(requestedType, default(RuntimeTypeHandle));

            return null;
        }

        private unsafe object GetDynamicAdapterUsingVariance(RuntimeTypeHandle requestedType, AdditionalComInterfaceCacheContext[] cacheContext)
        {
            //
            // We may have already QI'd for an interface of a *compatible* type.  For example, we may be asking for
            // IEnumerable, and we know the object supports IEnumerable<Foo>.  Or we may be asking for
            // IReadOnlyList<object>, but the object supports IReadOnlyList<Foo>.  So we search for any existing
            // adapter that implements the requested interface.
            //
            if (cacheContext != null)
            {
                for (int i = 0; i < cacheContext.Length; i++)
                {
                    var cache = cacheContext[i];
                    if (cache == null) continue;

                    foreach (AdditionalComInterfaceCacheItem existingType in cache.items)
                    {
                        if (existingType.dynamicAdapter != null && InteropExtensions.IsInstanceOfInterface(existingType.dynamicAdapter, requestedType))
                            return existingType.dynamicAdapter;
                    }
                }
            }


            //
            // We may have already QI'd for an interface of a compatible type, but not a type with a dynamic adapter.
            // For example, we've QI'd for IList<T>, and we're now asking for IEnumerable.  Every IList<T> is an IEnumerable<T>,
            // which is an IEnumerable - but we haven't constructed an adapter for IEnumerable<T> yet.
            //
            for (int i = 0; i < m_cachedInterfaces.Length; ++i)
            {
                RuntimeTypeHandle cachedType;
                if (m_cachedInterfaces[i].TryGetType(out cachedType))
                {
                    object adapter = FindDynamicAdapterForInterface(requestedType, cachedType);

                    if (adapter != null)
                        return adapter;
                }
            }

            if (cacheContext != null)
            {
                for (int i = 0; i < cacheContext.Length; i++)
                {
                    var cache = cacheContext[i];
                    if (cache == null) continue;

                    foreach (AdditionalComInterfaceCacheItem existingType in cache.items)
                    {
                        // in a race, it's possible someone else already set up an adapter.
                        if (existingType.dynamicAdapter != null && InteropExtensions.IsInstanceOfInterface(existingType.dynamicAdapter, requestedType))
                            return existingType.dynamicAdapter;

                        object adapter = FindDynamicAdapterForInterface(requestedType, existingType.typeHandle);

                        if (adapter != null)
                            return adapter;
                    }
                }
            }

            //
            // At this point we *could* just go ahead and QI for every known type that is assignable to the requested type.
            // But that is potentially hundreds of types, and we may be making these calls across process boundaries, which
            // would be very expensive.  At any rate, the CLR doesn't do this, so we'll maintain compatibility and simply fail
            // here.
            //
            return null;
        }

        /// <summary>
        /// The GetDynamicAdapterInternal searches for the Dynamic Adapter in the following order:
        /// 1. Search exact match for requestedType in cache and QI
        /// 2. Search exact match for targetType in cache and QI
        /// 3. Search cache using variant rules for requestedType
        /// </summary>
        private unsafe object GetDynamicAdapterInternal(RuntimeTypeHandle requestedType, RuntimeTypeHandle targetType)
        {
            Debug.Assert(requestedType.HasDynamicAdapterClass());

            Debug.Assert(targetType.IsNull() || targetType.HasDynamicAdapterClass());

            AdditionalComInterfaceCacheContext[] cacheContext = AcquireAdditionalCacheForRead();

            //
            //  Try to find an exact match for requestedType in the cache and QI
            //
            object dynamicAdapter = GetDynamicAdapterUsingQICache(requestedType, cacheContext);

            if (dynamicAdapter != null)
                return dynamicAdapter;

            //
            // If targetType is null or targetType and requestedType are same we don't need 
            // process targetType because that will not provide us the required dynamic adapter
            //
            if (!targetType.IsNull() && !requestedType.Equals(targetType))
            {
                dynamicAdapter = GetDynamicAdapterUsingQICache(targetType, cacheContext);

                if (dynamicAdapter != null)
                    return dynamicAdapter;
            }

            //
            // Search for exact matche of requestedType and targetType failed. Try to get dynamic
            // adapter for types using variance.
            //
            return GetDynamicAdapterUsingVariance(requestedType, cacheContext);
        }

        private object FindDynamicAdapterForInterface(RuntimeTypeHandle requestedType, RuntimeTypeHandle existingType)
        {
            if (!existingType.IsNull() && // IJupiterObject has a null InterfaceType
                InteropExtensions.AreTypesAssignable(existingType, requestedType))
            {
                //
                // Now we need to find a type that is assignable *from* the compatible type, and *to* the requested
                // type.  So if we just found IList<T>, and are asking for IEnumerable, we need to find IEnumerable<T>.
                // We can't directly construct a RuntimeTypeHandle for IEnumerable<T> without reflection, so we have to
                // go search McgModuleManager for a suitable type.
                //
                RuntimeTypeHandle intermediateType = McgModuleManager.FindTypeSupportDynamic(
                    type => InteropExtensions.AreTypesAssignable(existingType, type) &&
                            InteropExtensions.AreTypesAssignable(type, requestedType) &&
                            QueryInterface_NoAddRef_Internal(type, /* cacheOnly= */ false, /* throwOnQueryInterfaceFailure= */ false) != default(IntPtr));

                if (!intermediateType.IsNull())
                    return GetDynamicAdapterInternal(intermediateType, default(RuntimeTypeHandle));
            }

            return null;
        }

#if ENABLE_WINRT

        /// <summary>
        /// Try to find matching Property in cached interface
        /// </summary>
        /// <param name="matchingDelegate"></param>
        /// <returns>if it cann't find it, return null</returns>
        internal PropertyInfo GetMatchingProperty(Func<PropertyInfo, bool> matchingDelegate)
        {
            // first check Simple ComInterface Cache
            for (int i = 0; i < m_cachedInterfaces.Length; i++)
            {
                RuntimeTypeHandle cachedType;
                if (m_cachedInterfaces[i].TryGetType(out cachedType))
                {
                    Type interfaceType = InteropExtensions.GetTypeFromHandle(cachedType);
                    foreach (PropertyInfo propertyInfo in interfaceType.GetRuntimeProperties())
                    {
                        if (matchingDelegate(propertyInfo))
                            return propertyInfo;
                    }
                }
            }

            // Check additional interface cache
            AdditionalComInterfaceCacheContext[] cacheContext = AcquireAdditionalCacheForRead();
            if (cacheContext != null)
            {
                for (int i = 0; i < cacheContext.Length; i++)
                {
                    var item = cacheContext[i];
                    if (item != null)
                    {
                        Type interfaceType = InteropExtensions.GetTypeFromHandle(item.items.GetTypeHandle());
                        foreach (var propertyInfo in interfaceType.GetRuntimeProperties())
                        {
                            if (matchingDelegate(propertyInfo))
                                return propertyInfo;
                        }
                    }
                }
            }

            // doesn't find anything
            return null;
        }
#endif

        /// <summary>
        /// This method implements the ToString() method for weakly typed RCWs
        /// 1. Compute whether the __ComObject supports IStringable and cache the value.
        /// 2. If the __ComObject supports IStringable call ToString() of that method.
        /// 3. else call default Object.ToString() method.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
#if ENABLE_WINRT
            string toString;

            if (IStringableHelper.TryGetIStringableToString(this, out toString))
            {
                return toString;
            }
            else
#endif
            {
                return base.ToString();
            }
        }
    }

}

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Represents COM context cookie
    /// NOTE: The cookie could become INVALID if the context/apartment is gone and reused for another context!!!
    /// It is only safe if you use it with ContextEntry which can make sure the context cookie can always
    /// be valid
    /// </summary>
    internal struct ContextCookie
    {
        internal IntPtr pCookie;

        private ContextCookie(IntPtr _pCookie)
        {
            pCookie = _pCookie;
        }

        /// <summary>
        /// Returns the default context cookie
        /// </summary>
        static internal ContextCookie Default
        {
            get
            {
                return new ContextCookie(default(IntPtr));
            }
        }

        /// <summary>
        /// Whether this is a default context cookie
        /// </summary>
        internal bool IsDefault
        {
            get
            {
                return pCookie == default(IntPtr);
            }
        }

        /// <summary>
        /// Whether the two context cookie are the same
        /// </summary>
        internal bool Equals(ContextCookie cookie)
        {
            return (this.pCookie == cookie.pCookie);
        }

        /// <summary>
        /// Whether this context cookie matches the current context
        /// NOTE: This does a P/Invoke so try to avoid this whenever possible
        /// </summary>
        internal bool IsCurrent
        {
            get
            {
                return (Current.pCookie == this.pCookie);
            }
        }

        /// <summary>
        /// Retrieve ContextCookie of current apartment
        /// NOTE: This does a P/Invoke so try to cache this whenever possible
        /// </summary>
        /// <returns>The current context cookie</returns>
        static internal ContextCookie Current
        {

            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
#if ENABLE_WINRT

                IntPtr pCookie;
                int hr = ExternalInterop.CoGetContextToken(out pCookie);
                if (hr < 0)
                {
                    Debug.Assert(false, "CoGetContextToken failed");
                    pCookie = default(IntPtr);
                }
                return new ContextCookie(pCookie);

#else
                return ContextCookie.Default;
#endif
            }
        }
    }

    /// <summary>
    /// Comparer for ContextCookie
    /// </summary>
    internal class ContextCookieComparer : IEqualityComparer<ContextCookie>
    {
        bool IEqualityComparer<ContextCookie>.Equals(ContextCookie x, ContextCookie y)
        {
            return x.Equals(y);
        }

        int IEqualityComparer<ContextCookie>.GetHashCode(ContextCookie obj)
        {
            return obj.pCookie.GetHashCode();
        }
    }

    /// <summary>
    /// Marshaling type of a ContextBoundInterfacePointer
    /// </summary>
    internal enum ComMarshalingType
    {
        /// <summary>
        /// It has not yet been computed or the MarshalingBehavior[MarshalingType.InvalidMarshaling] or we found an unknown value in the attribute itself.
        /// </summary>
        Unknown,

        /// <summary>
        /// This COM object does not support marshalling and says so in its WinRT metadata
        /// We should not attempt to do any marshalling and should fail immediately
        /// </summary>
        Inhibit,

        /// <summary>
        /// A free-threaded COM object
        /// This is usually indicated by
        /// 1. This object aggregates FTM
        /// 2. This object implements IAgileObject
        /// 3. This object says so in WinRT metadata
        /// </summary>
        Free,

        /// <summary>
        /// A normal COM interface pointer that supports marshalling
        /// NOTE: This doesn't mean the actual underlying object supports marshalling - it only means
        /// we ATTEMPT to do such marshalling
        /// </summary>
        Standard
    }

    /// <summary>
    /// A interface pointer that is COM-context-aware and will always return you the correct marshalled
    /// interface pointer for the current context
    /// </summary>
    internal struct ContextBoundInterfacePointer
    {
        /// <summary>
        /// The COM interface pointer
        /// </summary>
        private IntPtr m_pComPtr;

        /// <summary>
        /// The ContextEntry where this COM interface pointer is obtained
        /// NOTE: Unlike CLR, this is always non-null
        /// </summary>
        private ContextEntry m_context;

        /// <summary>
        /// Stream that contains a marshalled COM pointer that can be unmarshalled to any context
        /// Once we've created a stream, we cache it here.  A thread that needs to do marshaling will first
        /// "check out" this stream by Interlocked.Exchange'ing a null into this field, and using the previous
        /// value.  In a race, another thread might see a null stream, in which case it will just create a new
        /// one.  This should be rare.
        /// </summary>
        private IntPtr m_pCachedStream;

        /// <summary>
        /// The threading type of this ContextBoundInterfacePointer
        /// </summary>
        private ComMarshalingType m_type;

        /// <summary>
        /// Initialize a context bound interface pointer with current context cookie
        /// </summary>
        /// <param name="currentCookie">Passed in for better perf</param>
        internal void Initialize(IntPtr pComPtr, ComMarshalingType type)
        {
            m_pComPtr = pComPtr;
            McgMarshal.ComAddRef(m_pComPtr);

            // Initialize current context
            // NOTE: Unlike CLR, we always initialize the current context cookie
            // This is done to avoid having another m_contextCookie field
            m_context = ContextEntry.GetCurrentContext(ContextCookie.Current);

            // This is a good opportunity to clean up any interfaces that need released in this context
            m_context.PerformDelayedCleanup();

            if (type == ComMarshalingType.Unknown)
            {
                m_type = GetComMarshalingBehaviorAtRuntime();
            }
            else
            {
                m_type = type;
            }

            Contract.Assert(!IsUnknown, "m_type can't be null");
        }

        /// <summary>
        /// Returns a AddRefed COM pointer - you'll need to release it by calling McgMarshal.ComRelease
        /// </summary>
        internal IntPtr GetIUnknownForCurrContext(ContextCookie currentCookie)
        {
            Debug.Assert(currentCookie.IsCurrent);

            // Matching context or FreeThreaded should already be handled
            Debug.Assert(!(m_context.ContextCookie.Equals(currentCookie) || IsFreeThreaded));

#if !RHTESTCL
            if (IsInhibit)
            {
                throw new System.InvalidCastException(SR.Arg_NoMarshalCreatedObjectUsedOutOfTreadContext);
            }
#endif
            return GetAddRefedComPointerForCurrentContext();
        }

        /// <summary>
        /// Returns the "raw" interface pointer without a AddRef
        /// It can only be used as a comparison, or under the context where this context bound interface
        /// pointer is obtained
        /// </summary>
        internal IntPtr ComPointer_UnsafeNoAddRef
        {
            get
            {
                return m_pComPtr;
            }
        }

        /// <summary>
        /// Returns the ContextCookie where this context bound interface pointer is obtained *INITIALLY*
        /// </summary>
        internal ContextCookie ContextCookie
        {
            get
            {
                return m_context.ContextCookie;
            }
        }

        /// <summary>
        /// Returns the ContextEntry where this context bound interface pointer is obtained *INITIALLY*
        /// </summary>
        internal ContextEntry ContextEntry
        {
            get
            {
                return m_context;
            }
        }

        /// <summary>
        /// This method get's the ComMarshalingType by checking the IntPtr at runtime.
        /// </summary>
        /// <returns></returns>
        private ComMarshalingType GetComMarshalingBehaviorAtRuntime()
        {
            Contract.Assert(IsUnknown, "ComMarshalingType is not ComMarshalingType.Uknown");

            if (McgComHelpers.IsFreeThreaded(m_pComPtr))
            {
                // The object is free threaded and hence.
                return ComMarshalingType.Free;
            }

            IntPtr pINoMarshal = McgMarshal.ComQueryInterfaceNoThrow(m_pComPtr, ref Interop.COM.IID_INoMarshal);

            if (pINoMarshal != default(IntPtr))
            {
                McgMarshal.ComSafeRelease(m_pComPtr);
                pINoMarshal = default(IntPtr);

                // This object implements INoMarshal and hence the marshaling across context is inhibited.
                return ComMarshalingType.Inhibit;
            }

            return ComMarshalingType.Standard;
        }

        /// <summary>
        /// Whether this interface pointer represents a free-threaded COM object
        /// </summary>
        internal bool IsFreeThreaded
        {
            get
            {
                return m_type == ComMarshalingType.Free;
            }
        }

        /// <summary>
        /// Whether this interface pointer represents a COM object which can't be marshaled to other contexts.
        /// </summary>
        internal bool IsInhibit
        {
            get
            {
                return m_type == ComMarshalingType.Inhibit;
            }
        }

        /// <summary>
        /// Whether this interface pointer represents a COM object that supports marshalling
        /// </summary>
        internal bool IsStandard
        {
            get
            {
                return m_type == ComMarshalingType.Standard;
            }
        }

        /// <summary>
        /// Whether this interface pointer represents a COM object that supports marshalling
        /// </summary>
        internal bool IsUnknown
        {
            get
            {
                return m_type == ComMarshalingType.Unknown;
            }
        }

        // Used to indicate that there is a marshaled stream, but it's currently in use by some thread.
        const int StreamInUse = 1;

        /// <summary>
        /// Marshales the IUnknown* into a stream and then unmarshal it back to the current context
        /// </summary>
        /// <returns>A AddRef-ed interface pointer that can be used under current context</returns>
        private IntPtr GetAddRefedComPointerForCurrentContext()
        {
            Contract.Assert(IsStandard, "ComMarshalingType is not standard");
            bool failedBefore = false;
            IntPtr pStream = IntPtr.Zero;

            //
            // Acquire the cached stream.
            //
            SpinWait spin = new SpinWait();

            while ((pStream = Interlocked.Exchange(ref m_pCachedStream, (IntPtr)StreamInUse)) == (IntPtr)StreamInUse)
                spin.SpinOnce();

            //
            // Keep retrying until we encounter a critical failure, fail twice, or succeed
            //
            while (true)
            {
                try
                {
                    //
                    // If we have a cached stream, use it.  Otherwise, try to marshal this com pointer 
                    // to a stream.
                    //
                    if (pStream == IntPtr.Zero)
                    {
                        pStream = MarshalComPointerToStream();
                    }

                    if (pStream == IntPtr.Zero)
                    {
                        //
                        // NOTE: This is different than CLR
                        // In CLR we hand out the raw IUnknown pointer and hope it works (and it usually does, 
                        // until it does not).
                        // In ProjectN we try to do the right thing and fail here
                        //
                        throw new InvalidCastException();
                    }
                    else
                    {
                        //
                        // First, reset the stream (ignoring any errors).
                        // 
                        McgComHelpers.SeekStreamToBeginning(pStream);

                        //
                        // Marshalling into stream has succeeded
                        // Now try unmarshal the stream into a pointer of current context
                        //
                        IntPtr pUnknown;
                        int hr = ExternalInterop.CoUnmarshalInterface(pStream, ref Interop.COM.IID_IUnknown, out pUnknown);

                        if (hr < 0)
                        {
                            //
                            // Unmarshalled failed and the stream is now no good.
                            //
                            McgComHelpers.SafeReleaseStream(pStream);
                            pStream = IntPtr.Zero;

                            // If we've already failed before, stop retrying and fail immediately
                            if (failedBefore)
                            {
                                throw new InvalidCastException();
                            }

                            // Remember we've already failed before and avoid infinite loop
                            failedBefore = true;
                        }
                        else
                        {
                            //
                            // Reset the stream (ignoring any errors).
                            // 
                            McgComHelpers.SeekStreamToBeginning(pStream);
                            return pUnknown;
                        }
                    }
                }
                finally
                {
                    Volatile.Write(ref m_pCachedStream, pStream);
                }
            }
        }

        /// <summary>
        /// Marshals m_pComPtr into a IStream*
        /// </summary>
        private IntPtr MarshalComPointerToStream()
        {
            if (!m_context.IsCurrent)
                return MarshalComPointerToStream_InDifferentContext(m_context, m_pComPtr);
            
            // Current Context
            return MarshalComPointerToStream_InCurrentContext(m_pComPtr);
        }

        private static IntPtr MarshalComPointerToStream_InDifferentContext(ContextEntry context, IntPtr pComPtr)
        {
            IntPtr pStream = IntPtr.Zero;
            context.EnterContext(() =>
            {
                pStream = MarshalComPointerToStream_InCurrentContext(pComPtr);
            });
            return pStream;
        }

        private static  IntPtr MarshalComPointerToStream_InCurrentContext(IntPtr pComPtr)
        {
            IntPtr pStream;
            if (MarshalInterThreadInterfaceInStream(ref Interop.COM.IID_IUnknown, pComPtr, out pStream))
                return pStream;

            return IntPtr.Zero;
        }

        /// <summary>
        /// Marshal IUnknown * into a IStream*
        /// </summary>
        /// <returns>True if succeeded, false otherwise</returns>
        private static bool MarshalInterThreadInterfaceInStream(ref Guid iid, IntPtr pUnknown, out IntPtr pRetStream)
        {
            ulong lSize;

            //
            // Retrieve maximum size required
            //
            int hr = ExternalInterop.CoGetMarshalSizeMax(
                out lSize,
                ref Interop.COM.IID_IUnknown,
                pUnknown,
                Interop.COM.MSHCTX.MSHCTX_INPROC,
                IntPtr.Zero,
                Interop.COM.MSHLFLAGS.MSHLFLAGS_TABLESTRONG
            );

            IntPtr pStream = IntPtr.Zero;

            try
            {
                if (hr == Interop.COM.S_OK)
                {
                    //
                    // Create a stream
                    //
                    pStream = __com_IStream.CreateMemStm(lSize);
                    if (pStream != IntPtr.Zero)
                    {
                        //
                        // Masrhal the interface into the stream TABLE STRONG
                        //
                        hr = ExternalInterop.CoMarshalInterface(
                            pStream,
                            ref iid,
                            pUnknown,
                            Interop.COM.MSHCTX.MSHCTX_INPROC,
                            IntPtr.Zero,
                            Interop.COM.MSHLFLAGS.MSHLFLAGS_TABLESTRONG
                        );
                    }
                    else
                    {
                        pRetStream = IntPtr.Zero;
                        return false;
                    }
                }

                if (hr >= 0)
                {
                    if (McgComHelpers.SeekStreamToBeginning(pStream))
                    {
                        //
                        // Everything succeeded - transfer ownership to pRetStream
                        //
                        pRetStream = pStream;
                        pStream = IntPtr.Zero;

                        return true;
                    }
                }

                pRetStream = IntPtr.Zero;
                return false;
            }
            finally
            {
                if (pStream != IntPtr.Zero)
                {
                    McgMarshal.ComRelease(pStream);
                }
            }
        }

        /// <summary>
        /// Whether this has been already disposed
        /// </summary>
        internal bool IsDisposed
        {
            get
            {
                return m_pComPtr == default(IntPtr);
            }
        }

        /// <summary>
        /// Dispose and set status to disposed
        /// </summary>
        internal void Dispose(bool inCurrentContext)
        {
            if (IsDisposed)
                throw new ObjectDisposedException("");

            if (inCurrentContext)
                McgMarshal.ComRelease(m_pComPtr);
            else
                m_context.EnqueueDelayedRelease(m_pComPtr);

            m_pComPtr = default(IntPtr);

            if (m_pCachedStream != default(IntPtr))
                McgComHelpers.SafeReleaseStream(m_pCachedStream);
        }
    }

    /// <summary>
    /// Represents a COM context. It has the following characteristics:
    /// 1. All ContextEntry objects are managed in a global cache, and as a result you won't get duplicated
    /// ContextEntry for the same context.
    /// 2. This ContextEntry keeps a ref count on m_pObjectContext - as a result the context won't become
    /// invalid. This is VERY important
    /// </summary>
    internal class ContextEntry
    {
        /// <summary>
        /// The cookie that represents the COM context
        /// It is used as unique ID
        /// </summary>
        private ContextCookie m_cookie;

        /// <summary>
        /// The AddRefed IObjectContext* for this context
        /// Used for the context transition
        /// The AddRef is real important because it prevents the target context from going away, and prevents
        /// m_cookie and m_pObjectContext becoming invalid
        /// </summary>
        private IntPtr m_pObjectContext;

        /// <summary>
        /// Initialize a new context entry
        /// This is used by GetCurrentContext
        /// After initialization, ref count = 1
        /// </summary>
        /// <param name="cookie">Current cookie. Passed for better perf</param>
        private ContextEntry(ContextCookie cookie)
        {
            // Make sure the cookie is the current context cookie
            // We pass the cookie in for better performance (avoid a P/Invoke)
            Debug.Assert(cookie.IsCurrent);
            m_cookie = cookie;
            m_pObjectContext = IntPtr.Zero;

#if !CORECLR
            int hr = ExternalInterop.CoGetObjectContext(ref Interop.COM.IID_IUnknown, out m_pObjectContext);
            if (hr < 0)
            {
                throw Marshal.GetExceptionForHR(hr);
            }
#endif
        }

        /// <summary>
        /// Returns the context cookie associated with this context
        /// </summary>
        internal ContextCookie ContextCookie
        {
            get
            {
                return m_cookie;
            }
        }

        /// <summary>
        /// Whether this context is the current context
        /// </summary>
        internal bool IsCurrent
        {
            get
            {
                return m_cookie.IsCurrent;
            }
        }

        ~ContextEntry()
        {
            if (!Environment.HasShutdownStarted)
            {
                // m_pObjectContext could be NULL
                McgMarshal.ComSafeRelease(m_pObjectContext);
                m_pObjectContext = default(IntPtr);
            }            
        }

        internal static void RemoveCurrentContext()
        {
            ContextCookie cookie = ContextCookie.Current;
            ComObjectCache.RemoveRCWsForContext(cookie);
            ContextEntryManager.RemoveContextEntry(cookie);
        }

        /// <summary>
        /// User-defined callback type
        /// This callback will be called in the right context with data that was passed to EnterContext
        /// </summary>
        internal delegate void EnterContextCallback();

        /// <summary>
        /// Transition to this context and make the callback in that context
        /// </summary>
        /// <returns>True if succeed. False otherwise</returns>
        internal unsafe bool EnterContext(EnterContextCallback callback)
        {
            //
            // Retrieve the IContextCallback interface from the IObjectContext
            //
            IntPtr pContextCallback =
                McgMarshal.ComQueryInterfaceNoThrow(m_pObjectContext, ref Interop.COM.IID_IContextCallback);

            if (pContextCallback == IntPtr.Zero)
                throw new InvalidCastException();

            //
            // Setup the callback data structure with the callback Args
            //
            Interop.COM.ComCallData comCallData = new Interop.COM.ComCallData();
            comCallData.dwDispid = 0;
            comCallData.dwReserved = 0;

            //
            // Allocate a GCHandle in order to pass a managed class in ComCallData.pUserDefined
            //
            GCHandle gchCallback = GCHandle.Alloc(callback);

            try
            {
                comCallData.pUserDefined = GCHandle.ToIntPtr(gchCallback);

                //
                // Call IContextCallback::ContextCallback to transition into the right context
                // This is a blocking operation
                //
                Interop.COM.__IContextCallback* pContextCallbackNativePtr =
                    (Interop.COM.__IContextCallback*)(void*)pContextCallback;
                fixed (Guid* unsafe_iid = &Interop.COM.IID_IEnterActivityWithNoLock)
                {
                    int hr = CalliIntrinsics.StdCall<int>(
                        pContextCallbackNativePtr->vtbl->pfnContextCallback,
                        pContextCallbackNativePtr,                              // Don't forget 'this pointer
                        AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(EnterContextCallbackProc),
                        &comCallData,
                        unsafe_iid,
                        (int)2,
                        IntPtr.Zero);

                    return (hr >= 0);
                }
            }
            finally
            {
                gchCallback.Free();
            }
        }

        /// <summary>
        /// This is the callback gets called in the right context and we'll call to the user callback with
        /// user supplied data
        /// </summary>
        [NativeCallable]
        private static unsafe int EnterContextCallbackProc(IntPtr ptr)
        {
            Interop.COM.ComCallData* pComCallData = (Interop.COM.ComCallData*)ptr;
            GCHandle gchCallback = GCHandle.FromIntPtr(pComCallData->pUserDefined);
            EnterContextCallback callback = (EnterContextCallback)gchCallback.Target;
            callback();
            return Interop.COM.S_OK;
        }

        /// <summary>
        /// Manage ContextCookie->ContextEntry mapping
        /// </summary>
        internal static class ContextEntryManager
        {
            [ThreadStatic]
            static ContextEntry s_lastContextEntry;

            internal static ContextEntry GetContextEntry(ContextCookie currentCookie)
            {
                ContextEntry last = s_lastContextEntry;

                // Shortcut to skip locking + dictionary lookup
                if ((last != null) && currentCookie.Equals(last.ContextCookie))
                {
                    return last;
                }
                else
                {
                    return GetContextEntrySlow(currentCookie);
                }
            }

            static ContextEntry GetContextEntrySlow(ContextCookie currentCookie)
            {
                try
                {
                    s_contextEntryLock.Acquire();

                    //
                    // Look up ContextEntry based on cache
                    //
                    ContextEntry contextEntry;

                    if (!s_contextEntryCache.TryGetValue(currentCookie, out contextEntry))
                    {
                        //
                        // Not found - create new entry in cache
                        //
                        contextEntry = new ContextEntry(currentCookie);

                        s_contextEntryCache.Add(currentCookie, contextEntry);
                    }

                    s_lastContextEntry = contextEntry; // Update cached entry

                    return contextEntry;
                }
                finally
                {
                    s_contextEntryLock.Release();
                }
            }

            /// <summary>
            /// Remove context entry from global cache
            /// </summary>
            internal static void RemoveContextEntry(ContextCookie cookie)
            {
                try
                {
                    s_contextEntryLock.Acquire();

                    s_lastContextEntry = null;   // Clear cached entry

                    s_contextEntryCache.Remove(cookie);
                }
                finally
                {
                    s_contextEntryLock.Release();
                }
            }

            /// <summary>
            /// Global cache for every contextEntry
            /// This is done to avoid duplication
            /// </summary>
            static System.Collections.Generic.Internal.Dictionary<ContextCookie, ContextEntry> s_contextEntryCache;

            /// <summary>
            /// Lock that protects m_contextEntryCache
            /// </summary>
            static Lock s_contextEntryLock;

            static internal void InitializeStatics()
            {
                s_contextEntryCache = new System.Collections.Generic.Internal.Dictionary<ContextCookie, ContextEntry>(new ContextCookieComparer());

                s_contextEntryLock = new Lock();
            }
        }

        /// <summary>
        /// Retrieve current ContextEntry from cache
        /// </summary>
        /// <param name="currentCookie">Current context cookie. Passed in for better perf</param>
        static internal ContextEntry GetCurrentContext(ContextCookie currentCookie)
        {
            Debug.Assert(currentCookie.IsCurrent);
            return ContextEntryManager.GetContextEntry(currentCookie);
        }

        //
        // Per-context list of interfaces that need to be released, next time we get a chance to do
        // so in this context.
        //
        private Lock m_delayedReleaseListLock = new Lock();
        private System.Collections.Generic.Internal.List<IntPtr> m_delayedReleaseList;

        internal void EnqueueDelayedRelease(IntPtr pComPtr)
        {
            try
            {
                m_delayedReleaseListLock.Acquire();

                if (m_delayedReleaseList == null)
                    m_delayedReleaseList = new System.Collections.Generic.Internal.List<IntPtr>(1);

                m_delayedReleaseList.Add(pComPtr);
            }
            finally
            {
                m_delayedReleaseListLock.Release();
            }
        }

        internal void PerformDelayedCleanup()
        {
            // fast path, hopefully inlined
            if (m_delayedReleaseList != null)
                PerformDelayedCleanupWorker();
        }

        private void PerformDelayedCleanupWorker()
        {
            Debug.Assert(this.IsCurrent);

            System.Collections.Generic.Internal.List<IntPtr> list = null;

            try
            {
                m_delayedReleaseListLock.Acquire();

                if (m_delayedReleaseList != null)
                {
                    list = m_delayedReleaseList;
                    m_delayedReleaseList = null;
                }
            }
            finally
            {
                m_delayedReleaseListLock.Release();
            }

            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    McgMarshal.ComRelease(list[i]);
                }
            }
        }
    }

    /// <summary>
    /// Simplified version of ComInterfaceCacheItem that only contains IID + ptr
    /// The cached interface will always have the same context as the RCW
    /// </summary>
    internal unsafe struct SimpleComInterfaceCacheItem
    {
        /// <summary>
        /// NOTE: Managed debugger depends on field name:"ptr" and field type:IntPtr
        /// Update managed debugger whenever field name/field type is changed.
        /// See CordbObjectValue::GetInterfaceData in debug\dbi\values.cpp
        /// </summary>
        private IntPtr ptr;    // Interface pointer under this context

        /// <summary>
        /// RuntimeTypeHandle is added to the cache to make faster cache lookups.
        /// </summary>
        private RuntimeTypeHandle typeHandle;

        /// <summary>
        /// Indict whether the entry is filled or not
        /// Note: This field should be accessed only through Volatile.Read/Write
        /// </summary>
        private bool hasValue;

        private bool HasValue
        {
            get { 
                return Volatile.Read(ref hasValue);
            }
        }

        /// <summary>
        /// Assign ComPointer/RuntimeTypeHandle
        /// </summary>
        /// <returns>Return true if winning race. False otherwise</returns>
        internal bool Assign(IntPtr pComPtr, RuntimeTypeHandle handle)
        {
            // disable warning for ref volatile
#pragma warning disable 0420
            if (Interlocked.CompareExchange(ref ptr, pComPtr, default(IntPtr)) == default(IntPtr))
            {
                Debug.Assert(!HasValue, "Entry should be empty");
                // We win the race
                // The aforementioned  compareExchange ensures that if the key is there the value will be there
                // too. It doesn't guarantee the correct value of the other key. So we should be careful about retrieving
                // cache item using one key and trying to access the other key of the cache as it might lead to interesting
                // problems
                typeHandle = handle;
                Volatile.Write(ref hasValue, true);

                return true;
            }
            else
            {
                return false;
            }
#pragma warning restore 0420
        }

        /// <summary>
        /// Get Ptr value
        /// </summary>
        /// <returns></returns>
        internal bool TryGetPtr(out IntPtr retIntPtr)
        {
            if (HasValue)
            {
                retIntPtr = ptr;
                return true;
            }
            else
            {
                retIntPtr = default(IntPtr);
                return false;
            }
        }

        internal bool TryGetType(out RuntimeTypeHandle retInterface)
        {
            if (HasValue)
            {
                retInterface = typeHandle;
                return true;
            }
            else
            {
                retInterface = default(RuntimeTypeHandle);
                return false;
            }
        }

        /// <summary>
        /// NOTE: This Func doesn't check whether the item "hasValue" or not
        /// Please make sure 'this' has value, then call this GetPtr(); 
        /// </summary>
        /// <returns></returns>
        internal IntPtr GetPtr()
        {
            Debug.Assert(HasValue, "Please make sure item contains valid data or you can use TryGetTypeInfo instead.");
            return ptr;
        }



        /// <summary>
        /// Check whether current entry matching the passed fields value
        /// </summary>
        internal bool IsMatchingEntry(IntPtr pComPtr, RuntimeTypeHandle interfaceType)
        {
            if (HasValue)
            {
                if (ptr == pComPtr && typeHandle.Equals(interfaceType))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// If current entry's RuntimeTypeHandle equals to the passed RuntimeTypeHandle, then return true and its interface pointer
        /// </summary>
        internal bool TryReadCachedNativeInterface(RuntimeTypeHandle interfaceType, out IntPtr pComPtr)
        {
            if (HasValue)
            {
                if (typeHandle.Equals(interfaceType))
                {
                    pComPtr = ptr;
                    return true;
                }
            }

            pComPtr = default(IntPtr);
            return false;
        }
    }

    /// <summary>
    /// Caches information about a particular COM interface under a particular COM context
    /// </summary>
    internal struct AdditionalComInterfaceCacheItem
    {
        /// <summary>
        /// Create a new cached item
        /// Assumes the interface pointer has already been AddRef-ed
        /// Will transfer the ref count to this data structure
        /// </summary>
        internal AdditionalComInterfaceCacheItem(RuntimeTypeHandle interfaceType, IntPtr pComPtr, object adapter)
        {
            typeHandle = interfaceType;
            ptr = pComPtr;
            dynamicAdapter = adapter;
        }
        /// <summary>
        /// NOTE: Managed debugger depends on field name: "typeHandle" and field type: RuntimeTypeHandle
        /// See CordbObjectValue::WalkAdditionalCacheItem in debug\dbi\values.cpp
        /// </summary>
        internal readonly RuntimeTypeHandle typeHandle;        // Table entry of this cached COM interface
        /// <summary>
        /// NOTE: Managed debugger depends on field name: "ptr" and field type: IntPtr
        /// See CordbObjectValue::WalkAdditionalCacheItem in debug\dbi\values.cpp
        /// </summary>
        internal readonly IntPtr ptr;             // Interface pointer under this context
        internal readonly object dynamicAdapter;  // Adapter object for this type.  Only used for some projected interfaces.
    }

    internal class AdditionalComInterfaceCacheContext
    {
        internal AdditionalComInterfaceCacheContext(ContextCookie contextCookie)
        {
            context = ContextEntry.GetCurrentContext(contextCookie);
        }

        /// <returns>false if duplicate found</returns>
        internal unsafe bool Add(RuntimeTypeHandle interfaceType, IntPtr pComPtr, object adapter, bool checkDup)
        {
            if (checkDup) // checkDup
            {
                foreach (AdditionalComInterfaceCacheItem existingType in items)
                {
                    if (existingType.typeHandle.Equals(interfaceType))
                    {
                        return false;
                    }
                }
            }

            items.Add(new AdditionalComInterfaceCacheItem(interfaceType, pComPtr, adapter));

            return true;
        }

        internal readonly ContextEntry context;
        /// <summary>
        /// NOTE: Managed debugger depends on field name: "items" and field type:WithInlineStorage
        /// Update managed debugger whenever field name/field type is changed.
        /// See CordbObjectValue::GetInterfaceData in debug\dbi\values.cpp
        /// </summary>
        internal LightweightList<AdditionalComInterfaceCacheItem>.WithInlineStorage items;
    }

    [Flags]
    enum ComObjectFlags
    {
        /// <summary>
        /// Default value
        /// </summary>
        None = 0,

        /// <summary>
        /// A Duplicate RCW that has the same identity pointer as another RCW in the cache
        /// This could be created due to a race or intentionally
        /// We don't put duplicate RCWs into our cache
        /// </summary>
        IsDuplicate = 0x1,

        /// <summary>
        /// This RCW represents a Jupiter object
        /// </summary>
        IsJupiterObject = 0x2,

        /// <summary>
        /// Whether this is a managed class deriving from a RCW. For example, managed MyButton class deriving
        /// from native Button class. Button RCW is being aggregated by MyButton CCW
        /// </summary>
        ExtendsComObject = 0x4,

        /// <summary>
        /// These enums are used to find the right GCPressure values.
        /// We have 5 possible states.
        /// a. None.
        /// b. Default
        /// c. GCPressureRange.Low
        /// d. GCPressureRange.Medium
        /// e. GCPressureRange.High
        ///
        /// We basically use 3 bits for the 5 values as follows.
        /// 1. None - Represented by the absence of GCPressure_Set bit.
        /// 2. Default - Presence of only GCPRessure_Set
        /// 3. All the rest are marked by 2 in conjunction with specific GCPressureRange.
        ///
        /// We need to use the state None and Default both since some of the __ComObjects are created without really initiating them with any basePtr and hence while
        /// releasing the ComObject we need to know whether we Added GC.AddMemoryPressure for the given __ComObject or not.
        /// This also ensures 1-1 mapping between the AddMemoryPressure and RemoveMemoryPressure.
        /// </summary>

        GCPressure_Set = 0x8,
        GCPressureWinRT_Low = 0x10,
        GCPressureWinRT_Medium = 0x20,
        GCPressureWinRT_High = 0x30,

        GCPressureWinRT_Mask = 0x30,

        /// <summary>
        /// This represents the types MarshalingBehavior.
        /// </summary>
        MarshalingBehavior_Inhibit = 0x40,
        MarshalingBehavior_Free = 0x80,
        MarshalingBehavior_Standard = 0xc0,

        MarshalingBehavior_Mask = 0xc0,
        // Add other enums here.
    }

#if ENABLE_WINRT
    /// <summary>
    /// This class helps call the IStringableToString() by QI IStringable.ToString() method call.
    /// </summary>
    internal class IStringableHelper
    {
        internal unsafe static bool TryGetIStringableToString(object obj, out string toStringResult)
        {
            bool isIStringableToString = false;
            toStringResult = String.Empty;

            __ComObject comObject = obj as __ComObject;

            if (comObject != null)
            {
                // Check whether the object implements IStringable
                // If so, use IStringable.ToString() behavior
                IntPtr pIStringableItf = comObject.QueryInterface_NoAddRef_Internal(InternalTypes.IStringable, /* cacheOnly= */ false, /* throwOnQueryInterfaceFailure= */ false);

                if (pIStringableItf != default(IntPtr))
                {
                    __com_IStringable* pIStringable = (__com_IStringable*)pIStringableItf;
                    void* unsafe_hstring = null;

                    // The method implements isIStringableToString
                    isIStringableToString = true;

                    try
                    {
                        int hr = CalliIntrinsics.StdCall<int>(
                            pIStringable->pVtable->pfnToString,
                            pIStringable,
                            &unsafe_hstring
                            );

                        GC.KeepAlive(obj);

                        // Don't throw if the call fails
                        if (hr >= 0)
                        {
                            toStringResult = McgMarshal.HStringToString(new IntPtr(unsafe_hstring));
                        }
                    }
                    finally
                    {
                        if (unsafe_hstring != null)
                            McgMarshal.FreeHString(new IntPtr(unsafe_hstring));
                    }
                }
            }

            return isIStringableToString;
        }
    }
#endif

    internal enum GCPressureRange
    {
        None,
        WinRT_Default,
        WinRT_Low,
        WinRT_Medium,
        WinRT_High
    }

#if !RHTESTCL
    /// <summary>
    /// These values are taken from the CLR implementation and can be changed later if perf shows so.
    /// </summary>
    internal struct GCMemoryPressureConstants
    {
#if WIN64
        internal const int GC_PRESSURE_DEFAULT       = 1000;
        internal const int GC_PRESSURE_WINRT_LOW        = 12000;
        internal const int GC_PRESSURE_WINRT_MEDIUM     = 120000;
        internal const int GC_PRESSURE_WINRT_HIGH       = 1200000;
#else
        internal const int GC_PRESSURE_DEFAULT = 750;
        internal const int GC_PRESSURE_WINRT_LOW = 8000;
        internal const int GC_PRESSURE_WINRT_MEDIUM = 80000;
        internal const int GC_PRESSURE_WINRT_HIGH = 800000;
#endif
    }
#endif


    //
    // Base class for all "dynamic adapters."  This allows us to instantiate
    // these via RhNewObject (which requires a default constructor), and also
    // initialize them (via ComInterfaceDynamicAdapter.Initialize).
    //
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ComInterfaceDynamicAdapter
    {
        __ComObject m_comObject;

        internal void Initialize(__ComObject comObject)
        {
            m_comObject = comObject;
        }

        public __ComObject ComObject
        {
            get
            {
                return m_comObject;
            }
        }
    }

    /// <summary>
    /// A global cache for all __ComObject(s)
    /// We do a lookup based on IUnknown to find the corresponding cached __ComObject the cache
    /// </summary>
    /// <remarks>
    /// If a __ComObject is in finalizer queue, it is no longer considered valid in the cache
    /// The reason is to avoid unintentionally resurrecting __ComObject during marshalling and therefore
    /// resurrecting anything that is pointed by __ComObject that might already been finalized.
    /// Therefore, all __ComObject are tracked with short weak GC handles.
    /// </remarks>
    /// <remarks>
    /// NOTE: Not all __ComObject are stored in ComObject cache. __ComObject with IsDuplicated == true
    /// are not saved here, because they are not the "identity" RCW for that specific interface pointer and
    /// there can only be one
    /// </remarks>
    internal static class ComObjectCache
    {
        /// <summary>
        /// Adds the __ComObject into cache
        /// This either insert its into the cache, or updates an inavlid entry with this __ComObject
        /// </summary>
        /// <returns>true if add/update succeeded. false if there is already a valid entry in the cache.
        /// In that case, you should insert it as a duplicate RCW
        /// </returns>
        internal static bool Add(IntPtr pComItf, __ComObject o)
        {
            int hashCode = pComItf.GetHashCode();

            try
            {
                s_lock.Acquire();

                IntPtr handlePtr;

                if (s_comObjectMap.TryGetValue(pComItf, hashCode, out handlePtr))
                {
                    GCHandle handle = GCHandle.FromIntPtr(handlePtr);
                    __ComObject cachedComObject = (__ComObject)handle.Target;

                    if (cachedComObject == null)
                    {
                        //
                        // We have another __ComObject in the cache but it is now in the finalizer queue
                        // We can safely reuse this entry for this new object
                        // This could happen if the same interface pointer is marshalled back to managed
                        // code again
                        //
                        handle.Target = o;
                    }
                    else
                    {
                        //
                        // There is a already a valid __ComObject in the cache
                        // This is a "duplicate" RCW and we don't need to insert it into the cache as
                        // we won't be returning this RCW during lookup
                        //
                        return false;
                    }
                }
                else
                {
                    GCHandle newHandle = GCHandle.Alloc(o, GCHandleType.Weak);
                    handlePtr = GCHandle.ToIntPtr(newHandle);
                    s_comObjectMap.Add(pComItf, handlePtr, hashCode);
                }

                return true;
            }
            finally
            {
                s_lock.Release();
            }
        }

        /// <summary>
        /// Remove entry from the global IUnknown->__ComObject map
        /// </summary>
        internal static void Remove(IntPtr pComItf, __ComObject o)
        {
            Debug.Assert(o != null);

            try
            {
                s_lock.Acquire();

                //
                // Only remove if the entry is indeed in the cache and is a match
                // The entry might not be even in the cache if the following event occurs:
                // 1. a RCW A is created for interface pointer 0x100 and is added into cache with key=0x100
                // 2. RCW A is not referenced anymore and is now in the fnialization queue
                // 3. The same interface pointer 0x100 is marshalled back into managed again - this time
                // we should NOT reuse the same RCW to resurrect it. See the remark section in this class
                // for more details. We will create a new RCW B, and it's not a duplicate.
                // 4. RCW B is also considered dead and enters the finalizer queue
                // 5. RCW A now finally gets its chance to finalize, and remove 0x100 from the cache, as
                // cachedObject is now null (was pointing to RCW B which is now in finalize queue)
                // 5. RCW B eventually gets its chance to finalize, and there is no 0x100 in the cache
                //
                IntPtr handlePtr;

                if (s_comObjectMap.TryGetValue(pComItf, out handlePtr))
                {
                    GCHandle handle = GCHandle.FromIntPtr(handlePtr);
                    Object cachedObject = handle.Target;

                    //
                    // Only remove if
                    // 1) The cached object matches
                    // 2) The cached object is null, which means it is now invalidated and we can
                    // safely remove it now
                    //
                    // If the cached object is non-null and doesn't match, this means there is a new
                    // __ComObject that now occupies this cache entry and we should not remove it
                    //
                    if (cachedObject == o ||
                        cachedObject == null)
                    {
                        //
                        // Remove it from the map and free the handle
                        // The order is very important as GC could come at any time
                        // We want to destroy the handle after we've remove this entry from the list
                        // NOTE: if GC comes before we even remove it from the list, it's OK because we
                        // haven't actually released the interfaces in the RCW yet
                        //
                        s_comObjectMap.Remove(pComItf);
                        handle.Free();
                    }
                }
            }
            finally
            {
                s_lock.Release();
            }
        }

        /// <summary>
        /// Return the corresponding __ComObject that has the IUnknown* as its identity
        /// NOTE: If a __ComObject is in finalizer queue or is resurrected, it is not considered valid and
        /// won't be returned. This is done to avoid resurrection by accident during marshalling
        /// </summary>
        internal static __ComObject Lookup(IntPtr pComItf)
        {
            try
            {
                s_lock.Acquire();

                IntPtr handlePtr;

                if (s_comObjectMap.TryGetValue(pComItf, out handlePtr))
                {
                    object target = GCHandle.FromIntPtr(handlePtr).Target;
                    Debug.Assert(target == null || target is __ComObject);

                    return InteropExtensions.UncheckedCast<__ComObject>(target);
                }

                return null;
            }
            finally
            {
                s_lock.Release();
            }
        }

        internal static void RemoveRCWsForContext(ContextCookie contextCookie)
        {
            try
            {
                s_lock.Acquire();

                System.Collections.Generic.Internal.Dictionary<IntPtr, IntPtr> map = s_comObjectMap;

                for (int i = 0; i < map.GetMaxCount(); i++)
                {
                    IntPtr handlePtr = default(IntPtr);

                    if (map.GetValue(i, ref handlePtr) && (handlePtr != default(IntPtr)))
                    {
                        GCHandle handle = GCHandle.FromIntPtr(handlePtr);
                        __ComObject obj = handle.Target as __ComObject;

                        if (obj != null)
                            obj.RemoveInterfacesForContext(contextCookie);
                    }
                }
            }
            finally
            {
                s_lock.Release();
            }
        }

        static Lock s_lock;

        /// <summary>
        /// Map of all __ComObject instances
        /// This points to an index in s_comObjectList
        /// </summary>
        internal static System.Collections.Generic.Internal.Dictionary<IntPtr, IntPtr> s_comObjectMap;

        internal static void InitializeStatics()
        {
            s_lock = new Lock();

            s_comObjectMap = new System.Collections.Generic.Internal.Dictionary<IntPtr, IntPtr>(101, new EqualityComparerForIntPtr());
        }
    }

    internal sealed class EqualityComparerForIntPtr : EqualityComparer<IntPtr>
    {
        public override bool Equals(IntPtr x, IntPtr y)
        {
            return x == y;
        }

        public override int GetHashCode(IntPtr x)
        {
            return x.GetHashCode();
        }
    }

#if ENABLE_WINRT
    /// <summary>
    /// WinRT factory cache item caching the context + factory RCW
    /// </summary>
    internal struct FactoryCacheItem
    {
        internal ContextEntry contextEntry;
        internal __ComObject factoryObject;
    }

    /// <summary>
    /// WinRT Factory cache
    /// We only remember the last cached factory per context + type
    /// The type part is obvious but the context part is probably less so.
    /// When we retrieve a factory, we must make sure the factory interface pointer is the same as if we
    /// were calling RoGetActivationFactory from the current context. This is VERY important for factories
    /// that are 'BOTH'. If you have a 'BOTH' factory created in STA, and marshal that factory to a MTA,
    /// when you call marshalled factory interface pointer in MTA, you'll get a STA object! That is different
    /// from when you call RoGetActivationFactory in MTA and call the factory which creates a MTA object
    /// </summary>
    internal class FactoryCache
    {
        const int DefaultSize = 11; // Small prime number to avoid resizing dictionary resizing in start up code

        private Lock m_factoryLock = new Lock();
        private System.Collections.Generic.Internal.Dictionary<string, FactoryCacheItem> m_cachedFactories = new System.Collections.Generic.Internal.Dictionary<string, FactoryCacheItem>(DefaultSize);

        private static volatile FactoryCache s_factoryCache;

        static internal FactoryCache Get()
        {
#pragma warning disable 0420
            if (s_factoryCache == null)
            {
                Interlocked.CompareExchange(ref s_factoryCache, new FactoryCache(), null);
            }
#pragma warning restore 0420

            return s_factoryCache;
        }

        /// <summary>
        /// Retrieve the class factory for a specific type
        /// </summary>
        private static unsafe __ComObject GetActivationFactoryInternal(
            string typeName,
            RuntimeTypeHandle factoryItf,
            ContextEntry currentContext)
        {
            IntPtr pFactory = default(IntPtr);
            try
            {
                Guid itfGuid = factoryItf.GetInterfaceGuid();
                ExternalInterop.RoGetActivationFactory(typeName, ref itfGuid, out pFactory);

                return (__ComObject)McgComHelpers.ComInterfaceToComObject(
                    pFactory,
                    factoryItf,
                    default(RuntimeTypeHandle),
                    currentContext.ContextCookie,           // Only want factory RCW from matching context
                    McgComHelpers.CreateComObjectFlags.SkipTypeResolutionAndUnboxing
                    );
            }
            finally
            {
                if (pFactory != default(IntPtr))
                    McgMarshal.ComRelease(pFactory);
            }
        }

        /// <summary>
        /// Retrieves the class factory RCW for the specified class name + IID under the right context
        /// The context part is really important
        /// </summary>
        internal __ComObject GetActivationFactory(string className, RuntimeTypeHandle factoryIntf, bool skipCache = false)
        {
            ContextEntry currentContext = ContextEntry.GetCurrentContext(ContextCookie.Current);

            FactoryCacheItem cacheItem;

            int hashCode = className.GetHashCode();

            if (!skipCache)
            {
                try
                {
                    m_factoryLock.Acquire();

                    if (m_cachedFactories.TryGetValue(className, hashCode, out cacheItem))
                    {
                        if (cacheItem.contextEntry == currentContext)
                        {
                            //
                            // We've found a matching entry
                            //
                            return cacheItem.factoryObject;
                        }
                    }
                }
                finally
                {
                    m_factoryLock.Release();
                }
            }

            //
            // No matching entry found - let's create a new one
            // This is kinda slow so do it outside of a lock
            //
            __ComObject factoryObject = GetActivationFactoryInternal(className, factoryIntf, currentContext);

            cacheItem.contextEntry = currentContext;
            cacheItem.factoryObject = factoryObject;

            if (!skipCache)
            {
                //
                // Insert into or update cache
                //
                try
                {
                    m_factoryLock.Acquire();

                    m_cachedFactories[className] = cacheItem;
                }
                finally
                {
                    m_factoryLock.Release();
                }
            }

            return cacheItem.factoryObject;
        }

    }
#endif
}
