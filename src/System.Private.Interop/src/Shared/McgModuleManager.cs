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
        private static McgModule[] s_modules; // work around for multi-file cctor ordering issue: don't initialize in cctor, initialize lazily
        private static volatile int s_moduleCount;
        private static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, McgInterfaceInfo> s_runtimeTypeHandleToMcgInterfaceInfoMap;
        private static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, CCWTemplateInfo> s_runtimeTypeHandleToCCWTemplateInfoMap;

        static McgModuleManager()
        {
            CCWLookupMap.InitializeStatics();
            ContextEntry.ContextEntryManager.InitializeStatics();
            ComObjectCache.InitializeStatics();

            const int DefaultSize = 101; // small prime number to avoid resizing in start up code
            s_runtimeTypeHandleToMcgInterfaceInfoMap = new Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, McgInterfaceInfo>(DefaultSize, new RuntimeTypeHandleComparer(), /* sync = */ true);
            s_runtimeTypeHandleToCCWTemplateInfoMap = new Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, CCWTemplateInfo>(DefaultSize, new RuntimeTypeHandleComparer(), /* sync = */ true);
        }

        private static InternalModule s_internalModule;

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

        internal static McgModule GetModule(int moduleIndex)
        {
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
        /// 
        /// 
        /// </summary>
        internal static bool TryGetClassTypeFromName(string name, out RuntimeTypeHandle classType)
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
                if (s_modules[i].TryGetClassFromNameInClassData(name, out classType))
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
                if (s_modules[i].TryGetClassFromNameInAdditionalClassData(name, out classType))
                {
                    return true;
                }
            }

            //
            // There were no matches in the m_classData or m_additionalClassData tables, so no class
            // info is available for the requested type.
            //
            classType = default(RuntimeTypeHandle);
            return false;
        }

        internal static bool TryGetInterfaceTypeFromName(string name, out RuntimeTypeHandle interfaceType)
        {
            // Go through each module
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetInterfaceTypeFromName(name, out interfaceType))
                    return true;
            }

            interfaceType = default(RuntimeTypeHandle);
            return false;
        }

        internal static string GetTypeName(RuntimeTypeHandle type, out bool isWinRT)
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

        internal static Type GetTypeFromName(string name, out bool isWinRT)
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
        /// Given a GUID, retrieve the corresponding type(s)
        /// </summary>
        internal static IEnumerable<RuntimeTypeHandle> GetTypesFromGuid(ref Guid guid)
        {
            List<RuntimeTypeHandle> rets = new List<RuntimeTypeHandle>(s_moduleCount);
            for (int i = 0; i < s_moduleCount; ++i)
            {
                RuntimeTypeHandle ret = s_modules[i].GetTypeFromGuid(ref guid);

                if (!ret.IsNull())
                {
                    rets.Add(ret);
                }
            }

            return rets;
        }

        #region "Cache"
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
        internal static McgInterfaceInfo GetInterfaceInfoByHandle(RuntimeTypeHandle typeHnd)
        {
            McgInterfaceInfo interfaceInfo;

            try
            {
                s_runtimeTypeHandleToMcgInterfaceInfoMap.LockAcquire();
                if (!s_runtimeTypeHandleToMcgInterfaceInfoMap.TryGetValue(typeHnd, out interfaceInfo))
                {
                    interfaceInfo = GetInterfaceInfoByHandleInternal(typeHnd);
                    s_runtimeTypeHandleToMcgInterfaceInfoMap.Add(typeHnd, interfaceInfo);
                }
            }
            finally
            {
                s_runtimeTypeHandleToMcgInterfaceInfoMap.LockRelease();
            }

            return interfaceInfo;
        }

        private static McgInterfaceInfo GetInterfaceInfoByHandleInternal(RuntimeTypeHandle typeHnd)
        {
            int interfaceIndex;
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryLookupInterfaceType(typeHnd, out interfaceIndex))
                {
                    return new McgInterfaceInfo(i, interfaceIndex);
                }
            }

            return null;
        }

        internal static McgInterfaceData GetInterfaceDataByIndex(int moduleIndex, int interfaceIndex)
        {
            return s_modules[moduleIndex].GetInterfaceDataByIndex(interfaceIndex);
        }

        internal static McgClassData GetClassDataByIndex(int moduleIndex, int classIndex)
        {
            return s_modules[moduleIndex].GetClassDataByIndex(classIndex);
        }

        internal static CCWTemplateData GetCCWTemplateDataByIndex(int moduleIndex, int ccwTemplateIndex)
        {
            return s_modules[moduleIndex].GetCCWTemplateDataByIndex(ccwTemplateIndex);
        }
        
        internal static IEnumerable<RuntimeTypeHandle> GetImplementedInterfacesByIndex(int moduleIndex, int ccwTemplateIndex)
        {
            IEnumerable<RuntimeTypeHandle> implementedInterfaces;
            if (s_modules[moduleIndex].TryGetImplementedInterfaces(ccwTemplateIndex, out implementedInterfaces))
            {
                return implementedInterfaces;
            }

            return new RuntimeTypeHandle[0];
        }

        /// <summary>
        /// Given a RuntimeTypeHandle, return the corresonding McgClassInfo
        /// </summary>
        internal static McgClassInfo GetClassInfoFromTypeHandle(RuntimeTypeHandle typeHnd)
        {
            int classIndex;
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryLookupClassType(typeHnd, out classIndex))
                {
                    return new McgClassInfo(i, classIndex);
                }
            }

            return null;
        }

        internal static CCWTemplateInfo GetCCWTemplateDataInfoFromTypeHandle(RuntimeTypeHandle typeHnd)
        {
            CCWTemplateInfo ccwTemplateInfo;

            try
            {
                s_runtimeTypeHandleToCCWTemplateInfoMap.LockAcquire();
                if (!s_runtimeTypeHandleToCCWTemplateInfoMap.TryGetValue(typeHnd, out ccwTemplateInfo))
                {
                    ccwTemplateInfo = GetCCWTemplateDataInfoFromTypeHandleInternal(typeHnd);
                    s_runtimeTypeHandleToCCWTemplateInfoMap.Add(typeHnd, ccwTemplateInfo);
                }
            }
            finally
            {
                s_runtimeTypeHandleToCCWTemplateInfoMap.LockRelease();
            }

            return ccwTemplateInfo;
        }

        private static CCWTemplateInfo GetCCWTemplateDataInfoFromTypeHandleInternal(RuntimeTypeHandle typeHnd)
        {
            int ccwTemplateIndex;
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryLookupCCWTemplateType(typeHnd, out ccwTemplateIndex))
                {
                    return new CCWTemplateInfo(i, ccwTemplateIndex);
                }
            }

            return null;
        }
        #endregion

        #region "Interface Data"
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

        internal static RuntimeTypeHandle FindTypeSupportDynamic(Func<RuntimeTypeHandle, bool> predicate)
        {
            for (int i = 0; i < s_moduleCount; i++)
            {
                RuntimeTypeHandle info = s_modules[i].FindTypeSupportDynamic(predicate);

                if (!info.IsNull())
                    return info;
            }

            return default(RuntimeTypeHandle);
        }
        #endregion

        #region "CCWTemplate Data"
        internal static bool TryGetCCWRuntimeClassName(RuntimeTypeHandle ccwTypeHandle, out string ccwRuntimeClassName)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetCCWRuntimeClassName(ccwTypeHandle, out ccwRuntimeClassName))
                    return true;
            }

            ccwRuntimeClassName = default(string);
            return false;
        }
        internal static bool TryGetBaseType(RuntimeTypeHandle ccwType, out RuntimeTypeHandle baseType)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetBaseType(ccwType, out baseType))
                {
                    return true;
                }
            }

            baseType = default(RuntimeTypeHandle);
            return false;
        }

        internal static bool TryGetImplementedInterfaces(RuntimeTypeHandle ccwType, out IEnumerable<RuntimeTypeHandle> interfaces)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetImplementedInterfaces(ccwType, out interfaces))
                {
                    return true;
                }
            }

            interfaces = null;
            return false;
        }

        internal static bool TryGetIsWinRTType(RuntimeTypeHandle ccwType, out bool isWinRTType)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetIsWinRTType(ccwType, out isWinRTType))
                {
                    return true;
                }
            }

            isWinRTType = default(bool);
            return false;
        }
        #endregion

        #region "Struct Data"
        internal static bool TryGetStructUnsafeStructType(RuntimeTypeHandle structureTypeHandle, out RuntimeTypeHandle unsafeStructType)
        {
            McgStructMarshalData structMarshalData;
            if (TryGetStructMarshalData(structureTypeHandle, out structMarshalData))
            {
                unsafeStructType = structMarshalData.UnsafeStructType;
                return true;
            }

            unsafeStructType = default(RuntimeTypeHandle);
            return false;
        }

        internal static bool TryGetStructUnmarshalStub(RuntimeTypeHandle structureTypeHandle, out IntPtr unmarshalStub)
        {
            McgStructMarshalData structMarshalData;
            if (TryGetStructMarshalData(structureTypeHandle, out structMarshalData))
            {
                unmarshalStub = structMarshalData.UnmarshalStub;
                return true;
            }

            unmarshalStub = default(IntPtr);
            return false;
        }

        internal static bool TryGetStructMarshalStub(RuntimeTypeHandle structureTypeHandle, out IntPtr marshalStub)
        {
            McgStructMarshalData structMarshalData;
            if (TryGetStructMarshalData(structureTypeHandle, out structMarshalData))
            {
                marshalStub = structMarshalData.MarshalStub;
                return true;
            }

            marshalStub = default(IntPtr);
            return false;
        }

        internal static bool TryGetDestroyStructureStub(RuntimeTypeHandle structureTypeHandle, out IntPtr destroyStructureStub, out bool hasInvalidLayout)
        {
            McgStructMarshalData structMarshalData;
            if (TryGetStructMarshalData(structureTypeHandle, out structMarshalData))
            {
                destroyStructureStub = structMarshalData.DestroyStructureStub;
                hasInvalidLayout = structMarshalData.HasInvalidLayout;
                return true;
            }

            destroyStructureStub = default(IntPtr);
            hasInvalidLayout = default(bool);
            return false;
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
        #endregion

        #region "Boxing"
        internal static bool TryGetBoxingWrapperType(RuntimeTypeHandle typeHandle, bool IsSystemType, out RuntimeTypeHandle boxingWrapperType, out int boxingPropertyType, out IntPtr boxingStub)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetBoxingWrapperType(typeHandle, IsSystemType, out boxingWrapperType, out boxingPropertyType, out boxingStub))
                {
                    return true;
                }
            }

            boxingWrapperType = default(RuntimeTypeHandle);
            boxingPropertyType = default(int);
            boxingStub = default(IntPtr);
            return false;
        }

        internal static bool TryGetUnboxingStub(string className, out IntPtr unboxingStub)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetUnboxingStub(className, out unboxingStub))
                {
                    return true;
                }
            }

            unboxingStub = default(IntPtr);
            return false;
        }
        #endregion

        #region "PInvoke Delegate"
        internal static bool GetPInvokeDelegateData(RuntimeTypeHandle delegateType, out McgPInvokeDelegateData pinvokeDelegateData)
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
        #endregion

        #region "GenericArgumentData"
        internal static bool TryGetGenericArgumentMarshalInfo(RuntimeTypeHandle interfaceType, out McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo)
        {
            for (int i = 0; i < s_moduleCount; ++i)
            {
                if (s_modules[i].TryGetGenericArgumentMarshalInfo(interfaceType, out mcgGenericArgumentMarshalInfo))
                {
                    return true;
                }
            }

            mcgGenericArgumentMarshalInfo = default(McgGenericArgumentMarshalInfo);
            return false;
        }
        #endregion
    }
}
