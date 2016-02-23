// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
//
// Implementation for McgModuleManager which provides access to all modules in the app
// and does global lookup across all modules
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

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Manage a list of McgModules
    /// NOTE: This class is not CLS compliant but it is only used in Mcg output which is C#
    /// NOTE: Managed debugger depends on class full name: "System.Runtime.InteropServices.McgModuleManager"
    /// </summary>
    [CLSCompliant(false)]
    [EagerOrderedStaticConstructor(EagerStaticConstructorOrder.McgModuleManager)]
    public static class McgModuleManager
    {
        internal const int MAX_MODULES = 8;

        /// <summary>
        /// NOTE: Managed debugger depends on field name: "s_modules" and field type must be Array
        /// Update managed debugger whenever field name/field type is changed.
        /// See CordbObjectValue::InitMcgModules in debug\dbi\values.cpp for more info
        /// </summary>
        internal static McgModule[] s_modules; // work around for multi-file cctor ordering issue: don't initialize in cctor, initialize lazily
        internal static volatile int s_moduleCount;

        internal static int s_numInteropThunksAllocatedSinceLastCleanup = 0;

        private static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, McgTypeInfo> s_runtimeTypeHandleToMcgTypeInfoMap;

#if !CORECLR
        private static class AsmCode
        {

            private const MethodImplOptions InternalCall = (MethodImplOptions)0x1000;

            [MethodImplAttribute(InternalCall)]
            [RuntimeImport("*", "InteropNative_GetCurrentThunk")]
            public static extern IntPtr GetCurrentInteropThunk();

            [MethodImplAttribute(InternalCall)]

            [RuntimeImport("*", "InteropNative_GetCommonStubAddress")]

            public static extern IntPtr GetInteropCommonStubAddress();
        }
#endif

        static McgModuleManager()
        {
            CCWLookupMap.InitializeStatics();
            ContextEntry.ContextEntryManager.InitializeStatics();
            ComObjectCache.InitializeStatics();

            const int DefaultSize = 101; // small prime number to avoid resizing in start up code
            s_runtimeTypeHandleToMcgTypeInfoMap = new Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, McgTypeInfo>(DefaultSize, new RuntimeTypeHandleComparer(), /* sync = */ true);

        }


        internal static InternalModule s_internalModule;

        public static McgTypeInfo IUnknown
        {
            get
            {
                return new McgTypeInfo((int)InternalModule.Indexes.IUnknown, s_internalModule);
            }
        }

        public static McgTypeInfo IInspectable
        {
            get
            {
                return new McgTypeInfo((int)InternalModule.Indexes.IInspectable, s_internalModule);
            }
        }

        public static McgTypeInfo HSTRING
        {
            get
            {
#if ENABLE_WINRT
                return new McgTypeInfo((int)InternalModule.Indexes.HSTRING, s_internalModule);
#else
                throw new PlatformNotSupportedException();
#endif
            }
        }

        internal static McgTypeInfo IJupiterObject
        {
            get
            {
#if ENABLE_WINRT
                return new McgTypeInfo((int)InternalModule.Indexes.IJupiterObject, s_internalModule);
#else
                throw new PlatformNotSupportedException();
#endif
            }
        }

        internal static McgTypeInfo IStringable
        {
            get
            {
#if ENABLE_WINRT
                return new McgTypeInfo((int)InternalModule.Indexes.IStringable, s_internalModule);
#else
                throw new PlatformNotSupportedException();
#endif
            }
        }

#if ENABLE_WINRT


        internal static McgTypeInfo ICCW
        {
            get
            {
                return new McgTypeInfo((int)InternalModule.Indexes.ICCW, s_internalModule);
            }
        }

        internal static McgTypeInfo IRestrictedErrorInfo
        {
            get
            {
                return new McgTypeInfo((int)InternalModule.Indexes.IRestrictedErrorInfo, s_internalModule);
            }
        }


        public static McgTypeInfo IActivationFactoryInternal
        {
            get
            {
                return new McgTypeInfo((int)InternalModule.Indexes.IActivationFactoryInternal, s_internalModule);
            }
        }




#endif //ENABLE_WINRT
        public unsafe static bool IsIJupiterObject(McgTypeInfo pEntry)
        {
#if ENABLE_WINRT
            return (pEntry == IJupiterObject);
#else
            return false;
#endif
        }

        internal static McgTypeInfo IWeakReferenceSource
        {
            get
            {
                return new McgTypeInfo((int)InternalModule.Indexes.IWeakReferenceSource, s_internalModule);
            }
        }

        internal static McgTypeInfo IWeakReference
        {
            get
            {
                return new McgTypeInfo((int)InternalModule.Indexes.IWeakReference, s_internalModule);
            }
        }

        internal static McgTypeInfo IMarshal
        {
            get
            {
                return new McgTypeInfo((int)InternalModule.Indexes.IMarshal, s_internalModule);
            }
        }



        /// <summary>
        /// Register the module and add it into global module list
        /// This should always be called from eagerly constructed ctors so threading is not a concern
        /// </summary>
        public static void Register(McgModule module)
        {
            // This API is called by .cctors, so we don't need to support multi-threaded callers.
            if (s_internalModule == null)
            {
                s_internalModule = new InternalModule();
                Add(s_internalModule);
            }

            Add(module);
        }

        private static void Add(McgModule module)
        {
            if (s_moduleCount >= MAX_MODULES)
                Environment.FailFast("Limit of modules reached"); //Can`t be localized, eager cctor dependency error.

            // workaround for multifile cctor ordering issue: init s_modules lazily
            if (s_modules == null)
            {
                s_modules = new McgModule[MAX_MODULES];
                s_modules[s_moduleCount++] = module;
                return;
            }

            // insert module into s_modules with correct order
            // make sure modules with larger priority values are listed first
            int index = s_moduleCount - 1;
            for (; index >= 0; index--)
            {
                if (s_modules[index].ModulePriority < module.ModulePriority)
                {
                    s_modules[index + 1] = s_modules[index];
                }
                else
                    break;
            }

            s_modules[index + 1] = module;

            // Increment the count after we make the assignment to avoid off-by-1 mistakes
            s_moduleCount++;
        }

        internal static int GetModuleIndex(McgModule module)
        {
            // Go through each module
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i] == module)
                {
                    return i;
                }
            }

            return -1;
        }

        internal static McgModule GetModule(int moduleIndex)
        {
            // the index value is added by 1 in GetModuleIndex()
            Debug.Assert(moduleIndex >= 0);
            return s_modules[moduleIndex];
        }

        /// <summary>
        /// This function scans all McgModules in search of the m_classData table row that "best
        /// describes" the requested type (i.e., describes the exact requested type or describes the
        /// nearest base class of that type that is listed anywhere in the aggregate m_classData
        /// content across all McgModules).
        ///
        /// If such a row is found, this function return true and returns an McgClassInfo attached
        /// to that row. Otherwise, this function returns false.
        /// </summary>
        internal static bool TryGetClassInfoFromName(string name, out McgClassInfo typeInfo)
        {
            //
            // Search all m_classData tables. If any of these tables contain a row describing the
            // requested type, then it is the best possible match (i.e., it either contains precise
            // information or anchors a linked list that reliably leads to the nearest available base
            // class).
            //
            // Note that the module list is ordered (see McgModuleManager.Add). This guarantees that
            // "higher layers" will be searched first (i.e., app will be searched first, then the
            // highest shared library layer, then the next shared library layer, etc).
            //
            // Note: This search does NOT distinguish between public and private interop. This means it
            // can erroneously return a private interop type which "should have" been hidden from the
            // caller's layer, and can also erroneously return a public interop type in a higher layer
            // when it "should have" returned a private interop type in the lower layer where the
            // caller resides. As a result, requests targeting private interop types (at any layer) are
            // currently unsupported (and do not occur in the product configurations that exist today).
            //
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetClassInfoFromClassDataTable(name, out typeInfo))
                {
                    return true;
                }
            }

            //
            // The m_classData tables did not contain any information on the requested type. Fall back
            // to searching the m_additionalClassData tables in case they contain information on the
            // "next best" base class that should be used in the absence of an exact match.
            //
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetClassInfoFromAdditionalClassDataTable(name, out typeInfo))
                {
                    return true;
                }
            }

            //
            // There were no matches in the m_classData or m_additionalClassData tables, so no class
            // info is available for the requested type.
            //
            typeInfo = McgClassInfo.Null;
            return false;
        }

        internal static bool TryGetInterfaceTypeInfoFromName(string name, out McgTypeInfo typeInfo)
        {
            // Go through each module
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetInterfaceTypeInfoFromName(name, out typeInfo))
                    return true;
            }

            typeInfo = McgTypeInfo.Null;
            return false;
        }

        public static string GetTypeName(RuntimeTypeHandle type, out bool isWinRT)
        {
            isWinRT = false;

            for (int i = 0; i < s_moduleCount; ++i)
            {
                string ret = s_modules[i].GetTypeName(type, ref isWinRT);

                if (ret != null)
                    return ret;
            }

            return null;
        }

        public static Type GetTypeFromName(string name, out bool isWinRT)
        {
            isWinRT = false;

            // Our type info tables could have types with no names, these are non-WinRT types.  But we don't
            // want to return just any one of those, so we have to special-case the null/empty string case here.
            if (name == null || name == "")
                return null;

            for (int i = 0; i < s_moduleCount; ++i)
            {
                Type ret = s_modules[i].GetTypeFromName(name, ref isWinRT);

                if (ret != null)
                    return ret;
            }

            return null;
        }

        /// <summary>
        /// Given a GUID, retrieve the corresponding type info(s)
        /// </summary>
        public static IEnumerable<McgTypeInfo> GetTypeInfosFromGuid(ref Guid guid)
        {
            List<McgTypeInfo> rets = new List<McgTypeInfo>(s_moduleCount);
            McgTypeInfo ret;
            for (int i = 0; i < s_moduleCount; ++i)
            {
                ret = s_modules[i].GetTypeInfo(ref guid);

                if (!ret.IsNull)
                {
                    rets.Add(ret);
                }
            }

            return rets;
        }

        /// <summary>
        /// Given a RuntimeTypeHandle, return the corresonding McgTypeInfo
        /// </summary>
        internal static McgTypeInfo GetTypeInfoFromTypeHandle(RuntimeTypeHandle typeHandle, out McgTypeInfo secondTypeInfo)
        {
            McgTypeInfo ret;
            secondTypeInfo = McgTypeInfo.Null;

            // First, search interface data, if type exists in InterfaceData, then just return
            for (int i = 0; i < s_moduleCount; ++i)
            {
                ret = s_modules[i].GetTypeInfoFromTypeHandleInInterfaceData(typeHandle);

                if (!ret.IsNull)
                    return ret;
            }

            // Second, search ICollectionData, if the type is ICollection<T>,  try to get 2 McgTypeInfos
            // The logical:
            //   if it find the first McgTypeInfo for ICollection<T>, it will continue search unless we found 2 McgTypeInfos or all module has been searched.
            // The reason behind this logical is for multi-file mode--if we dont search all McgModules, we may miss one McgTypeInfo for ICollection<T>
            // Example:
            // If Dictionary<string, object> is used in shared assembly and List<KeyValuePair<string, object>> isn't used in shared assembly, we will generate
            // a McgCollectionData entry in shared.Interop.dll:
            // McgCollectionData {
            //      CollectionType : ICollection<KeyValuePair<string, object>>
            //      FirstType: Dictionary<string, object>
            //      SecondType: Null
            // };
            // And If List<KeyValuePair<string, object>> is used in app assembly, we will generate a McgCollectionData entry in App.Interop.dll:
            // McgCollectionData {
            //      CollectionType : ICollection<KeyValuePair<string, object>>
            //      FirstType: List<KeyValuePair<string, object>>
            //      SecondType: Null
            // };
            // In this example, if we want to get all these McgTypeInfo for ICollection<KeyValuePair<string, object>> , we have to search all McgModules.
            McgTypeInfo firstTypeInfoLocal = McgTypeInfo.Null; // store first McgTypeInfo for ICollection<T>
            McgTypeInfo secondTypeInfoLocal = McgTypeInfo.Null; // store second McgTypeInfo for ICollection<T>
            for (int i = 0; i < s_moduleCount; ++i)
            {
                ret = s_modules[i].GetTypeInfoFromTypeHandleInCollectionData(typeHandle, out secondTypeInfoLocal);

                if (ret.IsNull)
                    continue;

                if (firstTypeInfoLocal.IsNull)
                {
                    // store the first McgTypeInfo
                    firstTypeInfoLocal = ret;
                }
                else
                {
                    // if we found the first McgTypeInfo and saved as firstTypeInfoLocal
                    // and current ret's value is different than firstTypeInfoLocal,
                    // then save ret value as secondTypeInfoLocal
                    if (secondTypeInfoLocal.IsNull && !ret.Equals(firstTypeInfoLocal))
                    {
                        secondTypeInfoLocal = ret;
                    }
                }

                // if find both McgTypeInfo, return
                if(!firstTypeInfoLocal.IsNull && !secondTypeInfoLocal.IsNull)
                {
                    secondTypeInfo = secondTypeInfoLocal;
                    return firstTypeInfoLocal;
                }
            }

            // third, return either null or the only McgTypeInfo for ICollection<T>
            return firstTypeInfoLocal;
        }

        /// <summary>
        /// Comparer for RuntimeTypeHandle
        /// This custom comparer is required because RuntimeTypeHandle is different between ProjectN(IntPtr) 
        /// and in CoreCLR. The default Equals and GetHashCode functions on RuntimeTypeHandle "do new EETypePtr" which 
        /// adds additional cost to the dictionary lookup. In ProjectN we can get away with only checking the IntPtr
        /// which is significantly less expensive.
        /// </summary>
        internal class RuntimeTypeHandleComparer : IEqualityComparer<RuntimeTypeHandle>
        {
            //
            // Calculates Hash code when RuntimeTypeHandle is an IntPtr(ProjectN)
            //
            private unsafe int GetHashCodeHelper(ref RuntimeTypeHandle handle)
            {
                IntPtr val;
                fixed (RuntimeTypeHandle* pHandle = &handle)
                {
                    val = *(IntPtr*)pHandle;
                }
                return unchecked((int)(val.ToInt64()));
            }

            //
            // Determines whether two types are equal when RuntimeTypeHandle is an IntPtr(ProjectN)
            //
            private unsafe bool EqualsHelper(ref RuntimeTypeHandle handle1, ref RuntimeTypeHandle handle2)
            {
                IntPtr val1, val2;
                fixed (RuntimeTypeHandle* pHandle1 = &handle1, pHandle2 = &handle2)
                {
                    val1 = *(IntPtr*)pHandle1;
                    val2 = *(IntPtr*)pHandle2;
                }
                return val1.Equals(val2);
            }

            bool IEqualityComparer<RuntimeTypeHandle>.Equals(RuntimeTypeHandle handle1, RuntimeTypeHandle handle2)
            {
//
// Ideally, here we should use a symbol that identifies we are in ProjctN as in ProjectN RuntimeTypeHandle
// is an IntPtr. Since we don't have such symbol yet,  I am using ENABLE_WINRT which is synonymous to
// ProjectN for now. It may change in the future with CoreRT, in that case we may need to revisit this.
//
#if ENABLE_WINRT
                return EqualsHelper(ref handle1 , ref handle2);
#else
                return handle1.Equals(handle2);
#endif
            }

            int IEqualityComparer<RuntimeTypeHandle>.GetHashCode(RuntimeTypeHandle obj)
            {
//
//  See the comment for Equals
//
#if ENABLE_WINRT
                return GetHashCodeHelper(ref obj);
#else
                return obj.GetHashCode();
#endif
            }
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static McgTypeInfo GetTypeInfoByHandle(RuntimeTypeHandle typeHnd)
        {
            McgTypeInfo typeInfo;

            try
            {
                s_runtimeTypeHandleToMcgTypeInfoMap.LockAcquire();
                if (!s_runtimeTypeHandleToMcgTypeInfoMap.TryGetValue(typeHnd, out typeInfo))
                {
                    typeInfo = GetTypeInfoByHandleInternal(typeHnd);
                    s_runtimeTypeHandleToMcgTypeInfoMap.Add(typeHnd, typeInfo);
                }
            }
            finally
            {
                s_runtimeTypeHandleToMcgTypeInfoMap.LockRelease();
            }

            return typeInfo;
        }


        private static McgTypeInfo GetTypeInfoByHandleInternal(RuntimeTypeHandle typeHnd)
        {
            McgTypeInfo typeInfo;
            for (int i = 0; i < s_moduleCount; ++i)
            {
                typeInfo = s_modules[i].GetTypeInfoByHandle(typeHnd);
                if (!typeInfo.IsNull)
                    return typeInfo;
            }

            return McgTypeInfo.Null;
        }

        internal static McgTypeInfo FindTypeInfo(Func<McgTypeInfo, bool> predecate)
        {
            for (int i = 0; i < s_moduleCount; i++)
            {
                McgTypeInfo info = s_modules[i].FindTypeInfo(predecate);

                if (!info.IsNull)
                    return info;
            }

            return McgTypeInfo.Null;
        }

        /// <summary>
        /// Given a RuntimeTypeHandle, return the corresonding McgTypeInfo
        /// </summary>
        internal static McgClassInfo GetClassInfoFromTypeHandle(RuntimeTypeHandle typeHandle)
        {
            McgClassInfo ret;

            for (int i = 0; i < s_moduleCount; ++i)
            {
                ret = s_modules[i].GetClassInfoByHandle(typeHandle);

                if (!ret.IsNull)
                    return ret;
            }

            return McgClassInfo.Null;
        }

        internal static CCWTemplateInfo GetCCWTemplateInfo(RuntimeTypeHandle handle)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                int slot = s_modules[i].CCWTemplateDataLookup(handle);

                if (slot >= 0)
                    return new CCWTemplateInfo(s_modules[i], slot);
            }

            return CCWTemplateInfo.Null;
        }

        public static object UnboxIfBoxed(object obj)
        {
            return UnboxIfBoxed(obj, null);
        }

        public static object UnboxIfBoxed(object obj, string className)
        {
            //
            // If it is a managed wrapper, unbox it
            //
            object unboxedObj = McgComHelpers.UnboxManagedWrapperIfBoxed(obj);
            if (unboxedObj != obj)
                return unboxedObj;

            if (className == null)
                className = System.Runtime.InteropServices.McgComHelpers.GetRuntimeClassName(obj);

            if (!String.IsNullOrEmpty(className))
            {
                for (int i = 0; i < s_moduleCount; ++i)
                {
                    object ret = s_modules[i].UnboxIfBoxed(obj, className);

                    if (ret != null)
                        return ret;
                }
            }

            return null;
        }

        public static object BoxIfBoxable(object obj)
        {
            return BoxIfBoxable(obj, default(RuntimeTypeHandle));
        }

        public static object BoxIfBoxable(object obj, RuntimeTypeHandle typeHandleOverride)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                object ret = s_modules[i].BoxIfBoxable(obj, typeHandleOverride);

                if (ret != null)
                    return ret;
            }

            return null;
        }

        internal static bool TryGetStructMarshalData(RuntimeTypeHandle structureTypeHandle, out McgStructMarshalData structMarshalData)
        {
            for (int i = 0; i < s_moduleCount; i++)
            {
                if (s_modules[i].TryGetStructMarshalData(structureTypeHandle, out structMarshalData))
                {
                    return true;
                }
            }

            structMarshalData = default(McgStructMarshalData);
            return false;
        }

        /// <summary>
        /// Try to get Field Offset
        /// </summary>
        /// <param name="structureTypeHandle"></param>
        /// <param name="fieldName">field Name</param>
        /// <param name="structExists">whether the struct marshalling data exists or not</param>
        /// <param name="offset"></param>
        /// <returns></returns>
        internal static bool TryGetStructFieldOffset(RuntimeTypeHandle structureTypeHandle, string fieldName, out bool structExists, out uint offset)
        {
            structExists = false;
            offset = default(uint);

            for (int i = 0; i < s_moduleCount; i++)
            {
                McgStructMarshalData structMarshalData;
                if (s_modules[i].TryGetStructMarshalData(structureTypeHandle, out structMarshalData))
                {
                    structExists = true;
                    if (s_modules[i].TryGetStructFieldOffset(structMarshalData, fieldName, out offset))
                    {
                        return true;
                    }
                    else
                    {
                        // Stop search other modules
                        return false;
                    }
                }
            }

            return false;
        }

        public static object ComInterfaceToObject_NoUnboxing(System.IntPtr pComItf, RuntimeTypeHandle interfaceType)
        {
            McgTypeInfo secondTypeInfo;
            McgTypeInfo typeInfo = GetTypeInfoFromTypeHandle(interfaceType, out secondTypeInfo);
            return McgMarshal.ComInterfaceToObject_NoUnboxing(pComItf, typeInfo);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr GetInterface(
                    __ComObject obj,
                    RuntimeTypeHandle typeHnd)
        {
            return obj.QueryInterface_NoAddRef_Internal(
                typeHnd);
        }

        /// <summary>
        /// Shared CCW marshalling to native: from type index to object, supporting HSTRING
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        static internal IntPtr ObjectToComInterface(object data, McgTypeInfo typeInfo)
        {
#if ENABLE_WINRT
            if (typeInfo.Equals(McgModuleManager.IInspectable))
            {
                return McgMarshal.ObjectToIInspectable(data);
            }
            else if (typeInfo.Equals(McgModuleManager.HSTRING))
            {
                return McgMarshal.StringToHString((string)data).handle;
            }
#endif

            return McgMarshal.ObjectToComInterface(data, typeInfo);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr ObjectToComInterface(
                    object obj,
                    RuntimeTypeHandle typeHnd)
        {
#if  ENABLE_WINRT
            if (typeHnd.Equals(typeof(object).TypeHandle))
            {
                return McgMarshal.ObjectToIInspectable(obj);
            }

            if (typeHnd.Equals(typeof(string).TypeHandle))
            {
                return McgMarshal.StringToHString((string)obj).handle;
            }

            if (!InteropExtensions.IsInterface(typeHnd))
            {
                Debug.Assert(obj == null || obj is __ComObject);
                ///
                /// This code path should be executed only for WinRT classes
                ///
                typeHnd =  GetClassInfoFromTypeHandle(typeHnd).DefaultInterface;
                Debug.Assert(!typeHnd.IsNull());
            }
#endif
            McgTypeInfo typeInfo = McgModuleManager.GetTypeInfoByHandle(typeHnd);

            return McgMarshal.ObjectToComInterface(
                obj,
                typeInfo
            );
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr ManagedObjectToComInterface(
                    object obj,
                    RuntimeTypeHandle typeHnd)
        {
            return McgMarshal.ManagedObjectToComInterface(
                obj,
                McgModuleManager.GetTypeInfoByHandle(typeHnd)
            );
        }

        public static object ComInterfaceToObject(System.IntPtr pComItf, RuntimeTypeHandle typeHandle)
        {
#if ENABLE_WINRT
            if (typeHandle.Equals(typeof(object).TypeHandle))
            {
                return McgMarshal.IInspectableToObject(pComItf);
            }

            if (typeHandle.Equals(typeof(string).TypeHandle))
            {
                return McgMarshal.HStringToString(pComItf);
            }

            if (!InteropExtensions.IsInterface(typeHandle))
            {
                return ComInterfaceToObject(pComItf, GetClassInfoFromTypeHandle(typeHandle).DefaultInterface, typeHandle);
            }
#endif
            return ComInterfaceToObject(pComItf, typeHandle, default(RuntimeTypeHandle));
            
        }
        /// <summary>
        /// Shared CCW Interface To Object
        /// </summary>
        /// <param name="pComItf"></param>
        /// <param name="interfaceType"></param>
        /// <param name="classTypeInSignature"></param>
        /// <returns></returns>
        public static object ComInterfaceToObject(System.IntPtr pComItf, RuntimeTypeHandle interfaceType,
                                           RuntimeTypeHandle classTypeInSignature)
        {
            if (interfaceType.Equals(typeof(object).TypeHandle))
            {
                return McgMarshal.IInspectableToObject(pComItf);
            }

#if ENABLE_WINRT
            if (interfaceType.Equals(typeof(string).TypeHandle))
            {
                return McgMarshal.HStringToString(pComItf);
            }
#endif

            return ComInterfaceToObject(
                pComItf,
                GetTypeInfoByHandle(interfaceType),
                (classTypeInSignature.Equals(default(RuntimeTypeHandle)))
                    ? McgClassInfo.Null
                    : GetClassInfoFromTypeHandle(classTypeInSignature)
            );
        }

        public static object ComInterfaceToObject(System.IntPtr pComItf, McgTypeInfo interfaceTypeInfo)
        {
            return ComInterfaceToObject(pComItf, interfaceTypeInfo,  McgClassInfo.Null);
        }

        public static object ComInterfaceToObject(System.IntPtr pComItf, McgTypeInfo interfaceTypeInfo, McgClassInfo classInfoInSignature)
        {
#if ENABLE_WINRT
            if (interfaceTypeInfo == McgModuleManager.HSTRING)
            {
                return McgMarshal.HStringToString(pComItf);
            }
#endif
            return McgMarshal.ComInterfaceToObject(
                pComItf,
                interfaceTypeInfo,
                classInfoInSignature
            );
        }

        // This is not a safe function to use for any funtion pointers that do not point
        // at a static function. This is due to the behavior of shared generics,
        // where instance function entry points may share the exact same address
        // but static functions are always represented in delegates with customized
        // stubs.
        private static bool DelegateTargetMethodEquals(Delegate del, IntPtr pfn)
        {
            RuntimeTypeHandle thDummy;
            return del.GetFunctionPointer(out thDummy) == pfn;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr DelegateToComInterface(Delegate del, RuntimeTypeHandle typeHnd, IntPtr stubFunctionAddr)
        {
            if (del == null)
                return default(IntPtr);

            object targetObj;

            //
            // If the delegate points to the forward stub for the native delegate,
            // then we want the RCW associated with the native interface.  Otherwise,
            // this is a managed delegate, and we want the CCW associated with it.
            //
            if (DelegateTargetMethodEquals(del, stubFunctionAddr))
                targetObj = del.Target;
            else
                targetObj = del;

            return McgMarshal.ObjectToComInterface(targetObj,
                GetTypeInfoByHandle(typeHnd)
                );
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Delegate ComInterfaceToDelegate(IntPtr pComItf, RuntimeTypeHandle typeHnd, IntPtr stubFunctionAddr)
        {
            if (pComItf == default(IntPtr))
                return null;

            object obj = ComInterfaceToObject(pComItf, typeHnd, /* classIndexInSignature */ default(RuntimeTypeHandle));

            //
            // If the object we got back was a managed delegate, then we're good.  Otherwise,
            // the object is an RCW for a native delegate, so we need to wrap it with a managed
            // delegate that invokes the correct stub.
            //
            Delegate del = obj as Delegate;
            if (del == null)
            {
                Debug.Assert(obj is __ComObject);
                Debug.Assert(GetTypeInfoByHandle(typeHnd).InterfaceType.Equals(typeHnd));

                del = InteropExtensions.CreateDelegate(
                    typeHnd,
                    stubFunctionAddr,
                    obj,
                    /*isStatic:*/ true,
                    /*isVirtual:*/ false,
                    /*isOpen:*/ false);
            }

            return del;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static __ComObject GetActivationFactory(
                    string typeName,
                    RuntimeTypeHandle typeHnd)
        {
            return McgMarshal.GetActivationFactory(
                typeName,
                McgModuleManager.GetTypeInfoByHandle(typeHnd)
            );
        }

#if ENABLE_WINRT
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static unsafe IntPtr ActivateInstance(string typeName)
        {
            __ComObject target = McgMarshal.GetActivationFactory(
                typeName,
                McgModuleManager.IActivationFactoryInternal
            );

            IntPtr pIActivationFactoryInternalItf = target.QueryInterface_NoAddRef_Internal(
                McgModuleManager.IActivationFactoryInternal,
                /* cacheOnly= */ false,
                /* throwOnQueryInterfaceFailure= */ true
            );

            __com_IActivationFactoryInternal* pIActivationFactoryInternal = (__com_IActivationFactoryInternal*)pIActivationFactoryInternalItf;

            IntPtr pResult = default(IntPtr);

            int hr = CalliIntrinsics.StdCall<int>(
                pIActivationFactoryInternal->pVtable->pfnActivateInstance,
                pIActivationFactoryInternal,
                &pResult
            );

            GC.KeepAlive(target);

            if (hr < 0)
            {
                throw McgMarshal.GetExceptionForHR(hr, /* isWinRTScenario = */ true);
            }

            return pResult;
        }
#endif
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static object GetDynamicAdapter(__ComObject obj, RuntimeTypeHandle typeHnd, RuntimeTypeHandle secondTypeHnd = default(RuntimeTypeHandle))
        {
            McgTypeInfo typeInfo = McgModuleManager.GetTypeInfoByHandle(typeHnd);
            Debug.Assert(!typeInfo.IsNull);

            McgTypeInfo secondTypeInfo;

            if (!secondTypeHnd.Equals(default(RuntimeTypeHandle)))
            {
                secondTypeInfo = McgModuleManager.GetTypeInfoByHandle(secondTypeHnd);
                Debug.Assert(!secondTypeInfo.IsNull);
            }
            else
            {
                secondTypeInfo = McgTypeInfo.Null;
            }
            return obj.GetDynamicAdapter(typeInfo, secondTypeInfo);
        }

        /// <summary>
        /// Marshal array of objects
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        unsafe public static void ObjectArrayToComInterfaceArray(uint len, System.IntPtr* dst, object[] src, RuntimeTypeHandle typeHnd)
        {
            for (uint i = 0; i < len; i++)
            {
                dst[i] = McgMarshal.ObjectToComInterface(src[i], McgModuleManager.GetTypeInfoByHandle(typeHnd));
            }
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        unsafe public static void ObjectArrayToComInterfaceArray(uint len, System.IntPtr* dst, object[] src, McgTypeInfo specialTypeInfo)
        {
            Debug.Assert(!specialTypeInfo.IsNull); // this overload of the API is only for our 'special' indexes.
            for (uint i = 0; i < len; i++)
            {
                dst[i] = McgModuleManager.ObjectToComInterface(src[i], specialTypeInfo);
            }
        }

        /// <summary>
        /// Allocate native memory, and then marshal array of objects
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        unsafe public static System.IntPtr* ObjectArrayToComInterfaceArrayAlloc(object[] src, RuntimeTypeHandle typeHnd, out uint len)
        {
            System.IntPtr* dst = null;

            len = 0;

            if (src != null)
            {
#if ENABLE_WINRT
                // @TODO: this seems somewhat inefficient, should this be fixed by having the generated code
                // call the right overload directly?
                if (typeHnd.Equals(typeof(object).TypeHandle))
                    return ObjectArrayToComInterfaceArrayAlloc(src, McgModuleManager.IInspectable, out len);
#endif
                len = (uint)src.Length;

                dst = (System.IntPtr*)ExternalInterop.CoTaskMemAlloc((System.IntPtr)(len * (sizeof(System.IntPtr))));

                for (uint i = 0; i < len; i++)
                {
                    dst[i] = McgMarshal.ObjectToComInterface(src[i], McgModuleManager.GetTypeInfoByHandle(typeHnd));
                }
            }

            return dst;
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        unsafe public static System.IntPtr* ObjectArrayToComInterfaceArrayAlloc(object[] src, McgTypeInfo specialTypeInfo, out uint len)
        {
            Debug.Assert(!specialTypeInfo.IsNull); // this overload of the API is only for our 'special' indexes.
            System.IntPtr* dst = null;

            len = 0;

            if (src != null)
            {
                len = (uint)src.Length;

                dst = (System.IntPtr*)ExternalInterop.CoTaskMemAlloc((System.IntPtr)(len * (sizeof(System.IntPtr))));

                for (uint i = 0; i < len; i++)
                {
                    dst[i] = McgModuleManager.ObjectToComInterface(src[i], specialTypeInfo);
                }
            }

            return dst;
        }

        /// <summary>
        /// (p/invoke delegate instance) -> (thunk)
        /// Used in following scenarios:
        /// 1) When marshalling managed delegates to native function pointer, the native function pointer
        /// is pointing to the thunk address which calls the real marshalling code, which in turn retrieves
        /// the current delegate instance based on the thunk address in order to make the call
        /// 2) When marshalling the thunk address back, we need to retrieve the p/invoke delegate instance
        ///
        /// In both cases, the delegate is a managed delegate created from managed code
        /// For delegates created from native function pointer, we use NativeFunctionWrapper to retrieve
        /// the original function pointer. See GetStubForPInvokeDelegate for more details
        /// </summary>
        static System.Collections.Generic.Internal.Dictionary<IntPtr, DelegateInstEntry> s_thunkToPInvokeDelegateInstMap;

        class DelegateInstEntry : IEquatable<DelegateInstEntry>
        {
            internal IntPtr Thunk;
            internal GCHandle Handle;
            internal int HashCode;

            internal DelegateInstEntry(Delegate del, IntPtr pThunk)
            {
                Handle = GCHandle.Alloc(del, GCHandleType.Weak);
                HashCode = RuntimeHelpers.GetHashCode(del);
                Thunk = pThunk;
            }

            public static int GetHashCode(Delegate del)
            {
                return RuntimeHelpers.GetHashCode(del);
            }

            public bool Equals(DelegateInstEntry other)
            {
                return (Thunk == other.Thunk);
            }

            public bool Equals(Delegate del)
            {
                return (Object.ReferenceEquals(del, Handle.Target));
            }
        }

        static bool GetPInvokeDelegateData(RuntimeTypeHandle delegateType, out McgPInvokeDelegateData pinvokeDelegateData)
        {
            pinvokeDelegateData = default(McgPInvokeDelegateData);

            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetPInvokeDelegateData(delegateType, out pinvokeDelegateData))
                {
                    return true;
                }
            }
#if ENABLE_WINRT
           throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(delegateType));
#else
           return false;
#endif
        }

        /// <summary>
        /// Used to lookup whether a delegate already has an entry
        /// </summary>
        static System.Collections.Generic.Internal.HashSet<DelegateInstEntry> s_delegateInstEntryHashSet;

        public static IntPtr GetStubForPInvokeDelegate(RuntimeTypeHandle delegateType, Delegate dele)
        {
            return GetStubForPInvokeDelegate(dele);
        }

        /// <summary>
        /// Return the stub to the pinvoke marshalling stub
        /// </summary>
        /// <param name="del">The delegate</param>
        static internal IntPtr GetStubForPInvokeDelegate(Delegate del)
        {
            if (del == null)
                return IntPtr.Zero;

            NativeFunctionPointerWrapper fpWrapper = del.Target as NativeFunctionPointerWrapper;
            if (fpWrapper != null)
            {
                //
                // Marshalling a delegate created from native function pointer back into function pointer
                // This is easy - just return the 'wrapped' native function pointer
                //
                return fpWrapper.NativeFunctionPointer;
            }
            else
            {
                //
                // Marshalling a managed delegate created from managed code into a native function pointer
                //
                return GetOrAllocateThunk(del);
            }
        }

        static Collections.Generic.Internal.Dictionary<IntPtr, DelegateInstEntry> GetThunkMap(out System.Collections.Generic.Internal.HashSet<DelegateInstEntry> delegateMap)
        {
            //
            // Create the map on-demand to avoid the dependency in the McgModule.ctor
            // Otherwise NUTC will complain that McgModule being eager ctor depends on a deferred
            // ctor type
            //
            if (s_thunkToPInvokeDelegateInstMap == null)
            {
                Interlocked.CompareExchange(
                    ref s_thunkToPInvokeDelegateInstMap,
                    new Collections.Generic.Internal.Dictionary<IntPtr, DelegateInstEntry>(/* sync = */ true),
                    null
                );
            }

            if (s_delegateInstEntryHashSet == null)
            {
                Interlocked.CompareExchange(
                    ref s_delegateInstEntryHashSet,
                    new System.Collections.Generic.Internal.HashSet<DelegateInstEntry>(50),
                    null
                );
            }

            delegateMap = s_delegateInstEntryHashSet;

            return s_thunkToPInvokeDelegateInstMap;
        }

        const int THUNK_RECYCLING_FREQUENCY = 200;                                                  // Every 200 thunks that we allocate, do a round of cleanup
        static List<IntPtr> s_thunksFreeupList = new List<IntPtr>(THUNK_RECYCLING_FREQUENCY);       // Fixed sized buffer to keep track of thunks to free form the map

        static private IntPtr GetOrAllocateThunk(Delegate del)
        {
#if ENABLE_WINRT
            System.Collections.Generic.Internal.HashSet<DelegateInstEntry> delegateMap;
            System.Collections.Generic.Internal.Dictionary<IntPtr, DelegateInstEntry> thunkMap = GetThunkMap(out delegateMap);

            try
            {
                thunkMap.LockAcquire();

                DelegateInstEntry key = null;
                int hashCode = DelegateInstEntry.GetHashCode(del);
                for (int entry = delegateMap.FindFirstKey(ref key, hashCode); entry >= 0; entry = delegateMap.FindNextKey(ref key, entry))
                {
                    if (key.Equals(del))
                        return key.Thunk;
                }

                //
                // Keep allocating thunks until we reach the recycling frequency - we have a virtually unlimited
                // number of thunks that we can allocate (until we run out of virtual address space), but we
                // still need to cleanup thunks that are no longer being used, to avoid leaking memory.
                // This is helpful to detect bugs where user are calling into thunks whose delegate are already
                // collected. In desktop CLR, they'll simple AV, while in .NET Native, there is a good chance we'll
                // detect the delegate instance is NULL (by looking at the GCHandle in the map) and throw out a
                // good exception
                //
                if (s_numInteropThunksAllocatedSinceLastCleanup == THUNK_RECYCLING_FREQUENCY)
                {
                    //
                    // Cleanup the thunks that were previously allocated and are no longer in use to avoid memory leaks
                    //

                    GC.Collect();

                    foreach (var item in thunkMap)
                    {
                        // Don't exceed the size of the buffer to avoid new allocations during a freeing operation
                        if (s_thunksFreeupList.Count == THUNK_RECYCLING_FREQUENCY)
                            break;

                        DelegateInstEntry instEntry = item.Value;

                        if (instEntry.Handle.Target == null)
                        {
                            ThunkPool.FreeThunk(AsmCode.GetInteropCommonStubAddress(), instEntry.Thunk);
                            instEntry.Handle.Free();

                            bool removed = delegateMap.Remove(instEntry, instEntry.HashCode);
                            if (!removed)
                                Environment.FailFast("Inconsistency in delegate map");

                            s_thunksFreeupList.Add(instEntry.Thunk);
                        }
                    }
                    foreach (var item in s_thunksFreeupList)
                    {
                        bool removed = thunkMap.Remove(item);
                        if (!removed)
                            Environment.FailFast("Inconsistency in delegate map");
                    }
                    s_thunksFreeupList.Clear();

                    s_numInteropThunksAllocatedSinceLastCleanup = 0;
                }


                IntPtr pThunk = ThunkPool.AllocateThunk(AsmCode.GetInteropCommonStubAddress());

                if (pThunk == IntPtr.Zero)
                {
                    // We've either run out of memory, or failed to allocate a new thunk due to some other bug. Now we should fail fast
                    Environment.FailFast("Insufficient number of thunks.");
                    return IntPtr.Zero;
                }
                else
                {
                    McgPInvokeDelegateData pinvokeDelegateData;
                    GetPInvokeDelegateData(del.GetTypeHandle(), out pinvokeDelegateData);
                    ThunkPool.SetThunkData(pThunk, pThunk, pinvokeDelegateData.ReverseStub);

                    s_numInteropThunksAllocatedSinceLastCleanup++;

                    //
                    // Allocate a weak GC handle pointing to the delegate
                    // Whenever the delegate dies, we'll know next time when we recycle thunks
                    //
                    DelegateInstEntry newEntry = new DelegateInstEntry(del, pThunk);

                    thunkMap.Add(pThunk, newEntry);
                    delegateMap.Add(newEntry, newEntry.HashCode);

                    return pThunk;
                }
            }
            finally
            {
                thunkMap.LockRelease();
            }
#else
            throw new PlatformNotSupportedException("GetOrAllocateThunk");
#endif
        }

        /// <summary>
        /// Retrieve the corresponding P/invoke instance from the stub
        /// </summary>
        static public Delegate GetPInvokeDelegateForStub(IntPtr pStub, RuntimeTypeHandle delegateType)
        {
            if (pStub == IntPtr.Zero)
                return null;

            System.Collections.Generic.Internal.HashSet<DelegateInstEntry> delegateMap;
            System.Collections.Generic.Internal.Dictionary<IntPtr, DelegateInstEntry> thunkMap = GetThunkMap(out delegateMap);

            //
            // First try to see if this is one of the thunks we've allocated when we marshal a managed
            // delegate to native code
            //
            try
            {
                thunkMap.LockAcquire();

                DelegateInstEntry delegateEntry;
                if (thunkMap.TryGetValue(pStub, out delegateEntry))
                {
                    Delegate target = InteropExtensions.UncheckedCast<Delegate>(delegateEntry.Handle.Target);

                    //
                    // The delegate might already been garbage collected
                    // User should use GC.KeepAlive or whatever ways necessary to keep the delegate alive
                    // until they are done with the native function pointer
                    //
                    if (target == null)
                    {
                        Environment.FailFast(
                            "The corresponding delegate has been garbage collected. " +
                            "Please make sure the delegate is still referenced by managed code when you are using the marshalled native function pointer."
                        );
                    }

                    return target;
                }
            }
            finally
            {
                thunkMap.LockRelease();
            }

            //
            // Otherwise, the stub must be a pure native function pointer
            // We need to create the delegate that points to the invoke method of a
            // NativeFunctionPointerWrapper derived class
            //
            McgPInvokeDelegateData pInvokeDelegateData;
            if (!GetPInvokeDelegateData(delegateType, out pInvokeDelegateData))
            {
                return null;
            }

            return CalliIntrinsics.Call__Delegate(
                pInvokeDelegateData.ForwardDelegateCreationStub,
                pStub
            );
        }

        /// <summary>
        /// Retrieves the current delegate that is being called
        /// @TODO - We probably can do this more efficiently without taking a lock
        /// </summary>
        static public T GetCurrentCalleeDelegate<T>() where T : class // constraint can't be System.Delegate
        {
#if RHTESTCL || CORECLR
            throw new NotSupportedException();
#else
            System.Collections.Generic.Internal.HashSet<DelegateInstEntry> delegateMap;
            System.Collections.Generic.Internal.Dictionary<IntPtr, DelegateInstEntry> thunkMap = GetThunkMap(out delegateMap);

            //
            // RH keeps track of the current thunk that is being called through a secret argument / thread
            // statics. No matter how that's implemented, we get the current thunk which we can use for
            // look up later
            //
            IntPtr pThunk = AsmCode.GetCurrentInteropThunk();

            try
            {
                thunkMap.LockAcquire();

                DelegateInstEntry delegateEntry;
                if (thunkMap.TryGetValue(pThunk, out delegateEntry))
                {
                    object target = delegateEntry.Handle.Target;

                    //
                    // The delegate might already been garbage collected
                    // User should use GC.KeepAlive or whatever ways necessary to keep the delegate alive
                    // until they are done with the native function pointer
                    //
                    if (target == null)
                    {
                        Environment.FailFast(
                            "The corresponding delegate has been garbage collected. " +
                            "Please make sure the delegate is still referenced by managed code when you are using the marshalled native function pointer."
                        );
                    }

                    // Use a cast here to make sure we catch bugs in MCG code
                    return (T)target;
                }
                else
                {
                    //
                    // The thunk is not in the map.
                    // This should never happen in current allocation policy because the thunk will not get
                    // released, but rather reused. If this indeed is happening, we have a bug
                    //
                    Environment.FailFast("Unrecongized thunk");

                    return (T)null;
                }
            }
            finally
            {
                thunkMap.LockRelease();
            }
#endif
        }

        /// <summary>
        /// Try to get ICollectio<T>
        /// </summary>
        /// <param name="interfaceTypeHandle"></param>
        /// <param name="firstTypeHandle"></param>
        /// <param name="secondTypeHandle"></param>
        /// <returns></returns>
        internal static bool TryGetTypeHandleForICollecton(RuntimeTypeHandle interfaceTypeHandle, out RuntimeTypeHandle firstTypeHandle, out RuntimeTypeHandle secondTypeHandle)
        {
            for (int i = 0; i < s_moduleCount; i++)
            {
                if (s_modules[i].TryGetTypeHandleForICollecton(interfaceTypeHandle, out firstTypeHandle, out secondTypeHandle))
                    return true;
            }

            firstTypeHandle = default(RuntimeTypeHandle);
            secondTypeHandle = default(RuntimeTypeHandle);
            return false;
        }
    }


}
