// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
//
// Contains McgModule and various data that describes interop information for types
// seen by MCG. These data will be used at runtime for MCG to make runtime decisions
// during marshalling.
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
using Internal.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Module-level operations such as looking up strongly-typed RCWs
    /// NOTE: This interface is not CLS compliant but it is only used in Mcg output which is C#
    /// </summary>
    [CLSCompliant(false)]
    public unsafe abstract partial class McgModule
    {
        int m_mcgDataModulePriority; // access priority for this module in McgModuleManager

        StringPool m_stringPool;           // Compressed strings

        /// <summary>
        /// NOTE: Managed debugger depends on field name: "m_interfaceData" and field type must be McgInterfaceData[]
        /// Update managed debugger whenever field name/field type is changed.
        /// See CordbObjectValue::WalkTypeInfo in debug\dbi\values.cpp
        /// </summary>
        McgInterfaceData[] m_interfaceData;
        CCWTemplateData[] m_ccwTemplateData;          // All CCWTemplateData is saved here
        RuntimeTypeHandle[] m_supportedInterfaceList;   // List of supported interfaces type handle
        // CCWTemplateData
        McgClassData[] m_classData;                 // Used for TypeName marshalling and for CreateComObject.
        // Contains entries for WinRT classes and for interfaces
        // projected as value types (i.e. Nullable<T>/KeyValuePair<K,V>)
        McgBoxingData[] m_boxingData;
        McgAdditionalClassData[] m_additionalClassData;     // Additional class data that captures parent
        // child relationship
        McgCollectionData[] m_collectionData;       // Maps from an ICollection or IReadOnlyCollection type to
        //    the corresponding entries in m_interfaceTypeInfo for IList,
        //    IDictionary, IReadOnlyList, IReadOnlyDictionary
        McgPInvokeDelegateData[] m_pinvokeDelegateData;                 // List of pinvoke delegates
        McgCCWFactoryInfoEntry[] m_ccwFactories;  // List of CCW factories provided to native via DllGetActivationFactory
        McgStructMarshalData[] m_structMarshalData;         // List of struct marshalling data for Marshal APIs
        McgUnsafeStructFieldOffsetData[] m_unsafeStructOffsetData;
        McgGenericArgumentMarshalInfo[] m_genericArgumentMarshalInfo;   // Array of generic argument marshal information for shared CCW
        McgHashcodeVerifyEntry[] m_hashcodeVerifyData;
#if CORECLR
        private object m_DictionaryLock = new object();
        Dictionary<RuntimeTypeHandle, int> m_interfaceTypeInfo_Hashtable;
        Dictionary<RuntimeTypeHandle, int> m_ccwTemplateData_Hashtable;
        Dictionary<RuntimeTypeHandle, int> m_classData_Hashtable;
        Dictionary<RuntimeTypeHandle, int> m_collectionData_Hashtable;
        Dictionary<RuntimeTypeHandle, int> m_typeNameMarshalingData_Hashtable;
        Dictionary<RuntimeTypeHandle, int> m_boxingData_Hashtable;
#else
        NativeReader m_interfaceTypeInfo_Hashtable;
        NativeReader m_ccwTemplateData_Hashtable;
        NativeReader m_classData_Hashtable;
        NativeReader m_collectionData_Hashtable;
        NativeReader m_boxingData_Hashtable;
#endif

#if DEBUG
        bool m_hashcodesVerified;
#endif // DEBUG

        FixedHashTable m_guidMap;

#if CORECLR
        Dictionary<RuntimeTypeHandle, int> InitializeLookupHashTable(Func<int, RuntimeTypeHandle> getEntryTypeHandle,
                                                                     Func<int> getTableSize)
        {
            int tableSize = getTableSize();
            Dictionary<RuntimeTypeHandle, int> hashtable = new Dictionary<RuntimeTypeHandle, int>(tableSize);
            for (int tableIndex = 0; tableIndex < tableSize; tableIndex++)
            {
                RuntimeTypeHandle handle = getEntryTypeHandle(tableIndex);
                hashtable[handle] = tableIndex;
            }
            return hashtable;
        }

        // Build the lookup hashtable on the go since MCG can't generate table index lookup hashtable for CoreCLR.
        // We aggressively cache everything on first lookup
        int LookupTypeHandleInHashtable(
             RuntimeTypeHandle typeHandle,
             ref Dictionary<RuntimeTypeHandle, int> hashtable,
             Func<int, RuntimeTypeHandle> getEntryTypeHandle,
             Func<int> getTableSize)
        {
            if (hashtable == null)
            {
                Dictionary<RuntimeTypeHandle, int> lookupTable = InitializeLookupHashTable(getEntryTypeHandle, getTableSize);
                if (Interlocked.CompareExchange(ref hashtable, lookupTable, null) != null)
                {
                    // Another thread beat us to it , use the one from that thread
                    lookupTable = null;
                }
            }

            int tableIndex;
            if (hashtable.TryGetValue(typeHandle, out tableIndex))
                return tableIndex;

            // should never get here
            return -1;
        }

#else
        // The hashtables generated by MCG map from a RuntimeTypeHandle hashcode to a bucket of table indexes.
        // These indexes are used to lookup an entry in a corresponding MCG-generated table.  The table entry
        // must also contain a RuntimeTypeHandle that uniquely identifies it.  The getEntryTypeHandle delegate
        // is used to perform this lookup for the specific MCG-generated table.
        unsafe int LookupTypeHandleInHashtable(
            RuntimeTypeHandle typeHandle,
            ref NativeReader hashtable,
            Func<int, RuntimeTypeHandle> getEntryTypeHandle,
            Func<int> getTableSize)
        {
#if DEBUG
            if (!m_hashcodesVerified)
            {
                bool allVerified = this.VerifyHashCodes();
                Debug.Assert(allVerified);
                m_hashcodesVerified = true;
            }
#endif // DEBUG

            // Not all McgModules provide all hashtables.  A notable exception is the 'internal' module.
            // However, one could imagine if a particular compilation unit didn't have any entries in a table
            // it could also have a null hashtable (instead of an empty hashtable).
            if (hashtable == null)
                return -1;

            var htParser = new NativeParser(hashtable, 0);
            var ht = new NativeHashtable(htParser);

            NativeParser entryParser;
            var enumBucket = ht.Lookup(typeHandle.GetHashCode());
            while (!(entryParser = enumBucket.GetNext()).IsNull)
            {
                int tableIndex = (int)entryParser.GetUnsigned();
                if (getEntryTypeHandle(tableIndex).Equals(typeHandle))
                {
                    return tableIndex;
                }
            }
            return -1;
        }
#endif



        /// <summary>
        /// Interface lookup on m_interfaceTypeInfo array using on-demand generated hash table(if available)
        /// Derived McgModule can overide this method to implement its own way to Look up RuntimeTypeHandle in m_interfaceData
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal int InterfaceDataLookup(RuntimeTypeHandle typeHandle)
        {
#if CORECLR
            return LookupTypeHandleInHashtable(
                typeHandle,
                ref m_interfaceTypeInfo_Hashtable,
                m_interfaceDataLookup_GetEntryTypeHandleCallback,
                m_interfaceDataLookup_GetTableSizeCallback);
#else
            if (m_interfaceTypeInfo_Hashtable != null)
            {
                return LookupTypeHandleInHashtable(
                    typeHandle,
                    ref m_interfaceTypeInfo_Hashtable,
                    m_interfaceDataLookup_GetEntryTypeHandleCallback,
                    m_interfaceDataLookup_GetTableSizeCallback);
            }
            else
            {
                // InternalModule doesn't provide HashTable
                Debug.Assert(this is InternalModule);
                for (int i = 0; i < m_interfaceData.Length; i++)
                {
                    if (m_interfaceData[i].ItfType.Equals(typeHandle))
                        return i;
                }
                return -1;
            }
#endif
        }

        private Func<int, RuntimeTypeHandle> m_interfaceDataLookup_GetEntryTypeHandleCallback;
        private Func<int> m_interfaceDataLookup_GetTableSizeCallback;

        RuntimeTypeHandle InterfaceDataLookup_GetEntryTypeHandle(int index)
        {
            return m_interfaceData[index].ItfType;
        }

        int InterfaceDataLookup_GetTableSize()
        {
            return m_interfaceData.Length;
        }

        int CollectionDataLookup(RuntimeTypeHandle typeHandle)
        {
            return LookupTypeHandleInHashtable(
                typeHandle,
                ref m_collectionData_Hashtable,
                m_collectionDataLookup_GetEntryTypeHandleCallback,
                m_collectionDataLookup_GetTableSizeCallback);
        }

        private Func<int, RuntimeTypeHandle> m_collectionDataLookup_GetEntryTypeHandleCallback;
        private Func<int> m_collectionDataLookup_GetTableSizeCallback;

        RuntimeTypeHandle CollectionDataLookup_GetEntryTypeHandle(int index)
        {
            return m_collectionData[index].CollectionType;
        }

        int CollectionDataLookup_GetTableSize()
        {
            return m_collectionData.Length;
        }

        int ClassDataLookup(RuntimeTypeHandle typeHandle)
        {
            return LookupTypeHandleInHashtable(
                typeHandle,
                ref m_classData_Hashtable,
                m_classData_GetEntryTypeHandleCallback,
                m_classData_GetTableSizeCallback);
        }

        private Func<int, RuntimeTypeHandle> m_classData_GetEntryTypeHandleCallback;
        private Func<int> m_classData_GetTableSizeCallback;

        RuntimeTypeHandle ClassDataLookup_GetEntryTypeHandle(int index)
        {
            return m_classData[index].ClassType;
        }

        int ClassDataLookup_GetTableSize()
        {
            return m_classData == null ? 0 : m_classData.Length;
        }

        int BoxingDataLookup(RuntimeTypeHandle typeHandle)
        {
            return LookupTypeHandleInHashtable(
                typeHandle,
                ref m_boxingData_Hashtable,
                m_boxingDataLookup_GetEntryTypeHandleCallback,
                m_boxingDataLookup_GetTableSizeCallback);
        }

        private Func<int, RuntimeTypeHandle> m_boxingDataLookup_GetEntryTypeHandleCallback;
        private Func<int> m_boxingDataLookup_GetTableSizeCallback;

        RuntimeTypeHandle BoxingDataLookup_GetEntryTypeHandle(int index)
        {
            return m_boxingData[index].ManagedClassType;
        }

        int BoxingDataLookup_GetTableSize()
        {
            return m_boxingData.Length;
        }

        /// <summary>
        /// Returns the corresponding CCW template index for the specified type
        /// </summary>
        /// <returns>index if the type is found. -1 if it is not a known type in current module</returns>
        internal int CCWTemplateDataLookup(RuntimeTypeHandle typeHandle)
        {
            return LookupTypeHandleInHashtable(
                typeHandle,
                ref m_ccwTemplateData_Hashtable,
                m_CCWTemplateDataLookup_GetEntryTypeHandleCallback,
                m_CCWTemplateDataLookup_GetTableSizeCallback);

        }

        private Func<int, RuntimeTypeHandle> m_CCWTemplateDataLookup_GetEntryTypeHandleCallback;
        private Func<int> m_CCWTemplateDataLookup_GetTableSizeCallback;

        RuntimeTypeHandle CCWTemplateDataLookup_GetEntryTypeHandle(int index)
        {
            return m_ccwTemplateData[index].ClassType;
        }

        int CCWTemplateDataLookup_GetTableSize()
        {
            return m_ccwTemplateData == null ? 0 : m_ccwTemplateData.Length;
        }

        public static readonly RuntimeTypeHandle s_DependencyReductionTypeRemovedTypeHandle =
            typeof(DependencyReductionTypeRemoved).TypeHandle;

        /// <summary>
        /// Construct McgModule
        /// </summary>
        public unsafe McgModule(
            int mcgDataModulePriority,
            McgInterfaceData[] interfaceData,
            CCWTemplateData[] ccwTemplateData,
            FixupRuntimeTypeHandle[] supportedInterfaceList,
            McgClassData[] classData,
            McgBoxingData[] boxingData,
            McgAdditionalClassData[] additionalClassData,
            McgCollectionData[] collectionData,
            McgPInvokeDelegateData[] pinvokeDelegateData,
            McgCCWFactoryInfoEntry[] ccwFactories,
            McgStructMarshalData[] structMarshalData,
            McgUnsafeStructFieldOffsetData[] unsafeStructFieldOffsetData,
            McgGenericArgumentMarshalInfo[] genericArgumentMarshalInfo,
            McgHashcodeVerifyEntry[] hashcodeVerifyData,
            byte[] interfaceTypeInfo_Hashtable,
            byte[] ccwTemplateData_Hashtable,
            byte[] classData_Hashtable,
            byte[] collectionData_Hashtable,
            byte[] boxingData_Hashtable)
        {
            m_mcgDataModulePriority = mcgDataModulePriority;
            m_interfaceData = interfaceData;
            m_classData = classData;
            m_boxingData = boxingData;
            m_collectionData = collectionData;
            m_pinvokeDelegateData = pinvokeDelegateData;
            m_additionalClassData = additionalClassData;
            m_ccwFactories = ccwFactories;
            m_structMarshalData = structMarshalData;
            m_unsafeStructOffsetData = unsafeStructFieldOffsetData;
            m_genericArgumentMarshalInfo = genericArgumentMarshalInfo;

            m_ccwTemplateData = ccwTemplateData;

            // Following code is disabled due to lazy static constructor dependency from McgModule which is
            // static eager constructor. Undo this when McgCurrentModule is using ModuleConstructorAttribute
#if EAGER_CTOR_WORKAROUND
            Debug.Assert(m_interfaceTypeInfo != null);
#endif
            if (supportedInterfaceList != null)
            {
                m_supportedInterfaceList = new RuntimeTypeHandle[supportedInterfaceList.Length];
                for (int i = 0; i < supportedInterfaceList.Length; i++)
                {
                    m_supportedInterfaceList[i] = supportedInterfaceList[i].RuntimeTypeHandle;
                }
            }
            else
            {
                m_supportedInterfaceList = null;
            }

            m_hashcodeVerifyData = hashcodeVerifyData;
#if CORECLR
            // Will be Lazy intialized
            m_interfaceTypeInfo_Hashtable = null;
            m_ccwTemplateData_Hashtable = null;
            m_classData_Hashtable = null;
            m_collectionData_Hashtable = null;
            m_typeNameMarshalingData_Hashtable = null;
            m_boxingData_Hashtable = null;
#else
            m_interfaceTypeInfo_Hashtable = NewHashtableReader(interfaceTypeInfo_Hashtable);
            m_ccwTemplateData_Hashtable = NewHashtableReader(ccwTemplateData_Hashtable);
            m_classData_Hashtable = NewHashtableReader(classData_Hashtable);
            m_collectionData_Hashtable = NewHashtableReader(collectionData_Hashtable);
            m_boxingData_Hashtable = NewHashtableReader(boxingData_Hashtable);
#endif

            //
            // Initialize cached instance delegates (we won't get that if you use lambda even though we only
            // use instance member variables inside the lambda).
            //
            m_interfaceDataLookup_GetEntryTypeHandleCallback =
                new Func<int, RuntimeTypeHandle>(this.InterfaceDataLookup_GetEntryTypeHandle);
            m_interfaceDataLookup_GetTableSizeCallback =
                new Func<int>(this.InterfaceDataLookup_GetTableSize);

            m_collectionDataLookup_GetEntryTypeHandleCallback =
                new Func<int, RuntimeTypeHandle>(this.CollectionDataLookup_GetEntryTypeHandle);
            m_collectionDataLookup_GetTableSizeCallback =
                new Func<int>(this.CollectionDataLookup_GetTableSize);

            m_classData_GetEntryTypeHandleCallback =
                new Func<int, RuntimeTypeHandle>(this.ClassDataLookup_GetEntryTypeHandle);
            m_classData_GetTableSizeCallback =
                new Func<int>(this.ClassDataLookup_GetTableSize);

            m_boxingDataLookup_GetEntryTypeHandleCallback =
                new Func<int, RuntimeTypeHandle>(this.BoxingDataLookup_GetEntryTypeHandle);
            m_boxingDataLookup_GetTableSizeCallback =
                new Func<int>(this.BoxingDataLookup_GetTableSize);

            m_CCWTemplateDataLookup_GetEntryTypeHandleCallback =
                new Func<int, RuntimeTypeHandle>(this.CCWTemplateDataLookup_GetEntryTypeHandle);
            m_CCWTemplateDataLookup_GetTableSizeCallback =
                new Func<int>(this.CCWTemplateDataLookup_GetTableSize);

#if DEBUG
            // Check no duplicate RuntimeTypeHandle in hashtable
            if (m_interfaceData != null)
            {
                System.Collections.Generic.Internal.HashSet<EquatableRuntimeTypeHandle> intfHashSet =
                    new System.Collections.Generic.Internal.HashSet<EquatableRuntimeTypeHandle>(m_interfaceData.Length);
                foreach (McgInterfaceData item in m_interfaceData)
                {
                    RuntimeTypeHandle typeHnd = item.ItfType;
                    if (!typeHnd.Equals(s_DependencyReductionTypeRemovedTypeHandle) && !typeHnd.Equals(default(RuntimeTypeHandle)))
                    {
                        if (intfHashSet.Add(new EquatableRuntimeTypeHandle(typeHnd), typeHnd.GetHashCode()))
                        {
                            Debug.Assert(false, "Duplicate RuntimeTypeHandle found in m_interfaceData");
                        }
                    }
                }
            }

            if (m_classData != null)
            {
                System.Collections.Generic.Internal.HashSet<EquatableRuntimeTypeHandle> classHashSet =
                    new System.Collections.Generic.Internal.HashSet<EquatableRuntimeTypeHandle>(m_classData.Length);
                foreach (McgClassData item in m_classData)
                {
                    RuntimeTypeHandle typeHnd = item.ClassType;
                    if (!typeHnd.Equals(s_DependencyReductionTypeRemovedTypeHandle) && !typeHnd.Equals(default(RuntimeTypeHandle)))
                    {
                        if (classHashSet.Add(new EquatableRuntimeTypeHandle(typeHnd), typeHnd.GetHashCode()))
                        {
                            Debug.Assert(false, "Duplicate RuntimeTypeHandle found in m_classData");
                        }
                    }
                }
            }
#endif
        }

        public int ModulePriority
        {
            get
            {
                return m_mcgDataModulePriority;
            }
        }

        private NativeReader NewHashtableReader(byte[] dataArray)
        {
            if (dataArray == null)
                return null;

            fixed (byte* pData = dataArray) // WARNING: must be pre-initialized and, therefore, frozen in place
            {
                return new NativeReader(pData, (uint)dataArray.Length);
            }
        }

        /// <summary>
        /// Set thunk function for interface using shared ccwVtable (generic AddrOf not supported by initdata transform + NUTC)
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public void SetThunk(int index, IntPtr thunk)
        {
            m_interfaceData[index].CcwVtable = thunk;
        }

        /// <summary>
        /// Set data for compressed strings
        /// </summary>
        public void SetStringPool(byte[] dictionary, byte[] strings, ushort[] index)
        {
            m_stringPool = new StringPool(dictionary, strings, index);
        }

        StringMap m_interfaceNameMap;
        StringMap m_classNameMap;
        StringMap m_additionalClassNameMap;
        StringMap m_ccwTemplateDataNameMap;
        StringMap m_ccwFactoriesNameMap;
        StringMap m_boxingDataNameMap;
        StringMap m_typeNameMarshalingDataNameMap;
        StringMap m_unsafeStructFieldNameMap;

        // There are two sets of functions here: one for 16-bit indices, good for 64k characters, the other for 32-bit indices for bigger applications
        // MCG will generate the right calls for the right width.
        // Functions not called will be trimmed away by DR

        public void SetinterfaceDataNameIndices(UInt16[] nameIndices)
        {
            m_interfaceNameMap = new StringMap16(m_stringPool, nameIndices);
        }

        public void SetclassDataNameIndices(UInt16[] nameIndices)
        {
            m_classNameMap = new StringMap16(m_stringPool, nameIndices);
        }

        public void SetadditionalClassDataNameIndices(UInt16[] nameIndices)
        {
            m_additionalClassNameMap = new StringMap16(m_stringPool, nameIndices);
        }

        public void SetccwFactoriesNameIndices(UInt16[] nameIndices)
        {
            m_ccwFactoriesNameMap = new StringMap16(m_stringPool, nameIndices);
        }

        public void SetccwTemplateDataNameIndices(UInt16[] nameIndices)
        {
            m_ccwTemplateDataNameMap = new StringMap16(m_stringPool, nameIndices);
        }

        public void SetboxingDataNameIndices(UInt16[] nameIndices)
        {
            m_boxingDataNameMap = new StringMap16(m_stringPool, nameIndices);
        }

        public void SettypeNameMarshalingDataNameIndices(UInt16[] nameIndices)
        {
            m_typeNameMarshalingDataNameMap = new StringMap16(m_stringPool, nameIndices);
        }

        public void SetinterfaceDataNameIndices(UInt32[] nameIndices)
        {
            m_interfaceNameMap = new StringMap32(m_stringPool, nameIndices);
        }

        public void SetclassDataNameIndices(UInt32[] nameIndices)
        {
            m_classNameMap = new StringMap32(m_stringPool, nameIndices);
        }

        public void SetadditionalClassDataNameIndices(UInt32[] nameIndices)
        {
            m_additionalClassNameMap = new StringMap32(m_stringPool, nameIndices);
        }

        public void SetccwFactoriesNameIndices(UInt32[] nameIndices)
        {
            m_ccwFactoriesNameMap = new StringMap32(m_stringPool, nameIndices);
        }

        public void SetccwTemplateDataNameIndices(UInt32[] nameIndices)
        {
            m_ccwTemplateDataNameMap = new StringMap32(m_stringPool, nameIndices);
        }

        public void SetboxingDataNameIndices(UInt32[] nameIndices)
        {
            m_boxingDataNameMap = new StringMap32(m_stringPool, nameIndices);
        }

        public void SettypeNameMarshalingDataNameIndices(UInt32[] nameIndices)
        {
            m_typeNameMarshalingDataNameMap = new StringMap32(m_stringPool, nameIndices);
        }

        public void SetunsafeStructFieldOffsetDataNameIndices(UInt16[] nameIndices)
        {
            m_unsafeStructFieldNameMap = new StringMap16(m_stringPool, nameIndices);
        }

        public void SetunsafeStructFieldOffsetDataNameIndices(UInt32[] nameIndices)
        {
            m_unsafeStructFieldNameMap = new StringMap32(m_stringPool, nameIndices);
        }

        internal unsafe bool TryGetCCWRuntimeClassName(RuntimeTypeHandle ccwTypeHandle, out string ccwRuntimeClassName)
        {
            ccwRuntimeClassName = null;
            if (m_ccwTemplateDataNameMap == null)
                return false;

            int slot = CCWTemplateDataLookup(ccwTypeHandle);
            if (slot >= 0)
            {
                ccwRuntimeClassName = m_ccwTemplateDataNameMap.GetString(slot);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to get offset data according to McgStructMarshalData's FieldOffsetStartIndex/NumOfFields
        /// </summary>
        /// <param name="structMarshalData">The Struct Marshal Data</param>
        /// <param name="fieldName">Name of field</param>
        /// <param name="offset">offset in bytes</param>
        /// <returns>if the offset value exists, return true; else return false </returns>
        internal bool TryGetStructFieldOffset(McgStructMarshalData structMarshalData, string fieldName, out uint offset)
        {
            // Try to find its field in map
            if (m_unsafeStructFieldNameMap != null)
            {
                int start = structMarshalData.FieldOffsetStartIndex;
                int count = structMarshalData.NumOfFields;

                for (int i = start; i < start + count; i++)
                {
                    if (fieldName == m_unsafeStructFieldNameMap.GetString(i))
                    {
                        offset = m_unsafeStructOffsetData[i].Offset;
                        return true;
                    }
                }
            }

            // Couldn't find its field
            offset = 0;
            return false;
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal unsafe bool TryLookupInterfaceType(RuntimeTypeHandle typeHnd, out int interfaceIndex)
        {
            int tableIndex = InterfaceDataLookup(typeHnd);
            if (tableIndex >= 0)
            {
                interfaceIndex = tableIndex;
                return true;
            }

            interfaceIndex = -1;
            return false;
        }

        internal unsafe bool TryLookupClassType(RuntimeTypeHandle typeHnd, out int classIndex)
        {
            int tableIndex = ClassDataLookup(typeHnd);
            if (tableIndex >= 0)
            {
                classIndex = tableIndex;
                return true;
            }

            classIndex = -1;
            return false;
        }

        internal bool TryLookupCCWTemplateType(RuntimeTypeHandle typeHnd, out int ccwTemplateIndex)
        {
            int tableIndex = CCWTemplateDataLookup(typeHnd);
            if (tableIndex >=0)
            {
                ccwTemplateIndex = tableIndex;
                return true;
            }

            ccwTemplateIndex = -1;
            return false;
        }

        internal unsafe bool TryLookupGenericArgumentType(RuntimeTypeHandle typeHnd, out int genericArgumentIndex)
        {
            int slot = InterfaceDataLookup(typeHnd);

            if (slot >= 0)
            {
                if (m_genericArgumentMarshalInfo != null)
                {
                    genericArgumentIndex = m_interfaceData[slot].MarshalIndex;
                    return true;
                }
            }

            genericArgumentIndex = -1;
            return false;
        }

        /// <summary>
        /// Given a GUID, retrieve the corresponding RuntimeTypeHandle
        /// @TODO: we should switch everything to RuntimeTypeHandle instead of relying on Guid
        /// </summary>
        unsafe internal RuntimeTypeHandle GetTypeFromGuid(ref Guid guid)
        {
            if (m_interfaceData != null)
            {
                if (m_guidMap == null)
                {
                    int size = m_interfaceData.Length;

                    FixedHashTable map = new FixedHashTable(size);

                    for (int i = 0; i < size; i++)
                    {
                        map.Add(m_interfaceData[i].ItfGuid.GetHashCode(), i);
                    }

                    Interlocked.CompareExchange(ref m_guidMap, map, null);
                }

                int hash = guid.GetHashCode();

                // Search hash table
                for (int slot = m_guidMap.GetFirst(hash); slot >= 0; slot = m_guidMap.GetNext(slot))
                {
                    if (InteropExtensions.GuidEquals(ref guid, ref m_interfaceData[slot].ItfGuid))
                    {
                        return m_interfaceData[slot].ItfType;
                    }
                }
            }

            return default(RuntimeTypeHandle);
        }

        /// <summary>
        /// Get the WinRT name of a given Type.  If the type is projected, returns the projected name.
        /// Sets isWinRT to true if the type definition is from a WinMD file.
        /// </summary>
        internal string GetTypeName(RuntimeTypeHandle type, ref bool isWinRT)
        {
            int slot = InterfaceDataLookup(type);

            if (slot >= 0 && m_interfaceNameMap != null)
            {
                //
                // WinRT interface or WinRT delegate
                //
                isWinRT = m_interfaceData[slot].IsIInspectableOrDelegate;

                return m_interfaceNameMap.GetString(slot);
            }

            if (m_classData != null && m_classNameMap != null)
            {
                int i = ClassDataLookup(type);

                if (i >= 0)
                {
                    isWinRT = (m_classData[i].Flags & McgClassFlags.IsWinRT) != 0;

                    return m_classNameMap.GetString(i);
                }
            }

            return null;
        }

        /// <summary>
        /// Get a Type object representing the named type, along with a boolean indicating if the type
        /// definition is from a WinMD file.
        /// </summary>
        internal Type GetTypeFromName(string name, ref bool isWinRT)
        {
            if (m_interfaceNameMap != null)
            {
                int i = m_interfaceNameMap.FindString(name);

                if (i >= 0)
                {
                    isWinRT = m_interfaceData[i].IsIInspectableOrDelegate;

                    return InteropExtensions.GetTypeFromHandle(m_interfaceData[i].ItfType);
                }
            }

            if (m_classNameMap != null)
            {
                int i = m_classNameMap.FindString(name);

                if (i >= 0)
                {
                    isWinRT = (m_classData[i].Flags & McgClassFlags.IsWinRT) != 0;

                    return InteropExtensions.GetTypeFromHandle(m_classData[i].ClassType);
                }
            }

            return null;
        }

        /// <summary>
        /// Search this module's m_classData table for information on the requested type.
        /// This function returns true if and only if it is able to locate a non-null RuntimeTypeHandle
        /// record for the requested type.
        /// </summary>
        internal bool TryGetClassFromNameInClassData(string name, out RuntimeTypeHandle classType)
        {
            if (m_classNameMap != null)
            {
                //
                // Search to see if we have a strong-typed RCW for this type.
                //
                int i = m_classNameMap.FindString(name);

                if ((i >= 0) && ((m_classData[i].Flags & McgClassFlags.NotComObject) == 0))
                {
                    //
                    // This module contains an m_classData row which matches the requested type. This row can
                    // be immediately used to compute the RuntimeTypeHandle that best describes the type.
                    //
                    classType = ComputeClosestClassForClassIndex(i);
                    if (!classType.Equals(default(RuntimeTypeHandle)))
                        return true;
                }
            }

            classType = default(RuntimeTypeHandle);
            return false;
        }

        /// <summary>
        /// Search this module's m_additionalClassData table for information on the requested type.
        /// This function returns true if and only if it is able to locate a non-null RuntimeTypeHandle
        /// record for the requested type.
        /// </summary>
        internal bool TryGetClassFromNameInAdditionalClassData(string name, out RuntimeTypeHandle classType)
        {
            if (m_additionalClassNameMap != null)
            {
                //
                // Search in additional class data for the closest match (for example, for MyButton, the
                // closest match is probably Button)
                //
                int i = m_additionalClassNameMap.FindString(name);

                if (i >= 0)
                {
                    //
                    // We've found a match. The matching row points to the "next best" strongly-typed RCW
                    // type that we should create (in place of the original class)
                    // For example, if native code is passing MatrixTransform, and MCG only knows about its
                    // base class DependencyObject, we should hand out DependencyObject
                    //
                    int classDataIndex = m_additionalClassData[i].ClassDataIndex;
                    RuntimeTypeHandle typeHandle = m_additionalClassData[i].ClassType;

                    if (classDataIndex >= 0)
                    {
                        //
                        // This module's m_additionalClassData table points to a m_classData row which describes
                        // the nearest available base class of the requested type. This row can be immediately used
                        // to compute the RuntimeTypeHandle that best describes the type.
                        //
                        classType = ComputeClosestClassForClassIndex(classDataIndex);
                        if (!classType.Equals(default(RuntimeTypeHandle)))
                            return true;
                    }
                    else
                    {
                        //
                        // This module's m_additionalClassData table lists a RuntimeTypeHandle which describes
                        // the nearest available base class of the requested type. If this nearest base class was
                        // not reduced away, then use it to locate RuntimeTypeHandle describing this "next best" type.
                        //
                        if (!typeHandle.Equals(s_DependencyReductionTypeRemovedTypeHandle))
                        {
                            classType = typeHandle;
                            if (!classType.Equals(default(RuntimeTypeHandle)))
                                return true;
                        }
                    }
                }
            }

            classType = default(RuntimeTypeHandle);
            return false;
        }

        /// <summary>
        /// This function computes an RuntimeTypeHandle instance that represents the best possible
        /// description of the type associated with the requested row in the m_classData table.
        ///
        /// RuntimeTypeHandle can generally be attached directly to the requested row. That said, in the
        /// case where the associated type was removed by the dependency reducer, it is necessary to
        /// walk the base class chain to find the nearest base type that is actually present at
        /// runtime.
        ///
        /// Note: This function can return default(RuntimeTypeHandle) if it determines that all information
        /// associated with the supplied row has been reduced away.
        /// </summary>
        private RuntimeTypeHandle ComputeClosestClassForClassIndex(int index)
        {
            Debug.Assert((index >= 0) && (index < m_classData.Length));

            if (!m_classData[index].ClassType.Equals(s_DependencyReductionTypeRemovedTypeHandle))
            {
                //
                // The current row lists either an non-reduced exact type or the nearest non-reduced base
                // type and is therefore the best possible description of the requested row.
                //
                return m_classData[index].ClassType;
            }
            else
            {
                //
                // The type in the current row was reduced away. Try to proceed to the base class.
                //
                int baseClassIndex = m_classData[index].BaseClassIndex;

                if (baseClassIndex >= 0)
                {
                    //
                    // The base class is described elsewhere in this m_classData table. Make a recursive call
                    // to compute its associated RuntimeTypeHandle.
                    //
                    return ComputeClosestClassForClassIndex(baseClassIndex);
                }
                else
                {
                    RuntimeTypeHandle baseClassTypeHandle = m_classData[index].BaseClassType;

                    if (baseClassTypeHandle.Equals(default(RuntimeTypeHandle)) ||
                        baseClassTypeHandle.Equals(s_DependencyReductionTypeRemovedTypeHandle))
                    {
                        //
                        // The reduced type either does not have a base class or refers to a base class that the
                        // dependency reducer found to be unnecessary.
                        //
                        return default(RuntimeTypeHandle);
                    }
                    else
                    {
                        return baseClassTypeHandle;
                    }
                }
            }
        }

        internal bool TryGetInterfaceTypeFromName(string name, out RuntimeTypeHandle interfaceType)
        {
            if (m_interfaceData != null && m_interfaceNameMap != null)
            {
                int index = m_interfaceNameMap.FindString(name);
                if (index >= 0)
                {
                    interfaceType = m_interfaceData[index].ItfType;
                    return true;
                }
            }

            interfaceType = default(RuntimeTypeHandle);
            return false;
        }
        internal McgInterfaceData GetInterfaceDataByIndex(int index)
        {
            Debug.Assert(index >= 0 && index < m_interfaceData.Length);
            return m_interfaceData[index];
        }

        internal McgClassData GetClassDataByIndex(int index)
        {
            Debug.Assert(index >= 0 && index < m_classData.Length);
            return m_classData[index];
        }

        internal CCWTemplateData GetCCWTemplateDataByIndex(int index)
        {
            Debug.Assert(index >= 0 && index < m_ccwTemplateData.Length);
            return m_ccwTemplateData[index];
        }

        int m_boxingDataTypeSlot = -1; // Slot for System.Type in boxing data table

        /// <summary>
        /// Given a boxed value type, return a wrapper supports the IReference interface
        /// </summary>
        /// <param name="typeHandleOverride">
        /// You might want to specify how to box this. For example, any object[] derived array could
        /// potentially boxed as object[] if everything else fails
        /// </param>
        internal bool TryGetBoxingWrapperType(RuntimeTypeHandle typeHandle, bool IsSystemType,
            out RuntimeTypeHandle boxingWrapperType, out int boxingPropertyType, out IntPtr boxingStub)
        {
            boxingWrapperType = default(RuntimeTypeHandle);
            boxingPropertyType = default(int);
            boxingStub = default(IntPtr);

            if ((m_boxingData == null) || (m_boxingData.Length == 0))
            {
                return false;
            }

            if (m_boxingDataTypeSlot == -1)
            {
                m_boxingDataTypeSlot = BoxingDataLookup(typeof(System.Type).TypeHandle);
            }

            //
            // Is this the exact type that we want? (Don't use 'is' check - it won't work well for
            // arrays)
            int slot = BoxingDataLookup(typeHandle);

            // NOTE: For System.Type marshalling, use 'is' check instead, as the actual type would be
            // some random internal type from reflection
            //
            if ((slot < 0) && IsSystemType)
            {
                slot = m_boxingDataTypeSlot;
            }

            if (slot >= 0)
            {
                boxingWrapperType = m_boxingData[slot].CLRBoxingWrapperType;
                boxingPropertyType = m_boxingData[slot].PropertyType;
                boxingStub = m_boxingData[slot].BoxingStub;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Unbox the WinRT boxed IReference<T>/IReferenceArray<T> and box it into Object so that managed
        /// code can unbox it later into the real T
        /// </summary>
        internal bool TryGetUnboxingStub(string className, out IntPtr unboxingStub)
        {
            unboxingStub = default(IntPtr);
            if (m_boxingData == null)
            {
                return false;
            }
            Debug.Assert(!String.IsNullOrEmpty(className));
            //
            // Avoid searching for null/empty name. BoxingData has null name entries
            //
            int slot = m_boxingDataNameMap.FindString(className);

            if (slot >= 0)
            {
                unboxingStub = m_boxingData[slot].UnboxingStub;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Retrieves delegate data for the specified delegate handle
        /// </summary>
        internal bool TryGetPInvokeDelegateData(RuntimeTypeHandle typeHandle, out McgPInvokeDelegateData pinvokeDelegateData)
        {
            if (m_pinvokeDelegateData != null)
            {
                for (int i = 0; i < m_pinvokeDelegateData.Length; i++)
                {
                    if (typeHandle.Equals(m_pinvokeDelegateData[i].Delegate))
                    {
                        pinvokeDelegateData = m_pinvokeDelegateData[i];
                        return true;
                    }
                }
            }

            pinvokeDelegateData = default(McgPInvokeDelegateData);
            return false;
        }

        /// <summary>
        /// Finds McgStructMarshalData for a struct if it is defined in this module
        /// </summary>
        /// <param name="structTypeHandle">TypeHandle for the safe struct</param>
        /// <param name="structMarshalData">McgStructMarshalData for the struct</param>
        /// <returns>True if the struct marshal data is available in this module</returns>
        internal bool TryGetStructMarshalData(RuntimeTypeHandle structTypeHandle, out McgStructMarshalData structMarshalData)
        {
            structMarshalData = default(McgStructMarshalData);

            if (m_structMarshalData == null)
            {
                return false;
            }

            for (int i = 0; i < m_structMarshalData.Length; i++)
            {
                if (structTypeHandle.Equals(m_structMarshalData[i].SafeStructType))
                {
                    if (m_structMarshalData[i].HasInvalidLayout)
                        throw new ArgumentException(SR.Format(SR.Argument_MustHaveLayoutOrBeBlittable, structTypeHandle.GetDisplayName()));

                    structMarshalData = m_structMarshalData[i];

                    return true;
                }
            }

            return false;
        }

        internal unsafe RuntimeTypeHandle FindTypeSupportDynamic(Func<RuntimeTypeHandle, bool> predicate)
        {
            for (int i = 0; i < m_interfaceData.Length; i++)
            {
                McgInterfaceData data = m_interfaceData[i];

                if (!data.DynamicAdapterClassType.IsNull() && predicate(data.ItfType))
                    return data.ItfType;
            }

            return default(RuntimeTypeHandle);
        }

        internal unsafe bool TryGetBaseType(RuntimeTypeHandle ccwType, out RuntimeTypeHandle baseType)
        {
            int ccwTemplateIndex = CCWTemplateDataLookup(ccwType);
            if (ccwTemplateIndex >= 0)
            {
                // Field ParentCCWTemplateIndex >=0 means that its baseclass is in the same module
                // Field BaseType != default(RuntimeTypeHandle) means that its baseclass isn't in the same module
                int parentIndex = m_ccwTemplateData[ccwTemplateIndex].ParentCCWTemplateIndex;
                RuntimeTypeHandle baseTypeHandle = m_ccwTemplateData[ccwTemplateIndex].BaseType;
                if (parentIndex >= 0)
                {
                    baseType = m_ccwTemplateData[parentIndex].ClassType;
                    return true;
                }
                else if (!baseTypeHandle.Equals(default(RuntimeTypeHandle)))
                {
                    // DR will keep all base types if one of its derived type is used(rooted)
                    if (baseTypeHandle.Equals(s_DependencyReductionTypeRemovedTypeHandle))
                    {
#if !RHTESTCL
                        Environment.FailFast(String.Format("Base Type of {0} discarded.", m_ccwTemplateData[ccwTemplateIndex].ClassType.GetDisplayName()));
#else
                        Environment.FailFast("Base Type discarded.");
#endif
                    }

                    baseType = baseTypeHandle;
                    return true;
                }
            }

            baseType = default(RuntimeTypeHandle);
            return false;
        }

        internal bool TryGetImplementedInterfaces(int ccwTemplateIndex, out IEnumerable<RuntimeTypeHandle> interfaces)
        {
            if (ccwTemplateIndex >= 0)
            {
                interfaces = new Collections.Generic.List<RuntimeTypeHandle>();

                //
                // Walk the interface list
                //
                interfaces = new ArraySegment<RuntimeTypeHandle>(
                    m_supportedInterfaceList,
                    m_ccwTemplateData[ccwTemplateIndex].SupportedInterfaceListBeginIndex,
                    m_ccwTemplateData[ccwTemplateIndex].NumberOfSupportedInterface
                 );

                return true;
            }

            interfaces = null;
            return false;
        }

        internal bool TryGetImplementedInterfaces(RuntimeTypeHandle ccwType, out IEnumerable<RuntimeTypeHandle> interfaces)
        {
            int ccwTemplateIndex = CCWTemplateDataLookup(ccwType);
            return TryGetImplementedInterfaces(ccwTemplateIndex, out interfaces);
        }

        internal bool TryGetIsWinRTType(RuntimeTypeHandle ccwType, out bool isWinRTType)
        {
            int ccwTemplateIndex = CCWTemplateDataLookup(ccwType);
            if (ccwTemplateIndex >= 0)
            {
                isWinRTType = m_ccwTemplateData[ccwTemplateIndex].IsWinRTType;
                return true;
            }

            isWinRTType = default(bool);
            return false;
        }

        internal bool TryGetTypeHandleForICollecton(RuntimeTypeHandle interfaceTypeHandle, out RuntimeTypeHandle firstTypeHandle, out RuntimeTypeHandle secondTypeHandle)
        {
            // Loop over our I[ReadOnly]Collection<T1,T2> instantiations to find the type infos for 
            // I[ReadOnly]List<KeyValuePair<T1,T2>> and I[ReadOnly]Dictionary<T1,T2>
            //
            // Note that only one of IList/IDictionary may be present.  
            if (m_collectionData != null)
            {
                int slot = CollectionDataLookup(interfaceTypeHandle);

                if (slot >= 0)
                {
                    firstTypeHandle = m_collectionData[slot].FirstType;
                    secondTypeHandle = m_collectionData[slot].SecondType;
                    return true;
                }
            }

            firstTypeHandle = default(RuntimeTypeHandle);
            secondTypeHandle = default(RuntimeTypeHandle);
            return false;
        }

        internal bool TryGetGenericArgumentMarshalInfo(RuntimeTypeHandle typeHandle, out McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo)
        {
            int slot = InterfaceDataLookup(typeHandle);

            if (slot >= 0)
            {
                if (m_genericArgumentMarshalInfo != null)
                {
                    int marshalIndex = m_interfaceData[slot].MarshalIndex;
                    mcgGenericArgumentMarshalInfo = m_genericArgumentMarshalInfo[marshalIndex];
                    return true;
                }
            }

            mcgGenericArgumentMarshalInfo = default(McgGenericArgumentMarshalInfo);
            return false;
        }
#if ENABLE_WINRT
        static Guid s_IID_IActivationFactory = new Guid(0x00000035, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

        /// <summary>
        /// Returns the requested interface pointer specified by itfType from the CCWActivationFactory
        /// object instance. Typically the requested interface is the
        /// System.Runtime.InteropServices.WindowsRuntime.IActivationFactory interface.
        /// </summary>
        public unsafe int GetCCWActivationFactory(HSTRING activatableClassId, RuntimeTypeHandle itfType, IntPtr* factoryOut)
        {
            try
            {
                string classId = McgMarshal.HStringToString(activatableClassId);

                if (classId == null)
                    return Interop.COM.E_INVALIDARG;

                RuntimeTypeHandle factoryTypeHandle = default(RuntimeTypeHandle);

                if (m_ccwFactoriesNameMap != null)
                {
                    int slot = m_ccwFactoriesNameMap.FindString(classId);

                    if (slot >= 0)
                    {
                        factoryTypeHandle = m_ccwFactories[slot].FactoryType;
                    }
                }

                if (factoryTypeHandle.IsNull())
                    return Interop.COM.E_NOINTERFACE;

                object factory = InteropExtensions.RuntimeNewObject(factoryTypeHandle);

                *factoryOut = McgMarshal.ObjectToComInterface(
                                     factory,
                                     itfType);
            }
            catch (Exception ex)
            {
                *factoryOut = default(IntPtr);
                return McgMarshal.GetHRForExceptionWinRT(ex);
            }

            return Interop.COM.S_OK;
        }
#endif

#if DEBUG
        bool VerifyHashCodes()
        {
            if (m_hashcodeVerifyData == null)
                return true;

            bool success = true;
            RuntimeTypeHandle typeRemovedType = typeof(DependencyReductionTypeRemoved).TypeHandle;

            for (int i = 0; i < m_hashcodeVerifyData.Length; i++)
            {
                McgHashcodeVerifyEntry entry = m_hashcodeVerifyData[i];
                if (entry.TypeHandle.Equals(typeRemovedType))
                    continue;

                uint nutcHashcode = unchecked((uint)entry.TypeHandle.GetHashCode());
                uint mcgHashcode = entry.HashCode;

                if (nutcHashcode != mcgHashcode)
                {
                    success = false;
                    Debug.WriteLine("MCG hashcode mistatch at index: " + DebugUtil.BasicToString(i)
                                    + " MCG: " + DebugUtil.ToHexStringUnsigned(mcgHashcode)
                                    + " NUTC: " + DebugUtil.ToHexStringUnsigned(nutcHashcode));
                }
            }
            return success;
        }
#endif // DEBUG

#if DEBUG
    // VerifyHashCodes must not call String.Format or any of the integer formatting routines in System.Private.CoreLib because
    // they will trigger the globalization code which uses WinRT interop, which call back here creating an
    // infinite recursion.  So we have a simple implementation for printing hex numbers.
    static class DebugUtil
    {
        static char GetHexChar(uint u)
        {
            if (u < 10)
            {
                return unchecked((char)('0' + u));
            }
            if (u < 16)
            {
                return unchecked((char)('a' + (u - 10)));
            }
            return (char)0;
        }

        static public string ToHexStringUnsigned(uint u)
        {
            return ToHexStringUnsignedLong(u, true, 8);
        }
        static public unsafe string ToHexStringUnsignedLong(ulong u, bool zeroPrepad, int numChars)
        {
            char[] chars = new char[numChars];

            int i = numChars - 1;

            for (; i >= 0; i--)
            {
                chars[i] = GetHexChar((uint)(u % 16));
                u = u / 16;

                if ((i == 0) || (!zeroPrepad && (u == 0)))
                    break;
            }

            string str;
            fixed (char* p = &chars[i])
            {
                str = new String(p, 0, numChars - i);
            }
            return str;
        }
        static public unsafe string BasicToString(int num)
        {
            char* pRevBuffer = stackalloc char[16];
            char* pFwdBuffer = stackalloc char[16];

            bool isNegative = (num < 0);
            if (isNegative)
                num = -num;

            int len = 0;
            while (num > 0)
            {
                int ch = num % 10;
                num = num / 10;

                pRevBuffer[len++] = (char)('0' + ch);
            }
            if (isNegative)
                pRevBuffer[len++] = '-';

            for (int i = (len - 1); i >= 0; i--)
            {
                pFwdBuffer[i] = pRevBuffer[(len - 1) - i];
            }
            return new String(pFwdBuffer, 0, len);
        }
    }

     internal class EquatableRuntimeTypeHandle : IEquatable<EquatableRuntimeTypeHandle>
     {
         internal RuntimeTypeHandle TypeHand;

         internal EquatableRuntimeTypeHandle(RuntimeTypeHandle typeHand)
         {
             TypeHand = typeHand;
         }

         public bool Equals(EquatableRuntimeTypeHandle other)
         {
             return TypeHand.Equals(other.TypeHand);
         }
     }
#endif // DEBUG
    }
}
