// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Runtime.InteropServices;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;
using Internal.NativeFormat;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.TypeLoader
{
    //
    // Implements functionality to support ValueType.Equals and ValueType.GetHashCode
    //
    partial class TypeLoaderEnvironment
    {
        public bool ValueTypeEquals(ValueType thisObj, object thatObj)
        {
            RuntimeTypeHandle typeHandle = RuntimeAugments.GetRuntimeTypeHandleFromObjectReference(thisObj);

            Debug.Assert(typeHandle.Equals(RuntimeAugments.GetRuntimeTypeHandleFromObjectReference(thatObj)));
            Debug.Assert(thatObj != null);

            if (!RuntimeAugments.IsDynamicType(typeHandle))
            {
                return StaticValueTypeEquals(typeHandle, thisObj, thatObj);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public bool StaticValueTypeEquals(RuntimeTypeHandle typeHandle, ValueType thisObj, object thatObj)
        {
            NativeFormatModuleInfo module = ModuleList.GetModuleInfoByHandle(RuntimeAugments.GetModuleFromTypeHandle(typeHandle));
            
            NativeHashtable layoutHashTable;
            ExternalReferencesTable externalReferencesLookup;

            if (!GetHashtableFromBlob(module, ReflectionMapBlob.InstanceFieldLayoutHashtable, out layoutHashTable, out externalReferencesLookup))
                Environment.FailFast("Instance field layout not generated");

            ref byte thisRawData = ref RuntimeAugments.GetRawData(thisObj);
            ref byte thatRawData = ref RuntimeAugments.GetRawData(thatObj);

            var enumerator = layoutHashTable.Lookup(typeHandle.GetHashCode());

            //
            // First try to find instance field layout information in the hashtable.
            //
            NativeParser entryParser;
            while (!(entryParser = enumerator.GetNext()).IsNull)
            {
                RuntimeTypeHandle tentativeType = externalReferencesLookup.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!typeHandle.Equals(tentativeType))
                    continue;

                // Found the entry - for each field, compare the fields for equality
                int fieldCount = (int)entryParser.GetUnsigned();
                for (int i = 0; i < fieldCount; i++)
                {
                    RuntimeTypeHandle fieldType = externalReferencesLookup.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                    int fieldOffset = (int)entryParser.GetUnsigned();

                    // Fetch the value of the field on both types
                    object thisField = RuntimeAugments.RhBoxAny(ref Unsafe.Add(ref thisRawData, fieldOffset), fieldType);
                    object thatField = RuntimeAugments.RhBoxAny(ref Unsafe.Add(ref thatRawData, fieldOffset), fieldType);

                    if (thisField == null)
                    {
                        if (thatField != null)
                            return false;
                    }
                    else if (!thisField.Equals(thatField))
                    {
                        return false;
                    }
                }

                return true;
            }

            //
            // If the field layout information isn't present, we can memcompare
            //

            // Sanity check
            Debug.Assert(RuntimeAugments.GetGCDescSize(typeHandle) == 0);

            int valueTypeSize = typeHandle.GetValueTypeSize();
            for (int i = 0; i < valueTypeSize; i++)
                if (Unsafe.Add(ref thisRawData, i) != Unsafe.Add(ref thatRawData, i))
                    return false;

            return true;
        }

        public int ValueTypeGetHashCode(ValueType thisObj)
        {
            RuntimeTypeHandle typeHandle = RuntimeAugments.GetRuntimeTypeHandleFromObjectReference(thisObj);

            if (!RuntimeAugments.IsDynamicType(typeHandle))
            {
                return StaticValueTypeGetHashCode(typeHandle, thisObj);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private int StaticValueTypeGetHashCode(RuntimeTypeHandle typeHandle, ValueType thisObj)
        {
            // TODO: we should also use hashcode of the type

            NativeFormatModuleInfo module = ModuleList.GetModuleInfoByHandle(RuntimeAugments.GetModuleFromTypeHandle(typeHandle));

            NativeHashtable layoutHashTable;
            ExternalReferencesTable externalReferencesLookup;

            if (!GetHashtableFromBlob(module, ReflectionMapBlob.InstanceFieldLayoutHashtable, out layoutHashTable, out externalReferencesLookup))
                Environment.FailFast("Instance field layout not generated");

            ref byte thisRawData = ref RuntimeAugments.GetRawData(thisObj);

            var enumerator = layoutHashTable.Lookup(typeHandle.GetHashCode());

            //
            // First try to find instance field layout information in the hashtable.
            //
            NativeParser entryParser;
            while (!(entryParser = enumerator.GetNext()).IsNull)
            {
                RuntimeTypeHandle tentativeType = externalReferencesLookup.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!typeHandle.Equals(tentativeType))
                    continue;

                // Found the entry - find the first non-null field
                int fieldCount = (int)entryParser.GetUnsigned();
                for (int i = 0; i < fieldCount; i++)
                {
                    RuntimeTypeHandle fieldType = externalReferencesLookup.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                    int fieldOffset = (int)entryParser.GetUnsigned();
                    
                    // TODO: the CLR doesn't seem to be boxing and calling GetHashCode on valuetype fields, even if they
                    //       override GetHashCode? We might be able to get rid of the box here.
                    object fieldValue = RuntimeAugments.RhBoxAny(ref Unsafe.Add(ref thisRawData, fieldOffset), fieldType);
                    if (fieldValue != null)
                        return fieldValue.GetHashCode();
                }

                // Found no non-null fields
                return 0;
            }

            //
            // If the field layout information isn't present, we can hash the memory
            //
            return FastValueTypeGetHashCode(ref thisRawData, typeHandle);
        }

        private static int FastValueTypeGetHashCode(ref byte location, RuntimeTypeHandle type)
        {
            int size = RuntimeAugments.GetValueTypeSize(type);
            int hashCode = 0;

            for (int i = 0; i < size / 4; i++)
            {
                hashCode ^= Unsafe.As<byte, int>(ref Unsafe.Add(ref location, i * 4));
            }

            return hashCode;
        }
    }
}
