// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.IL.Stubs;
using Internal.TypeSystem.Interop;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// This class manages and caches  interop state information
    /// </summary>
    public sealed class InteropStateManager
    {
        private readonly ModuleDesc _generatedAssembly;
        private readonly NativeStructTypeHashtable _nativeStructHashtable;
        private readonly StructMarshallingThunkHashTable _structMarshallingThunkHashtable;


        public InteropStateManager(ModuleDesc generatedAssembly)
        {
            _generatedAssembly = generatedAssembly;
            _structMarshallingThunkHashtable = new StructMarshallingThunkHashTable(this, _generatedAssembly.GetGlobalModuleType());
            _nativeStructHashtable = new NativeStructTypeHashtable(this, _generatedAssembly);
        }

        //
        //  Struct Marshalling 
        //  To support struct marshalling compiler needs to generate a native type which
        //  imitates the original struct being passed to managed side with corresponding 
        //  fields of marshalled types. Additionally it needs to generate three thunks
        //      1. Managed to Native Thunk: For forward marshalling
        //      2. Native to Managed Thunk: For reverse marshalling
        //      3. Cleanup Thunk: for cleaning up any allocated resources
        //
        /// <summary>
        /// Generates a Native struct type which imitates the managed struct
        /// </summary>
        internal NativeStructType GetStructMarshallingNativeType(TypeDesc managedType)
        {
            if (managedType is ByRefType)
            {
                managedType = managedType.GetParameterType();
            }

            Debug.Assert(managedType is MetadataType);

            return _nativeStructHashtable.GetOrCreateValue((MetadataType)managedType);
        }

        /// <summary>
        ///  Generates a thunk to marshal the fields of the struct from managed to native
        /// </summary>
        internal MethodDesc GetStructMarshallingManagedToNativeThunk(TypeDesc managedType)
        {
            if (managedType is ByRefType)
            {
                managedType = managedType.GetParameterType();
            }

            Debug.Assert(managedType is MetadataType);

            var methodKey = new StructMarshallingThunkKey((MetadataType)managedType, StructMarshallingThunkType.ManagedToNative);
            return _structMarshallingThunkHashtable.GetOrCreateValue(methodKey);
        }

        /// <summary>
        ///  Generates a thunk to marshal the fields of the struct from native to managed
        /// </summary>
        internal MethodDesc GetStructMarshallingNativeToManagedThunk(TypeDesc managedType)
        {
            if (managedType is ByRefType)
            {
                managedType = managedType.GetParameterType();
            }

            Debug.Assert(managedType is MetadataType);


            var methodKey = new StructMarshallingThunkKey((MetadataType)managedType, StructMarshallingThunkType.NativeToManage);
            return _structMarshallingThunkHashtable.GetOrCreateValue(methodKey);
        }

        /// <summary>
        ///  Generates a thunk to cleanup any allocated resources during marshalling
        /// </summary>
        internal MethodDesc GetStructMarshallingCleanupThunk(TypeDesc managedType)
        {
            if (managedType is ByRefType)
            {
                managedType = ((ByRefType)managedType).GetParameterType();
            }

            Debug.Assert(managedType is MetadataType);


            var methodKey = new StructMarshallingThunkKey((MetadataType)managedType, StructMarshallingThunkType.Cleanup);
            return _structMarshallingThunkHashtable.GetOrCreateValue(methodKey);
        }


        private class NativeStructTypeHashtable : LockFreeReaderHashtable<MetadataType, NativeStructType>
        {
            protected override int GetKeyHashCode(MetadataType key)
            {
                return key.GetHashCode();
            }

            protected override int GetValueHashCode(NativeStructType value)
            {
                return value.ManagedStructType.GetHashCode();
            }

            protected override bool CompareKeyToValue(MetadataType key, NativeStructType value)
            {
                return Object.ReferenceEquals(key, value.ManagedStructType);
            }

            protected override bool CompareValueToValue(NativeStructType value1, NativeStructType value2)
            {
                return Object.ReferenceEquals(value1.ManagedStructType, value2.ManagedStructType);
            }

            protected override NativeStructType CreateValueFromKey(MetadataType key)
            {
                return new NativeStructType(_owningModule, key, _interopStateManager);
            }

            private readonly InteropStateManager _interopStateManager;
            private readonly ModuleDesc _owningModule;

            public NativeStructTypeHashtable(InteropStateManager interopStateManager, ModuleDesc owningModule)
            {
                _interopStateManager = interopStateManager;
                _owningModule = owningModule;
            }
        }

        private struct StructMarshallingThunkKey
        {
            public readonly MetadataType ManagedType;
            public readonly StructMarshallingThunkType ThunkType;

            public StructMarshallingThunkKey(MetadataType type, StructMarshallingThunkType thunkType)
            {
                ManagedType = type;
                ThunkType = thunkType;
            }
        }

        private class StructMarshallingThunkHashTable : LockFreeReaderHashtable<StructMarshallingThunkKey, StructMarshallingThunk>
        {
            protected override int GetKeyHashCode(StructMarshallingThunkKey key)
            {
                return key.ManagedType.GetHashCode() ^ (int)key.ThunkType;
            }

            protected override int GetValueHashCode(StructMarshallingThunk value)
            {
                return value.ManagedType.GetHashCode() ^ (int)value.ThunkType;
            }

            protected override bool CompareKeyToValue(StructMarshallingThunkKey key, StructMarshallingThunk value)
            {
                return Object.ReferenceEquals(key.ManagedType, value.ManagedType) &&
                        key.ThunkType == value.ThunkType;
            }

            protected override bool CompareValueToValue(StructMarshallingThunk value1, StructMarshallingThunk value2)
            {
                return Object.ReferenceEquals(value1.ManagedType, value2.ManagedType) &&
                        value1.ThunkType == value2.ThunkType;
            }

            protected override StructMarshallingThunk CreateValueFromKey(StructMarshallingThunkKey key)
            {
                return new StructMarshallingThunk(_owningType, key.ManagedType, key.ThunkType, _interopStateManager);
            }

            private readonly InteropStateManager _interopStateManager;
            private readonly TypeDesc _owningType;

            public StructMarshallingThunkHashTable(InteropStateManager interopStateManager, TypeDesc owningType)
            {
                _interopStateManager = interopStateManager;
                _owningType = owningType;
            }
        }
    }
}