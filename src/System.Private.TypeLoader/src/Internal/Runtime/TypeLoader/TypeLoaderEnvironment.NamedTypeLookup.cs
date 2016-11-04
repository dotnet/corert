// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using Internal.Runtime;
using Internal.Runtime.Augments;

using Internal.Metadata.NativeFormat;
using Internal.NativeFormat;
using Internal.TypeSystem;

namespace Internal.Runtime.TypeLoader
{
    public sealed partial class TypeLoaderEnvironment
    {
        private class NamedTypeLookupResult
        {
            public int RuntimeTypeHandleHashcode;
            public RuntimeTypeHandle RuntimeTypeHandle;
            public MetadataReader MetadataReader;
            public TypeDefinitionHandle TypeDefinition;
            public IntPtr GcStaticFields;
            public IntPtr NonGcStaticFields;
            public volatile int VersionNumber;
        }

        private volatile int _namedTypeLookupLiveVersion = 0;

        private NamedTypeRuntimeTypeHandleToMetadataHashtable _runtimeTypeHandleToMetadataHashtable = new NamedTypeRuntimeTypeHandleToMetadataHashtable();

        public readonly static IntPtr NoStaticsData = (IntPtr)1;

        private class NamedTypeRuntimeTypeHandleToMetadataHashtable : LockFreeReaderHashtable<RuntimeTypeHandle, NamedTypeLookupResult>
        {
            protected unsafe override int GetKeyHashCode(RuntimeTypeHandle key)
            {
                return (int)key.ToEETypePtr()->HashCode;
            }
            protected override bool CompareKeyToValue(RuntimeTypeHandle key, NamedTypeLookupResult value)
            {
                return key.Equals(value.RuntimeTypeHandle);
            }

            protected unsafe override int GetValueHashCode(NamedTypeLookupResult value)
            {
                return value.RuntimeTypeHandleHashcode;
            }

            protected override bool CompareValueToValue(NamedTypeLookupResult value1, NamedTypeLookupResult value2)
            {
                if (value1.RuntimeTypeHandle.IsNull() || value2.RuntimeTypeHandle.IsNull())
                {
                    return value1.TypeDefinition.Equals(value2.TypeDefinition) &&
                           value1.MetadataReader.Equals(value2.MetadataReader);
                }
                return value1.RuntimeTypeHandle.Equals(value2.RuntimeTypeHandle);
            }

            protected override NamedTypeLookupResult CreateValueFromKey(RuntimeTypeHandle key)
            {
                int hashCode = GetKeyHashCode(key);

                // Iterate over all modules, starting with the module that defines the EEType
                foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(key)))
                {
                    NativeReader typeMapReader;
                    if (TryGetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.TypeMap, out typeMapReader))
                    {
                        NativeParser typeMapParser = new NativeParser(typeMapReader, 0);
                        NativeHashtable typeHashtable = new NativeHashtable(typeMapParser);

                        ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
                        externalReferences.InitializeCommonFixupsTable(moduleHandle);

                        var lookup = typeHashtable.Lookup(hashCode);
                        NativeParser entryParser;
                        while (!(entryParser = lookup.GetNext()).IsNull)
                        {
                            RuntimeTypeHandle foundType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                            if (foundType.Equals(key))
                            {
                                Handle entryMetadataHandle = entryParser.GetUnsigned().AsHandle();
                                if (entryMetadataHandle.HandleType == HandleType.TypeDefinition)
                                {
                                    MetadataReader metadataReader = ModuleList.Instance.GetMetadataReaderForModule(moduleHandle);
                                    return new NamedTypeLookupResult()
                                    {
                                        MetadataReader = metadataReader,
                                        TypeDefinition = entryMetadataHandle.ToTypeDefinitionHandle(metadataReader),
                                        RuntimeTypeHandle = key,
                                        RuntimeTypeHandleHashcode = hashCode
                                    };
                                }
                            }
                        }
                    }
                }

                return new NamedTypeLookupResult()
                {
                    RuntimeTypeHandle = key,
                    RuntimeTypeHandleHashcode = hashCode
                };
            }
        }

        private struct NamedTypeMetadataDescription
        {
            public MetadataReader MetadataReader;
            public TypeDefinitionHandle TypeDefinition;
        }

        private NamedTypeMetadataToRuntimeTypeHandleHashtable _metadataToRuntimeTypeHandleHashtable = new NamedTypeMetadataToRuntimeTypeHandleHashtable();

        private class NamedTypeMetadataToRuntimeTypeHandleHashtable : LockFreeReaderHashtable<NamedTypeMetadataDescription, NamedTypeLookupResult>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int _rotl(int value, int shift)
            {
                return (int)(((uint)value << shift) | ((uint)value >> (32 - shift)));
            }

            protected unsafe override int GetKeyHashCode(NamedTypeMetadataDescription key)
            {
                return key.TypeDefinition.GetHashCode() ^ _rotl(key.MetadataReader.GetHashCode(), 8);
            }
            protected override bool CompareKeyToValue(NamedTypeMetadataDescription key, NamedTypeLookupResult value)
            {
                return key.TypeDefinition.Equals(value.TypeDefinition) &&
                       key.MetadataReader.Equals(value.MetadataReader);
            }

            protected unsafe override int GetValueHashCode(NamedTypeLookupResult value)
            {
                return value.TypeDefinition.GetHashCode() ^ _rotl(value.MetadataReader.GetHashCode(), 8);
            }

            protected override bool CompareValueToValue(NamedTypeLookupResult value1, NamedTypeLookupResult value2)
            {
                return value1.TypeDefinition.Equals(value2.TypeDefinition) &&
                       value1.MetadataReader.Equals(value2.MetadataReader);
            }

            protected override NamedTypeLookupResult CreateValueFromKey(NamedTypeMetadataDescription key)
            {
                int hashCode = key.TypeDefinition.ComputeHashCode(key.MetadataReader);

                IntPtr moduleHandle = ModuleList.Instance.GetModuleForMetadataReader(key.MetadataReader);
                RuntimeTypeHandle foundRuntimeTypeHandle = default(RuntimeTypeHandle);

                NativeReader typeMapReader;
                if (TryGetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.TypeMap, out typeMapReader))
                {
                    NativeParser typeMapParser = new NativeParser(typeMapReader, 0);
                    NativeHashtable typeHashtable = new NativeHashtable(typeMapParser);

                    ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
                    externalReferences.InitializeCommonFixupsTable(moduleHandle);

                    var lookup = typeHashtable.Lookup(hashCode);
                    NativeParser entryParser;
                    while (!(entryParser = lookup.GetNext()).IsNull)
                    {
                        var foundTypeIndex = entryParser.GetUnsigned();
                        if (entryParser.GetUnsigned().AsHandle().Equals(key.TypeDefinition))
                        {
                            foundRuntimeTypeHandle = externalReferences.GetRuntimeTypeHandleFromIndex(foundTypeIndex);
                            break;
                        }
                    }
                }

                return new NamedTypeLookupResult()
                {
                    TypeDefinition = key.TypeDefinition,
                    MetadataReader = key.MetadataReader,
                    RuntimeTypeHandle = foundRuntimeTypeHandle,
                    VersionNumber = TypeLoaderEnvironment.Instance._namedTypeLookupLiveVersion
                };
            }
        }

        /// <summary>
        /// Return the metadata handle for a TypeDef if the pay-for-policy enabled this type as browsable. This is used to obtain name and other information for types
        /// obtained via typeof() or Object.GetType(). This can include generic types (Foo<>) (not to be confused with generic instances of Foo<>).
        ///
        /// Preconditions:
        ///    runtimeTypeHandle is a typedef (not a constructed type such as an array or generic instance.)
        /// </summary>
        /// <param name="runtimeTypeHandle">Runtime handle of the type in question</param>
        /// <param name="metadataReader">Metadata reader located for the type</param>
        /// <param name="typeDefHandle">TypeDef handle for the type</param>
        public unsafe bool TryGetMetadataForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeDefinitionHandle typeDefHandle)
        {
            NamedTypeLookupResult result = _runtimeTypeHandleToMetadataHashtable.GetOrCreateValue(runtimeTypeHandle);
            metadataReader = result.MetadataReader;
            typeDefHandle = result.TypeDefinition;
            return metadataReader != null;
        }

        /// <summary>
        /// Get the static addresses of a type if it is in the table
        /// </summary>
        /// <param name="runtimeTypeHandle">Runtime handle of the type in question</param>
        /// <param name="nonGcStaticsData">non-gc static field address</param>
        /// <param name="gcStaticsData">gc static field address</param>
        /// <returns>true if nonGcStaticsData/gcStaticsData are valid, false if not</returns>
        public unsafe bool TryGetStaticsInfoForNamedType(RuntimeTypeHandle runtimeTypeHandle, out IntPtr nonGcStaticsData, out IntPtr gcStaticsData)
        {
            NamedTypeLookupResult result;

            if (!_runtimeTypeHandleToMetadataHashtable.TryGetValue(runtimeTypeHandle, out result))
            {
                gcStaticsData = IntPtr.Zero;
                nonGcStaticsData = IntPtr.Zero;
                return false;
            }

            gcStaticsData = result.GcStaticFields;
            nonGcStaticsData = result.NonGcStaticFields;

            bool noResults = gcStaticsData == IntPtr.Zero || gcStaticsData == IntPtr.Zero;

            if (gcStaticsData == (IntPtr)1)
                gcStaticsData = IntPtr.Zero;

            if (nonGcStaticsData == (IntPtr)1)
                nonGcStaticsData = IntPtr.Zero;

            return result.MetadataReader != null && !noResults;
        }

        /// <summary>
        /// Return the RuntimeTypeHandle for the named type described in metadata. This is used to implement the Create and Invoke
        /// apis for types.
        ///
        /// Preconditions:
        ///    metadataReader + typeDefHandle  - a valid metadata reader + typeDefinitionHandle where "metadataReader" is one
        ///                                      of the metadata readers returned by ExecutionEnvironment.MetadataReaders.
        ///
        /// Note: Although this method has a "bool" return value like the other mapping table accessors, the Project N pay-for-play design 
        /// guarantees that any type enabled for metadata also has a RuntimeTypeHandle underneath.
        /// </summary>
        /// <param name="metadataReader">Metadata reader for module containing the type</param>
        /// <param name="typeDefHandle">TypeDef handle for the type to look up</param>
        /// <param name="runtimeTypeHandle">Runtime type handle (EEType) for the given type</param>
        public unsafe bool TryGetNamedTypeForMetadata(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, out RuntimeTypeHandle runtimeTypeHandle)
        {
            NamedTypeMetadataDescription description = new NamedTypeMetadataDescription()
            {
                MetadataReader = metadataReader,
                TypeDefinition = typeDefHandle
            };

            runtimeTypeHandle = default(RuntimeTypeHandle);
            NamedTypeLookupResult result = _metadataToRuntimeTypeHandleHashtable.GetOrCreateValue(description);

            if (result.VersionNumber <= _namedTypeLookupLiveVersion)
                runtimeTypeHandle = result.RuntimeTypeHandle;

            return !runtimeTypeHandle.IsNull();
        }

        public void RegisterNewNamedTypeRuntimeTypeHandle(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, RuntimeTypeHandle runtimeTypeHandle, IntPtr nonGcStaticFields, IntPtr gcStaticFields)
        {
            NamedTypeMetadataDescription description = new NamedTypeMetadataDescription()
            {
                MetadataReader = metadataReader,
                TypeDefinition = typeDefHandle
            };

            TypeLoaderLogger.WriteLine("Register new type with eetype = " + runtimeTypeHandle.ToIntPtr().LowLevelToString() + " nonGcStaticFields " + nonGcStaticFields.LowLevelToString() + " gcStaticFields " + gcStaticFields.LowLevelToString());
            NamedTypeLookupResult result = _metadataToRuntimeTypeHandleHashtable.GetOrCreateValue(description);

            result.VersionNumber = _namedTypeLookupLiveVersion + 1;
            result.RuntimeTypeHandle = runtimeTypeHandle;
            result.GcStaticFields = gcStaticFields;
            result.NonGcStaticFields = nonGcStaticFields;
            unsafe
            {
                result.RuntimeTypeHandleHashcode = (int)runtimeTypeHandle.ToEETypePtr()->HashCode;
            }

            NamedTypeLookupResult rthToMetadataResult = _runtimeTypeHandleToMetadataHashtable.AddOrGetExisting(result);

            if (!Object.ReferenceEquals(rthToMetadataResult, result))
            {
                rthToMetadataResult.TypeDefinition = typeDefHandle;
                rthToMetadataResult.MetadataReader = metadataReader;
                rthToMetadataResult.GcStaticFields = gcStaticFields;
                rthToMetadataResult.NonGcStaticFields = nonGcStaticFields;
            }
        }

        public void UnregisterNewNamedTypeRuntimeTypeHandle(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, RuntimeTypeHandle runtimeTypeHandle)
        {
            NamedTypeMetadataDescription description = new NamedTypeMetadataDescription()
            {
                MetadataReader = metadataReader,
                TypeDefinition = typeDefHandle
            };

            NamedTypeLookupResult metadataLookupResult;
            if (_metadataToRuntimeTypeHandleHashtable.TryGetValue(description, out metadataLookupResult))
            {
                metadataLookupResult.RuntimeTypeHandle = default(RuntimeTypeHandle);
                metadataLookupResult.VersionNumber = -1;
            }

            NamedTypeLookupResult runtimeTypeHandleResult;
            if (_runtimeTypeHandleToMetadataHashtable.TryGetValue(runtimeTypeHandle, out runtimeTypeHandleResult))
            {
                metadataLookupResult.GcStaticFields = IntPtr.Zero;
                metadataLookupResult.NonGcStaticFields = IntPtr.Zero;
                metadataLookupResult.RuntimeTypeHandle = default(RuntimeTypeHandle);
            }
        }

        public void FinishAddingNewNamedTypes()
        {
            _namedTypeLookupLiveVersion++;
            if (_namedTypeLookupLiveVersion == Int32.MaxValue)
                Environment.FailFast("Too many types loaded");
        }
    }
}
