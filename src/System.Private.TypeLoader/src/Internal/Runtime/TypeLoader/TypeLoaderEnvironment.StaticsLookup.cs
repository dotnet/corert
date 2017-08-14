// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using Internal.Runtime;
using Internal.Runtime.Augments;

using Internal.NativeFormat;

namespace Internal.Runtime.TypeLoader
{
    public sealed partial class TypeLoaderEnvironment
    {
        private const int DynamicTypeTlsOffsetFlag = unchecked((int)0x80000000);

        // To keep the synchronization simple, we execute all TLS registration/lookups under a global lock
        private Lock _threadStaticsLock = new Lock();

        // Counter to keep track of generated offsets for TLS cells of dynamic types;
        private int _maxTlsCells = 0;
        private LowLevelDictionary<RuntimeTypeHandle, uint> _dynamicGenericsThreadStatics = new LowLevelDictionary<RuntimeTypeHandle, uint>();
        private LowLevelDictionary<uint, int> _dynamicGenericsThreadStaticSizes = new LowLevelDictionary<uint, int>();

        // Various functions in static access need to create permanent pointers for use by thread static lookup.
        #region GC/Non-GC Statics
        /// <summary>
        /// Get a pointer to the nongc static field data of a type. This function works for dynamic
        /// types, reflectable types, and for all generic types
        /// </summary>
        public IntPtr TryGetNonGcStaticFieldDataDirect(RuntimeTypeHandle runtimeTypeHandle)
        {
            unsafe
            {
                EEType* typeAsEEType = runtimeTypeHandle.ToEETypePtr();
                // Non-generic, non-dynamic types need special handling.
                if (!typeAsEEType->IsDynamicType && !typeAsEEType->IsGeneric)
                {
                    if (typeAsEEType->HasCctor)
                    {
                        // The non-gc area for a type is immediately following its cctor context if it has one
                        IntPtr dataAddress = TryGetStaticClassConstructionContext(runtimeTypeHandle);
                        if (dataAddress != IntPtr.Zero)
                        {
                            return (IntPtr)(((byte*)dataAddress.ToPointer()) + sizeof(System.Runtime.CompilerServices.StaticClassConstructionContext));
                        }
                    }
                    else
                    {
                        // If the type does not have a Cctor context, search for the field on the type in the field map which has the lowest offset,
                        // yet has the the correct type of storage. 
                        IntPtr staticAddress;
                        if (TryGetStaticFieldBaseFromFieldAccessMap(runtimeTypeHandle, FieldAccessStaticDataKind.NonGC, out staticAddress))
                        {
                            return staticAddress;
                        }
                    }
                }
            }

            IntPtr nonGcStaticsAddress;
            IntPtr gcStaticsAddress;
            if (TryGetStaticsInfoForNamedType(runtimeTypeHandle, out nonGcStaticsAddress, out gcStaticsAddress))
            {
                return nonGcStaticsAddress;
            }

            // The indirected helper function can be used to find all dynamic types not found via 
            // TryGetStaticsInfoForNamedType as well as generics
            IntPtr ptrToStaticFieldData = TryGetNonGcStaticFieldData(runtimeTypeHandle);
            if (ptrToStaticFieldData == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            else
            {
                unsafe
                {
                    return *(IntPtr*)ptrToStaticFieldData;
                }
            }
        }

        /// <summary>
        /// Get a pointer to a pointer to the nongc static field data of a type. This function works for all generic types
        /// </summary>
        public IntPtr TryGetNonGcStaticFieldData(RuntimeTypeHandle runtimeTypeHandle)
        {
            unsafe
            {
                // Non-generic, non-dynamic static data is found via the FieldAccessMap
                EEType* typeAsEEType = runtimeTypeHandle.ToEETypePtr();
                // Non-generic, non-dynamic types need special handling.
                Debug.Assert(typeAsEEType->IsDynamicType || typeAsEEType->IsGeneric);
            }

            // Search hashtable for static entry
            ExternalReferencesTable staticInfoLookup;
            var parser = GetStaticInfo(runtimeTypeHandle, out staticInfoLookup);
            if (!parser.IsNull)
            {
                var index = parser.GetUnsignedForBagElementKind(BagElementKind.NonGcStaticData);

                return index.HasValue ? staticInfoLookup.GetIntPtrFromIndex(index.Value) : IntPtr.Zero;
            }

            // Not found in hashtable... must be a dynamically created type
            Debug.Assert(runtimeTypeHandle.IsDynamicType());
            unsafe
            {
                EEType* typeAsEEType = runtimeTypeHandle.ToEETypePtr();
                if ((typeAsEEType->RareFlags & EETypeRareFlags.IsDynamicTypeWithNonGcStatics) != 0)
                {
                    return typeAsEEType->DynamicNonGcStaticsData;
                }
            }

            // Type has no non-GC statics
            return IntPtr.Zero;
        }

        /// <summary>
        /// Get a pointer to the gc static field data of a type. This function works for dynamic
        /// types, reflectable types, and for all generic types
        /// </summary>
        public IntPtr TryGetGcStaticFieldDataDirect(RuntimeTypeHandle runtimeTypeHandle)
        {
            unsafe
            {
                // Non-generic, non-dynamic static data is found via the FieldAccessMap
                EEType* typeAsEEType = runtimeTypeHandle.ToEETypePtr();
                // Non-generic, non-dynamic types need special handling.
                if (!typeAsEEType->IsDynamicType && !typeAsEEType->IsGeneric)
                {
                    //search for the field on the type in the field map which has the lowest offset,
                    // yet has the the correct type of storage. 
                    IntPtr staticAddress;
                    if (TryGetStaticFieldBaseFromFieldAccessMap(runtimeTypeHandle, FieldAccessStaticDataKind.GC, out staticAddress))
                    {
                        return staticAddress;
                    }
                    else
                    {
                        return IntPtr.Zero;
                    }
                }
            }

            IntPtr nonGcStaticsAddress;
            IntPtr gcStaticsAddress;
            if (TryGetStaticsInfoForNamedType(runtimeTypeHandle, out nonGcStaticsAddress, out gcStaticsAddress))
            {
                return gcStaticsAddress;
            }

            // The indirected helper function can be used to find all dynamic types not found via 
            // TryGetStaticsInfoForNamedType as well as generics
            IntPtr ptrToStaticFieldData = TryGetGcStaticFieldData(runtimeTypeHandle);
            if (ptrToStaticFieldData == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            else
            {
                unsafe
                {
                    return *(IntPtr*)ptrToStaticFieldData;
                }
            }
        }

        /// <summary>
        /// Get a pointer to a pointer to the gc static field data of a type. This function works for all generic types
        /// </summary>
        public IntPtr TryGetGcStaticFieldData(RuntimeTypeHandle runtimeTypeHandle)
        {
            unsafe
            {
                // Non-generic, non-dynamic static data is found via the FieldAccessMap
                EEType* typeAsEEType = runtimeTypeHandle.ToEETypePtr();
                // Non-generic, non-dynamic types need special handling.
                Debug.Assert(typeAsEEType->IsDynamicType || typeAsEEType->IsGeneric);
            }

            // Search hashtable for static entry
            ExternalReferencesTable staticInfoLookup;
            var parser = GetStaticInfo(runtimeTypeHandle, out staticInfoLookup);
            if (!parser.IsNull)
            {
                var index = parser.GetUnsignedForBagElementKind(BagElementKind.GcStaticData);

                return index.HasValue ? staticInfoLookup.GetIntPtrFromIndex(index.Value) : IntPtr.Zero;
            }

            // Not found in hashtable... must be a dynamically created type
            Debug.Assert(runtimeTypeHandle.IsDynamicType());
            unsafe
            {
                EEType* typeAsEEType = runtimeTypeHandle.ToEETypePtr();
                if ((typeAsEEType->RareFlags & EETypeRareFlags.IsDynamicTypeWithGcStatics) != 0)
                {
                    return typeAsEEType->DynamicGcStaticsData;
                }
            }

            // Type has no GC statics
            return IntPtr.Zero;
        }
        #endregion


        #region Thread Statics
        public int TryGetThreadStaticsSizeForDynamicType(int index, out int numTlsCells)
        {
            Debug.Assert((index & DynamicTypeTlsOffsetFlag) == DynamicTypeTlsOffsetFlag);

            numTlsCells = _maxTlsCells;

            using (LockHolder.Hold(_threadStaticsLock))
            {
                int storageSize;
                if (_dynamicGenericsThreadStaticSizes.TryGetValue((uint)index, out storageSize))
                    return storageSize;
            }

            Debug.Assert(false);
            return 0;
        }

        public uint GetNextThreadStaticsOffsetValue()
        {
            // Highest bit of the TLS offset used as a flag to indicate that it's a special TLS offset of a dynamic type
            var result = 0x80000000 | (uint)_maxTlsCells;
            // Use checked arithmetics to ensure there aren't any overflows/truncations
            _maxTlsCells = checked(_maxTlsCells + 1);
            return result;
        }

        public void RegisterDynamicThreadStaticsInfo(RuntimeTypeHandle runtimeTypeHandle, uint offsetValue, int storageSize)
        {
            bool registered = false;
            Debug.Assert(offsetValue != 0 && storageSize > 0 && runtimeTypeHandle.IsDynamicType());

            _threadStaticsLock.Acquire();
            try
            {
                // Sanity check to make sure we do not register thread statics for the same type more than once
                uint temp;
                Debug.Assert(!_dynamicGenericsThreadStatics.TryGetValue(runtimeTypeHandle, out temp) && storageSize > 0);

                _dynamicGenericsThreadStatics.Add(runtimeTypeHandle, offsetValue);
                _dynamicGenericsThreadStaticSizes.Add(offsetValue, storageSize);
                registered = true;
            }
            finally
            {
                if (!registered)
                {
                    _dynamicGenericsThreadStatics.Remove(runtimeTypeHandle);
                    _dynamicGenericsThreadStaticSizes.Remove(offsetValue);
                }

                _threadStaticsLock.Release();
            }
        }

        public unsafe IntPtr TryGetTlsIndexDictionaryCellForType(RuntimeTypeHandle runtimeTypeHandle)
        {
            IntPtr result;
            if ((result = TryGetTlsIndexDictionaryCellForStaticType(runtimeTypeHandle)) == IntPtr.Zero)
                if ((result = TryGetTlsIndexDictionaryCellForDynamicType(runtimeTypeHandle)) == IntPtr.Zero)
                    return IntPtr.Zero;

            return result;
        }

        public IntPtr TryGetTlsOffsetDictionaryCellForType(RuntimeTypeHandle runtimeTypeHandle)
        {
            IntPtr result;
            if ((result = TryGetTlsOffsetDictionaryCellForStaticType(runtimeTypeHandle)) == IntPtr.Zero)
                if ((result = TryGetTlsOffsetDictionaryCellForDynamicType(runtimeTypeHandle)) == IntPtr.Zero)
                    return IntPtr.Zero;

            return result;
        }
        #endregion


        #region Privates
        // get the statics hash table, external references, and static info table for a module
        // TODO multi-file: consider whether we want to cache this info
        private unsafe bool GetStaticsInfoHashtable(NativeFormatModuleInfo module, out NativeHashtable staticsInfoHashtable, out ExternalReferencesTable externalReferencesLookup, out ExternalReferencesTable staticInfoLookup)
        {
            byte* pBlob;
            uint cbBlob;

            staticsInfoHashtable = default(NativeHashtable);
            externalReferencesLookup = default(ExternalReferencesTable);
            staticInfoLookup = default(ExternalReferencesTable);

            // Load statics info hashtable
            if (!module.TryFindBlob(ReflectionMapBlob.StaticsInfoHashtable, out pBlob, out cbBlob))
                return false;
            NativeReader reader = new NativeReader(pBlob, cbBlob);
            NativeParser parser = new NativeParser(reader, 0);

            if (!externalReferencesLookup.InitializeNativeReferences(module))
                return false;

            if (!staticInfoLookup.InitializeNativeStatics(module))
                return false;

            staticsInfoHashtable = new NativeHashtable(parser);

            return true;
        }

        private NativeParser GetStaticInfo(RuntimeTypeHandle instantiatedType, out ExternalReferencesTable staticsInfoLookup)
        {
            TypeManagerHandle moduleHandle = RuntimeAugments.GetModuleFromTypeHandle(instantiatedType);
            NativeFormatModuleInfo module = ModuleList.Instance.GetModuleInfoByHandle(moduleHandle);
            NativeHashtable staticsInfoHashtable;
            ExternalReferencesTable externalReferencesLookup;
            if (!GetStaticsInfoHashtable(module, out staticsInfoHashtable, out externalReferencesLookup, out staticsInfoLookup))
                return new NativeParser();

            int lookupHashcode = instantiatedType.GetHashCode();
            var enumerator = staticsInfoHashtable.Lookup(lookupHashcode);

            NativeParser entryParser;
            while (!(entryParser = enumerator.GetNext()).IsNull)
            {
                RuntimeTypeHandle parsedInstantiatedType = externalReferencesLookup.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());

                if (!parsedInstantiatedType.Equals(instantiatedType))
                    continue;

                return entryParser;
            }

            return new NativeParser();
        }

        private unsafe IntPtr TryCreateDictionaryCellWithValue(uint value)
        {
            return PermanentAllocatedMemoryBlobs.GetPointerToUInt(value);
        }


        public unsafe bool TryGetThreadStaticStartOffset(RuntimeTypeHandle runtimeTypeHandle, out int threadStaticsStartOffset)
        {
            threadStaticsStartOffset = -1;

            if (runtimeTypeHandle.IsDynamicType())
            {
                // Specific TLS storage is allocated for each dynamic type. There is no starting offset since it's not a 
                // TLS storage block shared by multiple types.
                threadStaticsStartOffset = 0;
                return true;
            }
            else
            {
                IntPtr ptrToTlsOffset = TryGetTlsOffsetDictionaryCellForStaticType(runtimeTypeHandle);
                if (ptrToTlsOffset == IntPtr.Zero)
                    return false;

                threadStaticsStartOffset = *(int*)ptrToTlsOffset;
                return true;
            }
        }

        private IntPtr TryGetTlsIndexDictionaryCellForStaticType(RuntimeTypeHandle runtimeTypeHandle)
        {
            // Search hashtable for static entry
            ExternalReferencesTable staticsInfoLookup;
            var parser = GetStaticInfo(runtimeTypeHandle, out staticsInfoLookup);
            if (!parser.IsNull)
            {
                var index = parser.GetUnsignedForBagElementKind(BagElementKind.ThreadStaticIndex);
                return index.HasValue ? staticsInfoLookup.GetIntPtrFromIndex(index.Value) : IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private IntPtr TryGetTlsOffsetDictionaryCellForStaticType(RuntimeTypeHandle runtimeTypeHandle)
        {
            // Search hashtable for static entry
            ExternalReferencesTable staticsInfoLookup;
            var parser = GetStaticInfo(runtimeTypeHandle, out staticsInfoLookup);
            if (!parser.IsNull)
            {
                var index = parser.GetUnsignedForBagElementKind(BagElementKind.ThreadStaticOffset);
                return index.HasValue ? staticsInfoLookup.GetIntPtrFromIndex(index.Value) : IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private IntPtr TryGetTlsIndexDictionaryCellForDynamicType(RuntimeTypeHandle runtimeTypeHandle)
        {
            // Use TLS index of 0 for dynamic types (the index won't really be used)
            Debug.Assert(runtimeTypeHandle.IsDynamicType());
            return TryCreateDictionaryCellWithValue(0);
        }

        private IntPtr TryGetTlsOffsetDictionaryCellForDynamicType(RuntimeTypeHandle runtimeTypeHandle)
        {
            Debug.Assert(runtimeTypeHandle.IsDynamicType());

            using (LockHolder.Hold(_threadStaticsLock))
            {
                uint offsetValue;
                if (_dynamicGenericsThreadStatics.TryGetValue(runtimeTypeHandle, out offsetValue))
                    return TryCreateDictionaryCellWithValue(offsetValue);
            }

            return IntPtr.Zero;
        }
        #endregion
    }
}
