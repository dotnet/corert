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
using Internal.Runtime.CompilerServices;

using Internal.Metadata.NativeFormat;
using Internal.NativeFormat;
using Internal.TypeSystem;
using Internal.TypeSystem.NativeFormat;

namespace Internal.Runtime.TypeLoader
{
    /// <summary>
    /// This structure represents metadata-based information used to construct method invokers.
    /// TypeLoaderEnvironment.TryGetMethodInvokeMetadata fills in this structure based on metadata lookup across
    /// all currently registered binary modules.
    /// </summary>
    public struct MethodInvokeMetadata
    {
        /// <summary>
        /// module containing the relevant metadata, null when not found
        /// </summary>
        public IntPtr MappingTableModule;

        /// <summary>
        /// Method entrypoint
        /// </summary>
        public IntPtr MethodEntryPoint;

        /// <summary>
        /// Raw method entrypoint
        /// </summary>
        public IntPtr RawMethodEntryPoint;

        /// <summary>
        /// Method dictionary for components
        /// </summary>
        public IntPtr DictionaryComponent;

        /// <summary>
        /// Dynamic invoke cookie
        /// </summary>
        public uint DynamicInvokeCookie;

        /// <summary>
        /// Invoke flags
        /// </summary>
        public InvokeTableFlags InvokeTableFlags;
    }

    public sealed partial class TypeLoaderEnvironment
    {
        /// <summary>
        /// Cross-module RVA has the high bit set which denotes that an additional indirection
        /// through the indirection cell is required to reach the final destination address.
        /// </summary>
        private const uint RVAIsIndirect = 0x80000000u;

        /// <summary>
        /// Convert RVA to runtime type handle with indirection handling.
        /// </summary>
        /// <param name="moduleHandle">Module containing the pointer</param>
        /// <param name="rva">Relative virtual address, high bit denotes additional indirection</param>
        internal static unsafe RuntimeTypeHandle RvaToRuntimeTypeHandle(IntPtr moduleHandle, uint rva)
        {
            if ((rva & RVAIsIndirect) != 0)
            {
                return RuntimeAugments.CreateRuntimeTypeHandle(*(IntPtr*)((byte*)moduleHandle.ToPointer() + (rva & ~RVAIsIndirect)));
            }
            return RuntimeAugments.CreateRuntimeTypeHandle((IntPtr)((byte*)moduleHandle.ToPointer() + rva));
        }

        /// <summary>
        /// Convert RVA in a given module to function pointer. This may involve an additional indirection
        /// through the indirection cell if the RVA has the DynamicInvokeMapEntry.IsImportMethodFlag set
        /// (when the actual function in question resides in a different binary module).
        /// </summary>
        /// <param name="moduleHandle">Module handle (= load address) containing the function or indirection cell</param>
        /// <param name="rva">
        /// Relative virtual address, the DynamicInvokeMapEntry.IsImportMethodFlag bit denotes additional indirection
        /// </param>
        private static unsafe IntPtr RvaToFunctionPointer(IntPtr moduleHandle, uint rva)
        {
            if ((rva & DynamicInvokeMapEntry.IsImportMethodFlag) == DynamicInvokeMapEntry.IsImportMethodFlag)
            {
                return *((IntPtr*)((byte*)moduleHandle + (rva & DynamicInvokeMapEntry.InstantiationDetailIndexMask)));
            }
            else
            {
                return (IntPtr)((byte*)moduleHandle + rva);
            }
        }

        /// <summary>
        /// Locate generic dictionary based on given module handle and RVA. If bit #31 is set
        /// in the RVA, the RVA is indirect i.e. an indirection is performed through the indirection cell
        /// specified by the RVA.
        /// </summary>
        /// <param name="moduleHandle">Handle (address) of module containing the RVA</param>
        /// <param name="rva">Relative virtual address of generic dictionary or indirection cell (when bit 31 is set)</param>
        /// <returns>Final address of generic dictionary</return>
        private static unsafe IntPtr RvaToGenericDictionary(IntPtr moduleHandle, uint rva)
        {
            // Generic dictionaries may be imported as well. As with types, this is indicated by the high bit set
            if ((rva & 0x80000000) != 0)
                return *((IntPtr*)((byte*)moduleHandle + (rva & ~0x80000000)));
            else
                return (IntPtr)((byte*)moduleHandle + rva);
        }

        /// <summary>
        /// Compare two arrays sequentially.
        /// </summary>
        /// <param name="seq1">First array to compare</param>
        /// <param name="seq2">Second array to compare</param>
        /// <returns>
        /// true = arrays have the same values and Equals holds for all pairs of elements
        /// with the same indices
        /// </returns>
        private static bool SequenceEqual<T>(T[] seq1, T[] seq2)
        {
            if (seq1.Length != seq2.Length)
                return false;
            for (int i = 0; i < seq1.Length; i++)
                if (!seq1[i].Equals(seq2[i]))
                    return false;
            return true;
        }

        /// <summary>
        /// Locate blob with given ID and create native reader on it.
        /// </summary>
        /// <param name="module">Address of module to search for the blob</param>
        /// <param name="blob">Blob ID within blob map for the module</param>
        /// <returns>Native reader for the blob (asserts and returns an empty native reader when not found)</returns>
        internal unsafe static NativeReader GetNativeReaderForBlob(IntPtr module, ReflectionMapBlob blob)
        {
            NativeReader reader;
            if (TryGetNativeReaderForBlob(module, blob, out reader))
            {
                return reader;
            }

            Debug.Assert(false);
            return default(NativeReader);
        }

        /// <summary>
        /// Return the metadata handle for a TypeRef if this type was referenced indirectly by other type that pay-for-play has denoted as browsable
        /// (for example, as part of a method signature.)
        ///
        /// This is only used in "debug" builds to provide better MissingMetadataException diagnostics. 
        ///
        /// Preconditions:
        ///    runtimeTypeHandle is a typedef (not a constructed type such as an array or generic instance.)
        /// </summary>
        /// <param name="runtimeTypeHandle">EEType of the type in question</param>
        /// <param name="metadataReader">Metadata reader for the type</param>
        /// <param name="typeRefHandle">Located TypeRef handle</param>
        public unsafe static bool TryGetTypeReferenceForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeReferenceHandle typeRefHandle)
        {
            int hashCode = runtimeTypeHandle.GetHashCode();

            // Iterate over all modules, starting with the module that defines the EEType
            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle)))
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
                        if (foundType.Equals(runtimeTypeHandle))
                        {
                            Handle entryMetadataHandle = entryParser.GetUnsigned().AsHandle();
                            if (entryMetadataHandle.HandleType == HandleType.TypeReference)
                            {
                                metadataReader = ModuleList.Instance.GetMetadataReaderForModule(moduleHandle);
                                typeRefHandle = entryMetadataHandle.ToTypeReferenceHandle(metadataReader);
                                return true;
                            }
                        }
                    }
                }
            }

            metadataReader = null;
            typeRefHandle = default(TypeReferenceHandle);

            return false;
        }

        /// <summary>
        /// Return the RuntimeTypeHandle for the named type referenced by another type that pay-for-play denotes as browsable (for example,
        /// in a member signature.) This will only find the typehandle if it is not defined in the current module, and is primarily used
        /// to find non-browsable types.
        ///
        /// This is used to ensure that we can produce a Type object if requested and that it match up with the analogous
        /// Type obtained via typeof().
        /// 
        ///
        /// Preconditions:
        ///    metadataReader + typeRefHandle  - a valid metadata reader + typeReferenceHandle where "metadataReader" is one
        ///                                      of the metadata readers returned by ExecutionEnvironment.MetadataReaders.
        ///
        /// Note: Although this method has a "bool" return value like the other mapping table accessors, the Project N pay-for-play design 
        /// guarantees that any type that has a metadata TypeReference to it also has a RuntimeTypeHandle underneath.
        /// </summary>
        /// <param name="metadataReader">Metadata reader for module containing the type reference</param>
        /// <param name="typeRefHandle">TypeRef handle to look up</param>
        /// <param name="runtimeTypeHandle">Resolved EEType for the type reference</param>
        public unsafe static bool TryGetNamedTypeForTypeReference(MetadataReader metadataReader, TypeReferenceHandle typeRefHandle, out RuntimeTypeHandle runtimeTypeHandle, bool searchAllModules = false)
        {
            int hashCode = typeRefHandle.ComputeHashCode(metadataReader);
            IntPtr typeRefModuleHandle = ModuleList.Instance.GetModuleForMetadataReader(metadataReader);
            return TryGetNamedTypeForTypeReference_Inner(metadataReader, typeRefModuleHandle, typeRefHandle, hashCode, typeRefModuleHandle, out runtimeTypeHandle);
        }

        /// <summary>
        /// Return the RuntimeTypeHandle for the named type referenced by another type that pay-for-play denotes as browsable (for example,
        /// in a member signature.) This lookup will attempt to resolve to an EEType in any module to cover situations where the type
        /// does not have a TypeDefinition (non-browsable type) as well as cases where it does.
        ///
        /// Preconditions:
        ///    metadataReader + typeRefHandle  - a valid metadata reader + typeReferenceHandle where "metadataReader" is one
        ///                                      of the metadata readers returned by ExecutionEnvironment.MetadataReaders.
        ///
        /// Note: Although this method has a "bool" return value like the other mapping table accessors, the Project N pay-for-play design 
        /// guarantees that any type that has a metadata TypeReference to it also has a RuntimeTypeHandle underneath.
        /// </summary>
        /// <param name="metadataReader">Metadata reader for module containing the type reference</param>
        /// <param name="typeRefHandle">TypeRef handle to look up</param>
        /// <param name="runtimeTypeHandle">Resolved EEType for the type reference</param>
        public unsafe static bool TryResolveNamedTypeForTypeReference(MetadataReader metadataReader, TypeReferenceHandle typeRefHandle, out RuntimeTypeHandle runtimeTypeHandle)
        {
            int hashCode = typeRefHandle.ComputeHashCode(metadataReader);
            IntPtr typeRefModuleHandle = ModuleList.Instance.GetModuleForMetadataReader(metadataReader);
            runtimeTypeHandle = default(RuntimeTypeHandle);

            foreach (IntPtr moduleHandle in ModuleList.Enumerate(typeRefModuleHandle))
            {
                if (TryGetNamedTypeForTypeReference_Inner(metadataReader, typeRefModuleHandle, typeRefHandle, hashCode, moduleHandle, out runtimeTypeHandle))
                    return true;
            }

            return false;
        }

        private unsafe static bool TryGetNamedTypeForTypeReference_Inner(MetadataReader metadataReader,
            IntPtr typeRefModuleHandle,
            TypeReferenceHandle typeRefHandle,
            int hashCode,
            IntPtr moduleHandle,
            out RuntimeTypeHandle runtimeTypeHandle)
        {
            Debug.Assert(typeRefModuleHandle == ModuleList.Instance.GetModuleForMetadataReader(metadataReader));

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
                    var handle = entryParser.GetUnsigned().AsHandle();

                    if (moduleHandle == typeRefModuleHandle)
                    {
                        if (handle.Equals(typeRefHandle))
                        {
                            runtimeTypeHandle = externalReferences.GetRuntimeTypeHandleFromIndex(foundTypeIndex);
                            return true;
                        }
                    }
                    else if (handle.HandleType == HandleType.TypeReference)
                    {
                        MetadataReader mrFoundHandle = ModuleList.Instance.GetMetadataReaderForModule(moduleHandle);
                        // We found a type reference handle in another module.. see if it matches
                        if (MetadataReaderHelpers.CompareTypeReferenceAcrossModules(typeRefHandle, metadataReader, handle.ToTypeReferenceHandle(mrFoundHandle), mrFoundHandle))
                        {
                            runtimeTypeHandle = externalReferences.GetRuntimeTypeHandleFromIndex(foundTypeIndex);
                            return true;
                        }
                    }
                    else if (handle.HandleType == HandleType.TypeDefinition)
                    {
                        // We found a type definition handle in another module. See if it matches
                        MetadataReader mrFoundHandle = ModuleList.Instance.GetMetadataReaderForModule(moduleHandle);
                        // We found a type definition handle in another module.. see if it matches
                        if (MetadataReaderHelpers.CompareTypeReferenceToDefinition(typeRefHandle, metadataReader, handle.ToTypeDefinitionHandle(mrFoundHandle), mrFoundHandle))
                        {
                            runtimeTypeHandle = externalReferences.GetRuntimeTypeHandleFromIndex(foundTypeIndex);
                            return true;
                        }
                    }
                }
            }

            runtimeTypeHandle = default(RuntimeTypeHandle);
            return false;
        }

        /// <summary>
        /// Given a RuntimeTypeHandle for any non-dynamic type E, return a RuntimeTypeHandle for type E[]
        /// if the pay for play policy denotes E[] as browsable. This is used to implement Array.CreateInstance().
        /// This is not equivalent to calling TryGetMultiDimTypeForElementType() with a rank of 1!
        ///
        /// Preconditions:
        ///     elementTypeHandle is a valid RuntimeTypeHandle.
        /// </summary>
        /// <param name="elementTypeHandle">EEType of the array element type</param>
        /// <param name="arrayTypeHandle">Resolved EEType of the array type</param>
        public unsafe static bool TryGetArrayTypeForNonDynamicElementType(RuntimeTypeHandle elementTypeHandle, out RuntimeTypeHandle arrayTypeHandle)
        {
            arrayTypeHandle = new RuntimeTypeHandle();

            int arrayHashcode = TypeHashingAlgorithms.ComputeArrayTypeHashCode(elementTypeHandle.GetHashCode(), -1);

            // Note: ReflectionMapBlob.ArrayMap may not exist in the module that contains the element type.
            // So we must enumerate all loaded modules in order to find ArrayMap and the array type for
            // the given element.
            foreach (IntPtr moduleHandle in ModuleList.Enumerate())
            {
                NativeReader arrayMapReader;
                if (TryGetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.ArrayMap, out arrayMapReader))
                {
                    NativeParser arrayMapParser = new NativeParser(arrayMapReader, 0);
                    NativeHashtable arrayHashtable = new NativeHashtable(arrayMapParser);

                    ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
                    externalReferences.InitializeCommonFixupsTable(moduleHandle);

                    var lookup = arrayHashtable.Lookup(arrayHashcode);
                    NativeParser entryParser;
                    while (!(entryParser = lookup.GetNext()).IsNull)
                    {
                        RuntimeTypeHandle foundArrayType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                        RuntimeTypeHandle foundArrayElementType = RuntimeAugments.GetRelatedParameterTypeHandle(foundArrayType);
                        if (foundArrayElementType.Equals(elementTypeHandle))
                        {
                            arrayTypeHandle = foundArrayType;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The array table only holds some of the precomputed array types, others may be found in the template type.
        /// As our system requires us to find the RuntimeTypeHandle of templates we must not be in a situation where we fail to find
        /// a RuntimeTypeHandle for a type which is actually in the template table. This function fixes that problem for arrays.
        /// </summary>
        /// <param name="arrayType"></param>
        /// <param name="arrayTypeHandle"></param>
        /// <returns></returns>
        public bool TryGetArrayTypeHandleForNonDynamicArrayTypeFromTemplateTable(ArrayType arrayType, out RuntimeTypeHandle arrayTypeHandle)
        {
            arrayTypeHandle = default(RuntimeTypeHandle);

            // Only SzArray types have templates.
            if (!arrayType.IsSzArray)
                return false;

            // If we can't find a RuntimeTypeHandle for the element type, we can't find the array in the template table.
            if (!arrayType.ParameterType.RetrieveRuntimeTypeHandleIfPossible())
                return false;

            unsafe
            {
                // If the elementType is a dynamic type it cannot exist in the template table.
                if (arrayType.ParameterType.RuntimeTypeHandle.ToEETypePtr()->IsDynamicType)
                    return false;
            }

            // Try to find out if the type exists as a template
            var canonForm = arrayType.ConvertToCanonForm(CanonicalFormKind.Specific);
            var hashCode = canonForm.GetHashCode();
            foreach (var moduleHandle in ModuleList.Enumerate())
            {
                ExternalReferencesTable externalFixupsTable;

                NativeHashtable typeTemplatesHashtable = LoadHashtable(moduleHandle, ReflectionMapBlob.TypeTemplateMap, out externalFixupsTable);

                if (typeTemplatesHashtable.IsNull)
                    continue;

                var enumerator = typeTemplatesHashtable.Lookup(hashCode);

                NativeParser entryParser;
                while (!(entryParser = enumerator.GetNext()).IsNull)
                {
                    RuntimeTypeHandle candidateTemplateTypeHandle = externalFixupsTable.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                    TypeDesc foundType = arrayType.Context.ResolveRuntimeTypeHandle(candidateTemplateTypeHandle);
                    if (foundType == arrayType)
                    {
                        arrayTypeHandle = candidateTemplateTypeHandle;

                        // This lookup in the template table is fairly slow, so if we find the array here, add it to the dynamic array cache, so that
                        // we can find it faster in the future.
                        if (arrayType.IsSzArray)
                            TypeSystemContext.GetArrayTypesCache(false, -1).AddOrGetExisting(arrayTypeHandle);
                        return true;
                    }
                }
            }

            return false;
        }

        // Lazy loadings of hashtables (load on-demand only)
        private unsafe NativeHashtable LoadHashtable(IntPtr moduleHandle, ReflectionMapBlob hashtableBlobId, out ExternalReferencesTable externalFixupsTable)
        {
            // Load the common fixups table
            externalFixupsTable = default(ExternalReferencesTable);
            if (!externalFixupsTable.InitializeCommonFixupsTable(moduleHandle))
                return default(NativeHashtable);

            // Load the hashtable
            byte* pBlob;
            uint cbBlob;
            if (!RuntimeAugments.FindBlob(moduleHandle, (int)hashtableBlobId, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
                return default(NativeHashtable);

            NativeReader reader = new NativeReader(pBlob, cbBlob);
            NativeParser parser = new NativeParser(reader, 0);
            return new NativeHashtable(parser);
        }

        /// <summary>
        /// Locate the static constructor context given the runtime type handle (EEType) for the type in question.
        /// </summary>
        /// <param name="typeHandle">EEtype of the type to look up</param>
        public unsafe static IntPtr TryGetStaticClassConstructionContext(RuntimeTypeHandle typeHandle)
        {
            if (RuntimeAugments.HasCctor(typeHandle))
            {
                if (RuntimeAugments.IsDynamicType(typeHandle))
                {
                    // For dynamic types, its always possible to get the non-gc static data section directly.
                    byte* ptr = (byte*)Instance.TryGetNonGcStaticFieldDataDirect(typeHandle);

                    // what we have now is the base address of the non-gc statics of the type
                    // what we need is the cctor context, which is just before that
                    ptr = ptr - sizeof(System.Runtime.CompilerServices.StaticClassConstructionContext);

                    return (IntPtr)ptr;
                }
                else
                {
                    // Non-dynamic types do not provide a way to directly get at the non-gc static region. 
                    // Use the CctorContextMap instead.

                    var moduleHandle = RuntimeAugments.GetModuleFromTypeHandle(typeHandle);
                    Debug.Assert(moduleHandle != IntPtr.Zero);

                    NativeReader typeMapReader;
                    if (TryGetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.CCtorContextMap, out typeMapReader))
                    {
                        NativeParser typeMapParser = new NativeParser(typeMapReader, 0);
                        NativeHashtable typeHashtable = new NativeHashtable(typeMapParser);

                        ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
                        externalReferences.InitializeCommonFixupsTable(moduleHandle);

                        var lookup = typeHashtable.Lookup(typeHandle.GetHashCode());
                        NativeParser entryParser;
                        while (!(entryParser = lookup.GetNext()).IsNull)
                        {
                            RuntimeTypeHandle foundType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                            if (foundType.Equals(typeHandle))
                            {
                                byte* pNonGcStaticBase = (byte*)externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());

                                // cctor context is located before the non-GC static base
                                return (IntPtr)(pNonGcStaticBase - sizeof(System.Runtime.CompilerServices.StaticClassConstructionContext));
                            }
                        }
                    }
                }

                // If the type has a lazy/deferred Cctor, the compiler must have missed emitting
                // a data structure if we reach this.
                Debug.Assert(false);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Construct the native reader for a given blob in a specified module.
        /// </summary>
        /// <param name="module">Containing binary module for the blob</param>
        /// <param name="blob">Blob ID to fetch from the module</param>
        /// <param name="reader">Native reader created for the module blob</param>
        /// <returns>true when the blob was found in the module, false when not</returns>
        private unsafe static bool TryGetNativeReaderForBlob(IntPtr module, ReflectionMapBlob blob, out NativeReader reader)
        {
            byte* pBlob;
            uint cbBlob;

            if (RuntimeAugments.FindBlob(module, (int)blob, (IntPtr)(&pBlob), (IntPtr)(&cbBlob)))
            {
                reader = new NativeReader(pBlob, cbBlob);
                return true;
            }

            reader = default(NativeReader);
            return false;
        }

        /// <summary>
        /// Look up the default constructor for a given type. Should not be called by code which has already initialized 
        /// the type system.
        /// </summary>
        /// <param name="type">TypeDesc for the type in question</param>
        /// <returns>Function pointer representing the constructor, IntPtr.Zero when not found</returns>
        internal IntPtr TryGetDefaultConstructorForType(TypeDesc type)
        {
            // Try to find the default constructor in metadata first
            IntPtr result = IntPtr.Zero;

#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
            result = TryGetDefaultConstructorForTypeViaMetadata_Inner(type);
#endif

            DefType defType = type as DefType;
            if ((result == IntPtr.Zero) && (defType != null))
            {
#if GENERICS_FORCE_USG
                // In force USG mode, prefer universal matches over canon specific matches.
                CanonicalFormKind firstCanonFormKind = CanonicalFormKind.Universal;
                CanonicalFormKind secondCanonFormKind = CanonicalFormKind.Specific;
#else
                CanonicalFormKind firstCanonFormKind = CanonicalFormKind.Specific;
                CanonicalFormKind secondCanonFormKind = CanonicalFormKind.Universal;
#endif

                CanonicallyEquivalentEntryLocator canonHelperSpecific = new CanonicallyEquivalentEntryLocator(defType, firstCanonFormKind);

                foreach (IntPtr module in ModuleList.Enumerate())
                {
                    result = TryGetDefaultConstructorForType_Inner(module, ref canonHelperSpecific);

                    if (result != IntPtr.Zero)
                        break;
                }

                if (result == IntPtr.Zero)
                {
                    CanonicallyEquivalentEntryLocator canonHelperUniversal = new CanonicallyEquivalentEntryLocator(defType, secondCanonFormKind);

                    foreach (IntPtr module in ModuleList.Enumerate())
                    {
                        result = TryGetDefaultConstructorForType_Inner(module, ref canonHelperUniversal);

                        if (result != IntPtr.Zero)
                            break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Look up the default constructor for a given type. Should not be called by code which has already initialized 
        /// the type system.
        /// </summary>
        /// <param name="runtimeTypeHandle">Type handle (EEType) for the type in question</param>
        /// <returns>Function pointer representing the constructor, IntPtr.Zero when not found</returns>
        public IntPtr TryGetDefaultConstructorForType(RuntimeTypeHandle runtimeTypeHandle)
        {
            CanonicallyEquivalentEntryLocator canonHelperSpecific = new CanonicallyEquivalentEntryLocator(runtimeTypeHandle, CanonicalFormKind.Specific);

            foreach (IntPtr module in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle)))
            {
                IntPtr result = TryGetDefaultConstructorForType_Inner(module, ref canonHelperSpecific);

                if (result != IntPtr.Zero)
                    return result;
            }

            CanonicallyEquivalentEntryLocator canonHelperUniversal = new CanonicallyEquivalentEntryLocator(runtimeTypeHandle, CanonicalFormKind.Universal);

            foreach (IntPtr module in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle)))
            {
                IntPtr result = TryGetDefaultConstructorForType_Inner(module, ref canonHelperUniversal);

                if (result != IntPtr.Zero)
                    return result;
            }

            // Try to find the default constructor in metadata last (this is costly as it requires spinning up a TypeLoaderContext, and 
            // currently also the _typeLoaderLock) (TODO when the _typeLoaderLock is no longer necessary to correctly use the type system
            // context, remove the use of the lock here.)
            using (LockHolder.Hold(_typeLoaderLock))
            {
                TypeSystemContext context = TypeSystemContextFactory.Create();

                TypeDesc type = context.ResolveRuntimeTypeHandle(runtimeTypeHandle);
                IntPtr result = TryGetDefaultConstructorForTypeViaMetadata_Inner(type);

                TypeSystemContextFactory.Recycle(context);

                if (result != IntPtr.Zero)
                    return result;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Lookup default constructor via the typesystem api surface and such
        /// </summary>
        private IntPtr TryGetDefaultConstructorForTypeViaMetadata_Inner(TypeDesc type)
        {
            IntPtr metadataLookupResult = IntPtr.Zero;

            DefType defType = type as DefType;

            if (defType != null)
            {
                if (!defType.IsValueType && defType is MetadataType)
                {
                    MethodDesc defaultConstructor = ((MetadataType)defType).GetDefaultConstructor();
                    if (defaultConstructor != null)
                    {
                        IntPtr dummyUnboxingStub;
                        TypeLoaderEnvironment.MethodAddressType foundAddressType;
                        TypeLoaderEnvironment.TryGetMethodAddressFromMethodDesc(defaultConstructor, out metadataLookupResult, out dummyUnboxingStub, out foundAddressType);
                    }
                }
            }

            return metadataLookupResult;
        }

        /// <summary>
        /// Attempt to locate the default type constructor in a given module.
        /// </summary>
        /// <param name="module">Module to search for the constructor</param>
        /// <param name="canonHelper">Canonically equivalent entry locator representing the type</param>
        /// <returns>Function pointer representing the constructor, IntPtr.Zero when not found</returns>
        internal unsafe IntPtr TryGetDefaultConstructorForType_Inner(IntPtr mappingTableModule, ref CanonicallyEquivalentEntryLocator canonHelper)
        {
            NativeReader invokeMapReader;
            if (TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.InvokeMap, out invokeMapReader))
            {
                NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
                NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);

                ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
                externalReferences.InitializeCommonFixupsTable(mappingTableModule);

                var lookup = invokeHashtable.Lookup(canonHelper.LookupHashCode);
                NativeParser entryParser;
                while (!(entryParser = lookup.GetNext()).IsNull)
                {
                    InvokeTableFlags entryFlags = (InvokeTableFlags)entryParser.GetUnsigned();
                    if ((entryFlags & InvokeTableFlags.IsDefaultConstructor) == 0)
                        continue;

                    entryParser.GetUnsigned(); // Skip method handle or the NameAndSig cookie

                    RuntimeTypeHandle entryType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                    if (!canonHelper.IsCanonicallyEquivalent(entryType))
                        continue;

                    return externalReferences.GetFunctionPointerFromIndex(entryParser.GetUnsigned());
                }
            }

            // If not found in the invoke map, try the default constructor map
            NativeReader defaultCtorMapReader;
            if (TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.DefaultConstructorMap, out defaultCtorMapReader))
            {
                NativeParser defaultCtorMapParser = new NativeParser(defaultCtorMapReader, 0);
                NativeHashtable defaultCtorHashtable = new NativeHashtable(defaultCtorMapParser);

                ExternalReferencesTable externalReferencesForDefaultCtorMap = default(ExternalReferencesTable);
                externalReferencesForDefaultCtorMap.InitializeCommonFixupsTable(mappingTableModule);
                var lookup = defaultCtorHashtable.Lookup(canonHelper.LookupHashCode);
                NativeParser defaultCtorParser;
                while (!(defaultCtorParser = lookup.GetNext()).IsNull)
                {
                    RuntimeTypeHandle entryType = externalReferencesForDefaultCtorMap.GetRuntimeTypeHandleFromIndex(defaultCtorParser.GetUnsigned());
                    if (!canonHelper.IsCanonicallyEquivalent(entryType))
                        continue;

                    return externalReferencesForDefaultCtorMap.GetFunctionPointerFromIndex(defaultCtorParser.GetUnsigned());
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// <summary>
        /// Try to resolve a member reference in all registered binary modules containing metadata.
        /// </summary>
        /// <param name="metadataReader">Metadata reader for the member reference</param>
        /// <param name="memberReferenceHandle">Member reference handle (method, field, property, event) to resolve</param>
        /// <param name="resolvedMetadataReader">Metadata reader for the resolved reference</param>
        /// <param name="resolvedContainingTypeHandle">Resolved runtime handle to the containing type</param>
        /// <param name="resolvedMemberHandle">Resolved handle to the referenced member</param>
        /// <returns>true when the lookup was successful; false when not</return>
        public static bool TryResolveMemberReference(
            MetadataReader metadataReader,
            MemberReferenceHandle memberReferenceHandle,
            out MetadataReader resolvedMetadataReader,
            out RuntimeTypeHandle resolvedContainingTypeHandle,
            out Handle resolvedMemberHandle)
        {
            // TODO
            resolvedMetadataReader = null;
            resolvedContainingTypeHandle = default(RuntimeTypeHandle);
            resolvedMemberHandle = default(Handle);
            return false;
        }

        /// <summary>
        /// Get the information necessary to resolve to metadata given a vtable slot and a type that defines that vtable slot
        /// </summary>
        /// <param name="context">type context to use.</param>
        /// <param name="type">Type that defines the vtable slot. (Derived types are not valid here)</param>
        /// <param name="vtableSlot">vtable slot index</param>
        /// <param name="methodNameAndSig">output name/sig of method</param>
        /// <returns></returns>
        public unsafe static bool TryGetMethodMethodNameAndSigFromVTableSlotForPregeneratedOrTemplateType(TypeSystemContext context, RuntimeTypeHandle type, int vtableSlot, out MethodNameAndSignature methodNameAndSig)
        {
            int logicalSlot = vtableSlot;
            EEType* ptrType = type.ToEETypePtr();
            RuntimeTypeHandle openOrNonGenericTypeDefinition = default(RuntimeTypeHandle);

            // Compute the logical slot by removing space reserved for generic dictionary pointers
            if (ptrType->IsInterface && ptrType->IsGeneric)
            {
                openOrNonGenericTypeDefinition = RuntimeAugments.GetGenericDefinition(type);
                logicalSlot--;
            }
            else
            {
                EEType* searchForSharedGenericTypesInParentHierarchy = ptrType;
                while (searchForSharedGenericTypesInParentHierarchy != null)
                {
                    // See if this type is shared generic. If so, adjust the slot by 1.
                    if (searchForSharedGenericTypesInParentHierarchy->IsGeneric)
                    {
                        RuntimeTypeHandle[] genericTypeArgs;
                        RuntimeTypeHandle genericDefinition = RuntimeAugments.GetGenericInstantiation(searchForSharedGenericTypesInParentHierarchy->ToRuntimeTypeHandle(),
                                                                                                      out genericTypeArgs);

                        if (Instance.ConversionToCanonFormIsAChange(genericTypeArgs, CanonicalFormKind.Specific))
                        {
                            // Shared generic types have a slot dedicated to holding the generic dictionary.
                            logicalSlot--;
                        }
                        if (openOrNonGenericTypeDefinition.IsNull())
                            openOrNonGenericTypeDefinition = genericDefinition;
                    }
                    else if (searchForSharedGenericTypesInParentHierarchy->IsArray)
                    {
                        // Arrays are like shared generics
                        RuntimeTypeHandle arrayElementTypeHandle = searchForSharedGenericTypesInParentHierarchy->RelatedParameterType->ToRuntimeTypeHandle();

                        TypeDesc arrayElementType = context.ResolveRuntimeTypeHandle(arrayElementTypeHandle);
                        TypeDesc canonFormOfArrayElementType = context.ConvertToCanon(arrayElementType, CanonicalFormKind.Specific);

                        if (canonFormOfArrayElementType != arrayElementType)
                        {
                            logicalSlot--;
                        }
                    }

                    // Walk to parent
                    searchForSharedGenericTypesInParentHierarchy = searchForSharedGenericTypesInParentHierarchy->BaseType;
                }
            }

            if (openOrNonGenericTypeDefinition.IsNull())
                openOrNonGenericTypeDefinition = type;

            IntPtr module = RuntimeAugments.GetModuleFromTypeHandle(openOrNonGenericTypeDefinition);

            return TryGetMethodNameAndSigFromVirtualResolveData(module, openOrNonGenericTypeDefinition, logicalSlot, out methodNameAndSig);
        }

        public struct VirtualResolveDataResult
        {
            public RuntimeTypeHandle DeclaringInvokeType;
            public ushort SlotIndex;
            public RuntimeMethodHandle GVMHandle;
            public bool IsGVM;
        }

        public static bool TryGetVirtualResolveData(IntPtr moduleHandle,
            RuntimeTypeHandle methodHandleDeclaringType, RuntimeTypeHandle[] genericArgs,
            ref MethodSignatureComparer methodSignatureComparer,
            out VirtualResolveDataResult lookupResult)
        {
            lookupResult = default(VirtualResolveDataResult);
            NativeReader invokeMapReader = GetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.VirtualInvokeMap);
            NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
            NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);
            ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
            externalReferences.InitializeCommonFixupsTable(moduleHandle);

            RuntimeTypeHandle definitionType = Instance.GetTypeDefinition(methodHandleDeclaringType);

            int hashcode = definitionType.GetHashCode();

            var lookup = invokeHashtable.Lookup(hashcode);
            NativeParser entryParser;
            while (!(entryParser = lookup.GetNext()).IsNull)
            {
                // Grammar of an entry in the hash table:
                // Virtual Method uses a normal slot 
                // OpenType + NameAndSig metadata offset into the native layout metadata + (NumberOfStepsUpParentHierarchyToType << 1) + slot
                // OR
                // Generic Virtual Method 
                // OpenType + NameAndSig metadata offset into the native layout metadata + (NumberOfStepsUpParentHierarchyToType << 1 + 1)

                RuntimeTypeHandle entryType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!entryType.Equals(definitionType))
                    continue;

                uint nameAndSigPointerToken = externalReferences.GetRvaFromIndex(entryParser.GetUnsigned());

                MethodNameAndSignature nameAndSig;
                if (!TypeLoaderEnvironment.Instance.TryGetMethodNameAndSignatureFromNativeLayoutOffset(moduleHandle, nameAndSigPointerToken, out nameAndSig))
                {
                    Debug.Assert(false);
                    continue;
                }

                if (!methodSignatureComparer.IsMatchingNativeLayoutMethodNameAndSignature(nameAndSig.Name, nameAndSig.Signature.NativeLayoutSignature))
                {
                    continue;
                }

                uint parentHierarchyAndFlag = entryParser.GetUnsigned();
                uint parentHierarchy = parentHierarchyAndFlag >> 1;
                RuntimeTypeHandle declaringTypeOfVirtualInvoke = methodHandleDeclaringType;
                for (uint iType = 0; iType < parentHierarchy; iType++)
                {
                    if (!RuntimeAugments.TryGetBaseType(declaringTypeOfVirtualInvoke, out declaringTypeOfVirtualInvoke))
                    {
                        Debug.Assert(false); // This will only fail if the virtual invoke data is malformed as specifies that a type
                        // has a deeper inheritance hierarchy than it actually does.
                        return false;
                    }
                }

                bool isGenericVirtualMethod = ((parentHierarchyAndFlag & VirtualInvokeTableEntry.FlagsMask) == VirtualInvokeTableEntry.GenericVirtualMethod);

                Debug.Assert(isGenericVirtualMethod == ((genericArgs != null) && genericArgs.Length > 0));

                if (isGenericVirtualMethod)
                {
                    IntPtr methodName;
                    IntPtr methodSignature;

                    if (!TypeLoaderEnvironment.Instance.TryGetMethodNameAndSignaturePointersFromNativeLayoutSignature(moduleHandle, nameAndSigPointerToken, out methodName, out methodSignature))
                    {
                        Debug.Assert(false);
                        return false;
                    }

                    RuntimeMethodHandle gvmSlot;
                    if (!TypeLoaderEnvironment.Instance.TryGetRuntimeMethodHandleForComponents(declaringTypeOfVirtualInvoke, methodName, RuntimeMethodSignature.CreateFromNativeLayoutSignature(methodSignature), genericArgs, out gvmSlot))
                    {
                        return false;
                    }

                    lookupResult = new VirtualResolveDataResult
                    {
                        DeclaringInvokeType = declaringTypeOfVirtualInvoke,
                        SlotIndex = 0,
                        GVMHandle = gvmSlot,
                        IsGVM = true
                    };
                    return true;
                }
                else
                {
                    uint slot = entryParser.GetUnsigned();

                    RuntimeTypeHandle searchForSharedGenericTypesInParentHierarchy = declaringTypeOfVirtualInvoke;
                    while (!searchForSharedGenericTypesInParentHierarchy.IsNull())
                    {
                        // See if this type is shared generic. If so, adjust the slot by 1.
                        if (RuntimeAugments.IsGenericType(searchForSharedGenericTypesInParentHierarchy))
                        {
                            if (RuntimeAugments.IsInterface(searchForSharedGenericTypesInParentHierarchy))
                            {
                                // Generic interfaces always have a dictionary slot in the vtable (see binder code in MdilModule::SaveMethodTable)
                                // Interfaces do not have base types, so we can just break out of the loop here ...
                                slot++;
                                break;
                            }

                            RuntimeTypeHandle[] genericTypeArgs;
                            RuntimeAugments.GetGenericInstantiation(searchForSharedGenericTypesInParentHierarchy,
                                                                    out genericTypeArgs);
                            if (TypeLoaderEnvironment.Instance.ConversionToCanonFormIsAChange(genericTypeArgs, CanonicalFormKind.Specific))
                            {
                                // Shared generic types have a slot dedicated to holding the generic dictionary.
                                slot++;
                            }
                        }

                        // Walk to parent
                        if (!RuntimeAugments.TryGetBaseType(searchForSharedGenericTypesInParentHierarchy, out searchForSharedGenericTypesInParentHierarchy))
                        {
                            break;
                        }
                    }

                    lookupResult = new VirtualResolveDataResult
                    {
                        DeclaringInvokeType = declaringTypeOfVirtualInvoke,
                        SlotIndex = checked((ushort)slot),
                        GVMHandle = default(RuntimeMethodHandle),
                        IsGVM = false
                    };
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Given a virtual logical slot and its open defining type, get information necessary to acquire the associated metadata from the mapping tables.
        /// </summary>
        /// <param name="moduleHandle">Module to look in</param>
        /// <param name="definitionType">Open or non-generic type that is known to define the slot</param>
        /// <param name="logicalSlot">The logical slot that the method goes in. For this method, the logical 
        /// slot is defined as the nth virtual method defined in order on the type (including base types). 
        /// VTable slots reserved for dictionary pointers are ignored.</param>
        /// <param name="methodNameAndSig">The name and signature of the method</param>
        /// <returns>true if a definition is found, false if not</returns>
        public unsafe static bool TryGetMethodNameAndSigFromVirtualResolveData(IntPtr moduleHandle,
            RuntimeTypeHandle definitionType, int logicalSlot, out MethodNameAndSignature methodNameAndSig)
        {
            EEType* definitionEEType = definitionType.ToEETypePtr();
            NativeReader invokeMapReader = GetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.VirtualInvokeMap);
            NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
            NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);
            ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
            externalReferences.InitializeCommonFixupsTable(moduleHandle);

            int hashcode = definitionType.GetHashCode();
            methodNameAndSig = default(MethodNameAndSignature);

            var lookup = invokeHashtable.Lookup(hashcode);
            NativeParser entryParser;
            while (!(entryParser = lookup.GetNext()).IsNull)
            {
                // Grammar of an entry in the hash table:
                // Virtual Method uses a normal slot 
                // OpenType + NameAndSig metadata offset into the native layout metadata + (NumberOfStepsUpParentHierarchyToType << 1) + slot
                // OR
                // Generic Virtual Method 
                // OpenType + NameAndSig metadata offset into the native layout metadata + (NumberOfStepsUpParentHierarchyToType << 1 + 1)

                RuntimeTypeHandle entryType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!entryType.Equals(definitionType))
                    continue;

                uint nameAndSigPointerToken = externalReferences.GetRvaFromIndex(entryParser.GetUnsigned());

                uint parentHierarchyAndFlag = entryParser.GetUnsigned();
                bool isGenericVirtualMethod = ((parentHierarchyAndFlag & VirtualInvokeTableEntry.FlagsMask) == VirtualInvokeTableEntry.GenericVirtualMethod);

                // We're looking for a method with a specific slot. By definition, it isn't a GVM as we define GVM as not having slots in the vtable
                if (isGenericVirtualMethod)
                    continue;

                uint mappingTableSlot = entryParser.GetUnsigned();

                // Slot doesn't match
                if (logicalSlot != mappingTableSlot)
                    continue;

                if (!TypeLoaderEnvironment.Instance.TryGetMethodNameAndSignatureFromNativeLayoutOffset(moduleHandle, nameAndSigPointerToken, out methodNameAndSig))
                {
                    Debug.Assert(false);
                    continue;
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Try to look up method invoke info for given canon.
        /// </summary>
        /// <param name="metadataReader">Metadata reader for the declaring type</param>
        /// <param name="declaringTypeHandle">Declaring type for the method</param>
        /// <param name="methodHandle">Method handle</param>
        /// <param name="genericMethodTypeArgumentHandles">Handles of generic argument types</param>
        /// <param name="methodSignatureComparer">Helper class used to compare method signatures</param>
        /// <param name="canonFormKind">Canonical form to use</param>
        /// <param name="methodInvokeMetadata">Output - metadata information for method invoker construction</param>
        /// <returns>true when found, false otherwise</returns>
        public static bool TryGetMethodInvokeMetadata(
            MetadataReader metadataReader,
            RuntimeTypeHandle declaringTypeHandle,
            MethodHandle methodHandle,
            RuntimeTypeHandle[] genericMethodTypeArgumentHandles,
            ref MethodSignatureComparer methodSignatureComparer,
            CanonicalFormKind canonFormKind,
            out MethodInvokeMetadata methodInvokeMetadata)
        {
            if (TryGetMethodInvokeMetadataFromInvokeMap(
                metadataReader,
                declaringTypeHandle,
                methodHandle,
                genericMethodTypeArgumentHandles,
                ref methodSignatureComparer,
                canonFormKind,
                out methodInvokeMetadata))
            {
                return true;
            }

            TypeSystemContext context = TypeSystemContextFactory.Create();

            bool success = TryGetMethodInvokeMetadataFromNativeFormatMetadata(
                metadataReader,
                declaringTypeHandle,
                methodHandle,
                genericMethodTypeArgumentHandles,
                ref methodSignatureComparer,
                context,
                canonFormKind,
                out methodInvokeMetadata);

            TypeSystemContextFactory.Recycle(context);

            return success;
        }

#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
        /// <summary>
        /// Try to look up method invoke info for given canon in InvokeMap blobs for all available modules.
        /// </summary>
        /// <param name="typicalMethodDesc">Metadata MethodDesc to look for</param>
        /// <param name="method">method to search for</param>
        /// <param name="methodSignatureComparer">Helper class used to compare method signatures</param>
        /// <param name="canonFormKind">Canonical form to use</param>
        /// <param name="methodEntryPoint">Output - Output code address</param>
        /// <param name="foundAddressType">Output - The type of method address match found. A canonical address may require extra parameters to call.</param>
        /// <returns>true when found, false otherwise</returns>
        private static bool TryGetMethodInvokeDataFromInvokeMap(
            NativeFormatMethod typicalMethodDesc,
            MethodDesc method,
            ref MethodSignatureComparer methodSignatureComparer,
            CanonicalFormKind canonFormKind,
            out IntPtr methodEntryPoint,
            out MethodAddressType foundAddressType)
        {
            methodEntryPoint = IntPtr.Zero;
            foundAddressType = MethodAddressType.None;

            CanonicallyEquivalentEntryLocator canonHelper = new CanonicallyEquivalentEntryLocator(method.OwningType.GetClosestDefType(), canonFormKind);

            IntPtr methodHandleModule = typicalMethodDesc.MetadataUnit.RuntimeModule;

            foreach (IntPtr module in ModuleList.Enumerate(methodHandleModule))
            {
                NativeReader invokeMapReader;
                if (!TryGetNativeReaderForBlob(module, ReflectionMapBlob.InvokeMap, out invokeMapReader))
                {
                    continue;
                }

                NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
                NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);

                ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
                externalReferences.InitializeCommonFixupsTable(module);

                var lookup = invokeHashtable.Lookup(canonHelper.LookupHashCode);
                var entryData = new InvokeMapEntryDataEnumerator<TypeSystemTypeComparator, bool>(
                    new TypeSystemTypeComparator(method),
                    canonFormKind,
                    module,
                    typicalMethodDesc.Handle,
                    methodHandleModule);

                NativeParser entryParser;
                while (!(entryParser = lookup.GetNext()).IsNull)
                {
                    entryData.GetNext(ref entryParser, ref externalReferences, ref methodSignatureComparer, canonHelper);

                    if (!entryData.IsMatchingOrCompatibleEntry())
                        continue;

                    IntPtr rawMethodEntryPoint;
                    bool needsDictionaryForCall;

                    if (entryData.GetMethodEntryPoint(
                        out methodEntryPoint,
                        out needsDictionaryForCall,
                        out rawMethodEntryPoint))
                    {
                        // At this time, because we don't have any logic which generates a true fat function pointer
                        // in the TypeSystemTypeComparator, rawMethodEntryPoint should always be the same as methodEntryPoint
                        Debug.Assert(rawMethodEntryPoint == methodEntryPoint);

                        if (canonFormKind == CanonicalFormKind.Universal)
                        {
                            foundAddressType = MethodAddressType.UniversalCanonical;
                        }
                        else
                        {
                            Debug.Assert(canonFormKind == CanonicalFormKind.Specific);

                            if (needsDictionaryForCall)
                            {
                                foundAddressType = MethodAddressType.Canonical;
                            }
                            else
                            {
                                if (method.OwningType.IsValueType && method.OwningType != method.OwningType.ConvertToCanonForm(canonFormKind) && !method.Signature.IsStatic)
                                {
                                    // The entrypoint found is the unboxing stub for a non-generic instance method on a structure
                                    foundAddressType = MethodAddressType.Canonical;
                                }
                                else
                                {
                                    foundAddressType = MethodAddressType.Exact; // We may or may not have found a canonical method here, but if its exactly callable... its close enough
                                }
                            }
                        }
                    }

                    return true;
                }
            }

            return false;
        }
#endif

        /// <summary>
        /// Try to look up method invoke info for given canon in InvokeMap blobs for all available modules.
        /// </summary>
        /// <param name="metadataReader">Metadata reader for the declaring type</param>
        /// <param name="declaringTypeHandle">Declaring type for the method</param>
        /// <param name="methodHandle">Method handle</param>
        /// <param name="genericMethodTypeArgumentHandles">Handles of generic argument types</param>
        /// <param name="methodSignatureComparer">Helper class used to compare method signatures</param>
        /// <param name="canonFormKind">Canonical form to use</param>
        /// <param name="methodInvokeMetadata">Output - metadata information for method invoker construction</param>
        /// <returns>true when found, false otherwise</returns>
        private static bool TryGetMethodInvokeMetadataFromInvokeMap(
            MetadataReader metadataReader,
            RuntimeTypeHandle declaringTypeHandle,
            MethodHandle methodHandle,
            RuntimeTypeHandle[] genericMethodTypeArgumentHandles,
            ref MethodSignatureComparer methodSignatureComparer,
            CanonicalFormKind canonFormKind,
            out MethodInvokeMetadata methodInvokeMetadata)
        {
            CanonicallyEquivalentEntryLocator canonHelper = new CanonicallyEquivalentEntryLocator(declaringTypeHandle, canonFormKind);
            IntPtr methodHandleModule = ModuleList.Instance.GetModuleForMetadataReader(metadataReader);

            foreach (IntPtr module in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(declaringTypeHandle)))
            {
                NativeReader invokeMapReader;
                if (!TryGetNativeReaderForBlob(module, ReflectionMapBlob.InvokeMap, out invokeMapReader))
                {
                    continue;
                }

                NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
                NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);

                ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
                externalReferences.InitializeCommonFixupsTable(module);

                var lookup = invokeHashtable.Lookup(canonHelper.LookupHashCode);
                var entryData = new InvokeMapEntryDataEnumerator<PreloadedTypeComparator, IntPtr>(
                    new PreloadedTypeComparator(declaringTypeHandle, genericMethodTypeArgumentHandles),
                    canonFormKind,
                    module,
                    methodHandle,
                    methodHandleModule);

                NativeParser entryParser;
                while (!(entryParser = lookup.GetNext()).IsNull)
                {
                    entryData.GetNext(ref entryParser, ref externalReferences, ref methodSignatureComparer, canonHelper);

                    if (!entryData.IsMatchingOrCompatibleEntry())
                        continue;

                    if (entryData.GetMethodEntryPoint(
                        out methodInvokeMetadata.MethodEntryPoint,
                        out methodInvokeMetadata.DictionaryComponent,
                        out methodInvokeMetadata.RawMethodEntryPoint))
                    {
                        methodInvokeMetadata.MappingTableModule = module;
                        methodInvokeMetadata.DynamicInvokeCookie = entryData._dynamicInvokeCookie;
                        methodInvokeMetadata.InvokeTableFlags = entryData._flags;

                        return true;
                    }
                }
            }

            methodInvokeMetadata = default(MethodInvokeMetadata);
            return false;
        }

        /// <summary>
        /// Look up method entry point based on native format metadata information.
        /// </summary>
        /// <param name="metadataReader">Metadata reader for the declaring type</param>
        /// <param name="declaringTypeHandle">Declaring type for the method</param>
        /// <param name="methodHandle">Method handle</param>
        /// <param name="genericMethodTypeArgumentHandles">Handles of generic argument types</param>
        /// <param name="methodSignatureComparer">Helper class used to compare method signatures</param>
        /// <param name="typeSystemContext">Type system context to use</param>
        /// <param name="canonFormKind">Canonical form to use</param>
        /// <param name="methodInvokeMetadata">Output - metadata information for method invoker construction</param>
        /// <returns>true when found, false otherwise</returns>
        private static bool TryGetMethodInvokeMetadataFromNativeFormatMetadata(
            MetadataReader metadataReader,
            RuntimeTypeHandle declaringTypeHandle,
            MethodHandle methodHandle,
            RuntimeTypeHandle[] genericMethodTypeArgumentHandles,
            ref MethodSignatureComparer methodSignatureComparer,
            TypeSystemContext typeSystemContext,
            CanonicalFormKind canonFormKind,
            out MethodInvokeMetadata methodInvokeMetadata)
        {
            methodInvokeMetadata = default(MethodInvokeMetadata);

#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
            TypeDesc declaringType = typeSystemContext.ResolveRuntimeTypeHandle(declaringTypeHandle);
            NativeFormatType nativeFormatType = declaringType.GetTypeDefinition() as NativeFormatType;

            if (nativeFormatType == null)
                return false;

            Debug.Assert(metadataReader == nativeFormatType.MetadataReader);

            MethodDesc methodOnType = nativeFormatType.MetadataUnit.GetMethod(methodHandle, nativeFormatType);
            if (methodOnType == null)
            {
                return false;
            }

            if (nativeFormatType != declaringType)
            {
                // If we reach here, then the method is on a generic type, and we just found the uninstantiated form
                // Get the method on the instantiated type and continue
                methodOnType = typeSystemContext.GetMethodForInstantiatedType(methodOnType, (InstantiatedType)declaringType);
            }

            if (genericMethodTypeArgumentHandles.Length > 0)
            {
                // If we reach here, this is a generic method, instantiate and continue
                methodOnType = typeSystemContext.GetInstantiatedMethod(methodOnType, typeSystemContext.ResolveRuntimeTypeHandles(genericMethodTypeArgumentHandles));
            }

            IntPtr entryPoint = IntPtr.Zero;
            IntPtr unboxingStubAddress = IntPtr.Zero;
            MethodAddressType foundAddressType = MethodAddressType.None;
#if SUPPORTS_R2R_LOADING
            if (!TryGetCodeTableEntry(methodOnType, out entryPoint, out unboxingStubAddress, out foundAddressType))
            {
                return false;
            }
#endif
            if (foundAddressType == MethodAddressType.None)
                return false;

            // Only find a universal canon implementation if searching for one
            if (foundAddressType == MethodAddressType.UniversalCanonical &&
                !((canonFormKind == CanonicalFormKind.Universal) || (canonFormKind == CanonicalFormKind.Any)))
            {
                return false;
            }

            // TODO: This will probably require additional work to smoothly use unboxing stubs
            // in vtables - for plain reflection invoke everything seems to work
            // without additional changes thanks to the "NeedsParameterInterpretation" flag.
            methodInvokeMetadata.MappingTableModule = nativeFormatType.MetadataUnit.RuntimeModule;
            methodInvokeMetadata.MethodEntryPoint = entryPoint;
            methodInvokeMetadata.RawMethodEntryPoint = entryPoint;
            // TODO: methodInvokeMetadata.DictionaryComponent
            // TODO: methodInvokeMetadata.DefaultValueString
            // TODO: methodInvokeMetadata.DynamicInvokeCookie

            methodInvokeMetadata.InvokeTableFlags =
                InvokeTableFlags.HasMetadataHandle |
                InvokeTableFlags.HasEntrypoint |
                InvokeTableFlags.NeedsParameterInterpretation;
            if (methodOnType.Signature.GenericParameterCount != 0)
            {
                methodInvokeMetadata.InvokeTableFlags |= InvokeTableFlags.IsGenericMethod;
            }
            if (canonFormKind == CanonicalFormKind.Universal)
            {
                methodInvokeMetadata.InvokeTableFlags |= InvokeTableFlags.IsUniversalCanonicalEntry;
            }
            /* TODO
            if (methodOnType.HasDefaultParameters)
            {
                methodInvokeMetadata.InvokeTableFlags |= InvokeTableFlags.HasDefaultParameters;
            }
            */

            return true;
#else
            return false;
#endif
        }

        // Api surface for controlling invoke map enumeration.
        private interface IInvokeMapEntryDataDeclaringTypeAndGenericMethodParameterHandling<TDictionaryComponentType>
        {
            bool GetTypeDictionary(out TDictionaryComponentType dictionary);
            bool GetMethodDictionary(MethodNameAndSignature nameAndSignature, out TDictionaryComponentType dictionary);
            bool IsUninterestingDictionaryComponent(TDictionaryComponentType dictionary);
            bool CompareMethodInstantiation(RuntimeTypeHandle[] methodInstantiation);
            bool CanInstantiationsShareCode(RuntimeTypeHandle[] methodInstantiation, CanonicalFormKind canonFormKind);
            IntPtr ProduceFatFunctionPointerMethodEntryPoint(IntPtr methodEntrypoint, TDictionaryComponentType dictionary);
        }

        // Comparator for invoke map when used to find an invoke map entry and the search data is a set of
        // pre-loaded types, and metadata handles.
        private struct PreloadedTypeComparator : IInvokeMapEntryDataDeclaringTypeAndGenericMethodParameterHandling<IntPtr>
        {
            readonly private RuntimeTypeHandle _declaringTypeHandle;
            readonly private RuntimeTypeHandle[] _genericMethodTypeArgumentHandles;

            public PreloadedTypeComparator(RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
            {
                _declaringTypeHandle = declaringTypeHandle;
                _genericMethodTypeArgumentHandles = genericMethodTypeArgumentHandles;
            }

            public bool GetTypeDictionary(out IntPtr dictionary)
            {
                dictionary = RuntimeAugments.GetPointerFromTypeHandle(_declaringTypeHandle);
                Debug.Assert(dictionary != IntPtr.Zero);
                return true;
            }

            public bool GetMethodDictionary(MethodNameAndSignature nameAndSignature, out IntPtr dictionary)
            {
                return TypeLoaderEnvironment.Instance.TryGetGenericMethodDictionaryForComponents(_declaringTypeHandle,
                    _genericMethodTypeArgumentHandles,
                    nameAndSignature,
                    out dictionary);
            }

            public bool IsUninterestingDictionaryComponent(IntPtr dictionary)
            {
                return dictionary == IntPtr.Zero;
            }

            public IntPtr ProduceFatFunctionPointerMethodEntryPoint(IntPtr methodEntrypoint, IntPtr dictionary)
            {
                return FunctionPointerOps.GetGenericMethodFunctionPointer(methodEntrypoint, dictionary);
            }

            public bool CompareMethodInstantiation(RuntimeTypeHandle[] methodInstantiation)
            {
                return SequenceEqual(_genericMethodTypeArgumentHandles, methodInstantiation);
            }

            public bool CanInstantiationsShareCode(RuntimeTypeHandle[] methodInstantiation, CanonicalFormKind canonFormKind)
            {
                return TypeLoaderEnvironment.Instance.CanInstantiationsShareCode(methodInstantiation, _genericMethodTypeArgumentHandles, canonFormKind);
            }
        }

        // Comparator for invoke map when used to find an invoke map entry and the search data is
        // a type system object with Metadata
        private struct TypeSystemTypeComparator : IInvokeMapEntryDataDeclaringTypeAndGenericMethodParameterHandling<bool>
        {
            readonly private MethodDesc _targetMethod;

            public TypeSystemTypeComparator(MethodDesc targetMethod)
            {
                _targetMethod = targetMethod;
            }

            public bool GetTypeDictionary(out bool dictionary)
            {
                // The true is to indicate a dictionary is necessary
                dictionary = true;
                return true;
            }

            public bool GetMethodDictionary(MethodNameAndSignature nameAndSignature, out bool dictionary)
            {
                // The true is to indicate a dictionary is necessary
                dictionary = true;
                return true;
            }

            public bool IsUninterestingDictionaryComponent(bool dictionary)
            {
                return dictionary == false;
            }

            public IntPtr ProduceFatFunctionPointerMethodEntryPoint(IntPtr methodEntrypoint, bool dictionary)
            {
                // We don't actually want to produce the fat function pointer here. We want to delay until its actually needed
                return methodEntrypoint;
            }

            public bool CompareMethodInstantiation(RuntimeTypeHandle[] methodInstantiation)
            {
                if (!_targetMethod.HasInstantiation)
                    return false;

                if (_targetMethod.Instantiation.Length != methodInstantiation.Length)
                    return false;

                int i = 0;
                foreach (TypeDesc instantiationType in _targetMethod.Instantiation)
                {
                    TypeDesc genericArg2 = _targetMethod.Context.ResolveRuntimeTypeHandle(methodInstantiation[i]);
                    if (instantiationType != genericArg2)
                    {
                        return false;
                    }
                    i++;
                }

                return true;
            }

            public bool CanInstantiationsShareCode(RuntimeTypeHandle[] methodInstantiation, CanonicalFormKind canonFormKind)
            {
                if (!_targetMethod.HasInstantiation)
                    return false;

                if (_targetMethod.Instantiation.Length != methodInstantiation.Length)
                    return false;

                int i = 0;
                foreach (TypeDesc instantiationType in _targetMethod.Instantiation)
                {
                    TypeSystemContext context = _targetMethod.Context;
                    TypeDesc genericArg2 = context.ResolveRuntimeTypeHandle(methodInstantiation[i]);
                    if (context.ConvertToCanon(instantiationType, canonFormKind) != context.ConvertToCanon(genericArg2, canonFormKind))
                    {
                        return false;
                    }
                    i++;
                }

                return true;
            }
        }

        // Enumerator for discovering methods in the InvokeMap. This is generic to allow highly efficient
        // searching of this table with multiple different input data formats.
        private struct InvokeMapEntryDataEnumerator<TLookupMethodInfo, TDictionaryComponentType> where TLookupMethodInfo : IInvokeMapEntryDataDeclaringTypeAndGenericMethodParameterHandling<TDictionaryComponentType>
        {
            // Read-only inputs
            private TLookupMethodInfo _lookupMethodInfo;
            readonly private CanonicalFormKind _canonFormKind;
            readonly private IntPtr _moduleHandle;
            readonly private IntPtr _moduleForMethodHandle;
            readonly private MethodHandle _methodHandle;

            // Parsed data from entry in the hashtable
            public InvokeTableFlags _flags;
            public RuntimeTypeHandle _entryType;
            public IntPtr _methodEntrypoint;
            public uint _dynamicInvokeCookie;
            public IntPtr _entryDictionary;
            public RuntimeTypeHandle[] _methodInstantiation;

            // Computed data
            private bool _hasEntryPoint;
            private bool _isMatchingMethodHandleAndDeclaringType;
            private MethodNameAndSignature _nameAndSignature;
            private RuntimeTypeHandle[] _entryMethodInstantiation;

            public InvokeMapEntryDataEnumerator(
                TLookupMethodInfo lookupMethodInfo,
                CanonicalFormKind canonFormKind,
                IntPtr moduleHandle,
                MethodHandle methodHandle,
                IntPtr moduleForMethodHandle)
            {
                _lookupMethodInfo = lookupMethodInfo;
                _canonFormKind = canonFormKind;
                _moduleHandle = moduleHandle;
                _methodHandle = methodHandle;
                _moduleForMethodHandle = moduleForMethodHandle;

                _flags = 0;
                _entryType = default(RuntimeTypeHandle);
                _methodEntrypoint = IntPtr.Zero;
                _dynamicInvokeCookie = 0xffffffff;
                _hasEntryPoint = false;
                _isMatchingMethodHandleAndDeclaringType = false;
                _entryDictionary = IntPtr.Zero;
                _methodInstantiation = null;
                _nameAndSignature = null;
                _entryMethodInstantiation = null;
            }

            public void GetNext(
                ref NativeParser entryParser,
                ref ExternalReferencesTable extRefTable,
                ref MethodSignatureComparer methodSignatureComparer,
                CanonicallyEquivalentEntryLocator canonHelper)
            {
                // Read flags and reset members data
                _flags = (InvokeTableFlags)entryParser.GetUnsigned();
                _hasEntryPoint = ((_flags & InvokeTableFlags.HasEntrypoint) != 0);
                _isMatchingMethodHandleAndDeclaringType = false;
                _entryType = default(RuntimeTypeHandle);
                _methodEntrypoint = IntPtr.Zero;
                _dynamicInvokeCookie = 0xffffffff;
                _entryDictionary = IntPtr.Zero;
                _methodInstantiation = null;
                _nameAndSignature = null;
                _entryMethodInstantiation = null;

                // If the current entry is not a canonical entry of the same canonical form kind we are looking for, then this cannot be a match
                if (((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0) != (_canonFormKind == CanonicalFormKind.Universal))
                    return;

                if ((_flags & InvokeTableFlags.HasMetadataHandle) != 0)
                {
                    // Metadata handles are not known cross module, and cannot be compared across modules.
                    if (_moduleHandle != _moduleForMethodHandle)
                        return;

                    Handle entryMethodHandle = (((uint)HandleType.Method << 24) | entryParser.GetUnsigned()).AsHandle();
                    if (!_methodHandle.Equals(entryMethodHandle))
                        return;
                }
                else
                {
                    uint nameAndSigToken = extRefTable.GetRvaFromIndex(entryParser.GetUnsigned());
                    MethodNameAndSignature nameAndSig;
                    if (!TypeLoaderEnvironment.Instance.TryGetMethodNameAndSignatureFromNativeLayoutOffset(_moduleHandle, nameAndSigToken, out nameAndSig))
                    {
                        Debug.Assert(false);
                        return;
                    }
                    Debug.Assert(nameAndSig.Signature.IsNativeLayoutSignature);
                    if (!methodSignatureComparer.IsMatchingNativeLayoutMethodNameAndSignature(nameAndSig.Name, nameAndSig.Signature.NativeLayoutSignature))
                        return;
                }

                _entryType = extRefTable.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!canonHelper.IsCanonicallyEquivalent(_entryType))
                    return;

                // Method handle and entry type match at this point. Continue reading data from the entry...
                _isMatchingMethodHandleAndDeclaringType = true;

                if (_hasEntryPoint)
                    _methodEntrypoint = extRefTable.GetFunctionPointerFromIndex(entryParser.GetUnsigned());

                if ((_flags & InvokeTableFlags.NeedsParameterInterpretation) == 0)
                    _dynamicInvokeCookie = entryParser.GetUnsigned();

                if ((_flags & InvokeTableFlags.IsGenericMethod) == 0)
                    return;

                if ((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0)
                {
                    Debug.Assert((_hasEntryPoint || ((_flags & InvokeTableFlags.HasVirtualInvoke) != 0)) && ((_flags & InvokeTableFlags.RequiresInstArg) != 0));

                    uint nameAndSigPointerToken = extRefTable.GetRvaFromIndex(entryParser.GetUnsigned());
                    if (!TypeLoaderEnvironment.Instance.TryGetMethodNameAndSignatureFromNativeLayoutOffset(_moduleHandle, nameAndSigPointerToken, out _nameAndSignature))
                    {
                        Debug.Assert(false);    //Error
                        _isMatchingMethodHandleAndDeclaringType = false;
                    }
                }
                else if (((_flags & InvokeTableFlags.RequiresInstArg) != 0) && _hasEntryPoint)
                    _entryDictionary = RvaToGenericDictionary(_moduleHandle, extRefTable.GetRvaFromIndex(entryParser.GetUnsigned()));
                else
                    _methodInstantiation = GetTypeSequence(ref extRefTable, ref entryParser);
            }

            public bool IsMatchingOrCompatibleEntry()
            {
                // Check if method handle and entry type were matching or compatible
                if (!_isMatchingMethodHandleAndDeclaringType)
                    return false;

                // Nothing special about non-generic methods. 
                if ((_flags & InvokeTableFlags.IsGenericMethod) == 0)
                    return true;

                // A universal canonical method entry can share code with any method instantiation (no need to call CanInstantiationsShareCode())
                if ((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0)
                {
                    Debug.Assert(_canonFormKind == CanonicalFormKind.Universal);
                    return true;
                }

                // Generic non-shareable method: check the method instantiation arguments
                if (((_flags & InvokeTableFlags.RequiresInstArg) == 0) || !_hasEntryPoint)
                    return _lookupMethodInfo.CompareMethodInstantiation(_methodInstantiation);

                // Generic shareable method: check for canonical equivalency of the method instantiation arguments
                return GetNameAndSignatureAndMethodInstantiation() && _lookupMethodInfo.CanInstantiationsShareCode(_entryMethodInstantiation, _canonFormKind);
            }

            public bool GetMethodEntryPoint(out IntPtr methodEntrypoint, out TDictionaryComponentType dictionaryComponent, out IntPtr rawMethodEntrypoint)
            {
                // Debug-only sanity check before proceeding (IsMatchingOrCompatibleEntry is called from TryGetDynamicMethodInvokeInfo)
                Debug.Assert(IsMatchingOrCompatibleEntry());

                rawMethodEntrypoint = _methodEntrypoint;
                methodEntrypoint = IntPtr.Zero;
                dictionaryComponent = default(TDictionaryComponentType);

                if (!GetDictionaryComponent(out dictionaryComponent) || !GetMethodEntryPointComponent(dictionaryComponent, out methodEntrypoint))
                    return false;

                return true;
            }

            private bool GetDictionaryComponent(out TDictionaryComponentType dictionaryComponent)
            {
                dictionaryComponent = default(TDictionaryComponentType);

                if (((_flags & InvokeTableFlags.RequiresInstArg) == 0) || !_hasEntryPoint)
                    return true;

                // Dictionary for non-generic method is the type handle of the declaring type
                if ((_flags & InvokeTableFlags.IsGenericMethod) == 0)
                {
                    return _lookupMethodInfo.GetTypeDictionary(out dictionaryComponent);
                }

                // Dictionary for generic method (either found statically or constructed dynamically)
                return GetNameAndSignatureAndMethodInstantiation() && _lookupMethodInfo.GetMethodDictionary(_nameAndSignature, out dictionaryComponent);
            }

            private bool GetMethodEntryPointComponent(TDictionaryComponentType dictionaryComponent, out IntPtr methodEntrypoint)
            {
                methodEntrypoint = _methodEntrypoint;

                if (_lookupMethodInfo.IsUninterestingDictionaryComponent(dictionaryComponent))
                    return true;

                // Do not use a fat function-pointer for universal canonical methods because the converter data block already holds the 
                // dictionary pointer so it serves as its own instantiating stub
                if ((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) == 0)
                    methodEntrypoint = _lookupMethodInfo.ProduceFatFunctionPointerMethodEntryPoint(_methodEntrypoint, dictionaryComponent);

                return true;
            }

            private bool GetNameAndSignatureAndMethodInstantiation()
            {
                if (_nameAndSignature != null)
                {
                    Debug.Assert(((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0) || (_entryMethodInstantiation != null && _entryMethodInstantiation.Length > 0));
                    return true;
                }

                if ((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0)
                {
                    // _nameAndSignature should have been read from the InvokeMap entry directly!
                    Debug.Assert(false, "Universal canonical entries do NOT have dictionary entries!");
                    return false;
                }

                RuntimeTypeHandle dummy1;
                bool success = TypeLoaderEnvironment.Instance.TryGetGenericMethodComponents(_entryDictionary, out dummy1, out _nameAndSignature, out _entryMethodInstantiation);
                Debug.Assert(success && dummy1.Equals(_entryType) && _nameAndSignature != null && _entryMethodInstantiation != null && _entryMethodInstantiation.Length > 0);
                return success;
            }
        }

        public static ModuleInfo GetModuleInfoForType(TypeDesc type)
        {
            for (;;)
            {
                type = type.GetTypeDefinition();
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
                NativeFormatType nativeType = type as NativeFormatType;
                if (nativeType != null)
                {
                    MetadataReader metadataReader = nativeType.MetadataReader;
                    IntPtr moduleHandle = ModuleList.Instance.GetModuleForMetadataReader(metadataReader);
                    ModuleInfo moduleInfo = ModuleList.Instance.GetModuleInfoByHandle(moduleHandle);

                    return moduleInfo;
                }
#endif
                ArrayType arrayType = type as ArrayType;
                if (arrayType != null)
                {
                    // Arrays are defined in the core shared library
                    return ModuleList.Instance.SystemModule;
                }
                InstantiatedType instantiatedType = type as InstantiatedType;
                if (instantiatedType != null)
                {
                    type = instantiatedType.GetTypeDefinition();
                }
                ParameterizedType parameterizedType = type as ParameterizedType;
                if (parameterizedType != null)
                {
                    type = parameterizedType.ParameterType;
                    continue;
                }
                // Unable to resolve the native type
                return ModuleList.Instance.SystemModule;
            }
        }
    }
}
