// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//   Jupiter Lifetime Support

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Text;
using System.Reflection;

// Needed for NativeCallable attribute
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// GCCallbackAttribute
    ///
    /// This attribute doesn't do anything yet. It is there to tell whoever's looking at the code to be
    /// very CAREFUL when changing the code that is marked by this attribute
    ///
    /// This attribute marks a function that could be called inside a GC Callback, which by its nature is
    /// very dangerous. Any code inside this function (including the transitive closure) needs to satisfy
    /// the following requirements:
    ///
    /// 1. NO MANAGED ALLOCATION
    ///
    /// The reason of this one is very simple: doing a 'new' could trigger a GC and triggering GC inside GC
    /// would deadlock.
    ///
    /// Therefore, you can't write any code that call 'new', and you have to be VERY careful not to call any
    /// code that could potentially do it - you might be really surprised to find out many of our library
    /// routine does that in surprising places.
    ///
    /// This also implies no boxing, no throwing.
    ///
    /// The best way to make sure is to write your own code.
    ///
    /// 2. NO UNMANAGED CALLI AND NATIVECALLABLE
    ///
    /// The unmanaged calli and reverse calli thunks does a GC wait which again would deadlock. The simple
    /// solution here is to simply converts all of them to managed calls (CalliIntrinsics.Call) and callbacks
    /// (Simply don't add NativeCallable) and only utilize them under x64/arm (where the calling convention
    /// doesn't matter). This works out just fine because x86 isn't a supported platform in .NET Native anyway,
    /// and under x86 we can "safely" just let everything leak.
    ///
    /// See __vtable_IFindDependentWrappers for how to do this
    ///
    /// 3. NO CASTS (and whatever that is reading the EEType)
    ///
    /// GC will mark EETypes with a special bit and this will throw off whatever code that is not "GC-aware".
    ///
    /// </summary>
    class GCCallbackAttribute : Attribute
    {
    }

    /// <summary>
    /// Windows.UI.Xaml.Hosting.IReferenceTracker
    ///
    /// Every Jupiter UI object implements IJupiterObject for lifetime management support
    /// </summary>
    internal unsafe struct __com_IJupiterObject
    {
#pragma warning disable 0649
        public __vtable_IJupiterObject* pVtable;
#pragma warning restore 0649
    }

    /// <summary>
    /// V-table for IJupiterObject
    /// NOTE: no need to implement this v-table because Jupiter implements it, not us
    /// </summary>
    internal unsafe struct __vtable_IJupiterObject
    {
        // rgIUnknown is only used for making sure the rest of the fields have the right offset
#pragma warning disable 0169
        __vtable_IUnknown rgIIUnknown;
#pragma warning restore 0169

        // We never implement these functions - instead, we call Jupiter's implementation
#pragma warning disable 0649
        internal System.IntPtr pfnConnect;
        internal System.IntPtr pfnDisconnect;
        internal System.IntPtr pfnFindDependentWrappers;
        internal System.IntPtr pfnGetJupiterGCManager;
        internal System.IntPtr pfnAfterAddRef;
        internal System.IntPtr pfnBeforeRelease;
        internal System.IntPtr pfnPeg;
#pragma warning restore 0649
    }

    /// <summary>
    /// Windows.UI.Xaml.Hosting.IFindReferenceTargetsCallback
    ///
    /// Jupiter fires this callback to tell us the corresponding CCWs reachable from a specific RCW
    /// </summary>
    internal unsafe struct __com_IFindDependentWrappers
    {
#pragma warning disable 0649
        internal __vtable_IFindDependentWrappers* pVtable;
#pragma warning restore 0649
    }

    /// <summary>
    /// V-table implementation for IFindDependentWrappers
    ///
    /// NOTE: This v-table implementation doesn't rely on the default implementation of IUnknown -
    /// instead it implements its own IUnknown and acts like a COM object by itself
    /// </summary>
    internal unsafe struct __vtable_IFindDependentWrappers
    {
        __vtable_IUnknown rgIIUnknown;
        internal System.IntPtr pfnOnFoundDependentWrapper;

        static __vtable_IFindDependentWrappers s_theVtable;

        internal static __vtable_IFindDependentWrappers* GetVtable()
        {
            s_theVtable.InitVtable();

            // REDHAWK-ONLY: static field storage is unmovable
            fixed (__vtable_IFindDependentWrappers* pVtable = &s_theVtable)
                return pVtable;
        }

        private void InitVtable()
        {
            rgIIUnknown.pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(AddRef__STUB);
            rgIIUnknown.pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(Release__STUB);
            rgIIUnknown.pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(QueryInterface__STUB);
            pfnOnFoundDependentWrapper = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(OnFoundDependentWrapper__STUB);
        }

        /// <summary>
        /// Implements AddRef
        /// </summary>
        /// <remarks>
        /// WARNING: This function might be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        [NativeCallable]
        static int AddRef__STUB(System.IntPtr pComThis)
        {
            // This never gets released from native
            return 1;
        }

        /// <summary>
        /// Implements Release
        /// </summary>
        /// <remarks>
        /// WARNING: This function might be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        [NativeCallable]
        static int Release__STUB(System.IntPtr pComThis)
        {
            // This never gets released from native
            return 1;
        }

        /// <summary>
        /// Implements QueryInterface
        /// We only respond to IUnknown and IFindDependentWrappers
        /// </summary>
        /// <remarks>
        /// WARNING: This function might be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        [NativeCallable]
        static int QueryInterface__STUB(
                    System.IntPtr IntPtr__pComThis,
                    System.IntPtr IntPtr__pIID,
                    System.IntPtr IntPtr__ppvObject)
        {
            __com_IFindDependentWrappers* pComThis = (__com_IFindDependentWrappers*)IntPtr__pComThis.ToPointer();
            Guid* pIID = (Guid*)IntPtr__pIID.ToPointer();
            void** ppvObject = (void**)IntPtr__ppvObject.ToPointer();

            if (pIID->Equals(Interop.COM.IID_IUnknown) ||
                pIID->Equals(Interop.COM.IID_IFindDependentWrappers))
            {
                // You'll always get IFindDependentWrappers *
                // No need to AddRef - this is always 'alive' in the sense that it will be a pointer to a
                // static v-table
                *ppvObject = pComThis;

                return Interop.COM.S_OK;
            }

            return Interop.COM.E_NOINTERFACE;
        }

        /// <summary>
        /// Implements OnFoundDependentWrapper
        /// </summary>
        /// <remarks>
        /// WARNING: This function might be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        [NativeCallable]
        static int OnFoundDependentWrapper__STUB(System.IntPtr pComThis, IntPtr IntPtr__pCCW)
        {
            return RCWWalker.OnDependentWrapperCallback(
                    ComCallableObject.FromThisPointer(IntPtr__pCCW)
                    );
        }
    }

    /// <summary>
    /// Windows.UI.Xaml.Hosting.IReferenceTrackerManager
    ///
    /// We send Jupiter notifications for lifetime management purposes
    /// </summary>
    internal unsafe struct __com_IJupiterGCManager
    {
#pragma warning disable 0649
        internal __vtable_IJupiterGCManager* pVTable;
#pragma warning restore 0649
    }

    /// <summary>
    /// V-table for IJupiterGCManager
    ///
    /// NOTE: We don't implement IJupiterGCManager
    /// </summary>
    internal unsafe struct __vtable_IJupiterGCManager
    {
        // rgIUnknown is only used for making sure the rest of the fields have the right offset
#pragma warning disable 0169
        __vtable_IUnknown rgIIUnknown;
#pragma warning restore 0169

        // We never implement these functions - instead, we call Jupiter's implementation
#pragma warning disable 0649
        internal System.IntPtr pfnOnGCStarted;
        internal System.IntPtr pfnOnRCWWalkFinished;
        internal System.IntPtr pfnOnGCFinished;
        internal System.IntPtr pfnSetCLRServices;
#pragma warning restore 0649
    }


    /// <summary>
    /// Exposed services from CLR. Consumed by Jupiter
    /// Mostly lifetime related
    /// </summary>
    internal unsafe struct __com_ICLRServices
    {
#pragma warning disable 0649
        public __vtable_ICLRServices* pVtable;
#pragma warning restore 0649
    }

    /// <summary>
    /// V-table implementation for ICLRServices
    /// </summary>
    internal unsafe struct __vtable_ICLRServices
    {
        __vtable_IUnknown rgIIUnknown;

        internal System.IntPtr pfnGarbageCollect;
        internal System.IntPtr pfnFinalizerThreadWait;
        internal System.IntPtr pfnDisconnectRCWsInCurrentApartment;
        internal System.IntPtr pfnCreateManagedReference;
        internal System.IntPtr pfnAddMemoryPressure;
        internal System.IntPtr pfnRemoveMemoryPressure;

        static __vtable_ICLRServices s_theCcwVtable;

        internal static __vtable_ICLRServices* GetVtable()
        {
            s_theCcwVtable.InitVtable();

            // REDHAWK-ONLY: static field storage is unmovable
            fixed (__vtable_ICLRServices* pVtable = &s_theCcwVtable)
                return pVtable;
        }

        private void InitVtable()
        {
            rgIIUnknown.pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(AddRef__STUB);
            rgIIUnknown.pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(Release__STUB);
            rgIIUnknown.pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(QueryInterface__STUB);

            pfnGarbageCollect = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget2>(GarbageCollect__STUB);
            pfnFinalizerThreadWait = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(FinalizerThreadWait__STUB);
            pfnDisconnectRCWsInCurrentApartment = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(DisconnectRCWsInCurrentApartment__STUB);
            pfnCreateManagedReference = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfCreateManagedReference>(CreateManagedReference__STUB);
            pfnAddMemoryPressure = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfAddRemoveMemoryPressure>(AddMemoryPressure__STUB);
            pfnRemoveMemoryPressure = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfAddRemoveMemoryPressure>(RemoveMemoryPressure__STUB);
        }

        [NativeCallable]
        static int AddRef__STUB(System.IntPtr pComThis)
        {
            // This never getes released from native
            return 1;
        }

        [NativeCallable]
        static int Release__STUB(System.IntPtr pComThis)
        {
            // This never gets released from native
            return 1;
        }

        [NativeCallable]
        static int QueryInterface__STUB(
                    System.IntPtr IntPtr__pComThis,
                    System.IntPtr IntPtr__pIID,
                    System.IntPtr IntPtr__ppvObject)
        {
            __com_ICLRServices* pComThis = (__com_ICLRServices*)IntPtr__pComThis.ToPointer();
            Guid* pIID = (Guid*)IntPtr__pIID.ToPointer();
            void** ppvObject = (void**)IntPtr__ppvObject.ToPointer();

            if (pIID->Equals(Interop.COM.IID_IUnknown) ||
                pIID->Equals(Interop.COM.IID_ICLRServices))
            {
                // You'll always get ICLRServices *
                // No need to AddRef - this is always 'alive' in the sense that it will be a pointer to a
                // static v-table
                *ppvObject = pComThis;

                return Interop.COM.S_OK;
            }

            return Interop.COM.E_NOINTERFACE;
        }

        [NativeCallable]
        static int GarbageCollect__STUB(System.IntPtr pComThis, int flags)
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Optimized, /* blocking = */ true);

                if (InteropEventProvider.IsEnabled())
                    InteropEventProvider.Log.TaskJupiterGarbageCollect();
            }
            catch (Exception ex)
            {
                // If we set IErrorInfo/IRestrictedErrorInfo, Jupiter might leak
                return ex.HResult;
            }

            return Interop.COM.S_OK;
        }

        [NativeCallable]
        static int FinalizerThreadWait__STUB(System.IntPtr pComThis)
        {
            // This could lead to deadlock if finalizer thread is trying to get back to this thread, because
            // we are not pumping anymore. Disable this for now and we'll target fixing this in v2
            // GC.WaitForPendingFinalizers();

            return Interop.COM.S_OK;
        }

        [NativeCallable]
        static int DisconnectRCWsInCurrentApartment__STUB(System.IntPtr pComThis)
        {
            try
            {
                ContextEntry.RemoveCurrentContext();

                if (InteropEventProvider.IsEnabled())
                    InteropEventProvider.Log.TaskJupiterDisconnectRCWsInCurrentApartment();
            }
            catch (Exception ex)
            {
                // If we set IErrorInfo/IRestrictedErrorInfo, Jupiter might leak
                return ex.HResult;
            }

            return Interop.COM.S_OK;
        }

        /// <summary>
        /// Creates a proxy object that points to the given RCW
        /// The proxy
        /// 1. Has a managed reference pointing to the RCW, and therefore forms a cycle that can be resolved by GC
        /// 2. Forwards data binding requests
        /// For example:
        ///
        /// Grid <---- RCW             Grid <------RCW
        /// | ^                         |              ^
        /// | |             Becomes     |              |
        /// v |                         v              |
        /// Rectangle                  Rectangle ----->Proxy
        ///
        /// Arguments
        ///   pJupiterObject - The identity IUnknown* where a RCW points to (Grid, in this case)
        ///                    Note that
        ///                    1) we can either create a new RCW or get back an old one from cache
        ///                    2) This pJupiterObject could be a regular WinRT object (such as WinRT collection) for data binding
        ///  ppNewReference  - The ICCW* for the proxy created
        ///                    Jupiter will call ICCW to establish a jupiter reference
        ///
        /// NOTE: This is *NOT* entirely implemented yet
        /// </summary>
        [NativeCallable]
        static int CreateManagedReference__STUB(System.IntPtr pComThis, IntPtr __IntPtr__pUnknown,
                        IntPtr __IntPtr__ppNewReference)
        {
            try
            {
                void* pUnknown = (void *)__IntPtr__pUnknown;
                if (pUnknown == null)
                    return Interop.COM.E_POINTER;

                __com_ICCW** ppNewReference = (__com_ICCW**)__IntPtr__ppNewReference;
                if (ppNewReference == null)
                    return Interop.COM.E_POINTER;

                object comObj = null;
#if ENABLE_WINRT
            //
            // Converts IUnknown * to a RCW with type resolution
            // If it is already there in the cache, we return the existing one. Otherwise, we create a new
            // one, and create the right instance using the return value of GetRuntimeClassName
            //
             comObj = McgComHelpers.ComInterfaceToComObject(
                __IntPtr__pUnknown,
                InternalTypes.IUnknown,
                default(RuntimeTypeHandle),
                ContextCookie.Default,                                  // No restriction on context
                McgComHelpers.CreateComObjectFlags.IsWinRTObject
            );
#endif
                //
                // Create a proxy to the RCW for pJupiterObject
                // This ensures the object returned only supports standard interfaces and interfaces related to databinding.
                // Supporting all interfaces that pJupiterObject supports isn't required and causes issues since we return
                // our vtable instead of pJupiterObject's
                //
                IManagedWrapper customPropertyProviderProxy;
                System.Collections.IList comObjList;
                System.Collections.IEnumerable comObjEnumerable;

                if ((comObjList = comObj as System.Collections.IList) != null)
                {
                    customPropertyProviderProxy = new ListCustomPropertyProviderProxy(comObjList);
                }
                else if ((comObjEnumerable = comObj as System.Collections.IEnumerable) != null)
                {
                    customPropertyProviderProxy = new EnumerableCustomPropertyProviderProxy(comObjEnumerable);
                }
                else
                {
                    customPropertyProviderProxy = new StandardCustomPropertyProviderProxy(comObj);
                }
#if ENABLE_WINRT
            //
            // Then, create a new CCW that points to this RCW
            // @TODO: Create a Proxy object that points to the RCW, and then get a CCW for the proxy
            //
#if X86 && RHTESTCL
            // The contract is we must hand out ICCW (even though Jupiter probably doesn't care)
            // but we don't have it in rhtestcl X86 because StdCallCOOP is not available in RHTESTCL
            *ppNewReference = (__com_ICCW*)McgMarshal.ManagedObjectToComInterface(
                customPropertyProviderProxy,
                InternalTypes.IInspectable
            );
#else // X86 && RHTESTCL
            // The contract is we must hand out ICCW (even though Jupiter probably doesn't care)
            *ppNewReference = (__com_ICCW*)McgMarshal.ManagedObjectToComInterface(
                customPropertyProviderProxy,
                InternalTypes.ICCW
            );
#endif //

#endif //ENABLE_WINRT

                if (InteropEventProvider.IsEnabled())
                    InteropEventProvider.Log.TaskJupiterCreateManagedReference((long)__IntPtr__pUnknown, (long)comObj.GetTypeHandle().GetRawValue());
            }
            catch (Exception ex)
            {
                // If we set IErrorInfo/IRestrictedErrorInfo, Jupiter might leak
                return ex.HResult;
            }

            return Interop.COM.S_OK;
        }

        [NativeCallable]
        static int AddMemoryPressure__STUB(System.IntPtr pComThis, ulong bytesAllocated)
        {
            try
            {
#if !RHTESTCL
                GC.AddMemoryPressure((long)bytesAllocated);
#endif

                if (InteropEventProvider.IsEnabled())
                    InteropEventProvider.Log.TaskJupiterAddMemoryPressure((long)bytesAllocated);
            }
            catch(Exception ex)
            {
                // If we set IErrorInfo/IRestrictedErrorInfo, Jupiter might leak
                return ex.HResult;
            }

            return Interop.COM.S_OK;
        }

        [NativeCallable]
        static int RemoveMemoryPressure__STUB(System.IntPtr pComThis, ulong bytesAllocated)
        {
            try
            {
#if !RHTESTCL
                GC.RemoveMemoryPressure((long)bytesAllocated);
#endif
                if (InteropEventProvider.IsEnabled())
                    InteropEventProvider.Log.TaskJupiterRemoveMemoryPressure((long)bytesAllocated);
            }
            catch (Exception ex)
            {
                // If we set IErrorInfo/IRestrictedErrorInfo, Jupiter might leak
                return ex.HResult;
            }

            return Interop.COM.S_OK;
        }
    }

    /// <summary>
    /// RCW Walker
    /// Walks jupiter RCW objects and create references from RCW to referenced CCW (in native side)
    /// </summary>
    static unsafe class RCWWalker
    {
        static DependentHandleList s_dependentHandleList;           // List of dependent handles
        static volatile IntPtr s_pGCManager;                        // Points to Jupiter's GC manager
        static volatile bool s_bInitialized;                        // Whether RCWWalker has been fully
                                                                    // initialized
        static bool s_globalPeggingOn = true;                       // Global pegging. Default to true
        static bool s_gcStarted;                                    // Whether GC is started
        static __com_ICLRServices s_clrServices;                    // The global CLRServices object

        /// <summary>
        /// Whether global pegging is on.
        /// Global pegging is on by default except in gen2 GCs, where we temporarily set it to false.
        /// It is a way to simply let all the CCWs leak by default, and do expensive RCW walks in Gen2 GC
        /// </summary>
        internal static bool IsGlobalPeggingOn
        {
            [GCCallback]
            get
            {
                return s_globalPeggingOn;
            }
        }

        /// <summary>
        /// Initialize RCWWalker
        /// </summary>
        private unsafe static void Initialize(__com_IJupiterObject* pJupiterObject)
        {
            IntPtr pGCManager;
            int hr = CalliIntrinsics.StdCall<int>(pJupiterObject->pVtable->pfnGetJupiterGCManager, pJupiterObject, &pGCManager);
            if (hr >= 0)
            {
                // disable warning for ref volatile
#pragma warning disable 0420
                if (Interlocked.CompareExchange(ref s_pGCManager, pGCManager, default(IntPtr)) == default(IntPtr))
#pragma warning restore 0420
                {
                    // We won the race. Now start the real initialization
                    InitializeImpl();
                }
            }

            McgMarshal.ComRelease(pGCManager);
        }

        /// <summary>
        /// Real implementation of initializing RCW walker for jupiter lifetime feature
        /// </summary>
        [GCCallback]
        private static void InitializeImpl()
        {
            __com_IJupiterGCManager* pGCManager = (__com_IJupiterGCManager*)s_pGCManager;

            //
            // AddRef on IGCManager
            //
            __com_IUnknown* pGCManagerUnk = (__com_IUnknown*)pGCManager;
            CalliIntrinsics.StdCall<int>(pGCManagerUnk->pVtable->pfnAddRef, pGCManager);

            s_clrServices.pVtable = __vtable_ICLRServices.GetVtable();

            fixed (__com_ICLRServices* pCLRServices = &s_clrServices)
            {
                //
                // Tell Jupiter that we are ready for tracking life time of objects and provide Jupiter with
                // our life time realted services through ICLRServices
                //
                CalliIntrinsics.StdCall<int>(
                    pGCManager->pVTable->pfnSetCLRServices,
                    pGCManager,
                    pCLRServices
                    );

                //
                // No Jupiter lifetime on X86 RHTESTCL because StdCallCoop is not available
                //
#if !(X86 && RHTESTCL)
                //
                // Hook GC related callbacks
                //
                InteropExtensions.RuntimeRegisterGcCalloutForGCStart(AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfHookGCCallbacks>(OnGCStarted));
                InteropExtensions.RuntimeRegisterGcCalloutForAfterMarkPhase(AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfHookGCCallbacks>(AfterMarkPhase));
                InteropExtensions.RuntimeRegisterGcCalloutForGCEnd(AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfHookGCCallbacks>(OnGCFinished));
#endif
            }

            s_bInitialized = true;
        }

        /// <summary>
        /// Called when Jupiter RCW is being created
        /// We do one-time initialization for RCW walker here
        /// </summary>
        internal static void OnJupiterRCWCreated(__ComObject comObject)
        {
            Debug.Assert(comObject.IsJupiterObject);

            if (s_pGCManager == default(IntPtr))
            {
                RCWWalker.Initialize(comObject.GetIJupiterObject_NoAddRef());
            }
        }

        /// <summary>
        /// Called after Jupiter RCW has been created
        /// </summary>
        internal static void AfterJupiterRCWCreated(__ComObject comObject)
        {
            __com_IJupiterObject* pJupiterObject = comObject.GetIJupiterObject_NoAddRef();

            //
            // Notify Jupiter that we've created a new RCW for this Jupiter object
            // To avoid surprises, we should notify them before we fire the first AfterAddRef
            //
            CalliIntrinsics.StdCall<int>(pJupiterObject->pVtable->pfnConnect, pJupiterObject);

            //
            // Tell Jupiter that we've done AddRef for IJupiterObject* and IUnknown*
            // It's better to tell them later than earlier (prefering leaking than crashing)
            //
            AfterAddRef(comObject);
            AfterAddRef(comObject);
        }

        /// <summary>
        /// Reporting to Jupiter that we've done one AddRef to this object instance
        ///
        /// The ref count reporting is needed for Jupiter to determine whether there is something other than
        /// Jupiter and RCW that is holding onto this Jupiter object. If there is, this Jupiter object and
        /// all dependent CCWs must be pegged.
        ///
        /// We typically report the AddRef *after* an AddRef and report the Release *before* a Release
        /// It's better to leak (temporarily) than crash.
        /// </summary>
        internal static void AfterAddRef(__ComObject comObject)
        {
            Debug.Assert(comObject.IsJupiterObject);

            __com_IJupiterObject* pJupiterObject = comObject.GetIJupiterObject_NoAddRef();

            //
            // Send out AfterAddRef callbacks to notify Jupiter we've done AddRef for certain interfaces
            // We should do this *after* we made a AddRef because we should never
            // be in a state where report refs > actual refs
            //
            CalliIntrinsics.StdCall<int>(pJupiterObject->pVtable->pfnAfterAddRef, pJupiterObject);
        }

        /// <summary>
        /// Reporting to Jupiter that we've done one Release to this object instance
        ///
        /// The ref count reporting is needed for Jupiter to determine whether there is something other than
        /// Jupiter and RCW that is holding onto this Jupiter object. If there is, this Jupiter object and
        /// all dependent CCWs must be pegged.
        ///
        /// We typically report the AddRef *after* an AddRef and report the Release *before* a Release
        /// It's better to leak (temporarily) than crash.
        /// </summary>
        internal static void BeforeRelease(__ComObject comObject)
        {
            Debug.Assert(comObject.IsJupiterObject);

            __com_IJupiterObject* pJupiterObject = comObject.GetIJupiterObject_NoAddRef();

            CalliIntrinsics.StdCall<int>(pJupiterObject->pVtable->pfnBeforeRelease, pJupiterObject);
        }

        /// <summary>
        /// Walk all the Jupiter RCWs and build references from RCW->CCW as we go using dependent handles
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal static bool WalkRCWs()
        {
            //
            // Reset all dependent handles for use in the current GC
            // We also clear them to make sure they are cleared
            //
            if (!s_dependentHandleList.ResetHandles())
                return false;

            //
            // Go through each RCW
            //

            System.Collections.Generic.Internal.Dictionary<IntPtr, IntPtr> map = ComObjectCache.s_comObjectMap;

            for (int i = 0; i < map.GetMaxCount(); ++i)
            {
                IntPtr pHandle = default(IntPtr);

                if (map.GetValue(i, ref pHandle) && (pHandle != default(IntPtr)))
                {
                    GCHandle handle = GCHandle.FromIntPtr(pHandle);

                    //
                    // Does a magic unchecked cast that assumes the GCHandle always points to __ComObject,
                    // which is true. Doing a real cast would crash because we are inside GC callout
                    //
                    __ComObject comObject = InteropExtensions.UncheckedCast<__ComObject>(handle.Target);

                    //
                    // Only walk RCWs that have >0 ref count. In theory we should never see RCW with
                    // 0 ref count and has been cleaned up by DisconnectRCWsInCurrentApartment but for some
                    // reason this is happening in GCSTRESS.
                    //
                    if (comObject != null &&
                        comObject.IsJupiterObject &&
                        comObject.PeekRefCount() > 0)
                    {
                        if (!WalkOneRCW(comObject))
                            return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Ask Jupiter all the CCWs referenced (through native code) by this RCW and build reference for RCW -> CCW
        /// so that GC knows about this reference
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal static bool WalkOneRCW(__ComObject comObject)
        {
            __com_IJupiterObject* pJupiterObject = comObject.GetIJupiterObject_NoAddRef();

            s_currentComObjectToWalk = comObject;

            //
            // Start building references from this RCW to all dependent CCWs
            //
            // NOTE: StdCallCOOP is used instead of Calli to avoid deadlock
            //
            int hr = CalliIntrinsics.StdCallCOOP(
                pJupiterObject->pVtable->pfnFindDependentWrappers,
                pJupiterObject,
                GetDependentWrapperCallbackObject()
                );

            s_currentComObjectToWalk = null;

            return (hr >= 0);
        }

        /// <summary>
        /// Walk all RCWs and log them each of them by using ETW.
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal static void LogRCWs()
        {
            //
            // Go through each RCW
            //

            System.Collections.Generic.Internal.Dictionary<IntPtr, IntPtr> map = ComObjectCache.s_comObjectMap;

            for (int i = 0; i < map.GetMaxCount(); ++i)
            {
                IntPtr pHandle = default(IntPtr);

                if (map.GetValue(i, ref pHandle) && (pHandle != default(IntPtr)))
                {
                    GCHandle handle = GCHandle.FromIntPtr(pHandle);

                    //
                    // Does a magic unchecked cast that assumes the GCHandle always points to __ComObject,
                    // which is true. Doing a real cast would crash because we are inside GC callout
                    //
                    __ComObject comObject = InteropExtensions.UncheckedCast<__ComObject>(handle.Target);

                    if (comObject != null)
                    {
                        GCEventProvider.TaskLogLiveRCW(InteropExtensions.GetObjectID(comObject), comObject.GetTypeHandle().GetRawValue(), 
                            comObject.BaseIUnknown_UnsafeNoAddRef, comObject.SavedIUnknownVtbl, comObject.PeekRefCount(), comObject.Flags);
                    }
                }
            }
        }

        /// <summary>
        /// The global instance of IFindDependentWrapperCallback implementation
        /// </summary>
        private static __com_IFindDependentWrappers s_findDependentWrapperCallbackObject;

        /// <summary>
        /// Points the current RCW that is being walked
        /// It has to be static because we can't take address of this struct if this were a member
        /// </summary>
        internal static __ComObject s_currentComObjectToWalk;

        /// <summary>
        /// Returns a IFindDependentWrapper* to walk a specific RCW. Jupiter will call this interface to
        /// return all the dependent CCWs for this RCW
        /// </summary>
        [GCCallback]
        internal static __com_IFindDependentWrappers* GetDependentWrapperCallbackObject()
        {
            s_findDependentWrapperCallbackObject.pVtable = __vtable_IFindDependentWrappers.GetVtable();
            fixed (__com_IFindDependentWrappers* pCallbackObject = &s_findDependentWrapperCallbackObject)
                return pCallbackObject;
        }

        /// <summary>
        /// Called from Jupiter when they've found a dependent CCW from a specific RCW
        /// We'll create a dependent handle from RCW to this CCW to report the reference from RCW to CCW
        /// formed in native side
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal static int OnDependentWrapperCallback(ComCallableObject ccw)
        {
            __ComObject rcw = s_currentComObjectToWalk;

            //
            // Skip dependent handle creation if RCW/CCW points to the same managed object
            //
            if (rcw == ccw.TargetObject)
                return Interop.COM.S_OK;

            //
            // Avoid touching CCW target object that have already been GC collected
            //
            if (ccw.TargetObject == null)
                return Interop.COM.S_OK;

            //
            // Allocate (or use existing) a dependent handle for RCW->CCW
            //
            if (!s_dependentHandleList.AllocateHandle(rcw, ccw))
                return Interop.COM.E_FAIL;

            return Interop.COM.S_OK;
        }

        /// <summary>
        /// Called when mark phase is completed so that we can let Jupiter know which RCWs are about to die
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal static int AfterMarkPhase(int nCondemnedGeneration)
        {
            System.Collections.Generic.Internal.Dictionary<IntPtr, IntPtr> map = ComObjectCache.s_comObjectMap;

            for (int i = 0; i < map.GetMaxCount(); ++i)
            {
                //
                // I can't do this inside a generic .Equals(default(T)) because it will attempt to box
                // IntPtr (which does a new and deadlocks inside a GCCallback)
                // So the best option here is to do check explicitly outside
                //
                IntPtr pHandle = default(IntPtr);

                if (map.GetValue(i, ref pHandle) && (pHandle != default(IntPtr)))
                {
                    GCHandle handle = GCHandle.FromIntPtr(pHandle);

                    //
                    // Does a magic unchecked cast that assumes the GCHandle always points to __ComObject,
                    // which is true. Doing a real cast would crash because we are inside GC call out
                    //
                    __ComObject comObject = InteropExtensions.UncheckedCast<__ComObject>(handle.Target);

                    //
                    // Only walk RCWs that have >0 ref count. In theory we should never see RCW with
                    // 0 ref count and has been cleaned up by DisconnectRCWsInCurrentApartment but for some
                    // reason this is happening in GCSTRESS.
                    //
                    if (comObject != null &&
                        comObject.IsJupiterObject &&
                        !InteropExtensions.RuntimeIsPromoted(comObject) &&
                        comObject.PeekRefCount() > 0)
                    {
                        __com_IJupiterObject* pJupiterObject = comObject.GetIJupiterObject_NoAddRef();

                        //
                        // Notify Jupiter that we are about to destroy a RCW (same timing as short weak handle)
                        // for this Jupiter object.
                        // They need this information to disconnect weak refs and stop firing events,
                        // so that they can avoid resurrecting the Jupiter object (not the RCW - we prevent that)
                        // We only call this inside GC, so don't need to switch to preemptive here
                        // Ignore the failure as there is no way we can handle that failure during GC
                        // NOTE: StdCallCOOP must be used instead of Calli to avoid deadlock
                        //
                        CalliIntrinsics.StdCallCOOP(pJupiterObject->pVtable->pfnDisconnect, pJupiterObject);
                    }
                }
            }

            // Technically OnGCStarted should return void, but our AddrOf doesn't support void return value
            return 0;
        }

        /// <summary>
        ///
        /// Called when GC started
        /// We do most of our work here
        ///
        /// Note that we could get nested GCStart/GCEnd calls, such as :
        /// GCStart for Gen 2 background GC
        ///    GCStart for Gen 0/1 foregorund GC
        ///    GCEnd   for Gen 0/1 foreground GC
        ///    ....
        /// GCEnd for Gen 2 background GC
        ///
        /// The nCondemnedGeneration >= 2 check takes care of this nesting problem
        ///
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal static int OnGCStarted(int nCondemnedGeneration)
        {
            if (NeedToWalkRCWs())
            {
                if (nCondemnedGeneration >= 2)
                {
                    OnGCStartedWorker();
                }
            }

            // Technically OnGCStarted should return void, but our AddrOf doesn't support void return value
            return 0;
        }

        /// <summary>
        /// Whether we need to walk RCWs in this GC
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        static bool NeedToWalkRCWs()
        {
            // If s_pGCManager != null, we've seen a Jupiter RCW and that means we should do the RCW walk
            return (s_pGCManager != default(IntPtr) && s_bInitialized);
        }

        /// <summary>
        /// The actual RCW walk happens here
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal static void OnGCStartedWorker()
        {
            __com_IJupiterGCManager* pGCManager = (__com_IJupiterGCManager*)s_pGCManager;

            s_gcStarted = true;

            //
            // Let Jupiter know (Gen 2) GC started
            // NOTE: StdCallCOOP is used instead of StdCall to avoid deadlock
            //
            CalliIntrinsics.StdCallCOOP(pGCManager->pVTable->pfnOnGCStarted, pGCManager);

            //
            // Life off the global pegging flag so that Jupiter can peg them individually to determine
            // whether they should be rooted or not
            //
            s_globalPeggingOn = false;

            //
            // Walk all RCWs
            //
            int bRCWWalkFailed = 0;
            if (!WalkRCWs())
            {
                bRCWWalkFailed = 1;
            }

            //
            // Let Jupiter know we've finished RCW walking
            // NOTE: StdCallCOOP is used instead of StdCall to avoid deadlock
            //
            CalliIntrinsics.StdCallCOOP(
                pGCManager->pVTable->pfnOnRCWWalkFinished,
                pGCManager,
                bRCWWalkFailed
                );
        }

        /// <summary>
        ///
        /// Called when GC finished
        ///
        /// Note that we could get nested GCStart/GCEnd calls, such as :
        /// GCStart for Gen 2 background GC
        ///    GCStart for Gen 0/1 foregorund GC
        ///    GCEnd   for Gen 0/1 foreground GC
        ///    ....
        /// GCEnd for Gen 2 background GC
        ///
        /// The nCondemnedGeneration >= 2 check takes care of this nesting problem
        ///
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal static int OnGCFinished(int nCondemnedGeneration)
        {
            if (NeedToWalkRCWs() &&
                s_gcStarted &&
                nCondemnedGeneration >= 2)
            {
                OnGCFinishedWorker();
            }

            if (GCEventProvider.IsETWHeapCollectionEnabled())
            {
                CCWLookupMap.LogCCWs();
                RCWWalker.LogRCWs();

                GCEventProvider.FlushComETW();
            }

            // Technically OnGCStarted should return void, but our AddrOf doesn't support void return value
            return 0;
        }

        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        private static void OnGCFinishedWorker()
        {
            __com_IJupiterGCManager* pGCManager = (__com_IJupiterGCManager*)s_pGCManager;

            //
            // Let Jupiter know GC is finished
            // NOTE: StdCallCOOP is used instead of StdCall to avoid deadlock
            //
            CalliIntrinsics.StdCallCOOP(pGCManager->pVTable->pfnOnGCFinished, pGCManager);
            //
            // Set global pegging flag to protect CCWs until the next walk happens
            //
            s_globalPeggingOn = true;
            s_gcStarted = false;
        }
    }

    //=========================================================================================
    // This struct collects all operations on native DependentHandles. The DependentHandle
    // merely wraps an IntPtr so this struct serves mainly as a "managed typedef."
    //
    // DependentHandles exist in one of two states:
    //
    //    IsAllocated == false
    //        No actual handle is allocated underneath. Illegal to call GetPrimary
    //        or GetPrimaryAndSecondary(). Ok to call Free().
    //
    //        Initializing a DependentHandle using the nullary ctor creates a DependentHandle
    //        that's in the !IsAllocated state.
    //        (! Right now, we get this guarantee for free because (IntPtr)0 == NULL unmanaged handle.
    //         ! If that assertion ever becomes false, we'll have to add an _isAllocated field
    //         ! to compensate.)
    //
    //
    //    IsAllocated == true
    //        There's a handle allocated underneath. You must call Free() on this eventually
    //        or you cause a native handle table leak.
    //
    // This struct intentionally does no self-synchronization. It's up to the caller to
    // to use DependentHandles in a thread-safe way.
    //=========================================================================================
    [ComVisible(false)]
    struct DependentHandle
    {
        #region Constructors

        public DependentHandle(Object primary, Object secondary)
        {
            _handle = InteropExtensions.RuntimeHandleAllocDependent(primary, secondary);
        }
        #endregion

        #region Public Members
        public bool IsAllocated
        {
            get
            {
                return _handle != (IntPtr)0;
            }
        }

        public void SetPrimaryAndSecondary(Object primary, object secondary)
        {
            InteropExtensions.RuntimeHandleSet(_handle, primary);
            InteropExtensions.RuntimeHandleSetDependentSecondary(_handle, secondary);
        }

        //----------------------------------------------------------------------
        // Forces dependentHandle back to non-allocated state (if not already there)
        // and frees the handle if needed.
        //----------------------------------------------------------------------
        public void Free()
        {
            if (_handle != (IntPtr)0)
            {
                IntPtr handle = _handle;
                _handle = (IntPtr)0;
                InteropExtensions.RuntimeHandleFree(handle);
            }
        }
        #endregion


        #region Private Data Member
        private IntPtr _handle;
        #endregion

    } // struct DependentHandle

    /// <summary>
    /// A list of dependent handles that is growable under GC callouts
    /// </summary>
    unsafe struct DependentHandleList
    {
        int m_freeIndex;                        // The next available slot
        int m_capacity;                         // Total numbers of slots available in the list
        IntPtr* m_pHandles;                     // All handles
        int m_shrinkHint;                       // How many times we've consistently seen "hints" that a
                                                // shrink is needed

        internal const int DefaultCapacity = 100;       // Default initial capacity of this list
        internal const int ShrinkHintThreshold = 5;     // The number of hints we've seen before we really
                                                        // shrink the list

        /// <summary>
        /// Reset the list of handles to be used by the current GC
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal bool ResetHandles()
        {
            if (m_pHandles == null)
            {
                //
                // This is the first time we use this list
                // Initialize it
                // NOTE: Call must be used instead of StdCall to avoid deadlock
                //
                m_capacity = DefaultCapacity;
                uint newCapacity = (uint)(sizeof(IntPtr) * m_capacity);
                m_pHandles = (IntPtr*)ExternalInterop.MemAllocWithZeroInitializeNoThrow(new UIntPtr(newCapacity));

                if (m_pHandles == null)
                    return false;

                // Our job is done if we are allocating this for the first time
                return true;
            }


            if (m_freeIndex < m_capacity / 2 && m_capacity > DefaultCapacity)
            {
                m_shrinkHint++;
                if (m_shrinkHint > ShrinkHintThreshold)
                {
                    //
                    // If we ever seeing consistently (> 5 times) that free index is less than half of
                    // our current capacity, it is time to shrink the size
                    //
                    Shrink();

                    m_shrinkHint = 0;
                }
            }
            else
            {
                //
                // Reset shrink hint and start over the counting
                //
                m_shrinkHint = 0;
            }

            //
            // Clear all the handles that were used
            //
            for (int index = 0; index < m_freeIndex; ++index)
            {
                IntPtr handle = m_pHandles[index];
                if (handle != default(IntPtr))
                {
                    InteropExtensions.RuntimeHandleSet(handle, null);
                    InteropExtensions.RuntimeHandleSetDependentSecondary(handle, null);
                }
            }

            m_freeIndex = 0;

            return true;
        }

        /// <summary>
        /// Allocate a DependentHandle by either reusing an existing one or creating a new one
        /// </summary>
        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal bool AllocateHandle(object primary, object secondary)
        {
            if (m_freeIndex >= m_capacity)
            {
                // We need a bigger dependent handle array
                if (!Grow())
                    return false;
            }

            IntPtr pHandle = m_pHandles[m_freeIndex];
            if (pHandle != default(IntPtr))
            {
                InteropExtensions.RuntimeHandleSet(pHandle, primary);
                InteropExtensions.RuntimeHandleSetDependentSecondary(pHandle, secondary);
            }
            else
            {
                m_pHandles[m_freeIndex] = InteropExtensions.RuntimeHandleAllocDependent(primary, secondary);
            }

            m_freeIndex++;

            return true;
        }

        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal bool Grow()
        {
            int newCapacity = m_capacity * 2;

            // NOTE: Call must be used instead of StdCall to avoid deadlock
            IntPtr* pNewHandles = (IntPtr*)ExternalInterop.MemReAllocWithZeroInitializeNoThrow((IntPtr)m_pHandles,
                                                                      new UIntPtr( (uint) (sizeof(IntPtr) * m_capacity)),
                                                                      new UIntPtr( (uint) (sizeof(IntPtr) * newCapacity)));

            if (pNewHandles == null)
                return false;

            m_pHandles = pNewHandles;
            m_capacity = newCapacity;

            return true;
        }

        /// <remarks>
        /// WARNING: This function will be called under a GC callback. Please read the comments in
        /// GCCallbackAttribute to understand all the implications before you make any changes
        /// </remarks>
        [GCCallback]
        internal bool Shrink()
        {
            Debug.Assert(m_capacity > DefaultCapacity && m_capacity / 2 > 10);

            int newCapacity = m_capacity / 2;

            //
            // Free all handles that will go away
            //
            for (int index = newCapacity; index < m_capacity; ++index)
            {
                if (m_pHandles[index] != default(IntPtr))
                {
                    InteropExtensions.RuntimeHandleFree(m_pHandles[index]);

                    // Assign them back to null in case the reallocation fails
                    m_pHandles[index] = default(IntPtr);
                }
            }

            //
            // Shrink the size of the memory
            // If this fails, we don't really care (very unlikely to fail, though)
            // NOTE: Call must be used instead of StdCall to avoid deadlock
            //
            IntPtr* pNewHandles = (IntPtr*)ExternalInterop.MemReAllocWithZeroInitializeNoThrow((IntPtr) m_pHandles, 
                                                                        new UIntPtr((uint)(sizeof(IntPtr) * m_capacity)),
                                                                        new UIntPtr((uint)(sizeof(IntPtr) * newCapacity)));
            if (pNewHandles == null)
                return false;

            m_pHandles = pNewHandles;
            m_capacity = newCapacity;

            return true;
        }
    }
}
