// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
using Internal.NativeFormat;

namespace System.Runtime.InteropServices
{
    [CLSCompliant(false)]
    public unsafe struct __com_IUnknown
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IUnknown* pVtable;
#pragma warning restore 649
    }

#pragma warning disable 414 // The field 'blah' is assigned but its value is never used

    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    public unsafe struct __vtable_IUnknown
    {
        // IUnknown
        internal IntPtr pfnQueryInterface;
        internal IntPtr pfnAddRef;
        internal IntPtr pfnRelease;

        public static IntPtr pNativeVtable;

        private static __vtable_IUnknown s_theCcwVtable;

        /// <summary>
        /// Eager library initializer called from LibraryInitializer.cs for the module
        /// </summary>
        internal static void Initialize()
        {
            s_theCcwVtable = new __vtable_IUnknown
            {
                pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(QueryInterface),
                pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(AddRef),
                pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(Release),
            };
        }

        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IUnknown);
        }
        internal static unsafe IntPtr GetCcwvtable_IUnknown()
        {
            if (__vtable_IUnknown.pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IUnknown.pNativeVtable,sizeof(__vtable_IUnknown));
                }
            }
            return __vtable_IUnknown.pNativeVtable;
        }

        [NativeCallable]
        public static int QueryInterface(
                    IntPtr __IntPtr__pComThis,
                    IntPtr __IntPtr__pIID,
                    IntPtr __IntPtr__ppvObject)
        {
            // Prevent reentrancy due to message pumping in blocking operations.
            InteropExtensions.SuppressReentrantWaits();

            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;
            Guid* pIID = (Guid*)__IntPtr__pIID;
            void** ppvObject = (void**)__IntPtr__ppvObject;

            // @TODO: does not distinguish between E_NOINTERFACE and E_OUTOFMEMORY
            var cco = pComThis->ComCallableObject;
            IntPtr result = cco.GetComInterfaceForIID(ref *pIID);
            *ppvObject = (void*)result;

            int hr;
            if (result == default(IntPtr))
                hr = Interop.COM.E_NOINTERFACE;
            else
                hr = Interop.COM.S_OK;

            // Restore reentrant waits.  No need for a 'finally' because exceptions in NativeCallable methods will FailFast.
            InteropExtensions.RestoreReentrantWaits();
            return hr;
        }

        /// <summary>
        /// Special NO GC region support helpers
        ///
        /// Declare the GC trigger helpers here because we want to call them directly to avoid
        /// GC stress hijacking InteropExtensions.RhpSet/ClearThreadDoNoTriggerGC
        ///
        /// WARNING: Please avoid using this API whenver possible. If you find yourself must call this
        /// API, please make sure you follow these rules:
        /// 1. The block where GC is disabled need to be small and fast
        /// 2. You can't allocate or call GC.Collect (you'll be surprised how many code actually do)
        /// 3. You can't throw out exception (which allocates)
        /// 4. You can't P/invoke
        /// 5. others?
        /// </summary>

#if CORECLR

        [NativeCallable]
        public static int AddRef(IntPtr __IntPtr__pComThis)
        {
            int newRefCount;

            //
            // NO GC REGION
            //
            // Jupiter assumes AddRef doesn't block on GC and that is indeed the case in desktop CLR
            // We need to match desktop CLR behavior, otherwise Jupiter would be taking a lock and then
            // calling AddRef which triggers another GC and calls out to Jupiter OnGCStart code, which
            // attempts to take the same lock.
            //
            // NOTE 1: We don't call SetDoNotTriggerGC in Release because we do allocate memory in Release()
            // and Jupiter doesn't call it inside their lock for now
            //
            // NOTE 2: Typically you should put those calls in a try/finally, but since you can't throw new
            // exception and even if we throw an existing exception we would failfast inside AddRef anyway,
            // there is no need to put it in a finally
            //

            // RhpSetThreadDoNotTriggerGC are nop on CoreCLR
            GCHelpers.RhpSetThreadDoNotTriggerGC();
            {

                __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;

                //
                // Use ->NativeCCW to make sure we failfast when calling AddRef/Release for neutered CCWs
                //
                newRefCount = pComThis->NativeCCW->AddCOMRef();
            }

            GCHelpers.RhpClearThreadDoNotTriggerGC();

            return newRefCount;
        }

        class GCHelpers
        {
            internal  static void RhpClearThreadDoNotTriggerGC(){}
            internal  static void RhpSetThreadDoNotTriggerGC(){}
        }
#else
        //
        // AddRef is implemented in native code, via Interop.Native.lib.  Here we link in the native code;
        // a DllImport from "*" means "import the named method from a linked .lib file."
        // NOTE:
        // Having McgGeneratedNativeCallCodeAttribute is *REQUIRED* because this is not really a p/invoke but
        // a reverse p/invoke in disguise. It is just convenient to use DllImport as a way to indicate a call
        // into a externally linked module. Without this attribute, MCG will generate code for this as a
        // p/invoke and disaster happens
        //
#if X86
        [DllImport("*", EntryPoint = "_CCWAddRef@4")]
#else
        [DllImport("*", EntryPoint = "CCWAddRef")]
#endif
        [McgGeneratedNativeCallCodeAttribute]
        public static extern int AddRef(IntPtr __IntPtr__pComThis);

        class GCHelpers
        {
            private const string RuntimeLibrary = "[MRT]";
            private const MethodImplOptions InternalCall = (MethodImplOptions)0x1000;

            [MethodImplAttribute(InternalCall)]
            [RuntimeImport(RuntimeLibrary, "RhpClearThreadDoNotTriggerGC")]
            internal extern static void RhpClearThreadDoNotTriggerGC();

            [MethodImplAttribute(InternalCall)]
            [RuntimeImport(RuntimeLibrary, "RhpSetThreadDoNotTriggerGC")]
            internal extern static void RhpSetThreadDoNotTriggerGC();
        }
#endif //CORECLR

        static __vtable_IUnknown()
        {
            ComCallableObject.InitRefCountedHandleCallback();
        }

        [NativeCallable]
        public static int Release(IntPtr __IntPtr__pComThis)
        {
            // Prevent reentrancy due to message pumping in blocking operations.
            InteropExtensions.SuppressReentrantWaits();

            int hr = __interface_ccw.DirectRelease(__IntPtr__pComThis);

            // Restore reentrant waits.  No need for a 'finally' because exceptions in NativeCallable methods will FailFast.
            InteropExtensions.RestoreReentrantWaits();
            return hr;
        }
    }

    [CLSCompliant(false)]
    public unsafe struct __com_IInspectable
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        public __vtable_IInspectable* pVtable;
#pragma warning restore 649
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    public unsafe struct __vtable_IInspectable
    {
        // IUnknown
        internal IntPtr pfnQueryInterface;
        internal IntPtr pfnAddRef;
        internal IntPtr pfnRelease;

        // IInspectable
        internal IntPtr pfnGetIids;
        internal IntPtr pfnGetRuntimeClassName;
        internal IntPtr pfnGetTrustLevel;

        public static IntPtr pNativeVtable;
        private static __vtable_IInspectable s_theCcwVtable = new __vtable_IInspectable
        {
            // IUnknown
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),
            // IInspectable
            pfnGetIids = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetIID>(GetIIDs),
            pfnGetRuntimeClassName = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(GetRuntimeClassName),
            pfnGetTrustLevel = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(GetTrustLevel),
        };
        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IInspectable);
        }
        internal static unsafe IntPtr GetCcwvtable_IInspectable()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IInspectable.pNativeVtable,sizeof(__vtable_IInspectable));
                }
            }
            return __vtable_IInspectable.pNativeVtable;
        }

        const int S_OK = 0;
        const int E_NOTIMPL = unchecked((int)0x80000001);

        [NativeCallable]
        public static int GetIIDs(
                    IntPtr __IntPtr__pComThis,
                    IntPtr __IntPtr__iidCount,
                    IntPtr __IntPtr__iids)
        {
            // Prevent reentrancy due to message pumping in blocking operations.
            InteropExtensions.SuppressReentrantWaits();
            try
            {
                __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;
                int* iidCount = (int*)__IntPtr__iidCount;
                Guid** iids = (Guid**)__IntPtr__iids;

                if (iidCount == null || iids == null)
                    return Interop.COM.E_POINTER;

                *iidCount = 0;
                *iids = null;

                Object target = pComThis->ComCallableObject.TargetObject;

                System.Collections.Generic.Internal.List<Guid> guidList = target.GetTypeHandle().GetIIDs();

                int totalGuids = guidList.Count;

                if (totalGuids > 0)
                {
                    int guidArraySize = totalGuids * sizeof(Guid);
                    if (guidArraySize < 0)
                        return Interop.COM.E_OUTOFMEMORY;

                    *iids = (Guid*)PInvokeMarshal.CoTaskMemAlloc(new UIntPtr((uint)guidArraySize));

                    if (*iids == null)
                    {
                        return Interop.COM.E_OUTOFMEMORY;
                    }

                    for (int i = 0; i < totalGuids; ++i)
                        (*iids)[i] = guidList[i];

                    *iidCount = totalGuids;

                    return Interop.COM.S_OK;
                }


                return Interop.COM.S_OK;
            }
            finally
            {
                InteropExtensions.RestoreReentrantWaits();
            }
        }

        [NativeCallable]
        public static int GetRuntimeClassName(
                    IntPtr __IntPtr__pComThis,
                    IntPtr __IntPtr__className)
        {
#if  ENABLE_WINRT            
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;
            HSTRING* pClassName = (HSTRING*)__IntPtr__className;

            if (pClassName == null)
            {
                return Interop.COM.E_POINTER;
            }

            Object target = pComThis->ComCallableObject.TargetObject;

            string runtimeClassName = target.GetTypeHandle().GetCCWRuntimeClassName();

            if (!string.IsNullOrEmpty(runtimeClassName))
            {
                if (InteropEventProvider.IsEnabled())
                    InteropEventProvider.Log.TaskCCWQueryRuntimeClassName((long)pComThis->NativeCCW, runtimeClassName);
                return McgMarshal.StringToHStringNoNullCheck(runtimeClassName, pClassName);
            }

            *pClassName = default(HSTRING);

            if (InteropEventProvider.IsEnabled())
                InteropEventProvider.Log.TaskCCWQueryRuntimeClassName((long)pComThis->NativeCCW, String.Empty);

            return Interop.COM.S_OK;
#else
            throw new PlatformNotSupportedException("GetRuntimeClassName");
#endif
        }

        const int BaseTrust = 0;

        [NativeCallable]
        public static int GetTrustLevel(
                        IntPtr __IntPtr__pComThis,
                        IntPtr __IntPtr__pTrustLevel)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;
            int* pTrustLevel = (int*)__IntPtr__pTrustLevel;

            if (pTrustLevel == null)
            {
                return Interop.COM.E_POINTER;
            }

            *pTrustLevel = BaseTrust;

            return S_OK;
        }
    }

    [CLSCompliant(false)]
    public unsafe struct __com_IDispatch
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IDispatch* pVtable;
#pragma warning restore 649
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    public unsafe struct __vtable_IDispatch
    {
        // IUnknown
        internal IntPtr pfnQueryInterface;
        internal IntPtr pfnAddRef;
        internal IntPtr pfnRelease;

        // IDispatch
        internal IntPtr pfnGetTypeInfoCount;
        internal IntPtr pfnGetTypeInfo;
        internal IntPtr pfnGetIDsOfNames;
        internal IntPtr pfnInvoke;

        public static IntPtr pNativeVtable;
        private static __vtable_IDispatch s_theCcwVtable = new __vtable_IDispatch
        {
            // IUnknown
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),
            // IDispatch
            pfnGetTypeInfoCount = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(GetTypeInfoCount),
            pfnGetTypeInfo = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetTypeInfo>(GetTypeInfo),
            pfnGetIDsOfNames = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetIDsOfNames>(GetIDsOfNames),
            pfnInvoke = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfInvoke>(Invoke),
        };
        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IDispatch);
        }
        internal static unsafe IntPtr GetCcwvtable_IDispatch()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IDispatch.pNativeVtable, sizeof(__vtable_IDispatch));
                }
            }
            return __vtable_IDispatch.pNativeVtable;
        }

        const int E_NOTIMPL = unchecked((int)0x80000001);

        [NativeCallable]
        public static int GetTypeInfoCount(
            IntPtr pComThis, 
            IntPtr pctinfo)
        {
            return E_NOTIMPL;
        }

        [NativeCallable]
        public static int GetTypeInfo(
            IntPtr pComThis,
            uint iTInfo,
            uint lcid,
            IntPtr ppTInfo)
        {
            return E_NOTIMPL;
        }

        [NativeCallable]
        public static int GetIDsOfNames(
            IntPtr pComThis,
            IntPtr riid,
            IntPtr rgszNames,
            uint cNames,
            uint lcid,
            IntPtr rgDispId)
        {
            return E_NOTIMPL;
        }

        [NativeCallable]
        public static int Invoke(
            IntPtr pComThis,
            int dispIdMember,
            IntPtr riid,
            uint lcid,
            ushort wFlags,
            IntPtr pDispParams,
            IntPtr pVarResult,
            IntPtr pExcepInfo,
            IntPtr puArgErr)
        {
            return E_NOTIMPL;
        }
    }

#if ENABLE_WINRT
    internal unsafe struct __com_ICustomPropertyProvider
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        public __vtable_ICustomPropertyProvider* pVtable;
#pragma warning restore 649
    }

    internal unsafe struct __vtable_ICustomPropertyProvider
    {
        // The ICustomProperty interop implementation is generated by MCG so we need the
        // IID to convert it between a managed object and COM interface
        //30da92c0-23e8-42a0-ae7c-734a0e5d2782
        static Guid ICustomPropertyIID = new Guid(0x30da92c0, 0x23e8, 0x42a0, 0xae, 0x7c, 0x73, 0x4a, 0x0e, 0x5d, 0x27, 0x82);

        // IUnknown
        IntPtr pfnQueryInterface;
        IntPtr pfnAddRef;
        IntPtr pfnRelease;
        // IInspectable
        IntPtr pfnGetIids;
        IntPtr pfnGetRuntimeClassName;
        IntPtr pfnGetTrustLevel;
        // ICustomPropertyProvider
        IntPtr pfnGetCustomProperty;
        IntPtr pfnGetIndexedProperty;
        IntPtr pfnGetStringRepresentation;
        IntPtr pfnget_Type;

        public static IntPtr pNativeVtable;
        static __vtable_ICustomPropertyProvider s_theCcwVtable = new __vtable_ICustomPropertyProvider
        {
            // IUnknown
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),
            // IInspectable
            pfnGetIids = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetIID>(__vtable_IInspectable.GetIIDs),
            pfnGetRuntimeClassName = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IInspectable.GetRuntimeClassName),
            pfnGetTrustLevel = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IInspectable.GetTrustLevel),
            // ICustomPropertyProvider
            pfnGetCustomProperty = AddrOfIntrinsics.AddrOf<WinRTAddrOfIntrinsics.AddrOfGetCustomProperty>(GetCustomProperty__STUB),
            pfnGetIndexedProperty = AddrOfIntrinsics.AddrOf<WinRTAddrOfIntrinsics.AddrOfGetIndexedProperty>(GetIndexedProperty__STUB),
            pfnGetStringRepresentation = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(GetStringRepresentation__STUB),
            pfnget_Type = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(get_Type__STUB),
        };
        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_ICustomPropertyProvider);
        }
        internal static unsafe IntPtr GetCcwvtable_ICustomPropertyProvider()
        {
            if(pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_ICustomPropertyProvider.pNativeVtable,sizeof(__vtable_ICustomPropertyProvider));
                }
            }
            return __vtable_ICustomPropertyProvider.pNativeVtable;
        }

        [NativeCallable]
        static int GetCustomProperty__STUB(
                    System.IntPtr pComThis,
                    HSTRING unsafe_name,
                    IntPtr __IntPtr__unsafe_customProperty)
        {
            void** unsafe_customProperty = (void**)__IntPtr__unsafe_customProperty;
            // Initialize [out] parameters
            *unsafe_customProperty = default(void*);

            object target = ComCallableObject.FromThisPointer(pComThis).TargetObject;
            string propertyName = McgMarshal.HStringToString(unsafe_name);
            try
            {
                global::Windows.UI.Xaml.Data.ICustomProperty property = ManagedGetCustomProperty(target, propertyName);
                    *unsafe_customProperty = (void*)McgComHelpers.ManagedObjectToComInterface(
                        property,
                        typeof(global::Windows.UI.Xaml.Data.ICustomProperty).TypeHandle);
            }
            catch (Exception)
            {
                // Don't fail if property can't be found
            }
            return Interop.COM.S_OK;
        }

        /// <summary>
        /// Safe Implementation of ICustomPropertyProvider.GetCustomProperty
        /// </summary>
        /// <param name="target">Object to find a property on</param>
        /// <param name="propertyName">Name of the property to find</param>
        /// <returns>ICustomProperty representing the property</returns>
        private static global::Windows.UI.Xaml.Data.ICustomProperty ManagedGetCustomProperty(object target, string propertyName)
        {
            try
            {
                target = CustomPropertyImpl.UnwrapTarget(target);

                PropertyInfo propertyInfo = target.GetType().GetProperty(propertyName);

                if (propertyInfo != null)
                    return new CustomPropertyImpl(propertyInfo, supportIndexerWithoutMetadata : false);

                // Weakly-Typed RCW scenario
                // Check cached interface to see whether it supports propertyName Property
                if (McgMarshal.IsOfType(target, typeof(__ComObject).TypeHandle))
                {
                    __ComObject comObj = (__ComObject)target;
                    propertyInfo = comObj.GetMatchingProperty(
                        (PropertyInfo p) => { if (p.Name == propertyName) return true; return false; }
                    );
                    if (propertyInfo != null)
                        return new CustomPropertyImpl(propertyInfo, supportIndexerWithoutMetadata : false);
                }
            }
            catch (MissingMetadataException ex)
            {
                CustomPropertyImpl.LogDataBindingError(propertyName, ex);
            }

            return null;
        }

        [NativeCallable]
        static int GetIndexedProperty__STUB(
                    System.IntPtr pComThis,
                    HSTRING unsafe_name,
                    TypeName unsafe_type,
                    IntPtr __IntPtr__unsafe_customProperty)
        {
            void** unsafe_customProperty = (void**)__IntPtr__unsafe_customProperty;
            
            // Initialize [out] parameters
            *unsafe_customProperty = default(void*);
            try
            {
                object target = ComCallableObject.FromThisPointer(pComThis).TargetObject;
                string propertyName = McgMarshal.HStringToString(unsafe_name);
                Type indexerType = McgMarshal.TypeNameToType(unsafe_type.Name, (int)unsafe_type.Kind);

                global::Windows.UI.Xaml.Data.ICustomProperty property = ManagedGetIndexedProperty(target, propertyName, indexerType);
                *unsafe_customProperty = (void*)McgComHelpers.ManagedObjectToComInterface(
                    property,
                    typeof(global::Windows.UI.Xaml.Data.ICustomProperty).TypeHandle);
            }
            catch (Exception)
            {
                // Don't fail if property can't be found - just return S_OK and NULL property
            }

            return Interop.COM.S_OK;
        }


        /// <summary>
        /// Determine whether specifed property is the matching one based on propertyName and indexerType
        /// </summary>
        /// <param name="property"></param>
        /// <param name="propertyName"></param>
        /// <param name="indexerType"></param>
        /// <returns></returns>
        private static bool IsMatchingIndexedProperty(PropertyInfo property, string propertyName, Type indexerType)
        {
            if (property.Name != propertyName)
            {
                return false;
            }

            ParameterInfo[] indexParameters = property.GetIndexParameters();
            if (indexParameters.Length != 1)
            {
                return false;
            }

            if (indexParameters[0].ParameterType != indexerType)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Safe Implementation of ICustomPropertyProvider.GetIndexedProperty
        /// </summary>
        /// <param name="target">Object to find a property on</param>
        /// <param name="propertyName">Name of the property to find</param>
        /// <param name="indexerType">Type of indexer used on the indexed property to distinguish overloads</param>
        /// <returns>ICustomProperty representing the property</returns>
        private static global::Windows.UI.Xaml.Data.ICustomProperty ManagedGetIndexedProperty(object target, string propertyName, Type indexerType)
        {
            target = CustomPropertyImpl.UnwrapTarget(target);

            // We can do indexing on lists and dictionaries without metadata as a fallback
            bool supportIndexerWithoutMetadata = (target is IList || target is IDictionary);

            try
            {
                BindingFlags Everything = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
                foreach (PropertyInfo property in target.GetType().GetProperties(Everything))
                {
                    if (IsMatchingIndexedProperty(property, propertyName, indexerType))
                    {
                        return new CustomPropertyImpl(property, supportIndexerWithoutMetadata);
                    }
                }

                // Weakly-Typed RCW
                // Check cached interface to see whether it supports propertyName Property
                if (McgMarshal.IsOfType(target, typeof(__ComObject).TypeHandle))
                {
                    __ComObject comObj = (__ComObject)target;
                    PropertyInfo property = comObj.GetMatchingProperty(
                        (PropertyInfo p) => { return IsMatchingIndexedProperty(p, propertyName, indexerType); }
                    );

                    if (property != null)
                    {
                        return new CustomPropertyImpl(property, supportIndexerWithoutMetadata);
                    }
                }
            }
            catch (MissingMetadataException ex)
            {
                CustomPropertyImpl.LogDataBindingError(propertyName, ex);
            }
           
            if (supportIndexerWithoutMetadata)
            {
                return new CustomPropertyImpl(null, supportIndexerWithoutMetadata, target.GetType());
            }
            
            return null;
        }

        [NativeCallable]
        static int GetStringRepresentation__STUB(
                    System.IntPtr pComThis,
                    IntPtr __IntPtr__unsafe_stringRepresentation)
        {
            HSTRING* unsafe_stringRepresentation = (HSTRING*)__IntPtr__unsafe_stringRepresentation;

            try
            {
                // Check whether the ICustomPropertyProvider implements IStringable.
                // If so, we prefer IStringable implementation over object.ToString().
                object target = ComCallableObject.FromThisPointer(pComThis).TargetObject;

                string stringRepresentation;
                if (!IStringableHelper.TryGetIStringableToString(target, out stringRepresentation))
                {
                    stringRepresentation = ManagedGetStringRepresentation(target);
                }

                //
                // To align with desktop behavior: this is the only place we convert null string to
                // NULL HSTRING
                //
                if (stringRepresentation == null)
                    *unsafe_stringRepresentation = default(HSTRING);
                else
                    *unsafe_stringRepresentation = McgMarshal.StringToHString(stringRepresentation);
            }
            catch (Exception ex) when (McgMarshal.PropagateException(ex))
            {
                return McgMarshal.GetHRForExceptionWinRT(ex);
            }

            return Interop.COM.S_OK;
        }


        /// <summary>
        /// Safe Implementation of ICustomPropertyProvider.GetStringRepresentation.
        /// Just calls .ToString()
        /// </summary>
        /// <param name="target">Object to get a string representation for</param>
        /// <returns>String representation of the object</returns>
        private static string ManagedGetStringRepresentation(object target)
        {
            return CustomPropertyImpl.UnwrapTarget(target).ToString();
        }

        [NativeCallable]
        static int get_Type__STUB(
                    System.IntPtr pComThis,
                    IntPtr __IntPtr__unsafe_value)
        {

            TypeName* unsafe_value = (TypeName*)__IntPtr__unsafe_value;
            *unsafe_value = default(TypeName);
            int kind;
            try
            {
                // Since this is only used for databinding, we want to round trip to a McgFakeType if
                // the type is unreflectable. This prevents MissingMetadataExceptions in XAML-generated code.
                Type realType = CustomPropertyImpl.UnwrapTarget(ComCallableObject.FromThisPointer(pComThis).TargetObject).GetType();
                Type xamlSafeType = McgTypeHelpers.GetReflectableOrFakeType(realType);

                McgTypeHelpers.TypeToTypeName(
                    xamlSafeType,
                    out unsafe_value->Name,
                    out kind);

                unsafe_value->Kind = (TypeKind)kind;
            }
            catch (Exception)
            {
                // Don't fail---Align with desktop behavior
            }
            return Interop.COM.S_OK;
        }
    }
#endif //ENABLE_WINRT

    /// <summary>
    /// This is a special type we'll create CCW for but does not implement any WinRT interfaces
    /// We need to ask MCG to generate templates for it explicitly by marking with McgComCallableAttribute
    /// </summary>
    [McgComCallableAttribute]
    internal class StandardCustomPropertyProviderProxy : IManagedWrapper
    {
        Object m_target;

        internal StandardCustomPropertyProviderProxy(object target)
        {
            m_target = target;
        }

        public object GetTarget()
        {
            return m_target;
        }
    }

    internal class EnumerableCustomPropertyProviderProxy : IManagedWrapper, System.Collections.IEnumerable
    {
        System.Collections.IEnumerable m_target;

        internal EnumerableCustomPropertyProviderProxy(System.Collections.IEnumerable target)
        {
            m_target = target;
        }

        public object GetTarget()
        {
            return m_target;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_target.GetEnumerator();
        }
    }

    internal class ListCustomPropertyProviderProxy : IManagedWrapper, System.Collections.IList
    {
        System.Collections.IList m_target;

        internal ListCustomPropertyProviderProxy(System.Collections.IList target)
        {
            m_target = target;
        }

        public object GetTarget()
        {
            return m_target;
        }

        int System.Collections.IList.Add(object value)
        {
            return m_target.Add(value);
        }

        void System.Collections.IList.Clear()
        {
            m_target.Clear();
        }

        bool System.Collections.IList.Contains(object value)
        {
            return m_target.Contains(value);
        }

        int System.Collections.IList.IndexOf(object value)
        {
            return m_target.IndexOf(value);
        }

        void System.Collections.IList.Insert(int index, object value)
        {
            m_target.Insert(index, value);
        }

        bool System.Collections.IList.IsFixedSize
        {
            get { return m_target.IsFixedSize; }
        }

        bool System.Collections.IList.IsReadOnly
        {
            get { return m_target.IsReadOnly; }
        }

        void System.Collections.IList.Remove(object value)
        {
            m_target.Remove(value);
        }

        void System.Collections.IList.RemoveAt(int index)
        {
            m_target.RemoveAt(index);
        }

        object System.Collections.IList.this[int index]
        {
            get
            {
                return m_target[index];
            }
            set
            {
                m_target[index] = value;
            }
        }

        void System.Collections.ICollection.CopyTo(Array array, int index)
        {
            m_target.CopyTo(array, index);
        }

        int System.Collections.ICollection.Count
        {
            get { return m_target.Count; }
        }

        bool System.Collections.ICollection.IsSynchronized
        {
            get { return m_target.IsSynchronized; }
        }

        object System.Collections.ICollection.SyncRoot
        {
            get { return m_target.SyncRoot; }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_target.GetEnumerator();
        }
    }

#if  ENABLE_WINRT
    /// <summary>
    /// IWeakReferenceSource implementation
    /// Because it doesn't actually implement IWeakReferenceSource, we'll ask MCG to import this as a
    /// CCWTemplate explicitly to avoid global interface lookup.
    /// </summary>
    [McgComCallableAttribute]
    internal class WeakReferenceSource
    {
        // Here we use WeakReference<Object> instead of WeakReference<ComCallableObject>
        // The reason is:
        // .if we use WeakReference<ComCallableObject>, then we will create a relationship: IWeakReference-->Managed Object CCW-->Managed Object,
        // during this relationship, none will keep this Managed Object CCW alive(the link between IWeakReference to Managed Object CCW is WeakReference<T>).
        // So if GC occurs and Managed Object CCW will be GCed, The call IWeakReference.Resolve will fail even managed object is still alive.
        // .if use WeakReference<Object>, then we create a relationship:IWeakReference---> Managed Object, in this way, we don't care
        // whether the managed object CCW is alive or not as long as managed object is alive.
        // But there is a drawback for using WeakReference<Object>: The IUknown pointer of CCW will change.
        // since we will create new CCW if the old one is Gced and the mangaed object still is alive.
        // So if user try to cache this IUnknown pointer of CCW instead of IWeakReference Pointer, he/she may encounter problem during Resolve
        // Workaround for this drawback is to cache IWeakReference instead of IUnknown pointer of CCW if user need to cache.
        private WeakReference<Object> weakRef;

        internal WeakReferenceSource(Object obj)
        {
            weakRef = new WeakReference<Object>(obj);
        }

        internal object Resolve()
        {
            Object targetObject;

            if (weakRef.TryGetTarget(out targetObject))
            {
                return targetObject;
            }

            return null;
        }
    }

    internal unsafe struct __com_IWeakReferenceSource
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IWeakReferenceSource* pVtable;
#pragma warning restore 649
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal unsafe struct __vtable_IWeakReferenceSource
    {
        // IUnknown
        IntPtr pfnQueryInterface;
        IntPtr pfnAddRef;
        IntPtr pfnRelease;
        // IWeakReferenceSource
        internal IntPtr pfnGetWeakReference;

        public static IntPtr pNativeVtable;
        private static __vtable_IWeakReferenceSource s_theCcwVtable = new __vtable_IWeakReferenceSource
        {
            // IUnknown
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),
            // IWeakReferenceSource
            pfnGetWeakReference = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(GetWeakReference),
        };

        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IWeakReferenceSource);
        }
        internal static unsafe IntPtr GetCcwvtable_IWeakReferenceSource()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IWeakReferenceSource.pNativeVtable,sizeof(__vtable_IWeakReferenceSource));
                }
            }
            return __vtable_IWeakReferenceSource.pNativeVtable;
        }

        [NativeCallable]
        private static unsafe int GetWeakReference(
                                    IntPtr __IntPtr__pComThis,
                                    IntPtr __IntPtr__ppWeakReference)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;
            __com_IWeakReference** ppWeakReference = (__com_IWeakReference**)__IntPtr__ppWeakReference;

            if (ppWeakReference == null)
            {
                return Interop.COM.E_INVALIDARG;
            }

            var cco = pComThis->ComCallableObject;
            WeakReferenceSource source = new WeakReferenceSource(cco.TargetObject);
            (*ppWeakReference) = (__com_IWeakReference*)McgMarshal.ManagedObjectToComInterface(source, InternalTypes.IWeakReference);

            return Interop.COM.S_OK;
        }
    }

    internal unsafe struct __com_IWeakReference
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IWeakReference* pVtable;
#pragma warning restore 649
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal unsafe struct __vtable_IWeakReference
    {
        // IUnknown
        IntPtr pfnQueryInterface;
        IntPtr pfnAddRef;
        IntPtr pfnRelease;
        // IWeakReference
        internal IntPtr pfnResolve;

        public static IntPtr pNativeVtable;
        private static __vtable_IWeakReference s_theCcwVtable = new __vtable_IWeakReference
        {
            // IUnknown
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),
            // IWeakReference
            pfnResolve = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfResolve>(Resolve),
        };
        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IWeakReference);
        }
        internal static unsafe IntPtr GetCcwvtable_IWeakReference()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IWeakReference.pNativeVtable,sizeof(__vtable_IWeakReference));
                }
            }
            return __vtable_IWeakReference.pNativeVtable;
        }

        [NativeCallable]
        private static unsafe int Resolve(
                                    IntPtr __IntPtr__pComThis,
                                    IntPtr __IntPtr__piid,
                                    IntPtr __IntPtr__ppWeakReference)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;
            Guid* piid = (Guid*)__IntPtr__piid;
            __com_IInspectable** ppWeakReference = (__com_IInspectable**)__IntPtr__ppWeakReference;

            if (ppWeakReference == null)
            {
                return Interop.COM.E_INVALIDARG;
            }

            int hr;
            var cco = pComThis->ComCallableObject;
            WeakReferenceSource source = (WeakReferenceSource)cco.TargetObject;
            object targetObject = source.Resolve();

            if (targetObject == null)
            {
                hr = Interop.COM.S_OK;
                *ppWeakReference = null;
            }
            else
            {
                (*ppWeakReference) = (__com_IInspectable*)
                    McgComHelpers.ManagedObjectToComInterface(
                        targetObject,
                        ref *piid
                    );

                hr = (*ppWeakReference) != null ? Interop.COM.S_OK : Interop.COM.E_NOINTERFACE;
            }

            if ((hr < 0) && (InteropEventProvider.IsEnabled()))
                InteropEventProvider.Log.TaskCCWResolveFailure((long)pComThis->NativeCCW, (long)pComThis, *piid, hr);

            return hr;
        }
    }


    unsafe struct __com_IStringable
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IStringable* pVtable;
#pragma warning restore 649
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal unsafe struct __vtable_IStringable
    {
        // IUnknown
        IntPtr pfnQueryInterface;
        IntPtr pfnAddRef;
        IntPtr pfnRelease;

        // IInspectable
        IntPtr pfnGetIids;
        IntPtr pfnGetRuntimeClassName;
        IntPtr pfnGetTrustLevel;

        // IStringable
        internal IntPtr pfnToString;

        public static IntPtr pNativeVtable;
        private static __vtable_IStringable s_theCcwVtable = new __vtable_IStringable
        {
            // IUnknown
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),

            // IInspectable
            pfnGetIids = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetIID>(__vtable_IInspectable.GetIIDs),
            pfnGetRuntimeClassName = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IInspectable.GetRuntimeClassName),
            pfnGetTrustLevel = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IInspectable.GetTrustLevel),

            // IStringable
            pfnToString = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(ToString),
        };

        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IStringable);
        }
        internal static unsafe IntPtr GetCcwvtable_IStringable()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IStringable.pNativeVtable,sizeof(__vtable_IStringable));
                }
            }
            return __vtable_IStringable.pNativeVtable;
        }

        [NativeCallable]
        private static unsafe int ToString(
                                    IntPtr pComThis,
                                    IntPtr __IntPtr__pResult)
        {
            // If we have reached here then the managed object does not directly implement
            // IStringable and hence we need to rely on the default Object.ToString() behavior.

            // Since the default implementation is only a best effort scenario where we may or may
            // not have the metadata to give the Type.FullName, this method call might not be very useful
            HSTRING* pResult = (HSTRING*)__IntPtr__pResult;
            int hr;

            if (pResult == null)
                return Interop.COM.E_POINTER;

            object target = ComCallableObject.FromThisPointer(pComThis).TargetObject;
            Debug.Assert(target != null);

            try
            {
                string pResult_String = target.ToString();

                if (pResult_String == null)
                {
                    *pResult = default(HSTRING);
                    hr = Interop.COM.S_OK;
                }
                else
                {
                    hr = McgMarshal.StringToHStringNoNullCheck(pResult_String, pResult);
                }

                return hr;
            }
            catch (Exception ex) when (McgMarshal.PropagateException(ex))
            {
                return McgMarshal.GetHRForExceptionWinRT(ex);
            }
        }
    }
#endif
    /// <summary>
    /// Implementation of ICCW for Jupiter Lifetime Feature
    /// Currently this implementation doesn't do anything - it only delegates the Jupiter AddRef/Release
    /// calls to real COM ref. This is done to avoid CCW gets destroyed when Jupiter does the following:
    /// 1. COM Add Ref
    /// 2. Jupiter Add Ref
    /// 3. COM release
    /// </summary>
    internal unsafe struct __vtable_ICCW
    {
        // IUnknown
        IntPtr pfnQueryInterface;
        IntPtr pfnAddRef;
        IntPtr pfnRelease;

        // ICCW
        IntPtr pfnAddRefFromJupiter;
        IntPtr pfnReleaseFromJupiter;
        IntPtr pfnPeg;
        IntPtr pfnUnpeg;

        public static IntPtr pNativeVtable;
        static __vtable_ICCW s_theCcwVtable = new __vtable_ICCW
        {
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(QueryInterface__STUB),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(AddRef__STUB),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(Release__STUB),
            pfnAddRefFromJupiter = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(AddRefFromJupiter__STUB),
            pfnReleaseFromJupiter = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(ReleaseFromJupiter__STUB),
            pfnPeg = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(Peg__STUB),
            pfnUnpeg = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(Unpeg__STUB),
        };
        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_ICCW);
        }
        internal static unsafe IntPtr GetCcwvtable_ICCW()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_ICCW.pNativeVtable,sizeof(__vtable_ICCW));
                }
            }
            return __vtable_ICCW.pNativeVtable;
        }

        /// <remarks>
        /// This is safe to mark as [NativeCallable] because Jupiter should never call it inside GC callout
        /// in the first place
        /// <remarks>
        [NativeCallable]
        static int QueryInterface__STUB(
                    IntPtr __IntPtr__pComThis,
                    IntPtr __IntPtr__pIID,
                    IntPtr __IntPtr__ppvObject)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;
            Guid* pIID = (Guid*)__IntPtr__pIID;
            void** ppvObject = (void**)__IntPtr__ppvObject;

            //
            // Check for neutered CCWs
            // This is the only QI that can be called on a neutered CCW and we fail with a well defined
            // COR_E_ACCESSING_CCW error code back to Jupiter
            // NOTE: We should not use IsNeutered check here, instead, we have to keep a reference to CCW
            // from here to avoid CCW getting neutered after the check.
            //
            var cco = pComThis->NativeCCW->ComCallableObjectUnsafe;

            if (cco == null)
                return Interop.COM.COR_E_ACCESSING_CCW;

            // @TODO: does not distinguish between E_NOINTERFACE and E_OUTOFMEMORY
            IntPtr result = cco.GetComInterfaceForIID(ref *pIID);
            *ppvObject = (void*)result;

            if (result == default(IntPtr))
                return Interop.COM.E_NOINTERFACE;

            return Interop.COM.S_OK;
        }

        [NativeCallable]
        static int AddRef__STUB(IntPtr __IntPtr__pComThis)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;

            //
            // Use ->NativeCCW to support accessing neutered CCWs
            //
            return pComThis->NativeCCW->AddCOMRef();
        }

        [NativeCallable]
        static int Release__STUB(IntPtr __IntPtr__pComThis)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;

            //
            // Use ->NativeCCW to support accessing neutered CCWs
            //
            return pComThis->NativeCCW->ReleaseCOMRef();
        }

        [NativeCallable]
        static int AddRefFromJupiter__STUB(System.IntPtr __IntPtr__pComThis)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;

            //
            // Use ->NativeCCW to support accessing neutered CCWs
            //
            return pComThis->NativeCCW->AddJupiterRef();
        }

        [NativeCallable]
        static int ReleaseFromJupiter__STUB(System.IntPtr __IntPtr__pComThis)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;

            //
            // Use ->NativeCCW to support accessing neutered CCWs
            //
            return pComThis->NativeCCW->ReleaseJupiterRef();
        }

        [NativeCallable]
        static int Peg__STUB(System.IntPtr __IntPtr__pComThis)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;

            //
            // Use ->NativeCCW to support accessing neutered CCWs
            //
            pComThis->NativeCCW->IsPegged = true;

            return 0;
        }

        [NativeCallable]
        static int Unpeg__STUB(System.IntPtr __IntPtr__pComThis)
        {
            __interface_ccw* pComThis = (__interface_ccw*)__IntPtr__pComThis;

            //
            // Use ->NativeCCW to support accessing neutered CCWs
            //
            pComThis->NativeCCW->IsPegged = false;

            return 0;
        }
    }

#if ENABLE_MIN_WINRT
    unsafe struct __com_IActivationFactoryInternal
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IActivationFactoryInternal* pVtable;
#pragma warning restore 649
    }

    internal unsafe struct __vtable_IActivationFactoryInternal
    {
        // IUnknown
        IntPtr pfnQueryInterface;
        IntPtr pfnAddRef;
        IntPtr pfnRelease;
        // IInspectable
        IntPtr pfnGetIids;
        IntPtr pfnGetRuntimeClassName;
        IntPtr pfnGetTrustLevel;
        // IActivationFactoryInternal
        internal IntPtr pfnActivateInstance;

        static IntPtr pNativeVtable;
        private static __vtable_IActivationFactoryInternal s_theCcwVtable = new __vtable_IActivationFactoryInternal
        {
            // IUnknown
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),
            // IInspectable
            pfnGetIids = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetIID>(__vtable_IInspectable.GetIIDs),
            pfnGetRuntimeClassName = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IInspectable.GetRuntimeClassName),
            pfnGetTrustLevel = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IInspectable.GetTrustLevel),
            // IActivationFactoryInternal
            pfnActivateInstance = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(ActivateInstance)
        };
        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IActivationFactoryInternal);
        }
        internal static unsafe IntPtr GetCcwvtable_IActivationFactoryInternal()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IActivationFactoryInternal.pNativeVtable,sizeof(__vtable_IActivationFactoryInternal));
                    return __vtable_IActivationFactoryInternal.pNativeVtable;
                }
            }
            return __vtable_IActivationFactoryInternal.pNativeVtable;
        }

        [NativeCallable]
        internal static int ActivateInstance(
            IntPtr pComThis,
            IntPtr ppResult)
        {
            IntPtr* pResult = (IntPtr*)ppResult;
            if (pResult == null)
                return Interop.COM.E_POINTER;

            try
            {
                object target = ComCallableObject.FromThisPointer(pComThis).TargetObject;
                *pResult = (IntPtr)((IActivationFactoryInternal)target).ActivateInstance();
                return Interop.COM.S_OK;
            }
            catch (System.Exception hrExcep) when (McgMarshal.PropagateException(hrExcep))
            {
                *pResult = default(IntPtr);
                return McgMarshal.GetHRForExceptionWinRT(hrExcep);
            }
        }
    }
#endif // ENABLE_MIN_WINRT


    unsafe struct __com_IManagedActivationFactory
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IManagedActivationFactory* pVtable;
#pragma warning restore 649
    }

    internal unsafe struct __vtable_IManagedActivationFactory
    {
        // IUnknown
        IntPtr pfnQueryInterface;
        IntPtr pfnAddRef;
        IntPtr pfnRelease;
        // IInspectable
        IntPtr pfnGetIids;
        IntPtr pfnGetRuntimeClassName;
        IntPtr pfnGetTrustLevel;
        // IManagedActivationFactory
        IntPtr pfnRunClassConstructor;

        public static IntPtr pNativeVtable;
        private static __vtable_IManagedActivationFactory s_theCcwVtable = new __vtable_IManagedActivationFactory
        {
            // IUnknown
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),
            // IInspectable
            pfnGetIids = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetIID>(__vtable_IInspectable.GetIIDs),
            pfnGetRuntimeClassName = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IInspectable.GetRuntimeClassName),
            pfnGetTrustLevel = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IInspectable.GetTrustLevel),
            // IManagedActivationFactory
            pfnRunClassConstructor = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(RunClassConstructor),
        };
        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IManagedActivationFactory);
        }
        internal static unsafe IntPtr GetCcwvtable_IManagedActivationFactory()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IManagedActivationFactory.pNativeVtable,sizeof(__vtable_IManagedActivationFactory));

                }
            }
            return __vtable_IManagedActivationFactory.pNativeVtable;
        }

        [NativeCallable]
        static int RunClassConstructor(
            IntPtr pComThis)
        {
            try
            {
                ((IManagedActivationFactory)ComCallableObject.GetTarget(pComThis)).RunClassConstructor();
                return Interop.COM.S_OK;
            }
            catch (System.Exception hrExcep) when (McgMarshal.PropagateException(hrExcep))
            {
                return McgMarshal.GetHRForExceptionWinRT(hrExcep);
            }
        }
    }

    unsafe struct __com_IMarshal
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IMarshal* pVtable;
#pragma warning restore 649
    }

    /// <summary>
    /// IMarshal implementation that delegates to FTM
    /// </summary>
    internal unsafe struct __vtable_IMarshal
    {
        // IUnknown
        IntPtr pfnQueryInterface;
        IntPtr pfnAddRef;
        IntPtr pfnRelease;
        // IMarshal
        IntPtr pfnGetUnmarshalClass;
        IntPtr pfnGetMarshalSizeMax;
        IntPtr pfnMarshalInterface;
        IntPtr pfnUnmarshalInterface;
        IntPtr pfnReleaseMarshalData;
        IntPtr pfnDisconnectObject;

        public static IntPtr pNativeVtable;
        private static __vtable_IMarshal s_theCcwVtable = new __vtable_IMarshal
        {
            // IUnknown
            pfnQueryInterface       = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(__vtable_IUnknown.QueryInterface),
            pfnAddRef               = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(__vtable_IUnknown.AddRef),
            pfnRelease              = AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release),
            // IMarshal
            pfnGetUnmarshalClass    = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetMarshalUnMarshal>(__vtable_IMarshal.GetUnmarshalClass),
            pfnGetMarshalSizeMax    = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfGetMarshalUnMarshal>(__vtable_IMarshal.GetMarshalSizeMax),
            pfnMarshalInterface     = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfMarshalInterface>(__vtable_IMarshal.MarshalInterface),
            pfnUnmarshalInterface   = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget5>(__vtable_IMarshal.UnmarshalInterface),
            pfnReleaseMarshalData   = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget3>(__vtable_IMarshal.ReleaseMarshalData),
            pfnDisconnectObject     = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget2>(__vtable_IMarshal.DisconnectObject)
        };

        internal static IntPtr GetVtableFuncPtr()
        {
            return AddrOfIntrinsics.AddrOf<AddrOfGetCCWVtable>(GetCcwvtable_IMarshal);
        }

        internal static unsafe IntPtr GetCcwvtable_IMarshal()
        {
            if (pNativeVtable == default(IntPtr))
            {
                fixed (void* pVtbl = &s_theCcwVtable)
                {
                    McgMarshal.GetCCWVTableCopy(pVtbl, ref __vtable_IMarshal.pNativeVtable,sizeof(__vtable_IMarshal));
                }
            }
            return __vtable_IMarshal.pNativeVtable;
        }

        static unsafe int GetIMarshal(void **ppIMarshal)
        {
#if ENABLE_MIN_WINRT
            void *pUnk = null;
            int hr = ExternalInterop.CoCreateFreeThreadedMarshaler(null, (void **)&pUnk);
            if (hr < 0) return hr;

            *ppIMarshal = (void *)McgMarshal.ComQueryInterfaceNoThrow(
                new IntPtr(pUnk), ref Interop.COM.IID_IMarshal, out hr);

            McgMarshal.ComSafeRelease(new IntPtr(pUnk));

            return hr;
#else
            throw new PlatformNotSupportedException("GetIMarshal");
#endif
        }

        [NativeCallable]
        static int GetUnmarshalClass(
            IntPtr pComThis,
            IntPtr piid,
            IntPtr pv,
            int dwDestContext,
            IntPtr pvDestContext,
            int mshlflags,
            IntPtr pclsid
            )
        {
            __vtable_IMarshal **pIMarshal = null;
            try
            {
                int hr = GetIMarshal((void **)&pIMarshal);
                if (hr < 0) return hr;

                return CalliIntrinsics.StdCall__int(
                    (*pIMarshal)->pfnGetUnmarshalClass,
                    pIMarshal,
                    piid,
                    pv,
                    dwDestContext,
                    pvDestContext,
                    mshlflags,
                    pclsid);
            }
            finally
            {
                McgMarshal.ComSafeRelease(new IntPtr(pIMarshal));
            }
        }

        [NativeCallable]
        static int GetMarshalSizeMax(
            IntPtr pComThis,
            IntPtr piid,
            IntPtr pv,
            int dwDestContext,
            IntPtr pvDestContext,
            int mshlflags,
            IntPtr pSize
            )
        {
            __vtable_IMarshal **pIMarshal = null;
            try
            {
                int hr = GetIMarshal((void**)&pIMarshal); ;
                if (hr < 0) return hr;

                return CalliIntrinsics.StdCall__int(
                    (*pIMarshal)->pfnGetMarshalSizeMax,
                    pIMarshal,
                    piid,
                    pv,
                    dwDestContext,
                    pvDestContext,
                    mshlflags,
                    pSize);
            }
            finally
            {
                McgMarshal.ComSafeRelease(new IntPtr(pIMarshal));
            }
        }

        [NativeCallable]
        static int MarshalInterface(
            IntPtr pComThis,
            IntPtr pStm,
            IntPtr piid,
            IntPtr pv,
            int dwDestContext,
            IntPtr pvDestContext,
            int mshlflags
            )
        {
            __vtable_IMarshal **pIMarshal = null;
            try
            {
                int hr = GetIMarshal((void**)&pIMarshal);
                if (hr < 0) return hr;

                return CalliIntrinsics.StdCall__int(
                    (*pIMarshal)->pfnMarshalInterface,
                    pIMarshal,
                    pStm,
                    piid,
                    pv,
                    dwDestContext,
                    pvDestContext,
                    mshlflags);
            }
            finally
            {
                McgMarshal.ComSafeRelease(new IntPtr(pIMarshal));
            }
        }

        [NativeCallable]
        static int UnmarshalInterface(
            IntPtr pComThis,
            IntPtr pStm,
            IntPtr piid,
            IntPtr ppvObj
            )
        {
            __vtable_IMarshal **pIMarshal = null;
            try
            {
                int hr = GetIMarshal((void**)&pIMarshal);
                if (hr < 0) return hr;

                return CalliIntrinsics.StdCall__int(
                    (*pIMarshal)->pfnUnmarshalInterface,
                    pIMarshal,
                    pStm,
                    piid,
                    ppvObj);
            }
            finally
            {
                McgMarshal.ComSafeRelease(new IntPtr(pIMarshal));
            }
        }

        [NativeCallable]
        static int ReleaseMarshalData(
            IntPtr pComThis,
            IntPtr pStm
            )
        {
            __vtable_IMarshal **pIMarshal = null;
            try
            {
                int hr = GetIMarshal((void**)&pIMarshal);
                if (hr < 0) return hr;

                return CalliIntrinsics.StdCall__int(
                    (*pIMarshal)->pfnReleaseMarshalData,
                    pIMarshal,
                    pStm);
            }
            finally
            {
                McgMarshal.ComSafeRelease(new IntPtr(pIMarshal));
            }
        }

        [NativeCallable]
        static int DisconnectObject(
            IntPtr pComThis,
            int dwReserved
            )
        {
            __vtable_IMarshal **pIMarshal = null;
            try
            {
                int hr = GetIMarshal((void**)&pIMarshal);
                if (hr < 0) return hr;

                return CalliIntrinsics.StdCall__int(
                    (*pIMarshal)->pfnDisconnectObject,
                    pIMarshal,
                    dwReserved);
            }
            finally
            {
                McgMarshal.ComSafeRelease(new IntPtr(pIMarshal));
            }
        }
    }

    /// <summary>
    /// Windows.UI.Xaml.Hosting.IReferenceTrackerTarget
    /// Jupiter use this interface to coordinate with us
    /// </summary>
    internal unsafe struct __com_ICCW
    {
#pragma warning disable 0649
        internal __vtable_ICCW* pVtable;
#pragma warning restore 0649
    }


    unsafe struct __com_IStream
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value null
        internal __vtable_IStream* pVtable;
        public byte* m_pMem;            // Memory for the read.
        public int m_cbSize;            // Size of the memory.
#pragma warning restore 649

        public int m_cbCurrent;         // Current offset.
        public int m_cRef;              // Ref count.

        /// <summary>
        /// Create a predefined size of IStream*
        /// </summary>
        /// <remarks>
        /// NOTE: The reason to create own IStream is that the API SHCreateMemStream doesn't belong to UWP API Sets
        /// </remarks>
        /// <param name="lSize">Requested size</param>
        /// <returns>The IStream*</returns>
        internal static unsafe IntPtr CreateMemStm(ulong lSize)
        {
#if ENABLE_MIN_WINRT
            __com_IStream* pIStream = (__com_IStream*)PInvokeMarshal.CoTaskMemAlloc(new UIntPtr((uint)sizeof(__com_IStream)));
            pIStream->pVtable = (__vtable_IStream*)__vtable_IStream.GetVtable();
            pIStream->m_cbCurrent = 0;
            pIStream->m_cRef = 1;
            pIStream->m_cbSize = (int)lSize;
            pIStream->m_pMem = (byte*)PInvokeMarshal.CoTaskMemAlloc(new UIntPtr((uint)lSize));

            return new IntPtr(pIStream);
#else
            throw new PlatformNotSupportedException("CreateMemStm");
#endif
        }

        internal static unsafe void DestroyMemStm(__com_IStream* pIStream)
        {
            // Release memory allocated by CreateMemStm
            if (pIStream->m_pMem != null)
                PInvokeMarshal.CoTaskMemFree(new IntPtr(pIStream->m_pMem));

            PInvokeMarshal.CoTaskMemFree(new IntPtr(pIStream));
        }
    }

    /// <summary>
    /// Native Value for STATSTG
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct STATSTG_UnsafeType
    {
        public IntPtr pwcsName;
        public int type;
        public long cbSize;
        public ComTypes.FILETIME mtime;
        public ComTypes.FILETIME ctime;
        public ComTypes.FILETIME atime;
        public int grfMode;
        public int grfLocksSupported;
        public Guid clsid;
        public int grfStateBits;
        public int reserved;
    }

    unsafe internal struct __vtable_IStream
    {
        // IUnknown
        internal IntPtr pfnQueryInterface;
        internal IntPtr pfnAddRef;
        internal IntPtr pfnRelease;

        // ISequentialStream
        internal IntPtr pfnRead;
        internal IntPtr pfnWrite;

        // IStream 
        internal IntPtr pfnSeek;
        internal IntPtr pfnSetSize;
        internal IntPtr pfnCopyTo;
        internal IntPtr pfnCommit;
        internal IntPtr pfnRevert;
        internal IntPtr pfnLockRegion;
        internal IntPtr pfnUnlockRegion;
        internal IntPtr pfnStat;
        internal IntPtr pfnClone;

        public static IntPtr pNativeVtable;

        private static __vtable_IStream s_theCcwVtable = new __vtable_IStream
        {
            pfnQueryInterface = AddrOfIntrinsics.AddrOf<AddrOfQueryInterface>(QueryInterface),
            pfnAddRef = AddrOfIntrinsics.AddrOf<AddrOfAddRef>(AddRef),
            pfnRelease = AddrOfIntrinsics.AddrOf<AddrOfRelease>(Release),

            pfnRead = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamRead>(Read),
            pfnWrite = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamWrite>(Write),

            pfnSeek = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamSeek>(Seek),
            pfnSetSize = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamSetSize>(SetSize),
            pfnCopyTo = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamCopyTo>(CopyTo),
            pfnCommit = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget2>(Commit),
            pfnRevert = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfTarget1>(Revert),
            pfnLockRegion = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamLockRegion>(LockRegion),
            pfnUnlockRegion = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamUnlockRegion>(UnlockRegion),
            pfnStat = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamStat>(Stat),
            pfnClone = AddrOfIntrinsics.AddrOf<AddrOfIntrinsics.AddrOfIStreamClone>(Clone),
        };

        internal static IntPtr GetVtable()
        {
            fixed (void* pVtable = &s_theCcwVtable)
            {
                McgMarshal.GetCCWVTableCopy(pVtable, ref __vtable_IStream.pNativeVtable, sizeof(__vtable_IStream));
            }

            return __vtable_IStream.pNativeVtable;
        }

        [NativeCallable]
        internal static int QueryInterface(
            IntPtr __IntPtr__pComThis,
            IntPtr __IntPtr__pIID,
            IntPtr __IntPtr__ppvObject)
        {
            __com_IStream* pIStream = (__com_IStream*)__IntPtr__pComThis;
            void** ppvObject = (void**)__IntPtr__ppvObject;
            Guid* pIID = (Guid*)__IntPtr__pIID;

            if (ppvObject == null)
                return Interop.COM.E_POINTER;

            if (pIID->Equals(Interop.COM.IID_IUnknown) ||
                pIID->Equals(Interop.COM.IID_IStream) ||
                pIID->Equals(Interop.COM.IID_ISequentialStream))
            {
                CalliIntrinsics.StdCall__int(pIStream->pVtable->pfnAddRef, __IntPtr__pComThis);
                *ppvObject = pIStream;
                return Interop.COM.S_OK;
            }
            return Interop.COM.E_NOINTERFACE;
        }

        [NativeCallable]
        internal static int AddRef(System.IntPtr pComThis)
        {
            // This never gets released from native
            __com_IStream* pIStream = (__com_IStream*)pComThis;
            int cRef = System.Threading.Interlocked.Increment(ref pIStream->m_cRef);
            return cRef;
        }

        [NativeCallable]
        internal static int Release(IntPtr pComThis)
        {
            __com_IStream* pIStream = (__com_IStream*)pComThis;
            int cRef = System.Threading.Interlocked.Decrement(ref pIStream->m_cRef);
            if (cRef == 0)
            {
                __com_IStream.DestroyMemStm(pIStream);
            }
            return cRef;
        }

        [NativeCallable]
        internal static int CopyTo(System.IntPtr pComThis, IntPtr pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
        {
            // We don't handle pcbRead or pcbWritten.
            System.Diagnostics.Debug.Assert(pcbRead == IntPtr.Zero);
            System.Diagnostics.Debug.Assert(pcbWritten == IntPtr.Zero);
            System.Diagnostics.Debug.Assert(cb >= 0);

            try
            {
                __com_IStream* pIStream = (__com_IStream*)pComThis;
                __com_IStream* pToIStream = (__com_IStream*)pstm;
                long cbTotal = Math.Min(cb, pIStream->m_cbSize - pIStream->m_cbCurrent);
                long cbRead = Math.Min(1024, cbTotal);
                byte[] buffer = new byte[cbRead];

                while (cbTotal > 0)
                {
                    if (cbRead > cbTotal)
                        cbRead = cbTotal;
                    fixed (byte* pBuf = buffer)
                    {
                        int hr;

                        hr = CalliIntrinsics.StdCall__int(pIStream->pVtable->pfnRead, pComThis, pBuf, (int)cbRead, IntPtr.Zero);
                        if (hr < 0)
                            return hr;
                        hr = CalliIntrinsics.StdCall__int(pToIStream->pVtable->pfnWrite, pstm, pBuf, (int)cbRead, IntPtr.Zero);
                        if (hr < 0)
                            return hr;
                    }
                    cbTotal -= cbRead;
                }

                // Adjust seek pointer to the end.
                pIStream->m_cbCurrent = pIStream->m_cbSize;
                return Interop.COM.S_OK;
            }
            catch (System.OutOfMemoryException ex)
            {
                return ex.HResult;
            }
        }

        [NativeCallable]
        internal static unsafe int Read(System.IntPtr pComThis, IntPtr pv, int cb, IntPtr pcb)
        {
            __com_IStream* pIStream = (__com_IStream*)pComThis;
            byte* pByte = (byte*)pv;
            int* pcbRead = (int*)pcb;

            int cbRead = Math.Min(cb, pIStream->m_cbSize - pIStream->m_cbCurrent);
            if (cbRead <= 0)
                return Interop.COM.S_FALSE;

            Buffer.MemoryCopy(
                &pIStream->m_pMem[pIStream->m_cbCurrent],
                (void*)pv,
                cb,
                cb);
            if (pcbRead != null)
                *pcbRead  = cbRead;
            pIStream->m_cbCurrent += cbRead;

            return Interop.COM.S_OK;
        }

        [NativeCallable]
        internal static unsafe int Write(System.IntPtr pComThis, IntPtr pv, int cb, IntPtr pcbWritten)
        {
            __com_IStream* pIStream = (__com_IStream*)pComThis;
            byte* pByte = (byte*)pv;
            int* cbWritten = (int*)pcbWritten;
            if (cb + pIStream->m_cbCurrent > pIStream->m_cbSize)
                return Interop.COM.E_OUTOFMEMORY;

            Buffer.MemoryCopy(
                (void*)pv,
                &pIStream->m_pMem[pIStream->m_cbCurrent],
                cb,
                cb);
            pIStream->m_cbCurrent += cb;
            if (cbWritten != null)
                *cbWritten = cb;

            return Interop.COM.S_OK;
        }

        [NativeCallable]
        internal static unsafe int Seek(System.IntPtr pComThis, long dlibMove, int dwOrigin, IntPtr plib)
        {
            __com_IStream* pIStream = (__com_IStream*)pComThis;
            long* plibNewPosition = (long*)plib;
            Debug.Assert(dwOrigin == (int)Interop.COM.STREAM_SEEK.STREAM_SEEK_SET ||
                dwOrigin == (int)Interop.COM.STREAM_SEEK.STREAM_SEEK_CUR);
            Debug.Assert(dlibMove >= 0);

            if (dwOrigin == (int)Interop.COM.STREAM_SEEK.STREAM_SEEK_SET)
            {
                pIStream->m_cbCurrent = (int)dlibMove;
            }
            else if (dwOrigin == (int)Interop.COM.STREAM_SEEK.STREAM_SEEK_CUR)
            {
                pIStream->m_cbCurrent += (int)dlibMove;
            }

            if (plibNewPosition != null)
            {
                *plibNewPosition = pIStream->m_cbCurrent;
            }

            if (pIStream->m_cbCurrent > pIStream->m_cbSize)
                return Interop.COM.E_FAIL;

            return Interop.COM.S_OK;
        }

        const int STG_E_INVALIDPOINTER = unchecked((int)0x80030009);

        [NativeCallable]
        internal static unsafe int Stat(System.IntPtr pComThis, IntPtr pstatstg, int grfStatFlag)
        {
            __com_IStream* pIStream = (__com_IStream*)pComThis;
            STATSTG_UnsafeType* pUnsafeStatstg = (STATSTG_UnsafeType*)pstatstg;
            if (pUnsafeStatstg == null)
                return STG_E_INVALIDPOINTER;
            pUnsafeStatstg->pwcsName = IntPtr.Zero;
            pUnsafeStatstg->type = 2; // STGTY_STREAM
            pUnsafeStatstg->cbSize = pIStream->m_cbSize;
            return Interop.COM.S_OK;
        }

        #region Rest of IStream overrides that are not implemented
        [NativeCallable]
        internal static int Clone(System.IntPtr pComThis, IntPtr ppstm)
        {
            ppstm = default(IntPtr);
            return Interop.COM.E_NOTIMPL;
        }

        [NativeCallable]
        internal static int Commit(System.IntPtr pComThis, int grfCommitFlags)
        {
            return Interop.COM.E_NOTIMPL;
        }

        [NativeCallable]
        internal static int LockRegion(System.IntPtr pComThis, long libOffset, long cb, int dwLockType)
        {
            return Interop.COM.E_NOTIMPL;
        }

        [NativeCallable]
        internal static int Revert(System.IntPtr pComThis)
        {
            return Interop.COM.E_NOTIMPL;
        }

        [NativeCallable]
        internal static int SetSize(System.IntPtr pComThis, long libNewSize)
        {
            return Interop.COM.E_NOTIMPL;
        }

        [NativeCallable]
        internal static int UnlockRegion(System.IntPtr pComThis, long libOffset, long cb, int dwLockType)
        {
            return Interop.COM.E_NOTIMPL;
        }
        #endregion
    }

    /// The main puropse of this type is making McgIR happy
    /// The problem to solve is to generate "IUriRuntimeClass" McgData into shared assembly in production build.
    /// In Mcg, there is a place to determine which CompilationUnit(assembly) for a type should go to.
    /// Related Code: MCG.CompilationUnitCollection[ReportInteropTypeAndComputeDestination(typeDef)].IRCollection.Add(type);
    /// Solution 1: Use Windows.Foundation.IUriRuntimeClass type as typedef
    /// By default, the destination for a Windows.* WinRT type will be App CompilationUnit unless Windows.* WinRT type is shared type.
    /// Since marking Windows.Foundation.IUriRuntimeClass as shared type, it will introduce Windows.Foundation.WwwFormUrlDecoder as shared type and
    /// in reality, we don't care Windows.Foundation.WwwFormUrlDecoder.
    /// 
    /// [Current]Solution2: Use a WellKnown type(System.Runtime.InteropServices.IUriRuntimeClass) as typedef
    /// By default, the destination for a type in system.private.interop will be shared CompilationUnit.
    [Guid("9e365e57-48b2-4160-956f-c7385120bbfc")]
    public interface IUriRuntimeClass
    {
    }

    // The main puropse of this type is making McgIR happy during TypeImporter.ImporWinRTUri
    /// The problem to solve is to generate "IUriRuntimeClassFactory" McgData into shared assembly in production build
    /// In Mcg, there is a place to determine which CompilationUnit(assembly) for a type should go to.
    /// Related Code: MCG.CompilationUnitCollection[ReportInteropTypeAndComputeDestination(typeDef)].IRCollection.Add(type);
    /// Solution 1: Use Windows.Foundation.IUriRuntimeClassFactory type as typedef
    /// By default, the destination for a Windows.* WinRT type will be App CompilationUnit unless Windows.* WinRT type is shared type.
    /// Since marking Windows.Foundation.IUriRuntimeClassFactory as shared type, it will introduce Windows.Foundation.Uri as shared type 
    /// 
    /// [Current]Solution2: Use a WellKnown type(System.Runtime.InteropServices.IUriRuntimeClassFactory) as typedef
    /// By default, the destination for a type in system.private.interop will be shared CompilationUnit.
    [Guid("44a9796f-723e-4fdf-a218-033e75b0c084")]
    public interface IUriRuntimeClassFactory
    {
    }
}
