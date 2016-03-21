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
        RuntimeTypeHandle [] m_supportedInterfaceList;   // List of supported interfaces type handle
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
            Dictionary<RuntimeTypeHandle, int> hashtable = new Dictionary<RuntimeTypeHandle, int>();
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
            return m_classData.Length;
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

        internal CCWTemplateData[] CCWTemplateData
        {
            get
            {
                return m_ccwTemplateData;
            }
        }

        internal unsafe string GetRuntimeClassName(int ccwTemplateIndex)
        {
            return m_ccwTemplateDataNameMap.GetString(ccwTemplateIndex);
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

        /// <summary>
        /// Return the list of IIDs
        /// Used by IInspectable.GetIIDs implementation for every CCW
        /// </summary>
        internal unsafe System.Collections.Generic.Internal.List<Guid> GetIIDs(int ccwTemplateDataIndex)
        {
            System.Collections.Generic.Internal.List<Guid> iids = new System.Collections.Generic.Internal.List<Guid>();

            // Every CCW implements ICPP
            iids.Add(Interop.COM.IID_ICustomPropertyProvider);

            GetIIDsImpl(ccwTemplateDataIndex, iids);

            return iids;
        }

        private void GetIIDsImpl(int index, System.Collections.Generic.Internal.List<Guid> iids)
        {
            //
            // Go through the parent template first - this is important to match desktop CLR behavior
            //
            int parentIndex = m_ccwTemplateData[index].ParentCCWTemplateIndex;
            RuntimeTypeHandle baseClassTypeHandle = m_ccwTemplateData[index].BaseType;

            if (parentIndex >=0)
            {
                GetIIDsImpl(parentIndex, iids);
            }
            else if (!baseClassTypeHandle.Equals(default(RuntimeTypeHandle)))
            {
                Debug.Assert(!baseClassTypeHandle.Equals(s_DependencyReductionTypeRemovedTypeHandle));
                CCWTemplateInfo template = McgModuleManager.GetCCWTemplateInfo(baseClassTypeHandle);
                Debug.Assert(!template.IsNull);
                template.ContainingModule.GetIIDsImpl(template.Index, iids);
            }

            //
            // After we've collected IIDs from base templates, insert IIDs implemented by this class only
            //
            int start = m_ccwTemplateData[index].SupportedInterfaceListBeginIndex;
            int end = start + m_ccwTemplateData[index].NumberOfSupportedInterface;
            for (int i = start; i < end; i++)
            {
                //
                // Retrieve the GUID and add it to the list
                // Skip ICustomPropertyProvider - we've already added it as the first item
                //
                RuntimeTypeHandle typeHandle = m_supportedInterfaceList[i];

                // TODO: if customer depends on the result of GetIIDs,
                // then fix this by keeping these handle alive
                if (IsInvalidTypeHandle(typeHandle))
                    continue;

                McgTypeInfo typeInfo = McgModuleManager.GetTypeInfoByHandle(typeHandle);
                Guid guid = typeInfo.ItfGuid;

                if (!InteropExtensions.GuidEquals(ref guid, ref Interop.COM.IID_ICustomPropertyProvider))
                {
                    //
                    // Avoid duplicated ones
                    //
                    // The duplicates comes from duplicated interface declarations in the metadata across
                    // parent/child classes, as well as the "injected" override interfaces for protected
                    // virtual methods (for example, if a derived class implements a IShapeInternal protected
                    // method, it only implements a protected method and doesn't implement IShapeInternal
                    // directly, and we have to "inject" it in MCG
                    //
                    // Doing a linear lookup is slow, but people typically never call GetIIDs perhaps except
                    // for debugging purposes (because the GUIDs returned back aren't exactly useful and you
                    // can't map it back to type), so I don't care about perf here that much
                    //
                    if (!iids.Contains(guid))
                        iids.Add(guid);
                }
            }
        }

        /// <summary>
        /// Get McgInterfaceData entry as an McgTypeInfo
        /// </summary>
        /// <param name="index"></param>
#if !RHTESTCL
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
#endif
        internal unsafe McgTypeInfo GetTypeInfoByIndex_Inline(int index)
        {
            Debug.Assert((index >= 0) && (index < m_interfaceData.Length));

            return new McgTypeInfo(index, this);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal unsafe McgTypeInfo GetTypeInfoByHandle(RuntimeTypeHandle typeHnd)
        {
            int tableIndex = InterfaceDataLookup(typeHnd);
            if (tableIndex >= 0)
            {
                return GetTypeInfoByIndex_Inline(tableIndex);
            }

            return McgTypeInfo.Null;
        }

        internal unsafe McgClassInfo GetClassInfoByHandle(RuntimeTypeHandle typeHnd)
        {
            int tableIndex = ClassDataLookup(typeHnd);
            if (tableIndex >= 0)
            {
                return GetClassInfoByIndex(tableIndex);
            }

            return McgClassInfo.Null;
        }

        /// <summary>
        /// Shared CCW marshalling T to native
        /// </summary>
        internal IntPtr ObjectToComInterface(object data, int marshalIndex)
        {
            RuntimeTypeHandle typeHandle = m_genericArgumentMarshalInfo[marshalIndex].ElementInterfaceType;
#if  ENABLE_WINRT
            if (typeHandle.Equals(typeof(object).TypeHandle))
            {
                return McgMarshal.ObjectToIInspectable(data);
            }

            if (typeHandle.Equals(typeof(string).TypeHandle))
            {
                return McgMarshal.StringToHString((string)data).handle;
            }
#endif
            return McgMarshal.ObjectToComInterface(data, McgModuleManager.GetTypeInfoByHandle(typeHandle));
        }

        /// <summary>
        /// Shared CCW marhsalling IVector<T> to IVectorView<T>
        /// </summary>
        internal IntPtr MarshalToVectorView(object data, int marshalIndex)
        {
            RuntimeTypeHandle typeHandle = m_genericArgumentMarshalInfo[marshalIndex].VectorViewType;
            if(IsInvalidTypeHandle(typeHandle))
            {
#if !RHTESTCL
                Environment.FailFast(String.Format("VectorView typehandle for CCW Thunk Index {0} discarded", marshalIndex));
#else
                Environment.FailFast("VectorView typehandle discarded");
#endif
            }
            return McgMarshal.ObjectToComInterface(data, McgModuleManager.GetTypeInfoByHandle(typeHandle));
        }

        /// <summary>
        /// Shared CCW marhsalling IEnumerable<T>> to IIterator<T>
        /// </summary>
        internal IntPtr MarshalToIterator(object data, int marshalIndex)
        {
            RuntimeTypeHandle typeHandle = m_genericArgumentMarshalInfo[marshalIndex].IteratorType;
            if (IsInvalidTypeHandle(typeHandle))
            {
#if !RHTESTCL
                Environment.FailFast(String.Format("Iterator typehandle for CCW Thunk Index {0} discarded", marshalIndex));
#else
                Environment.FailFast("Iterator typehandle discarded");
#endif
            }
            return McgMarshal.ObjectToComInterface(data, McgModuleManager.GetTypeInfoByHandle(m_genericArgumentMarshalInfo[marshalIndex].IteratorType));
        }

        /// <summary>
        /// Shared CCW marhsalling native to IAsyncOperation<T>
        /// </summary>
        internal object MarshalToAsyncOperation(IntPtr data, int marshalIndex)
        {
            RuntimeTypeHandle typeHandle = m_genericArgumentMarshalInfo[marshalIndex].AsyncOperationType;
            if (IsInvalidTypeHandle(typeHandle))
            {
#if !RHTESTCL
                Environment.FailFast(String.Format("AsyncOperation typehandle for CCW Thunk Index {0} discarded", marshalIndex));
#else
                Environment.FailFast("AsyncOperation typehandle discarded");
#endif
            }
            return ComInterfaceToObject(data, m_genericArgumentMarshalInfo[marshalIndex].AsyncOperationType);
        }

        internal object ComInterfaceToObject(System.IntPtr pComItf, RuntimeTypeHandle interfaceType)
        {
            return ComInterfaceToObject(pComItf, interfaceType, /* classIndexInSignature */ default(RuntimeTypeHandle));
        }

        internal object ComInterfaceToObject(System.IntPtr pComItf, McgTypeInfo interfaceTypeInfo)
        {
            return ComInterfaceToObject(pComItf, interfaceTypeInfo, McgClassInfo.Null);
        }

        internal object ComInterfaceToObject(System.IntPtr pComItf, RuntimeTypeHandle interfaceType,
                                           RuntimeTypeHandle classTypeInSignature)
        {
#if  ENABLE_WINRT
            if (interfaceType.Equals(typeof(string).TypeHandle))
            {
                return McgMarshal.HStringToString(pComItf);
            }
#endif

            // Current Shared CCW scenario is only for generic WinRT Interface
            // in WinRT, "typeof(object)" is IInspectable
            McgTypeInfo typeInfo;
            if (interfaceType.Equals(typeof(object).TypeHandle))
            {
                typeInfo = McgModuleManager.IInspectable;
            }
            else
            {
                typeInfo = GetTypeInfoByHandle(interfaceType);
            }

            return McgMarshal.ComInterfaceToObject(
                pComItf,
                typeInfo,
                (classTypeInSignature.Equals(default(RuntimeTypeHandle)))
                    ? McgClassInfo.Null
                    : GetClassInfoByHandle(classTypeInSignature)
            );
        }

        /// <summary>
        /// Lookup existing CCW/RCW, or create a new RCW for a type in this module
        /// </summary>
        /// <param name="pComItf">GetRuntimeClassName on the interface is used to find class name</param>
        /// <param name="interfaceTypeInfo">
        /// The type Info of the native interface. If we are marshalling a class (as specified in the
        /// functino signature), this would be the default interface of the class
        /// </param>
        /// <param name="classInfoInSignature">
        /// The class Info of the class type as specified in the signature
        /// </param>
        private object ComInterfaceToObject(System.IntPtr pComItf, McgTypeInfo interfaceTypeInfo, McgClassInfo classInfoInSignature)
        {
#if  ENABLE_WINRT
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

        /// <summary>
        /// Shared CCW marshalling to managed, use class form for sealed winrt class
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal object MarshalToObject(IntPtr data, int marshalIndex)
        {
            RuntimeTypeHandle classTypeHandle = m_genericArgumentMarshalInfo[marshalIndex].ElementClassType;
            if (!classTypeHandle.Equals(default(RuntimeTypeHandle))) // for sealed winrt class
            {
                if (classTypeHandle.Equals(s_DependencyReductionTypeRemovedTypeHandle))
                {
#if !RHTESTCL
                    Environment.FailFast(String.Format("ElementClassType typehandle for CCW Thunk Index {0} discarded", marshalIndex));
#else
                    Environment.FailFast("ElementClassType typehandle discarded");
#endif
                }

                return ComInterfaceToObject(
                    data,
                    m_genericArgumentMarshalInfo[marshalIndex].ElementInterfaceType,
                    classTypeHandle);
            }
            else
            {
                return ComInterfaceToObject(data, m_genericArgumentMarshalInfo[marshalIndex].ElementInterfaceType);
            }
        }

        /// <summary>
        /// Return sizeof(T) where T is blittable struct
        /// </summary>
        internal int GetByteSize(int marshalIndex)
        {
            Debug.Assert(m_genericArgumentMarshalInfo[marshalIndex].ElementSize > 0);
            return (int)m_genericArgumentMarshalInfo[marshalIndex].ElementSize;
        }

        /// <summary>
        /// Given a GUID, retrieve the corresponding type info
        /// @TODO: we should switch everything to McgTypeInfo instead of relying on Guid
        /// </summary>
        unsafe internal McgTypeInfo GetTypeInfo(ref Guid guid)
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
                        return GetTypeInfoByIndex_Inline(slot);
                    }
                }
            }

            return McgTypeInfo.Null;
        }

        /// <summary>
        /// Given a RuntimeTypeHandle, return the corresonding McgTypeInfo in InterfaceData
        /// NOTE: This method only search InterfaceData.
        /// NOTE: Don't call this method directory--call McgModuleManager.GetTypeInfoFromTypeHandle instead
        /// </summary>
        internal unsafe McgTypeInfo GetTypeInfoFromTypeHandleInInterfaceData(RuntimeTypeHandle typeHandle)
        {
            int slot = InterfaceDataLookup(typeHandle);

            if (slot >= 0)
            {
                return GetTypeInfoByIndex_Inline(slot);
            }

            return McgTypeInfo.Null;
        }

        /// <summary>
        /// Given a RuntimeTypeHandle, return the corresonding McgTypeInfo in CollectionData
        /// This method can also return secondaryTypeInfo in case the returned mcgTypeInfo query fails at runtime.
        /// This is done for ICollection<KeyValuePair<>> or IReadOnlyCollection<KeyValuePair<>>,
        /// where the returned and secondaryTypeInfo's are IDictionary and IList<keyValuePair<>> or
        /// IReadOnlyDictionary and IReadOnlyList<KeyValuePair<>> respectively in cases where we can't determine the
        /// mcgTypeInfo statically.
        /// </summary>
        /// <param name="typeHandle"></param>
        /// <param name="secondaryTypeInfo"></param>
        /// <returns></returns>
        internal McgTypeInfo GetTypeInfoFromTypeHandleInCollectionData(RuntimeTypeHandle typeHandle, out McgTypeInfo secondaryTypeInfo)
        {
            secondaryTypeInfo = McgTypeInfo.Null;

            // Loop over our I[ReadOnly]Collection<T1,T2> instantiations to find the type infos for
            // I[ReadOnly]List<KeyValuePair<T1,T2>> and I[ReadOnly]Dictionary<T1,T2>
            //
            // Note that only one of IList/IDictionary may be present.
            if (m_collectionData != null)
            {
                int slot = CollectionDataLookup(typeHandle);

                if (slot >= 0)
                {
                    RuntimeTypeHandle secondTypeHandle = m_collectionData[slot].SecondType;
                    if (!IsInvalidTypeHandle(secondTypeHandle))
                        secondaryTypeInfo = McgModuleManager.GetTypeInfoByHandle(secondTypeHandle);

                    return McgModuleManager.GetTypeInfoByHandle(m_collectionData[slot].FirstType);
                }
            }

            return McgTypeInfo.Null;
        }

        /// <summary>
        /// Can the target object be casted to the type?
        /// </summary>
        internal bool SupportsInterface(object obj, McgTypeInfo typeInfo)
        {
            return InteropExtensions.IsInstanceOfInterface(obj, typeInfo.InterfaceType);
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
                isWinRT = GetTypeInfoByIndex_Inline(slot).IsIInspectableOrDelegate;

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
                    isWinRT = GetTypeInfoByIndex_Inline(i).IsIInspectableOrDelegate;

                    return InteropExtensions.GetTypeFromHandle(GetTypeInfoByIndex_Inline(i).ItfType);
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
        /// Checks whether the CCW supports specified interface
        /// It could be either a GUID (in the case of a QI) or a TypeInfo (when marshaller asks for an
        /// interface explicitly
        /// NOTE: This support variances by having MCG injecting variant interfaces into the list of
        /// supported interfaces
        /// </summary>
        /// <param name="ccwTemplateIndex">The index of the CCWTemplateData in the module</param>
        /// <param name="guid">This value is always present</param>
        /// <param name="typeInfo">
        /// Can be null if in QI scenarios and it'll be updated to be the found typeInfo if we've found a
        /// match (despite whether it is supported or rejected).
        /// If not null, it'll be used to match against the list of interfaces in the template
        /// </param>
        internal unsafe InterfaceCheckResult SupportsInterface(int ccwTemplateIndex, ref Guid guid, ref McgTypeInfo typeInfo)
        {
            //
            // If this is a CCW template for a WinRT type, this means we've come to a base WinRT type of
            // a managed class, and we should fail at this point. Otherwise we'll be responding QIs to
            // interfaces not implemented in managed code
            //
            if (m_ccwTemplateData[ccwTemplateIndex].IsWinRTType)
                return InterfaceCheckResult.Rejected;

            //
            // Walk the interface list, looking for a matching interface
            //
            int begin = m_ccwTemplateData[ccwTemplateIndex].SupportedInterfaceListBeginIndex;
            int end = begin + m_ccwTemplateData[ccwTemplateIndex].NumberOfSupportedInterface;
            for (int index = begin; index < end; index++)
            {
                RuntimeTypeHandle currentInterfaceTypeHandle = m_supportedInterfaceList[index];
                Debug.Assert(!IsInvalidTypeHandle(currentInterfaceTypeHandle));
                McgTypeInfo currentInterfaceInfo = McgModuleManager.GetTypeInfoByHandle(currentInterfaceTypeHandle);

                bool match = false;
                if (typeInfo.IsNull)
                {
                    Guid intfGuid = currentInterfaceInfo.ItfGuid;
                    match = InteropExtensions.GuidEquals(ref intfGuid, ref guid);
                }
                else
                    match = (typeInfo.InterfaceType.Equals(currentInterfaceTypeHandle));

                if (match)
                {
                    // we found out a match using Guid / TypeInfo
                    if (typeInfo.IsNull)
                        typeInfo = currentInterfaceInfo;

                    return InterfaceCheckResult.Supported;
                }
            }

            //
            // Walk the parent too (if it is a WinRT type we'll stop)
            // Field ParentCCWTemplateIndex >=0 means that its baseclass is in the same module
            // Field BaseType != default(RuntimeTypeHandle) means that its baseclass isn't in the same module
            int parentIndex = m_ccwTemplateData[ccwTemplateIndex].ParentCCWTemplateIndex;
            RuntimeTypeHandle baseTypeHandle = m_ccwTemplateData[ccwTemplateIndex].BaseType;

            if (parentIndex >= 0)
            {
                return SupportsInterface(parentIndex, ref guid, ref typeInfo);
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

                CCWTemplateInfo baseCCWTemplateData = McgModuleManager.GetCCWTemplateInfo(baseTypeHandle);
                Debug.Assert(!baseCCWTemplateData.IsNull);
                return baseCCWTemplateData.ContainingModule.SupportsInterface(baseCCWTemplateData.Index, ref guid, ref typeInfo);

            }

            typeInfo = McgTypeInfo.Null;

            return InterfaceCheckResult.NotFound;
        }

        /// <summary>
        /// Search this module's m_classData table for information on the requested type.
        /// This function returns true if and only if it is able to locate a non-null McgClassInfo
        /// record for the requested type.
        /// </summary>
        internal bool TryGetClassInfoFromClassDataTable(string name, out McgClassInfo classInfo)
        {
            classInfo = McgClassInfo.Null;

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
                    // be immediately used to compute the McgClassInfo that best describes the type.
                    //
                    classInfo = ComputeClassInfoForClassDataTableRow(i);
                }
            }

            return !classInfo.IsNull;
        }

        /// <summary>
        /// Search this module's m_additionalClassData table for information on the requested type.
        /// This function returns true if and only if it is able to locate a non-null McgClassInfo
        /// record for the requested type.
        /// </summary>
        internal bool TryGetClassInfoFromAdditionalClassDataTable(string name, out McgClassInfo classInfo)
        {
            classInfo = McgClassInfo.Null;

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
                        // to compute the McgClassInfo that best describes the type.
                        //
                        classInfo = ComputeClassInfoForClassDataTableRow(classDataIndex);
                    }
                    else
                    {
                        //
                        // This module's m_additionalClassData table lists a RuntimeTypeHandle which describes
                        // the nearest available base class of the requested type. If this nearest base class was
                        // not reduced away, then use it to locate McgClassInfo describing this "next best" type.
                        //
                        if (!typeHandle.Equals(s_DependencyReductionTypeRemovedTypeHandle))
                        {
                            classInfo = McgModuleManager.GetClassInfoFromTypeHandle(typeHandle);
                            Debug.Assert(!classInfo.IsNull);
                        }
                    }
                }
            }

            return !classInfo.IsNull;
        }

        /// <summary>
        /// This function computes an McgClassInfo instance that represents the best possible
        /// description of the type associated with the requested row in the m_classData table.
        ///
        /// McgClassInfo can generally be attached directly to the requested row. That said, in the
        /// case where the associated type was removed by the dependency reducer, it is necessary to
        /// walk the base class chain to find the nearest base type that is actually present at
        /// runtime.
        ///
        /// Note: This function can return McgClassInfo.Null if it determines that all information
        /// associated with the supplied row has been reduced away.
        /// </summary>
        private McgClassInfo ComputeClassInfoForClassDataTableRow(int index)
        {
            Debug.Assert((index >= 0) && (index < m_classData.Length));

            if (!m_classData[index].ClassType.Equals(s_DependencyReductionTypeRemovedTypeHandle))
            {
                //
                // The current row lists either an non-reduced exact type or the nearest non-reduced base
                // type and is therefore the best possible description of the requested row.
                //
                return new McgClassInfo(index, this);
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
                    // to compute its associated McgClassInfo.
                    //
                    return ComputeClassInfoForClassDataTableRow(baseClassIndex);
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
                        return McgClassInfo.Null;
                    }
                    else
                    {
                        //
                        // The base class is described by type handle (probably because it is associated with a
                        // different McgModule and therefore isn't listed in the current m_classData table). Use
                        // the type handle to compute an McgClassInfo which describes the base class.
                        //
                        McgClassInfo classInfo = McgModuleManager.GetClassInfoFromTypeHandle(baseClassTypeHandle);
                        Debug.Assert(!classInfo.IsNull);
                        return classInfo;
                    }
                }
            }
        }

        internal bool TryGetInterfaceTypeInfoFromName(string name, out McgTypeInfo typeInfo)
        {
            if (m_interfaceData != null && m_interfaceNameMap != null)
            {
                int index = m_interfaceNameMap.FindString(name);
                if (index >= 0)
                {
                    typeInfo = GetTypeInfoByIndex_Inline(index);
                    return true;
                }
            }

            typeInfo = McgTypeInfo.Null;
            return false;
        }

        private unsafe McgClassInfo GetClassInfoByIndex(int index)
        {
            Debug.Assert(index >= 0 && index < m_classData.Length);

            return new McgClassInfo(index, this);
        }

        internal McgClassData GetClassDataByIndex(int index)
        {
            Debug.Assert(index >= 0 && index < m_classData.Length);

            return m_classData[index];
        }

        internal McgInterfaceData GetInterfaceDataByIndex(int index)
        {
            Debug.Assert(index >= 0 && index < m_interfaceData.Length);

            return m_interfaceData[index];
        }

        internal void SetCCW(int index, IntPtr value)
        {
            m_interfaceData[index].CcwVtable = value;
        }

        int m_boxingDataTypeSlot = -1; // Slot for System.Type in boxing data table

        /// <summary>
        /// Given a boxed value type, return a wrapper supports the IReference interface
        /// </summary>
        /// <param name="typeHandleOverride">
        /// You might want to specify how to box this. For example, any object[] derived array could
        /// potentially boxed as object[] if everything else fails
        /// </param>
        internal object BoxIfBoxable(object target, RuntimeTypeHandle typeHandleOverride)
        {
            if ((m_boxingData == null) || (m_boxingData.Length == 0))
            {
                return null;
            }

            if (m_boxingDataTypeSlot == -1)
            {
                m_boxingDataTypeSlot = BoxingDataLookup(typeof(System.Type).TypeHandle);
            }

            RuntimeTypeHandle expectedTypeHandle = typeHandleOverride;
            if (expectedTypeHandle.Equals(default(RuntimeTypeHandle)))
                expectedTypeHandle = target.GetTypeHandle();

            //
            // Is this the exact type that we want? (Don't use 'is' check - it won't work well for
            // arrays)
            int slot = BoxingDataLookup(expectedTypeHandle);

            // NOTE: For System.Type marshalling, use 'is' check instead, as the actual type would be
            // some random internal type from reflection
            //
            if ((slot < 0) && (target is Type))
            {
                slot = m_boxingDataTypeSlot;
            }

            if (slot >= 0)
            {
                return Box(target, slot);
            }

            return null;
        }

        internal object Box(object target, int boxingIndex)
        {
            if (!IsInvalidTypeHandle(m_boxingData[boxingIndex].CLRBoxingWrapperType))
            {
                //
                // IReference<T> / IReferenceArray<T> / IKeyValuePair<K, V>
                // All these scenarios require a managed wrapper
                //

                // Allocate the object
                object refImplType = InteropExtensions.RuntimeNewObject(m_boxingData[boxingIndex].CLRBoxingWrapperType);

                int type = m_boxingData[boxingIndex].PropertyType;

                if (type >= 0)
                {
                    Debug.Assert(refImplType is BoxedValue);

                    BoxedValue boxed = InteropExtensions.UncheckedCast<BoxedValue>(refImplType);

                    // Call ReferenceImpl<T>.Initialize(obj, type);
                    boxed.Initialize(target, type);
                }
                else
                {
                    Debug.Assert(refImplType is BoxedKeyValuePair);

                    BoxedKeyValuePair boxed = InteropExtensions.UncheckedCast<BoxedKeyValuePair>(refImplType);

                    // IKeyValuePair<,>,   call CLRIKeyValuePairImpl<K,V>.Initialize(object obj);
                    // IKeyValuePair<,>[], call CLRIKeyValuePairArrayImpl<K,V>.Initialize(object obj);
                    refImplType = boxed.Initialize(target);
                }

                return refImplType;
            }
            else
            {
                //
                // General boxing for projected types, such as System.Uri
                //
                return CalliIntrinsics.Call<object>(m_boxingData[boxingIndex].BoxingStub, target);
            }
        }

        private const int PropertyType_ArrayOffset = 1024;

        /// <summary>
        /// Unbox the WinRT boxed IReference<T>/IReferenceArray<T> and box it into Object so that managed
        /// code can unbox it later into the real T
        /// </summary>
        internal object UnboxIfBoxed(object target, string className)
        {
            if (m_boxingData == null)
            {
                return null;
            }

            Debug.Assert(!String.IsNullOrEmpty(className));
            //
            // Avoid searching for null/empty name. BoxingData has null name entries
            //
            int i = m_boxingDataNameMap.FindString(className);

            if (i >= 0)
            {
                //
                // Otherwise - call to our unboxing stub
                //
                return CallUnboxingStub(target, i);
            }

            return null;
        }

        private object CallUnboxingStub(object obj, int boxingIndex)
        {
            return CalliIntrinsics.Call<object>(m_boxingData[boxingIndex].UnboxingStub, obj);
        }

        internal object Unbox(object obj, int boxingIndex)
        {
            //
            // If it is our managed wrapper, unbox it
            //
            object unboxedObj = McgComHelpers.UnboxManagedWrapperIfBoxed(obj);
            if (unboxedObj != obj)
                return unboxedObj;

            //
            // Otherwise - call to our unboxing stub directly
            //
            return CallUnboxingStub(obj, boxingIndex);
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

        internal unsafe McgTypeInfo FindTypeInfo(Func<McgTypeInfo, bool> predecate)
        {
            for (int i = 0; i < m_interfaceData.Length; i++)
            {
                McgTypeInfo info = GetTypeInfoByIndex_Inline(i);

                if (predecate(info))
                    return info;
            }

            return McgTypeInfo.Null;
        }

#if RHTESTCL

        public object ComInterfaceToObjectType(System.IntPtr pComItf, RuntimeTypeHandle interfaceType)
        {
            return ComInterfaceToObject(pComItf, interfaceType, /* classTypeInSignature */ default(RuntimeTypeHandle));
        }

        public object ComInterfaceToObjectClass(System.IntPtr pComItf, RuntimeTypeHandle classTypeInSignature)
        {
            return ComInterfaceToObject(pComItf, default(RuntimeTypeHandle), classTypeInSignature);
        }

#endif

#if  ENABLE_WINRT
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

                *factoryOut = McgModuleManager.ObjectToComInterface(
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

        bool IsInvalidTypeHandle(RuntimeTypeHandle typeHandle)
        {
            if (typeHandle.Equals(typeof(DependencyReductionTypeRemoved).TypeHandle))
                return true;

            if (typeHandle.IsNull())
                return true;

            return false;
        }

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
    }

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
