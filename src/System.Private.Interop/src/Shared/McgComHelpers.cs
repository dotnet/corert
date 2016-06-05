// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
// 
// COM Marshalling helpers used by MCG 
//
// NOTE: 
//   These source code are being published to InternalAPIs and consumed by RH builds
//   Use PublishInteropAPI.bat to keep the InternalAPI copies in sync
// ----------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Runtime;
using System.Diagnostics.Contracts;
using Internal.NativeFormat;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    internal static unsafe class McgComHelpers
    {
        /// <summary>
        /// Returns runtime class name for a specific object
        /// </summary>
        internal static string GetRuntimeClassName(Object obj)
        {
#if  ENABLE_WINRT
            System.IntPtr pWinRTItf = default(IntPtr);

            try
            {
                pWinRTItf = McgMarshal.ObjectToIInspectable(obj);
                if (pWinRTItf == default(IntPtr))
                    return String.Empty;
                else
                    return GetRuntimeClassName(pWinRTItf);
            }
            finally
            {
                if (pWinRTItf != default(IntPtr)) McgMarshal.ComRelease(pWinRTItf);
            }
#else
            return string.Empty;
#endif
        }

        /// <summary>
        /// Returns runtime class name for a specific WinRT interface
        /// </summary>
        internal static string GetRuntimeClassName(IntPtr pWinRTItf)
        {
#if  ENABLE_WINRT
            void* unsafe_hstring = null;

            try
            {
                int hr = CalliIntrinsics.StdCall__int(
                    ((__com_IInspectable*)(void*)pWinRTItf)->pVtable->pfnGetRuntimeClassName,
                    pWinRTItf, &unsafe_hstring);

                // Don't throw if the call fails
                if (hr < 0)
                    return String.Empty;

                return McgMarshal.HStringToString(new IntPtr(unsafe_hstring));
            }
            finally
            {
                if (unsafe_hstring != null)
                    McgMarshal.FreeHString(new IntPtr(unsafe_hstring));
            }
#else
            throw new PlatformNotSupportedException("GetRuntimeClassName(IntPtr)");
#endif
        }

        /// <summary>
        /// Given a IStream*, seek to its beginning
        /// </summary>
        internal static unsafe bool SeekStreamToBeginning(IntPtr pStream)
        {
            Interop.COM.__IStream* pStreamNativePtr = (Interop.COM.__IStream*)(void*)pStream;
            UInt64 newPosition;

            int hr = CalliIntrinsics.StdCall<int>(
                pStreamNativePtr->vtbl->pfnSeek,
                pStreamNativePtr,
                0UL,
                (uint)Interop.COM.STREAM_SEEK.STREAM_SEEK_SET,
                &newPosition);
            return (hr >= 0);
        }

        /// <summary>
        /// Given a IStream*, change its size
        /// </summary>
        internal static unsafe bool SetStreamSize(IntPtr pStream, ulong lSize)
        {
            Interop.COM.__IStream* pStreamNativePtr = (Interop.COM.__IStream*)(void*)pStream;
            UInt64 newPosition;

            int hr = CalliIntrinsics.StdCall<int>(
                pStreamNativePtr->vtbl->pfnSetSize,
                pStreamNativePtr,
                lSize,
                (uint)Interop.COM.STREAM_SEEK.STREAM_SEEK_SET,
                &newPosition);
            return (hr >= 0);
        }

        /// <summary>
        /// Release a IStream that has marshalled data in it
        /// </summary>
        internal static void SafeReleaseStream(IntPtr pStream)
        {
            Debug.Assert(pStream != default(IntPtr));
#if ENABLE_WINRT
            // Release marshalled data and ignore any error
            ExternalInterop.CoReleaseMarshalData(pStream);

            McgMarshal.ComRelease(pStream);
#else
            throw new PlatformNotSupportedException("SafeReleaseStream");
#endif
        }

        /// <summary>
        /// Returns whether the IUnknown* is a free-threaded COM object
        /// </summary>
        /// <param name="pUnknown"></param>
        internal static unsafe bool IsFreeThreaded(IntPtr pUnknown)
        {
            //
            // Does it support IAgileObject?
            //
            IntPtr pAgileObject = McgMarshal.ComQueryInterfaceNoThrow(pUnknown, ref Interop.COM.IID_IAgileObject);

            if (pAgileObject != default(IntPtr))
            {
                // Anything that implements IAgileObject is considered to be free-threaded
                // NOTE: This doesn't necessarily mean that the object is free-threaded - it only means
                // we BELIEVE it is free-threaded
                McgMarshal.ComRelease_StdCall(pAgileObject);
                return true;
            }

            IntPtr pMarshal = McgMarshal.ComQueryInterfaceNoThrow(pUnknown, ref Interop.COM.IID_IMarshal);

            if (pMarshal == default(IntPtr))
                return false;

            try
            {
                //
                // Check the un-marshaler
                //
                Interop.COM.__IMarshal* pIMarshalNativePtr = (Interop.COM.__IMarshal*)(void*)pMarshal;

                fixed (Guid* pGuid = &Interop.COM.IID_IUnknown)
                {
                    Guid clsid;
                    int hr = CalliIntrinsics.StdCall__int(
                        pIMarshalNativePtr->vtbl->pfnGetUnmarshalClass,
                        new IntPtr(pIMarshalNativePtr),
                        pGuid,
                        default(IntPtr),
                        (uint)Interop.COM.MSHCTX.MSHCTX_INPROC,
                        default(IntPtr),
                        (uint)Interop.COM.MSHLFLAGS.MSHLFLAGS_NORMAL,
                        &clsid);

                    if (hr >= 0 && InteropExtensions.GuidEquals(ref clsid, ref Interop.COM.CLSID_InProcFreeMarshaler))
                    {
                        // The un-marshaller is indeed the unmarshaler for the FTM so this object 
                        // is free threaded.
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                McgMarshal.ComRelease_StdCall(pMarshal);
            }
        }

        /// <summary>
        /// Get from cache if available, else allocate from heap
        /// </summary>
#if !RHTESTCL
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
#endif
        static internal void* CachedAlloc(int size, ref IntPtr cache)
        {
            // Read cache, clear it
            void* pBlock = (void*)Interlocked.Exchange(ref cache, default(IntPtr));

            if (pBlock == null)
            {
                pBlock =(void*) ExternalInterop.MemAlloc(new UIntPtr((uint)size));
            }

            return pBlock;
        }

        /// <summary>
        /// Return to cache if empty, else free to heap
        /// </summary>
#if !RHTESTCL
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
#endif
        static internal void CachedFree(void* block, ref IntPtr cache)
        {
            if ((void*)Interlocked.CompareExchange(ref cache, new IntPtr(block), default(IntPtr)) != null)
            {
                ExternalInterop.MemFree((IntPtr)block);
            }
        }

        /// <summary>
        /// Return true if the object is a RCW. False otherwise
        /// </summary>
        internal static bool IsComObject(object obj)
        {
            return (obj is __ComObject);
        }

        /// <summary>
        /// Unwrap if this is a managed wrapper
        /// Typically used in data binding
        /// For example, you don't want to data bind against a KeyValuePairImpl<K, V> - you want the real 
        /// KeyValuePair<K, V>
        /// </summary>
        /// <param name="target">The object you want to unwrap</param>
        /// <returns>The original object or the unwrapped object</returns>
        internal static object UnboxManagedWrapperIfBoxed(object target)
        {
            //
            // If the target is boxed by managed code:
            // 1. BoxedValue
            // 2. BoxedKeyValuePair
            // 3. StandardCustomPropertyProviderProxy/EnumerableCustomPropertyProviderProxy/ListCustomPropertyProviderProxy
            // 
            // we should use its value for data binding
            //
            if (InteropExtensions.AreTypesAssignable(target.GetTypeHandle(), typeof(IManagedWrapper).TypeHandle))
            {
                target = ((IManagedWrapper)target).GetTarget();
                Debug.Assert(!(target is IManagedWrapper));
            }

            return target;
        }

        [Flags]
        internal enum CreateComObjectFlags
        {
            None = 0,
            IsWinRTObject,

            /// <summary>
            /// Don't attempt to find out the actual type (whether it is IInspectable nor IProvideClassInfo)
            /// of the incoming interface and do not attempt to unbox them using WinRT rules
            /// </summary>
            SkipTypeResolutionAndUnboxing
        }

        /// <summary>
        /// Returns the existing RCW or create a new RCW from the COM interface pointer
        /// NOTE: Don't use this overload if you already have the identity IUnknown
        /// </summary>
        /// <param name="expectedContext">
        /// The current context of this thread. If it is passed and is not Default, we'll check whether the
        /// returned RCW from cache matches this expected context. If it is not a match (from a different 
        /// context, and is not free threaded), we'll go ahead ignoring the cached entry, and create a new
        /// RCW instead - which will always end up in the current context
        /// We'll skip the check if current == ContextCookie.Default. 
        /// </param>
        internal static object ComInterfaceToComObject(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSigature,
            ContextCookie expectedContext,
            CreateComObjectFlags flags
            )
        {
            Debug.Assert(expectedContext.IsDefault || expectedContext.IsCurrent);

            //
            // Get identity IUnknown for lookup
            //
            IntPtr pComIdentityIUnknown = McgMarshal.ComQueryInterfaceNoThrow(pComItf, ref Interop.COM.IID_IUnknown);

            if (pComIdentityIUnknown == default(IntPtr))
                throw new InvalidCastException();

            try
            {
                object obj = ComInterfaceToComObjectInternal(
                    pComItf,
                    pComIdentityIUnknown,
                    interfaceType,
                    classTypeInSigature,
                    expectedContext,
                    flags
                    );

                return obj;
            }
            finally
            {
                McgMarshal.ComRelease(pComIdentityIUnknown);
            }
        }

        /// <summary>
        /// Returns the existing RCW or create a new RCW from the COM interface pointer
        /// NOTE: This does unboxing unless CreateComObjectFlags.SkipTypeResolutionAndUnboxing is specified
        /// </summary>
        /// <param name="expectedContext">
        /// The current context of this thread. If it is passed and is not Default, we'll check whether the
        /// returned RCW from cache matches this expected context. If it is not a match (from a different 
        /// context, and is not free threaded), we'll go ahead ignoring the cached entry, and create a new
        /// RCW instead - which will always end up in the current context
        /// We'll skip the check if current == ContextCookie.Default. 
        /// </param>
        internal static object ComInterfaceToComObjectInternal(
            IntPtr pComItf,
            IntPtr pComIdentityIUnknown,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSignature,
            ContextCookie expectedContext,
            CreateComObjectFlags flags
            )
        {
            string className;
            object obj = ComInterfaceToComObjectInternal_NoCache(
                pComItf,
                pComIdentityIUnknown,
                interfaceType,
                classTypeInSignature,
                expectedContext,
                flags,
                out className
                );

            //
            // The assumption here is that if the classInfoInSignature is null and interfaceTypeInfo
            // is either IUnknow and IInspectable we need to try unboxing.
            //
            bool doUnboxingCheck = 
                (flags & CreateComObjectFlags.SkipTypeResolutionAndUnboxing) == 0 &&
                obj != null &&
                classTypeInSignature.IsNull() && 
                (interfaceType.Equals(InternalTypes.IUnknown) ||
                 interfaceType.IsIInspectable());

            if (doUnboxingCheck)
            {
                //
                // Try unboxing
                // Even though this might just be a IUnknown * from the signature, we still attempt to unbox
                // if it implements IInspectable
                //
                // @TODO - We might need to optimize this by pre-checking the names to see if they 
                // potentially represents a boxed type, but for now let's keep it simple and I also don't
                // want to replicate the knowledge here
                // @TODO2- We probably should skip the creating the COM object in the first place.
                //
                // NOTE: the RCW here could be a cached one (for a brief time if GC doesn't kick in. as there
                // is nothing to hold the RCW alive for IReference<T> RCWs), so this could save us a RCW
                // creation cost potentially. Desktop CLR doesn't do this. But we also paying for unnecessary
                // cache management cost, and it is difficult to say which way is better without proper
                // measuring
                //
                object unboxedObj = McgMarshal.UnboxIfBoxed(obj, className);
                if (unboxedObj != null)
                    return unboxedObj;
            }

            //
            // In order for variance to work, we save the incoming interface pointer as specified in the 
            // signature into the cache, so that we know this RCW does support this interface and variance 
            // can take advantage of that later
            // NOTE: In some cases, native might pass a WinRT object as a 'compatible' interface, for example,
            // pass IVector<IFoo> as IVector<Object> because they are 'compatible', but QI for IVector<object>
            // won't succeed. In this case, we'll just believe it implements IVector<Object> as in the 
            // signature while the underlying interface pointer is actually IVector<IFoo>
            //
            __ComObject comObject = obj as __ComObject;

            if (comObject != null)
            {
                McgMarshal.ComAddRef(pComItf);

                try
                {
                    comObject.InsertIntoCache(interfaceType, ContextCookie.Current, ref pComItf, true);
                }
                finally
                {
                    //
                    // Only release when a exception is thrown or we didn't 'swallow' the ref count by
                    // inserting it into the cache
                    //
                    McgMarshal.ComSafeRelease(pComItf);
                }
            }

            return obj;
        }

        /// <summary>
        /// Returns the existing RCW or create a new RCW from the COM interface pointer
        /// NOTE: This does not do any unboxing at all. 
        /// </summary>
        /// <param name="expectedContext">
        /// The current context of this thread. If it is passed and is not Default, we'll check whether the
        /// returned RCW from cache matches this expected context. If it is not a match (from a different 
        /// context, and is not free threaded), we'll go ahead ignoring the cached entry, and create a new
        /// RCW instead - which will always end up in the current context
        /// We'll skip the check if current == ContextCookie.Default. 
        /// </param>
        private static object ComInterfaceToComObjectInternal_NoCache(
            IntPtr pComItf,
            IntPtr pComIdentityIUnknown,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSignature,
            ContextCookie expectedContext,
            CreateComObjectFlags flags,
            out string className
            )
        {
            className = null;

            //
            // Lookup RCW in global RCW cache based on the identity IUnknown
            //
            __ComObject comObject = ComObjectCache.Lookup(pComIdentityIUnknown);

            if (comObject != null)
            {
                bool useThisComObject = true;

                if (!expectedContext.IsDefault)
                {
                    //
                    // Make sure the returned RCW matches the context we specify (if any)
                    //
                    if (!comObject.IsFreeThreaded &&
                        !comObject.ContextCookie.Equals(expectedContext))
                    {
                        //
                        // This is a mismatch.
                        // We only care about context for WinRT factory RCWs (which is the only place we are
                        // passing in the context right now). 
                        // When we get back a WinRT factory RCW created in a different context. This means the
                        // factory is a singleton, and the returned IActivationFactory could be either one of 
                        // the following:
                        // 1) A raw pointer, and it acts like a free threaded object
                        // 2) A proxy that is used across different contexts. It might maintain a list of contexts
                        // that it is marshaled to, and will fail to be called if it is not marshaled to this 
                        // context yet.
                        //
                        // In this case, it is unsafe to use this RCW in this context and we should proceed
                        // to create a duplicated one instead. It might make sense to have a context-sensitive
                        // RCW cache but I don't think this case will be common enough to justify it
                        //
                        // @TODO: Check for DCOM proxy as well
                        useThisComObject = false;
                    }
                }

                if (useThisComObject)
                {
                    //
                    // We found one - AddRef and return
                    //
                    comObject.AddRef();
                    return comObject;
                }
            }

            string winrtClassName = null;

            bool isSealed = false;

            if (!classTypeInSignature.IsNull())
            {
                isSealed = classTypeInSignature.IsSealed();
            }

            //
            // Only look at runtime class name if the class type in signature is not sealed
            // NOTE: In the case of System.Uri, we are not pass the class type, only the interface
            //
            if (!isSealed &&
                (flags & CreateComObjectFlags.SkipTypeResolutionAndUnboxing) == 0)
            {
                IntPtr pInspectable;

                bool needRelease = false;

                if (interfaceType.IsWinRTInterface())
                {
                    //
                    // Use the interface pointer as IInspectable as we know it is indeed a WinRT interface that
                    // derives from IInspectable
                    //
                    pInspectable = pComItf;
                }
                else if ((flags & CreateComObjectFlags.IsWinRTObject) != 0)
                {
                    //
                    // Otherwise, if someone tells us that this is a WinRT object, but we don't have a 
                    // IInspectable interface at hand, we'll QI for it
                    //
                    pInspectable = McgMarshal.ComQueryInterfaceNoThrow(pComItf, ref Interop.COM.IID_IInspectable);
                    needRelease = true;
                }
                else
                {
                    pInspectable = default(IntPtr);
                }

                try
                {
                    if (pInspectable != default(IntPtr))
                    {
                        className = McgComHelpers.GetRuntimeClassName(pInspectable);
                        winrtClassName = className;
                    }
                }
                finally
                {
                    if (needRelease && pInspectable != default(IntPtr))
                    {
                        McgMarshal.ComRelease(pInspectable);
                        pInspectable = default(IntPtr);
                    }
                }
            }

            //
            // 1. Prefer using the class returned from GetRuntimeClassName
            // 2. Otherwise use the class (if there) in the signature
            // 3. Out of options - create __ComObject
            //
            RuntimeTypeHandle classTypeToCreateRCW = default(RuntimeTypeHandle);
            RuntimeTypeHandle interfaceTypeFromName = default(RuntimeTypeHandle);

            if (!String.IsNullOrEmpty(className))
            {
                if (!McgModuleManager.TryGetClassTypeFromName(className, out classTypeToCreateRCW))
                {
                    //
                    // If we can't find the class name in our map, try interface as well
                    // Such as IVector<Int32>
                    // This apparently won't work if we haven't seen the interface type in MCG
                    //
                    McgModuleManager.TryGetInterfaceTypeFromName(className, out interfaceTypeFromName);
                }
            }

            if (classTypeToCreateRCW.IsNull())
                classTypeToCreateRCW = classTypeInSignature;

            // Use identity IUnknown to create the new RCW
            // @TODO: Transfer the ownership of ref count to the RCW
            if (classTypeToCreateRCW.IsNull())
            {
                //
                // Create a weakly typed RCW because we have no information about this particular RCW
                // @TODO - what if this RCW is not seen by MCG but actually exists in WinMD and therefore we
                // are missing GCPressure and ComMarshallingType information for this object?
                //
                comObject = new __ComObject(pComIdentityIUnknown, default(RuntimeTypeHandle));
            }
            else
            {
                //
                // Create a strongly typed RCW based on RuntimeTypeHandle
                //
                comObject = CreateComObjectInternal(classTypeToCreateRCW, pComIdentityIUnknown);            // Use identity IUnknown to create the new RCW
            }

#if DEBUG
            //
            // Remember the runtime class name for debugging purpose
            // This way you can tell what the class name is, even when we failed to create a strongly typed
            // RCW for it
            //
            comObject.m_runtimeClassName = className;
#endif

            //
            // Make sure we QI for that interface
            //
            if (!interfaceType.IsNull())
            {
                comObject.QueryInterface_NoAddRef_Internal(interfaceType, /* cacheOnly= */ false, /* throwOnQueryInterfaceFailure= */ false);
            }

            return comObject;
        }

        private static __ComObject CreateComObjectInternal(RuntimeTypeHandle classType, IntPtr pComItf)
        {
            Debug.Assert(!classType.IsNull());

            if (classType.Equals(McgModule.s_DependencyReductionTypeRemovedTypeHandle))
            {
                // We should filter out the strongly typed RCW in TryGetClassInfoFromName step
#if !RHTESTCL
                Environment.FailFast(McgTypeHelpers.GetDiagnosticMessageForMissingType(classType));
#else
                Environment.FailFast("We should never see strongly typed RCW discarded here");
#endif
            }
            
            //Note that this doesn't run the constructor in RH but probably do in your reflection based implementation. 
            //If this were a real RCW, you would actually 'new' the RCW which is wrong. Fortunately in CoreCLR we don't have
            //this scenario so we are OK, but we should figure out a way to fix this by having a runtime API.
            object newClass = InteropExtensions.RuntimeNewObject(classType);

            Debug.Assert(newClass is __ComObject);

            __ComObject newObj = InteropExtensions.UncheckedCast<__ComObject>(newClass);

            IntPtr pfnCtor = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfAttachingCtor>(__ComObject.AttachingCtor);
            CalliIntrinsics.Call<int>(pfnCtor, newObj, pComItf, classType);

            return newObj;
        }


        /// <summary>
        /// Converts a COM interface pointer to a managed object
        /// This either gets back a existing CCW, or a existing RCW, or create a new RCW
        /// </summary>
        internal static object ComInterfaceToObjectInternal(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSignature,
            CreateComObjectFlags flags)
        {
            bool needUnboxing = (flags & CreateComObjectFlags.SkipTypeResolutionAndUnboxing) == 0;

            object ret = ComInterfaceToObjectInternal_NoManagedUnboxing(pComItf, interfaceType, classTypeInSignature, flags);
            if (ret != null && needUnboxing)
            {
                return UnboxManagedWrapperIfBoxed(ret);
            }

            return ret;
        }

        static object ComInterfaceToObjectInternal_NoManagedUnboxing(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSignature,
            CreateComObjectFlags flags)
        {
            if (pComItf == default(IntPtr))
                return null;

            //
            // Is this a CCW?
            //
            ComCallableObject ccw;

            if (ComCallableObject.TryGetCCW(pComItf, out ccw))
            {
                return ccw.TargetObject;
            }

            //
            // This pointer is not a CCW, but we need to do one additional check here for aggregation
            // In case the COM pointer is a interface implementation from native, but the outer object is a
            // managed object
            //
            IntPtr pComIdentityIUnknown = McgMarshal.ComQueryInterfaceNoThrow(pComItf, ref Interop.COM.IID_IUnknown);

            if (pComIdentityIUnknown == default(IntPtr))
                throw new InvalidCastException();

            try
            {
                //
                // Check whether the identity COM pointer to see if it is a aggregating CCW
                //
                if (ComCallableObject.TryGetCCW(pComIdentityIUnknown, out ccw))
                {
                    return ccw.TargetObject;
                }

                //
                // Nope, not a CCW - let's go down our RCW creation code path
                //
                return ComInterfaceToComObjectInternal(
                    pComItf,
                    pComIdentityIUnknown,
                    interfaceType,
                    classTypeInSignature,
                    ContextCookie.Default,
                    flags
                    );
            }
            finally
            {
                McgMarshal.ComRelease(pComIdentityIUnknown);
            }
        }


        internal unsafe static IntPtr ObjectToComInterfaceInternal(Object obj, RuntimeTypeHandle typeHnd)
        {
            if (obj == null)
                return default(IntPtr);

#if ENABLE_WINRT
            //
            // Try boxing if this is a WinRT object
            //
            if (typeHnd.Equals(InternalTypes.IInspectable))
            {
                object unboxed = McgMarshal.BoxIfBoxable(obj);

                //
                // Marshal ReferenceImpl<T> to WinRT as IInspectable
                //
                if (unboxed != null)
                {
                    obj = unboxed;
                }
                else
                {
                    //
                    // Anything that can be casted to object[] will be boxed as object[]
                    //
                    object[] objArray = obj as object[];
                    if (objArray != null)
                    {
                        unboxed = McgMarshal.BoxIfBoxable(obj, typeof(object[]).TypeHandle);
                        if (unboxed != null)
                            obj = unboxed;
                    }
                }
            }
#endif //ENABLE_WINRT

            //
            // If this is a RCW, and the RCW is not a base class (managed class deriving from RCW class), 
            // QI on the RCW
            //
            __ComObject comObject = obj as __ComObject;
            
            if (comObject != null && !comObject.ExtendsComObject)
            {
                IntPtr pComPtr = comObject.QueryInterface_NoAddRef_Internal(typeHnd,  /* cacheOnly= */ false, /* throwOnQueryInterfaceFailure= */ false);
                if (pComPtr == default(IntPtr))
                    return default(IntPtr);

                McgMarshal.ComAddRef(pComPtr);
                GC.KeepAlive(comObject); // make sure we don't collect the object before adding a refcount.

                return pComPtr;
            }

            //
            // Otherwise, go down the CCW code path
            //
            return ManagedObjectToComInterface(obj, typeHnd);
        }

        internal static unsafe IntPtr ManagedObjectToComInterface(Object obj, RuntimeTypeHandle interfaceType)
        {
            Guid iid = interfaceType.GetInterfaceGuid();
            return ManagedObjectToComInterfaceInternal(obj, ref iid, interfaceType);
        }


        internal static unsafe IntPtr ManagedObjectToComInterface(Object obj, ref Guid iid)
        {
            return ManagedObjectToComInterfaceInternal(obj, ref iid, default(RuntimeTypeHandle));
        }

        private static unsafe IntPtr ManagedObjectToComInterfaceInternal(Object obj, ref Guid iid, RuntimeTypeHandle interfaceType)
        {
            if (obj == null)
            {
                return default(IntPtr);
            }

            //
            // Look up ComCallableObject from the cache
            // If couldn't find one, create a new one
            //
            ComCallableObject ccw = null;

            try
            {
                //
                // Either return existing one or create a new one
                // In either case, the returned CCW is addref-ed to avoid race condition
                //
                IntPtr dummy;

                ccw = CCWLookupMap.GetOrCreateCCW(obj, interfaceType, out dummy);
                Debug.Assert(ccw != null);

                return ccw.GetComInterfaceForIID(ref iid, interfaceType);
            }
            finally
            {
                //
                // Free the extra ref count added by GetOrCreateCCW (to protect the CCW from being collected)
                //
                if (ccw != null)
                    ccw.Release();
            }
        }
    }
}
