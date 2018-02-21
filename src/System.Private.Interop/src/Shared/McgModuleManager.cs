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
using Internal.NativeFormat;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Manage a list of McgModules
    /// NOTE: This class is not CLS compliant but it is only used in Mcg output which is C#
    /// NOTE: Managed debugger depends on class full name: "System.Runtime.InteropServices.McgModuleManager"
    /// </summary>
    [CLSCompliant(false)]
    public static class McgModuleManager
    {
        internal const int NUM_BITS_FOR_MAX_MODULES = 3;
        internal const int MAX_MODULES = 1 << NUM_BITS_FOR_MAX_MODULES;

        /// <summary>
        /// NOTE: Managed debugger depends on field name: "s_modules" and field type must be Array
        /// Update managed debugger whenever field name/field type is changed.
        /// See CordbObjectValue::InitMcgModules in debug\dbi\values.cpp for more info
        /// </summary>
        private static McgModule[] s_modules; // work around for multi-file cctor ordering issue: don't initialize in cctor, initialize lazily
        private static volatile int s_moduleCount;
        private static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int> s_runtimeTypeHandleToInterfaceIndexMap;
        private static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int> s_runtimeTypeHandleToCCWTemplateIndexMap;
        private static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int> s_runtimeTypeHandleToClassIndexMap;
        private static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int> s_runtimeTypeHandleToCollectionIndexMap;
        private static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int> s_runtimeTypeHandleToBoxingIndexMap;

        /// <summary>
        /// Eager initialization code called from LibraryInitializer.
        /// </summary>
        internal static void Initialize()
        {
            UseDynamicInterop = false;
            
            CCWLookupMap.InitializeStatics();
            ContextEntry.ContextEntryManager.InitializeStatics();
            ComObjectCache.InitializeStatics();

        }

        private static InternalModule s_internalModule;

        public static bool UseDynamicInterop { get; set; }

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

        private static void InsertDataIntoDictionary(Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int> map, RuntimeTypeHandle typeHandle, int moduleIndex, int typeIndex)
        {
            if (!typeHandle.Equals(McgModule.s_DependencyReductionTypeRemovedTypeHandle))
            {
                int index;
                // TODO: Remove this check. Ideally there shouldn't be any duplicate types across all the modules, but today we do have them. When queried for
                // type we return the one which resides in the module with highest priority.
                if (!map.TryGetValue(typeHandle, out index))
                {

                    int totalIndex = (typeIndex << NUM_BITS_FOR_MAX_MODULES) | moduleIndex;
                    map.Add(typeHandle, totalIndex);
                }
            }
        }

        public static void LateInitialize()
        {
            int totalInterfaces = 0;
            int totalCCWTemplates = 0;
            int totalClasses = 0;
            int totalCollections = 0;
            int totalBoxings = 0;
            for (int moduleIndex = 0; moduleIndex < s_moduleCount; moduleIndex++)
            {
                totalInterfaces += s_modules[moduleIndex].GetInterfaceDataCount();
                totalCCWTemplates += s_modules[moduleIndex].GetCCWTemplateDataCount();
                totalClasses += s_modules[moduleIndex].GetClassDataCount();
                totalCollections += s_modules[moduleIndex].GetCollectionDataCount();
                totalBoxings += s_modules[moduleIndex].GetBoxingDataCount();
            }

            s_runtimeTypeHandleToInterfaceIndexMap = new Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int>(totalInterfaces, RuntimeTypeHandleComparer.Instance, /* sync = */ false);
            s_runtimeTypeHandleToCCWTemplateIndexMap = new Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int>(totalCCWTemplates, RuntimeTypeHandleComparer.Instance, /* sync = */ false);
            s_runtimeTypeHandleToClassIndexMap = new Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int>(totalClasses, RuntimeTypeHandleComparer.Instance, /* sync = */ false);
            s_runtimeTypeHandleToCollectionIndexMap = new Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int>(totalCollections, RuntimeTypeHandleComparer.Instance, /* sync = */ false);
            s_runtimeTypeHandleToBoxingIndexMap = new Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int>(totalBoxings, RuntimeTypeHandleComparer.Instance, /* sync = */ false);

            for (int moduleIndex = 0; moduleIndex < s_moduleCount; moduleIndex++)
            {
                McgInterfaceData[] interfaceData = s_modules[moduleIndex].GetAllInterfaceData();
                if (interfaceData != null)
                {
                    for (int typeIndex = 0; typeIndex < interfaceData.Length; typeIndex++)
                    {
                        InsertDataIntoDictionary(s_runtimeTypeHandleToInterfaceIndexMap, interfaceData[typeIndex].ItfType, moduleIndex, typeIndex);
                    }
                }

                CCWTemplateData[] ccwTemplateData = s_modules[moduleIndex].GetAllCCWTemplateData();
                if (ccwTemplateData != null)
                {
                    for (int typeIndex = 0; typeIndex < ccwTemplateData.Length; typeIndex++)
                    {
                        InsertDataIntoDictionary(s_runtimeTypeHandleToCCWTemplateIndexMap, ccwTemplateData[typeIndex].ClassType, moduleIndex, typeIndex);
                    }
                }

                McgClassData[] classData = s_modules[moduleIndex].GetAllClassData();
                if (classData != null)
                {
                    for (int typeIndex = 0; typeIndex < classData.Length; typeIndex++)
                    {
                        InsertDataIntoDictionary(s_runtimeTypeHandleToClassIndexMap, classData[typeIndex].ClassType, moduleIndex, typeIndex);
                    }
                }

                McgCollectionData[] collectionData = s_modules[moduleIndex].GetAllCollectionData();
                if (collectionData != null)
                {
                    for (int typeIndex = 0; typeIndex < collectionData.Length; typeIndex++)
                    {
                        InsertDataIntoDictionary(s_runtimeTypeHandleToCollectionIndexMap, collectionData[typeIndex].CollectionType, moduleIndex, typeIndex);
                    }
                }

                McgBoxingData[] boxingData = s_modules[moduleIndex].GetAllBoxingData();
                if (boxingData != null)
                {
                    for (int typeIndex = 0; typeIndex < boxingData.Length; typeIndex++)
                    {
                        InsertDataIntoDictionary(s_runtimeTypeHandleToBoxingIndexMap, boxingData[typeIndex].ManagedClassType, moduleIndex, typeIndex);
                    }
                }
            }

#if DEBUG
            if (McgModuleManager.UseDynamicInterop)
            {
                for (int moduleIndex = 0; moduleIndex < s_moduleCount; moduleIndex++)
                {
                    s_modules[moduleIndex].VerifyWinRTGenericInterfaceGuids();
                }
            }
#endif
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

            int moduleIndex, typeIndex;
            string name;
            if (GetIndicesForInterface(type, out moduleIndex, out typeIndex))
            {
                if (s_modules[moduleIndex].TryGetInterfaceTypeNameByIndex(typeIndex, out name))
                {
                    // WinRT interface or WinRT delegate
                    isWinRT = s_modules[moduleIndex].GetInterfaceDataByIndex(typeIndex).IsIInspectableOrDelegate;
                    return name;
                }
            }

            if (GetIndicesForClass(type, out moduleIndex, out typeIndex))
            {
                if (s_modules[moduleIndex].TryGetClassTypeNameByIndex(typeIndex, out name))
                {
                    isWinRT = (s_modules[moduleIndex].GetClassDataByIndex(typeIndex).Flags & McgClassFlags.IsWinRT) != 0;
                    return name;
                }
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

            public static readonly RuntimeTypeHandleComparer Instance = new RuntimeTypeHandleComparer();

            bool IEqualityComparer<RuntimeTypeHandle>.Equals(RuntimeTypeHandle handle1, RuntimeTypeHandle handle2)
            {
                return handle1.Equals(handle2);
            }

            int IEqualityComparer<RuntimeTypeHandle>.GetHashCode(RuntimeTypeHandle obj)
            {
                return obj.GetHashCode();
            }
        }

        private static bool GetIndicesFromMap(System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, int> map, RuntimeTypeHandle typeHandle, out int moduleIndex, out int typeIndex)
        {
            int totalIndex;
            if (map.TryGetValue(typeHandle, out totalIndex))
            {
                moduleIndex = totalIndex & (MAX_MODULES - 1);
                typeIndex = totalIndex >> NUM_BITS_FOR_MAX_MODULES;
                return true;
            }
            moduleIndex = -1;
            typeIndex = -1;
            return false;

        }
        internal static bool GetIndicesForInterface(RuntimeTypeHandle typeHandle, out int moduleIndex, out int interfaceIndex)
        {
            return GetIndicesFromMap(s_runtimeTypeHandleToInterfaceIndexMap, typeHandle, out moduleIndex, out interfaceIndex);
        }

        internal static bool GetIndicesForClass(RuntimeTypeHandle typeHandle, out int moduleIndex, out int classIndex)
        {
            return GetIndicesFromMap(s_runtimeTypeHandleToClassIndexMap, typeHandle, out moduleIndex, out classIndex);
        }

        internal static bool GetIndicesForCCWTemplate(RuntimeTypeHandle typeHandle, out int moduleIndex, out int ccwTemplateIndex)
        {
            return GetIndicesFromMap(s_runtimeTypeHandleToCCWTemplateIndexMap, typeHandle, out moduleIndex, out ccwTemplateIndex);
        }

        internal static bool GetIndicesForCollection(RuntimeTypeHandle typeHandle, out int moduleIndex, out int collecitonIndex)
        {
            return GetIndicesFromMap(s_runtimeTypeHandleToCollectionIndexMap, typeHandle, out moduleIndex, out collecitonIndex);
        }

        internal static bool GetIndicesForBoxing(RuntimeTypeHandle typeHandle, out int moduleIndex, out int boxingIndex)
        {
            return GetIndicesFromMap(s_runtimeTypeHandleToBoxingIndexMap, typeHandle, out moduleIndex, out boxingIndex);
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

        #endregion

        #region "Interface Data"
        internal static bool TryGetTypeHandleForICollecton(RuntimeTypeHandle interfaceTypeHandle, out RuntimeTypeHandle firstTypeHandle, out RuntimeTypeHandle secondTypeHandle)
        {
            int moduleIndex, typeIndex;
            if (GetIndicesForCollection(interfaceTypeHandle, out moduleIndex, out typeIndex))
            {
                return s_modules[moduleIndex].TryGetTypeHandleForICollecton(typeIndex, out firstTypeHandle, out secondTypeHandle);
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
            int moduleIndex, typeIndex;
            if (GetIndicesForCCWTemplate(ccwTypeHandle, out moduleIndex, out typeIndex))
            {
                if (s_modules[moduleIndex].TryGetCCWRuntimeClassName(typeIndex, out ccwRuntimeClassName))
                    return true;
            }

            ccwRuntimeClassName = default(string);
            return false;
        }
        internal static bool TryGetBaseType(RuntimeTypeHandle ccwType, out RuntimeTypeHandle baseType)
        {
            int moduleIndex, typeIndex;
            if (GetIndicesForCCWTemplate(ccwType, out moduleIndex, out typeIndex))
            {
                if (s_modules[moduleIndex].TryGetBaseType(typeIndex, out baseType))
                {
                    return true;
                }
            }

            baseType = default(RuntimeTypeHandle);
            return false;
        }

        internal static bool TryGetImplementedInterfaces(RuntimeTypeHandle ccwType, out IEnumerable<RuntimeTypeHandle> interfaces)
        {
            int moduleIndex, typeIndex;
            if (GetIndicesForCCWTemplate(ccwType, out moduleIndex, out typeIndex))
            {
                if (s_modules[moduleIndex].TryGetImplementedInterfaces(typeIndex, out interfaces))
                {
                    return true;
                }
            }

            interfaces = null;
            return false;
        }

        internal static bool TryGetIsWinRTType(RuntimeTypeHandle ccwType, out bool isWinRTType)
        {
            int moduleIndex, typeIndex;
            if (GetIndicesForCCWTemplate(ccwType, out moduleIndex, out typeIndex))
            {
                if (s_modules[moduleIndex].TryGetIsWinRTType(typeIndex, out isWinRTType))
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
        /// Fetch struct WinRT Name for a given struct. 
        /// The returned WinRT name is only valid for computing guid during runtime
        /// </summary>
        /// <param name="structTypeHandle">Specified struct</param>
        /// <param name="structWinRTName">Struct WinRT Name</param>
        /// <returns>true, if the structs exists in mcg generated module</returns>
        internal static bool TryGetStructWinRTName(RuntimeTypeHandle structTypeHandle, out string structWinRTName)
        {
            for (int i = 0; i < s_moduleCount; i++)
            {
                if (s_modules[i].TryGetStructWinRTName(structTypeHandle, out structWinRTName))
                {
                    return true;
                }
            }

            structWinRTName = default(string);
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
        internal static bool TryGetBoxingWrapperType(RuntimeTypeHandle typeHandle, object target, out RuntimeTypeHandle boxingWrapperType, out int boxingPropertyType, out IntPtr boxingStub)
        {
            int moduleIndex, typeIndex;
            if (GetIndicesForBoxing(typeHandle, out moduleIndex, out typeIndex))
            {
                McgBoxingData boxingData = s_modules[moduleIndex].GetBoxingDataByIndex(typeIndex);
                boxingWrapperType = boxingData.CLRBoxingWrapperType;
                boxingPropertyType = boxingData.PropertyType;
                boxingStub = boxingData.BoxingStub;
                return true;
            }

            if (target is Type)
            {
                if (GetIndicesForBoxing(typeof(System.Type).TypeHandle, out moduleIndex, out typeIndex))
                {
                    McgBoxingData boxingData = s_modules[moduleIndex].GetBoxingDataByIndex(typeIndex);
                    boxingWrapperType = boxingData.CLRBoxingWrapperType;
                    boxingPropertyType = boxingData.PropertyType;
                    boxingStub = boxingData.BoxingStub;
                    return true;
                }
            }

#if !RHTESTCL && PROJECTN && ENABLE_WINRT
            // Dynamic boxing support
            // TODO: Consider to use the field boxingStub for all projected reference types.
            // TODO: now it is only used for boxing "System.Uri".
            // TODO: For Projected value types, IReference<T>.get_Value(out T) should marshal it correctly.
            // TODO: For projected refernce types, IReference<Ojbect> won't do correct marshal for you.
            boxingStub = default(IntPtr); 
            if (McgModuleManager.UseDynamicInterop && DynamicInteropBoxingHelpers.Boxing(typeHandle, out boxingWrapperType, out boxingPropertyType))
                return true;
#endif

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
            return false;
        }
#endregion

#region "GenericArgumentData"
        internal static bool TryGetGenericArgumentMarshalInfo(RuntimeTypeHandle interfaceType, out McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo)
        {
            int moduleIndex, typeIndex;
            if (GetIndicesForInterface(interfaceType, out moduleIndex, out typeIndex))
            {
                if (s_modules[moduleIndex].TryGetGenericArgumentMarshalInfo(typeIndex, out mcgGenericArgumentMarshalInfo))
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
