// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;
using global::Internal.Runtime.CompilerServices;
using global::Internal.Runtime.TypeLoader;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Execution.MethodInvokers;
using global::Internal.Reflection.Execution.FieldAccessors;

using global::Internal.Metadata.NativeFormat;

using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;

using global::Internal.Runtime;
using global::Internal.NativeFormat;
using CanonicalFormKind = global::Internal.TypeSystem.CanonicalFormKind;


using Debug = System.Diagnostics.Debug;
using TargetException = System.ArgumentException;
using Interlocked = System.Threading.Interlocked;

namespace Internal.Reflection.Execution
{
    //==========================================================================================================
    // These ExecutionEnvironment entrypoints provide access to the NUTC-generated blob information that
    // enables Reflection invoke and tie-ins to native Type artifacts.
    //
    // - Except when otherwise noted, ExecutionEnvironment methods use the "TryGet*" pattern rather than throwing exceptions.
    //
    // - All methods on this class must be multi-thread-safe. Reflection can and does invoke them on different threads with no synchronization of its own.
    //
    //==========================================================================================================
    internal sealed partial class ExecutionEnvironmentImplementation : ExecutionEnvironment
    {
        struct MethodTargetAndDictionary { public IntPtr TargetPointer; public IntPtr DictionaryPointer; }

        LowLevelDictionary<IntPtr, MethodTargetAndDictionary> _callConverterWrappedMethodEntrypoints = new LowLevelDictionary<IntPtr, MethodTargetAndDictionary>();

        LowLevelDictionaryWithIEnumerable<IntPtr, MetadataReader> _moduleToMetadataReader;

#if REFLECTION_EXECUTION_TRACE
        private string GetTypeNameDebug(RuntimeTypeHandle rtth)
        {
            string result;

            if (RuntimeAugments.IsGenericType(rtth))
            {
                RuntimeTypeHandle openTypeDef;
                RuntimeTypeHandle[] typeArgumentsHandles;
                if (TryGetOpenTypeDefinition(rtth, out openTypeDef, out typeArgumentsHandles))
                {
                    result = GetTypeNameDebug(openTypeDef) + "<";
                    for (int i = 0; i < typeArgumentsHandles.Length; i++)
                        result += (i == 0 ? "" : ",") + GetTypeNameDebug(typeArgumentsHandles[i]);
                    return result + ">";
                }
            }
            else
            {
                if (TryGetMetadataNameForRuntimeTypeHandle(rtth, out result))
                    return result;
            }

            result = "EEType:0x";
            ulong num = (ulong)RuntimeAugments.GetPointerFromTypeHandle(rtth);

            int shift = IntPtr.Size * 8;
            const string HexDigits = "0123456789ABCDEF";
            while (shift > 0)
            {
                shift -= 4;
                int digit = (int)((num >> shift) & 0xF);
                result += HexDigits[digit];
            } 
            return result;
        }
#endif

        private bool TryGetOpenTypeDefinition(RuntimeTypeHandle typeHandle, out RuntimeTypeHandle typeDefHandle, out RuntimeTypeHandle[] typeArgumentsHandles)
        {
            if (RuntimeAugments.IsGenericType(typeHandle))
                return TryGetConstructedGenericTypeComponents(typeHandle, out typeDefHandle, out typeArgumentsHandles);

            typeDefHandle = typeHandle;
            typeArgumentsHandles = null;
            return true;
        }

        private RuntimeTypeHandle GetTypeDefinition(RuntimeTypeHandle typeHandle)
        {
            RuntimeTypeHandle[] typeArgumentsHandles;
            RuntimeTypeHandle result;
            bool success = TryGetOpenTypeDefinition(typeHandle, out result, out typeArgumentsHandles);
            Debug.Assert(success);
            return result;
        }
        
        private static bool RuntimeTypeHandleIsNonDefault(RuntimeTypeHandle runtimeTypeHandle)
        {
            return ((IntPtr)RuntimeAugments.GetPointerFromTypeHandle(runtimeTypeHandle)) != IntPtr.Zero;
        }

        internal static unsafe uint RuntimeTypeHandleToRva(ref IntPtr moduleHandle, RuntimeTypeHandle runtimeTypeHandle)
        {
            Debug.Assert(moduleHandle.ToPointer() < (RuntimeAugments.GetPointerFromTypeHandle(runtimeTypeHandle)).ToPointer());
            return (uint)((byte*)(RuntimeAugments.GetPointerFromTypeHandle(runtimeTypeHandle)) - (byte*)moduleHandle);
        }

        internal static unsafe RuntimeTypeHandle RvaToRuntimeTypeHandle(IntPtr moduleHandle, uint rva)
        {
            if ((rva & 0x80000000) != 0)
            {
                return RuntimeAugments.CreateRuntimeTypeHandle(*(IntPtr*)((byte*)moduleHandle.ToPointer() + (rva & ~0x80000000)));
            }
            return RuntimeAugments.CreateRuntimeTypeHandle((IntPtr)((byte*)moduleHandle.ToPointer() + rva));
        }

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

        private static unsafe IntPtr RvaToGenericDictionary(IntPtr moduleHandle, uint rva)
        {
            // Generic dictionaries may be imported as well. As with types, this is indicated by the high bit set
            if ((rva & 0x80000000) != 0)
                return *((IntPtr*)((byte*)moduleHandle + (rva & ~0x80000000)));
            else
                return (IntPtr)((byte*)moduleHandle + rva);
        }

        /// <summary>
        /// Resolve a given 32-bit integer (staticFieldRVA) representing a static field address. 
        /// For "local" static fields residing in the module given by moduleHandle, staticFieldRVA
        /// directly contains the RVA of the static field. For remote static fields residing in other
        /// modules, staticFieldRVA has the highest bit set (FieldAccessFlags.RemoteStaticFieldRVA)
        /// and it contains the RVA of a RemoteStaticFieldDescriptor structure residing in the module
        /// given by moduleHandle that holds a pointer to the indirection cell
        /// of the remote static field and its offset within the cell.
        /// </summary>
        /// <param name="moduleHandle">Reference module handle used for static field lookup</param>
        /// <param name="staticFieldRVA">
        /// RVA of static field for local fields; for remote fields, RVA of a RemoteStaticFieldDescriptor
        /// structure for the field or-ed with the FieldAccessFlags.RemoteStaticFieldRVA bit
        /// </param>
        private static unsafe IntPtr RvaToNonGenericStaticFieldAddress(IntPtr moduleHandle, int staticFieldRVA)
        {
            IntPtr staticFieldAddress;

            if ((staticFieldRVA & FieldAccessFlags.RemoteStaticFieldRVA) != 0)
            {
                RemoteStaticFieldDescriptor * descriptor = (RemoteStaticFieldDescriptor *)(moduleHandle +
                   (staticFieldRVA & ~FieldAccessFlags.RemoteStaticFieldRVA));
                staticFieldAddress = *descriptor->IndirectionCell + descriptor->Offset;
            }
            else
                staticFieldAddress = (IntPtr)(moduleHandle + staticFieldRVA);

            return staticFieldAddress;
        }

        internal unsafe MetadataReader GetMetadataReaderForModule(IntPtr module)
        {
            MetadataReader reader;
            if (_moduleToMetadataReader.TryGetValue(module, out reader))
                return reader;
            return null;
        }

        private unsafe IntPtr GetModuleForMetadataReader(MetadataReader reader)
        {
            foreach (var kvp in _moduleToMetadataReader)
            {
                if (kvp.Value == reader)
                {
                    return kvp.Key;
                }
            }

            // We should never have a reader that is not associated with a module (where does it come from?!)
            Debug.Assert(false);
            return IntPtr.Zero;
        }

        private unsafe static NativeReader GetNativeReaderForBlob(IntPtr module, ReflectionMapBlob blob)
        {
            NativeReader reader;
            if (TryGetNativeReaderForBlob(module, blob, out reader))
            {
                return reader;
            }
            
            Debug.Assert(false);
            return default(NativeReader);
        }

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

        private bool TryGetDiagnosticNameForRuntimeTypeHandle(RuntimeTypeHandle rtth, out string name)
        {
            // This API is not designed to decompose generic types into components since the consumers already do this.
            Debug.Assert(!RuntimeAugments.IsGenericType(rtth));

            MetadataReader reader;
            TypeReferenceHandle typeRefHandle;

            if (TryGetTypeReferenceForNamedType(rtth, out reader, out typeRefHandle))
            {
                name = typeRefHandle.GetFullName(reader);
                return true;
            }

            name = null;
            return false;
        }

        public sealed override bool TryGetMetadataNameForRuntimeTypeHandle(RuntimeTypeHandle rtth, out string name)
        {
            // This API is not designed to decompose generic types into components since the consumers already do this
            Debug.Assert(!RuntimeAugments.IsGenericType(rtth));

            MetadataReader reader;
            TypeDefinitionHandle typeDefHandle;

            // Check if we have metadata.
            if (TryGetMetadataForNamedType(rtth, out reader, out typeDefHandle))
            {
                name = typeDefHandle.GetFullName(reader);
                return true;
            }

            // No metadata, but maybe the diagnostic infrastructure has a name
            return TryGetDiagnosticNameForRuntimeTypeHandle(rtth, out name);
        }

        //
        // Return the metadata handle for a TypeDef if the pay-for-policy enabled this type as browsable. This is used to obtain name and other information for types
        // obtained via typeof() or Object.GetType(). This can include generic types (Foo<>) (not to be confused with generic instances of Foo<>).
        //
        // Preconditions:
        //    runtimeTypeHandle is a typedef (not a constructed type such as an array or generic instance.)
        //
        public unsafe sealed override bool TryGetMetadataForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeDefinitionHandle typeDefHandle)
        {
            metadataReader = null;
            typeDefHandle = default(TypeDefinitionHandle);

            // Iterate over all modules, starting with the module that defines the EEType
            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle)))
            {
                MetadataTable mapTable = new MetadataTable(moduleHandle, ReflectionMapBlob.TypeMap, sizeof(TypeMapEntry));
                foreach (var ptrEntry in mapTable)
                {
                    var pCurrentEntry = (TypeMapEntry*)ptrEntry;
                    RuntimeTypeHandle entryTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(pCurrentEntry->EEType);
                    if (entryTypeHandle.Equals(runtimeTypeHandle) && pCurrentEntry->TypeDefinitionHandle.IsTypeDefinitionHandle())
                    {
                        metadataReader = GetMetadataReaderForModule(moduleHandle);
                        typeDefHandle = pCurrentEntry->TypeDefinitionHandle.AsTypeDefinitionHandle();
                        return true;
                    }
                }
            }

            return false;
        }

        //
        // Return true for a TypeDef if the policy has decided this type is blocked from reflection.
        //
        // Preconditions:
        //    runtimeTypeHandle is a typedef or a generic type instance (not a constructed type such as an array)
        //
        public unsafe sealed override bool IsReflectionBlocked(RuntimeTypeHandle runtimeTypeHandle)
        {
            return false;
#if false
            // For generic types, use the generic type definition
            runtimeTypeHandle = GetTypeDefinition(runtimeTypeHandle);

            var moduleHandle = RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle);

            NativeReader blockedReflectionReader = GetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.BlockReflectionTypeMap);
            NativeParser blockedReflectionParser = new NativeParser(blockedReflectionReader, 0);
            NativeHashtable blockedReflectionHashtable = new NativeHashtable(blockedReflectionParser);
            ExternalReferencesTable externalReferences = new ExternalReferencesTable(moduleHandle, ReflectionMapBlob.CommonFixupsTable);

            int hashcode = runtimeTypeHandle.GetHashCode();
            var lookup = blockedReflectionHashtable.Lookup(hashcode);
            NativeParser entryParser;
            while (!(entryParser = lookup.GetNext()).IsNull)
            {
                RuntimeTypeHandle entryType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!entryType.Equals(runtimeTypeHandle))
                    continue;

                // Entry found, must be blocked
                return true;
            }
            // Entry not found, must not be blocked
            return false;
#endif
        }


        //
        // Return the RuntimeTypeHandle for the named type described in metadata. This is used to implement the Create and Invoke
        // apis for types.
        //
        // Preconditions:
        //    metadataReader + typeDefHandle  - a valid metadata reader + typeDefinitionHandle where "metadataReader" is one
        //                                      of the metadata readers returned by ExecutionEnvironment.MetadataReaders.
        //
        // Note: Although this method has a "bool" return value like the other mapping table accessors, the Project N pay-for-play design 
        // guarantees that any type enabled for metadata also has a RuntimeTypeHandle underneath.
        //
        public unsafe sealed override bool TryGetNamedTypeForMetadata(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, out RuntimeTypeHandle runtimeTypeHandle)
        {
            IntPtr moduleHandle = GetModuleForMetadataReader(metadataReader);

            MetadataTable mapTable = new MetadataTable(moduleHandle, ReflectionMapBlob.TypeMap, sizeof(TypeMapEntry));
            foreach (var ptrEntry in mapTable)
            {
                TypeMapEntry* pCurrentEntry = (TypeMapEntry*)ptrEntry;
                if (pCurrentEntry->TypeDefinitionHandle == typeDefHandle.AsInt())
                {
                    runtimeTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(pCurrentEntry->EEType);
                    return true;
                }
            }

            runtimeTypeHandle = default(RuntimeTypeHandle);
            return false;
        }

        //
        // Return the metadata handle for a TypeRef if this type was referenced indirectly by other type that pay-for-play has denoted as browsable
        // (for example, as part of a method signature.)
        //
        // This is only used in "debug" builds to provide better MissingMetadataException diagnostics. 
        //
        // Preconditions:
        //    runtimeTypeHandle is a typedef (not a constructed type such as an array or generic instance.)
        //
        public unsafe sealed override bool TryGetTypeReferenceForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeReferenceHandle typeRefHandle)
        {
            // Iterate over all modules, starting with the module that defines the EEType
            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle)))
            {
                MetadataTable mapTable = new MetadataTable(moduleHandle, ReflectionMapBlob.TypeMap, sizeof(TypeMapEntry));
                foreach (var ptrEntry in mapTable)
                {
                    var pCurrentEntry = (TypeMapEntry*)ptrEntry;
                    RuntimeTypeHandle entryTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(pCurrentEntry->EEType);
                    if (entryTypeHandle.Equals(runtimeTypeHandle) && pCurrentEntry->TypeDefinitionHandle.IsTypeReferenceHandle())
                    {
                        metadataReader = GetMetadataReaderForModule(moduleHandle);
                        typeRefHandle = pCurrentEntry->TypeDefinitionHandle.AsTypeReferenceHandle();
                        return true;
                    }
                }
            }

            metadataReader = null;
            typeRefHandle = default(TypeReferenceHandle);

            return false;
        }

        //
        // Return the RuntimeTypeHandle for the named type referenced by another type that pay-for-play denotes as browsable (for example,
        // in a member signature.) Typically, the type itself is *not* browsable (or it would have appeared in the TypeDef table.)
        //
        // This is used to ensure that we can produce a Type object if requested and that it match up with the analogous
        // Type obtained via typeof().
        // 
        //
        // Preconditions:
        //    metadataReader + typeRefHandle  - a valid metadata reader + typeReferenceHandle where "metadataReader" is one
        //                                      of the metadata readers returned by ExecutionEnvironment.MetadataReaders.
        //
        // Note: Although this method has a "bool" return value like the other mapping table accessors, the Project N pay-for-play design 
        // guarantees that any type that has a metadata TypeReference to it also has a RuntimeTypeHandle underneath.
        //
        public unsafe sealed override bool TryGetNamedTypeForTypeReference(MetadataReader metadataReader, TypeReferenceHandle typeRefHandle, out RuntimeTypeHandle runtimeTypeHandle)
        {
            IntPtr moduleHandle = GetModuleForMetadataReader(metadataReader);
            MetadataTable mapTable = new MetadataTable(moduleHandle, ReflectionMapBlob.TypeMap, sizeof(TypeMapEntry));
            foreach (var ptrEntry in mapTable)
            {
                TypeMapEntry* pCurrentEntry = (TypeMapEntry*)ptrEntry;
                if (pCurrentEntry->TypeDefinitionHandle == typeRefHandle.AsInt())
                {
                    runtimeTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(pCurrentEntry->EEType);
                    return true;
                }
            }

            runtimeTypeHandle = default(RuntimeTypeHandle);
            return false;
        }

        //
        // Given a RuntimeTypeHandle for any type E, return a RuntimeTypeHandle for type E[], if the pay for play policy denotes E[] as browsable. This is used to
        // implement Array.CreateInstance().
        //
        // Preconditions:
        //     elementTypeHandle is a valid RuntimeTypeHandle.
        //
        // This is not equivalent to calling TryGetMultiDimTypeForElementType() with a rank of 1!
        //
        public unsafe sealed override bool TryGetArrayTypeForElementType(RuntimeTypeHandle elementTypeHandle, out RuntimeTypeHandle arrayTypeHandle)
        {
            if (RuntimeAugments.IsUnmanagedPointerType(elementTypeHandle))
            {
                throw new PlatformNotSupportedException(SR.PlatformNotSupported_PointerArray);
            }

            if (RuntimeAugments.IsGenericTypeDefinition(elementTypeHandle))
            {
                throw new NotSupportedException(SR.NotSupported_OpenType);
            }

            // TODO: DYNAMIC ARRAYS - disable ArrayMap blob when we enable dynamic array creation for non-shareable arrays, like int[] or char[].
            // Today, we can't do it because we don't have valid array type templates that we can clone to dynamically create new array types for those non-shareable arrays.
            if (!RuntimeAugments.IsDynamicType(elementTypeHandle))
            {
                arrayTypeHandle = new RuntimeTypeHandle();

                // Note: ReflectionMapBlob.ArrayMap may not exist in the module that contains the element type.
                // So we must enumerate all loaded modules in order to find ArrayMap and the array type for
                // the given element.

                foreach (IntPtr moduleHandle in ModuleList.Enumerate())
                {
                    // Check whether this module has an array map. If it does then find an array entry
                    // with a matching element type.
                    MetadataTable arrayMap = new MetadataTable(moduleHandle, ReflectionMapBlob.ArrayMap, sizeof(ArrayMapEntry));
                    foreach (var ptrEntry in arrayMap)
                    {
                        var pCurrentEntry = (ArrayMapEntry*)ptrEntry;
                        RuntimeTypeHandle candidateElementTypeHandle;

                        candidateElementTypeHandle = RvaToRuntimeTypeHandle(moduleHandle, pCurrentEntry->ElementEETypeRva);
                        if (candidateElementTypeHandle.Equals(elementTypeHandle))
                        {
                            arrayTypeHandle = RvaToRuntimeTypeHandle(moduleHandle, pCurrentEntry->ArrayEETypeRva);
                            return true;
                        }
                    }
                }
            }

            // Array not found in the ArrayMap blob: attempt to dynamically create a new one:
            throw new NotImplementedException();
        }

        //
        // Given a RuntimeTypeHandle for any array type E[], return a RuntimeTypeHandle for type E, if the pay for play policy denoted E[] as browsable. 
        //
        // Preconditions:
        //      arrayTypeHandle is a valid RuntimeTypeHandle of type array.
        //
        // This is not equivalent to calling TryGetMultiDimTypeElementType() with a rank of 1!
        //
        public unsafe sealed override bool TryGetArrayTypeElementType(RuntimeTypeHandle arrayTypeHandle, out RuntimeTypeHandle elementTypeHandle)
        {
            elementTypeHandle = RuntimeAugments.GetRelatedParameterTypeHandle(arrayTypeHandle);
            return true;
        }


        //
        // Given a RuntimeTypeHandle for any type E, return a RuntimeTypeHandle for type E[,,], if the pay for policy denotes E[,,] as browsable. This is used to
        // implement Type.MakeArrayType(Type, int).
        //
        // Preconditions:
        //     elementTypeHandle is a valid RuntimeTypeHandle.
        //
        // Calling this with rank 1 is not equivalent to calling TryGetArrayTypeForElementType()! 
        //
        public unsafe sealed override bool TryGetMultiDimArrayTypeForElementType(RuntimeTypeHandle elementTypeHandle, int rank, out RuntimeTypeHandle arrayTypeHandle)
        {
            Debug.Assert(rank > 0);

            arrayTypeHandle = new RuntimeTypeHandle();

            RuntimeTypeHandle mdArrayTypeHandle;
            if (!RuntimeAugments.GetMdArrayRankTypeHandleIfSupported(rank, out mdArrayTypeHandle))
                return false;

            // We can call directly into the type loader, bypassing the constraint validator because we know
            // MDArrays have no constraints. This also prevents us from hitting a MissingMetadataException
            // due to MDArray not being metadata enabled.
            throw new NotImplementedException();
        }

        //
        // Given a RuntimeTypeHandle for any array type E[,,], return a RuntimeTypeHandle for type E, if the pay-for-play policy denotes E[,,] as browsable. 
        // It is used to implement Type.GetElementType().
        //
        // Preconditions:
        //      arrayTypeHandle is a valid RuntimeTypeHandle of type array.
        //
        // Calling this with rank 1 is not equivalent to calling TryGetArrayTypeElementType()! 
        //
        public unsafe sealed override bool TryGetMultiDimArrayTypeElementType(RuntimeTypeHandle arrayTypeHandle, int rank, out RuntimeTypeHandle elementTypeHandle)
        {
            Debug.Assert(rank > 0);

            elementTypeHandle = new RuntimeTypeHandle();

            RuntimeTypeHandle expectedMdArrayTypeHandle;
            if (!RuntimeAugments.GetMdArrayRankTypeHandleIfSupported(rank, out expectedMdArrayTypeHandle))
                return false;
            RuntimeTypeHandle actualMdArrayTypeHandle;
            RuntimeTypeHandle[] elementTypeHandles;
            if (!TryGetConstructedGenericTypeComponents(arrayTypeHandle, out actualMdArrayTypeHandle, out elementTypeHandles))
                return false;
            if (!(actualMdArrayTypeHandle.Equals(expectedMdArrayTypeHandle)))
                return false;
            if (elementTypeHandles.Length != 1)
                return false;
            elementTypeHandle = elementTypeHandles[0];
            return true;
        }

        //
        // Given a RuntimeTypeHandle for any type E, return a RuntimeTypeHandle for type E*, if the pay-for-play policy denotes E* as browsable. This is used to
        // ensure that "typeof(E*)" and "typeof(E).MakePointerType()" returns the same Type object.
        //
        // Preconditions:
        //     targetTypeHandle is a valid RuntimeTypeHandle.
        //
        public unsafe sealed override bool TryGetPointerTypeForTargetType(RuntimeTypeHandle targetTypeHandle, out RuntimeTypeHandle pointerTypeHandle)
        {
            throw new NotImplementedException();
        }

        //
        // Given a RuntimeTypeHandle for any pointer type E*, return a RuntimeTypeHandle for type E, if the pay-for-play policy denotes E* as browsable. 
        // This is used to implement Type.GetElementType() for pointers.
        //
        // Preconditions:
        //      pointerTypeHandle is a valid RuntimeTypeHandle of type pointer.
        //
        public unsafe sealed override bool TryGetPointerTypeTargetType(RuntimeTypeHandle pointerTypeHandle, out RuntimeTypeHandle targetTypeHandle)
        {
            targetTypeHandle = RuntimeAugments.GetRelatedParameterTypeHandle(pointerTypeHandle);
            return true;
        }

        //
        // Given a RuntimeTypeHandle for any closed generic instance G<T1,T2,...>, return the RuntimeTypeHandles for G, T1 and T2, if the pay-for-play
        // policy denotes G<T1,T2,...> as browsable. This is used to implement Type.GetGenericTypeDefinition() and Type.GenericTypeArguments.
        //
        // Preconditions: 
        //      runtimeTypeHandle is a valid RuntimeTypeHandle for a generic instance.
        //
        public unsafe sealed override bool TryGetConstructedGenericTypeComponents(RuntimeTypeHandle runtimeTypeHandle, out RuntimeTypeHandle genericTypeDefinitionHandle, out RuntimeTypeHandle[] genericTypeArgumentHandles)
        {
            throw new NotImplementedException();
        }

        //
        // Given a RuntimeTypeHandle for any closed generic instance G<T1,T2,...>, return the RuntimeTypeHandles for G, T1 and T2. This is the
        // "diagnostic" version which only returns useful information in "debug" builds. Unlike the normal version, its success is indepedent
        // of the type's inclusion in the pay-for-play policy as its very purpose to create useful MissingMetadataExceptions when a type
        // isn't in the policy.
        //
        // Preconditions: 
        //      runtimeTypeHandle is a valid RuntimeTypeHandle for a generic instance.
        //
        public unsafe bool TryGetConstructedGenericTypeComponentsDiag(RuntimeTypeHandle runtimeTypeHandle, out RuntimeTypeHandle genericTypeDefinitionHandle, out RuntimeTypeHandle[] genericTypeArgumentHandles)
        {
            throw new NotImplementedException();
        }

        //
        // Given a RuntimeTypeHandle for a generic type G and a set of RuntimeTypeHandles T1, T2.., return the RuntimeTypeHandle for the generic
        // instance G<T1,T2...> if the pay-for-play policy denotes G<T1,T2...> as browsable. This is used to implement Type.MakeGenericType().
        //
        // Preconditions:
        //      runtimeTypeDefinitionHandle is a valid RuntimeTypeHandle for a generic type.
        //      genericTypeArgumentHandles is an array of valid RuntimeTypeHandles.
        //
        public unsafe sealed override bool TryGetConstructedGenericTypeForComponents(RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles, out RuntimeTypeHandle runtimeTypeHandle)
        {
            throw new NotImplementedException();
        }

        public sealed override MethodInvoker TryGetMethodInvoker(MetadataReader reader, RuntimeTypeHandle declaringTypeHandle, MethodHandle methodHandle, RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
            if (RuntimeAugments.IsNullable(declaringTypeHandle))
                return new NullableInstanceMethodInvoker(reader, methodHandle, declaringTypeHandle, null);
            else if (declaringTypeHandle.Equals(typeof(String).TypeHandle))
            {
                Method method = methodHandle.GetMethod(reader);
                MethodAttributes methodAttributes = method.Flags;
                if (((method.Flags & MethodAttributes.MemberAccessMask) == MethodAttributes.Public) &&
                    ((method.Flags & MethodAttributes.SpecialName) == MethodAttributes.SpecialName) &&
                    (method.Name.GetConstantStringValue(reader).Value == ".ctor"))
                {
                    return new StringConstructorMethodInvoker(reader, methodHandle);
                }
            }
            else if (declaringTypeHandle.Equals(typeof(IntPtr).TypeHandle) || declaringTypeHandle.Equals(typeof(UIntPtr).TypeHandle))
            {
                Method method = methodHandle.GetMethod(reader);
                MethodAttributes methodAttributes = method.Flags;
                if (((method.Flags & MethodAttributes.MemberAccessMask) == MethodAttributes.Public) &&
                    ((method.Flags & MethodAttributes.SpecialName) == MethodAttributes.SpecialName) &&
                    (method.Name.GetConstantStringValue(reader).Value == ".ctor"))
                {
                    return new IntPtrConstructorMethodInvoker(reader, methodHandle);
                }
            }

            MethodInvokeInfo methodInvokeInfo = null;
            foreach (var module in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(declaringTypeHandle)))
            {
#if GENERICS_FORCE_USG
                // Stress mode to force the usage of universal canonical method targets for reflection invokes.
                // It is recommended to use "/SharedGenericsMode GenerateAllUniversalGenerics" NUTC command line argument when
                // compiling the application in order to effectively use the GENERICS_FORCE_USG mode.

                // If we are just trying to invoke a non-generic method on a non-generic type, we won't force the universal lookup
                if (!RuntimeAugments.IsGenericType(declaringTypeHandle) && (genericMethodTypeArgumentHandles == null || genericMethodTypeArgumentHandles.Length == 0))
                    methodInvokeInfo = TryGetDynamicMethodInvokeInfo(module, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles, CanonicalFormKind.Specific);
                else
                    methodInvokeInfo = TryGetDynamicMethodInvokeInfo(module, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles, CanonicalFormKind.Universal);
#else
                methodInvokeInfo = TryGetDynamicMethodInvokeInfo(module, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles, CanonicalFormKind.Specific);

                // If we failed to get a MethodInvokeInfo for an exact method, or a canonically equivalent method, check if there is a universal canonically
                // equivalent entry that could be used (it will be much slower, and require a calling convention converter)
                if (methodInvokeInfo == null)
                    methodInvokeInfo = TryGetDynamicMethodInvokeInfo(module, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles, CanonicalFormKind.Universal);
#endif

                if (methodInvokeInfo != null)
                    break;
            }

            if (methodInvokeInfo == null)
                return null;

            return MethodInvokerWithMethodInvokeInfo.CreateMethodInvoker(reader, declaringTypeHandle, methodHandle, methodInvokeInfo);
        }

        // Get the pointers necessary to call a dynamic method invocation function
        //
        // This is either a function pointer to call, or a function pointer and template token.
        private unsafe void GetDynamicMethodInvokeMethodInfo(IntPtr moduleHandle, uint cookie, RuntimeTypeHandle[] argHandles,
            ref ExternalReferencesTable extRefs, out IntPtr dynamicInvokeMethod, out IntPtr dynamicInvokeMethodGenericDictionary)
        {
            if ((cookie & 1) == 1)
            {
                throw new NotImplementedException();
            }
            else
            {
                // Nongeneric DynamicInvoke method. This is used to DynamicInvoke methods that have parameters that
                // cannot be shared (or if there are no parameters to begin with).
                dynamicInvokeMethod = RvaToFunctionPointer(moduleHandle, extRefs.GetRvaFromIndex(cookie >> 1));
                dynamicInvokeMethodGenericDictionary = IntPtr.Zero;
            }
        }

        private RuntimeTypeHandle[] GetDynamicInvokeInstantiationArguments(MethodBase reflectionMethodBase)
        {
            // The DynamicInvoke method is a generic method with arguments that match the arguments of the target method.
            // Prepare the list of arguments so that we can use it to instantiate the method.

            MethodParametersInfo methodParamsInfo = new MethodParametersInfo(reflectionMethodBase);
            LowLevelList<RuntimeTypeHandle> dynamicInvokeMethodGenArguments = methodParamsInfo.ParameterTypeHandles;

            // This is either a constructor ("returns" void) or an instance method
            MethodInfo reflectionMethodInfo = reflectionMethodBase as MethodInfo;
            Type returnType = reflectionMethodInfo != null ? reflectionMethodInfo.ReturnType : CommonRuntimeTypes.Void;

            // Only use the return type if it's not void
            if (!returnType.Equals(CommonRuntimeTypes.Void))
                dynamicInvokeMethodGenArguments.Add(returnType.TypeHandle);

            return dynamicInvokeMethodGenArguments.ToArray();
        }

        static RuntimeTypeHandle[] GetTypeSequence(ref ExternalReferencesTable extRefs, ref NativeParser parser)
        {
            uint count = parser.GetUnsigned();
            RuntimeTypeHandle[] result = new RuntimeTypeHandle[count];
            for (uint i = 0; i < count; i++)
            {
                result[i] = extRefs.GetRuntimeTypeHandleFromIndex(parser.GetUnsigned());
            }
            return result;
        }

        static bool SequenceEqual<T>(T[] seq1, T[] seq2)
        {
            if (seq1.Length != seq2.Length)
                return false;
            for (int i = 0; i < seq1.Length; i++)
                if (!seq1[i].Equals(seq2[i]))
                    return false;
            return true;
        }

        private bool IsMatchingMethodEntry(MethodBase methodToFind, MethodNameAndSignature nameAndSig)
        {
            if (nameAndSig.Name != methodToFind.Name)
                return false;

            return CompareNativeLayoutMethodSignatureToMethodInfo(nameAndSig.Signature, methodToFind);
        }

        private IntPtr TryGetVirtualResolveData(IntPtr moduleHandle, RuntimeTypeHandle methodHandleDeclaringType, MethodHandle methodHandle, RuntimeTypeHandle[] genericArgs)
        {
            throw new NotImplementedException();
        }

        private unsafe MethodInvokeInfo TryGetDynamicMethodInvokeInfo(IntPtr mappingTableModule, RuntimeTypeHandle declaringTypeHandle, MethodHandle methodHandle, RuntimeTypeHandle[] genericMethodTypeArgumentHandles, CanonicalFormKind canonFormKind)
        {
            NativeReader invokeMapReader;
            if (!TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.InvokeMap, out invokeMapReader))
            {
                return null;
            }

            NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
            NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);

            CanonicallyEquivalentEntryLocator canonHelper = new CanonicallyEquivalentEntryLocator(declaringTypeHandle, canonFormKind);
            ExternalReferencesTable externalReferences = new ExternalReferencesTable(mappingTableModule, ReflectionMapBlob.CommonFixupsTable);

            MethodBase methodInfo = ReflectionCoreExecution.ExecutionDomain.GetMethod(declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles);

            // Validate constraints first. This is potentially useless work if the method already exists, but it prevents bad
            // inputs to reach the type loader (we don't have support to e.g. represent pointer types within the type loader)
            if (genericMethodTypeArgumentHandles != null && genericMethodTypeArgumentHandles.Length > 0)
                ConstraintValidator.EnsureSatisfiesClassConstraints((MethodInfo)methodInfo);

            MetadataReader mdReader;
            TypeDefinitionHandle typeDefHandleUnused;
            bool success = TryGetMetadataForNamedType(GetTypeDefinition(declaringTypeHandle), out mdReader, out typeDefHandleUnused);
            Debug.Assert(success);

            var lookup = invokeHashtable.Lookup(canonHelper.LookupHashCode);
            var entryData = new InvokeMapEntryDataEnumerator(declaringTypeHandle, genericMethodTypeArgumentHandles, canonFormKind, mappingTableModule, methodHandle, methodInfo);

            NativeParser entryParser;
            while (!(entryParser = lookup.GetNext()).IsNull)
            {
                entryData.GetNext(ref entryParser, ref externalReferences, canonHelper);

                if (!entryData.IsMatchingOrCompatibleEntry())
                    continue;

                IntPtr methodEntrypoint, rawMethodEntrypoint, dictionaryComponent;
                if (!entryData.GetMethodEntryPoint(mdReader, declaringTypeHandle, out methodEntrypoint, out dictionaryComponent, out rawMethodEntrypoint))
                    return null;        // Error

                if (methodEntrypoint != rawMethodEntrypoint && !FunctionPointerOps.IsGenericMethodPointer(methodEntrypoint))
                {
                    // Keep track of the raw method entrypoints for the cases where they get wrapped into a calling convention converter thunk.
                    // This is needed for reverse lookups, like in TryGetMethodForOriginalLdFtnResult
                    Debug.Assert(canonFormKind == CanonicalFormKind.Universal);
                    lock (_callConverterWrappedMethodEntrypoints)
                    {
                        _callConverterWrappedMethodEntrypoints.LookupOrAdd(methodEntrypoint, new MethodTargetAndDictionary
                        {
                            TargetPointer = rawMethodEntrypoint,
                            DictionaryPointer = dictionaryComponent
                        });
                    }
                }

                string defaultValueString = entryData.GetDefaultValueString(ref externalReferences);

                RuntimeTypeHandle[] dynInvokeMethodArgs = GetDynamicInvokeInstantiationArguments(methodInfo);

                IntPtr dynamicInvokeMethod;
                IntPtr dynamicInvokeMethodGenericDictionary;
                GetDynamicMethodInvokeMethodInfo(mappingTableModule, entryData._dynamicInvokeCookie, dynInvokeMethodArgs, ref externalReferences, out dynamicInvokeMethod, out dynamicInvokeMethodGenericDictionary);

                IntPtr resolver = IntPtr.Zero;
                if ((entryData._flags & InvokeTableFlags.HasVirtualInvoke) != 0)
                    resolver = TryGetVirtualResolveData(mappingTableModule, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles);

                var methodInvokeInfo = new MethodInvokeInfo
                {
                    LdFtnResult = methodEntrypoint,
                    DynamicInvokeMethod = dynamicInvokeMethod,
                    DynamicInvokeGenericDictionary = dynamicInvokeMethodGenericDictionary,
                    DefaultValueString = defaultValueString,
                    VirtualResolveData = resolver,
                };
                return methodInvokeInfo;
            }

            // Can't create a method invoker for the method
            return null;
        }

        private RuntimeTypeHandle GetExactDeclaringType(RuntimeTypeHandle dstType, RuntimeTypeHandle srcType)
        {
            // The fact that for generic types we rely solely on the template type in the mapping table causes
            // trouble for lookups from method pointer to the declaring type and method metadata handle.

            // Suppose we have following code:
            // class Base<T> { void Frob() { } }
            // class Derived<T> : Base<T> { }
            // Let's pick Base<object>, Derived<object> as the template.
            // Now if someone calls TryGetMethodForOriginalLdFtnResult with a pointer to the Frob method and a RuntimeTypeHandle
            // of the Derived<string> object instance, we are expected to return the metadata handle for Frob with *Base*<string>
            // as the declaring type. The table obviously only has an entry for Frob with Base<object>.

            // This method needs to return "true" and "Base<string>" for cases like this.

            RuntimeTypeHandle dstTypeDef = default(RuntimeTypeHandle);
            if (RuntimeAugments.IsGenericType(dstType))
            {
                RuntimeTypeHandle[] dstComponent;
                bool success = TryGetConstructedGenericTypeComponents(dstType, out dstTypeDef, out dstComponent);
                Debug.Assert(success);
            }

            while (!srcType.IsNull())
            {
                if (RuntimeAugments.IsAssignableFrom(dstType, srcType))
                {
                    return dstType;
                }

                if (!dstTypeDef.IsNull() && RuntimeAugments.IsGenericType(srcType))
                {
                    RuntimeTypeHandle srcTypeDef;
                    RuntimeTypeHandle[] srcComponents;
                    bool success = TryGetConstructedGenericTypeComponents(srcType, out srcTypeDef, out srcComponents);
                    Debug.Assert(success);

                    // Compare TypeDefs. We don't look at the generic components. We already know that the right type
                    // to return must be somewhere in the inheritance chain.
                    if (dstTypeDef.Equals(srcTypeDef))
                    {
                        // Return the *other* type handle since dstType is instantiated over different arguments
                        return srcType;
                    }
                }

                if (!RuntimeAugments.TryGetBaseType(srcType, out srcType))
                {
                    break;
                }
            }

            Debug.Assert(false);
            return default(RuntimeTypeHandle);
        }

        internal unsafe bool TryGetMethodForOriginalLdFtnResult(IntPtr originalLdFtnResult, ref RuntimeTypeHandle declaringTypeHandle, out MethodHandle methodHandle, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
            foreach (IntPtr module in ModuleList.Enumerate())
            {
                if (TryGetMethodForOriginalLdFtnResult_Inner(module, originalLdFtnResult, ref declaringTypeHandle, out methodHandle, out genericMethodTypeArgumentHandles))
                    return true;
            }
            methodHandle = default(MethodHandle);
            genericMethodTypeArgumentHandles = null;
            return false;
        }

        unsafe bool TryGetMethodForOriginalLdFtnResult_Inner(IntPtr mappingTableModule, IntPtr originalLdFtnResult, ref RuntimeTypeHandle declaringTypeHandle, out MethodHandle methodHandle, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
            NativeReader invokeMapReader;
            if (!TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.InvokeMap, out invokeMapReader))
            {
                genericMethodTypeArgumentHandles = null;
                methodHandle = default(MethodHandle);
                return false;
            }

            NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
            NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);

            ExternalReferencesTable externalReferences = new ExternalReferencesTable(mappingTableModule, ReflectionMapBlob.CommonFixupsTable);

            IntPtr canonOriginalLdFtnResult;
            IntPtr instantiationArgument;
            if (FunctionPointerOps.IsGenericMethodPointer(originalLdFtnResult))
            {
                GenericMethodDescriptor* realTargetData = FunctionPointerOps.ConvertToGenericDescriptor(originalLdFtnResult);
                canonOriginalLdFtnResult = RuntimeAugments.GetCodeTarget(realTargetData->MethodFunctionPointer);
                instantiationArgument = realTargetData->InstantiationArgument;
            }
            else
            {
                bool isCallConverterWrappedEntrypoint;
                MethodTargetAndDictionary callConverterWrappedEntrypoint;
                lock (_callConverterWrappedMethodEntrypoints)
                {
                    isCallConverterWrappedEntrypoint = _callConverterWrappedMethodEntrypoints.TryGetValue(originalLdFtnResult, out callConverterWrappedEntrypoint);
                }

                if (isCallConverterWrappedEntrypoint)
                {
                    canonOriginalLdFtnResult = callConverterWrappedEntrypoint.TargetPointer;
                    instantiationArgument = callConverterWrappedEntrypoint.DictionaryPointer;
                }
                else
                {
                    canonOriginalLdFtnResult = RuntimeAugments.GetCodeTarget(originalLdFtnResult);
                    instantiationArgument = IntPtr.Zero;
                }
            }

            var lookup = invokeHashtable.EnumerateAllEntries();
            NativeParser entryParser;
            while (!(entryParser = lookup.GetNext()).IsNull)
            {
                InvokeTableFlags entryFlags = (InvokeTableFlags)entryParser.GetUnsigned();

                // If the passed in method was a fat function pointer, but the entry in the mapping table doesn't need
                // an instantiation argument (or the other way around), trivially reject it.
                if ((instantiationArgument == IntPtr.Zero) != ((entryFlags & InvokeTableFlags.RequiresInstArg) == 0))
                    continue;

                bool hasEntrypoint = ((entryFlags & InvokeTableFlags.HasEntrypoint) != 0);
                if (!hasEntrypoint)
                    continue;

                uint entryMethodHandleOrNameAndSigRaw = entryParser.GetUnsigned();
                uint entryDeclaringTypeRaw = entryParser.GetUnsigned();

                IntPtr entryMethodEntrypoint = RvaToFunctionPointer(mappingTableModule, externalReferences.GetRvaFromIndex(entryParser.GetUnsigned()));
                
                entryParser.GetUnsigned(); // skip dynamic invoke cookie

                if (entryMethodEntrypoint != canonOriginalLdFtnResult)
                    continue;

                if ((entryFlags & InvokeTableFlags.RequiresInstArg) == 0 && declaringTypeHandle.IsNull())
                    declaringTypeHandle = externalReferences.GetRuntimeTypeHandleFromIndex(entryDeclaringTypeRaw);

                if ((entryFlags & InvokeTableFlags.IsGenericMethod) != 0)
                {
                    if ((entryFlags & InvokeTableFlags.RequiresInstArg) != 0)
                    {
                        throw new NotImplementedException();
                    }
                    else
                        genericMethodTypeArgumentHandles = GetTypeSequence(ref externalReferences, ref entryParser);
                }
                else
                {
                    genericMethodTypeArgumentHandles = null;
                    if ((entryFlags & InvokeTableFlags.RequiresInstArg) != 0)
                        declaringTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(instantiationArgument);
                }

                RuntimeTypeHandle entryType = externalReferences.GetRuntimeTypeHandleFromIndex(entryDeclaringTypeRaw);
                declaringTypeHandle = GetExactDeclaringType(entryType, declaringTypeHandle);

                if ((entryFlags & InvokeTableFlags.HasMetadataHandle) != 0)
                {
                    methodHandle = (((int)HandleType.Method << 24) | (int)entryMethodHandleOrNameAndSigRaw).AsMethodHandle();
                }
                else
                {
                    throw new NotImplementedException();
                }
                
                return true;
            }

            methodHandle = default(MethodHandle);
            genericMethodTypeArgumentHandles = null;

            return false;
        }

        public sealed override FieldAccessor TryGetFieldAccessor(RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle, FieldHandle fieldHandle)
        {
            foreach (var moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(declaringTypeHandle)))
            {
                FieldAccessor result = TryGetFieldAccessor_Inner(moduleHandle, declaringTypeHandle, fieldTypeHandle, fieldHandle, CanonicalFormKind.Specific);

                if (result == null && RuntimeAugments.IsDynamicType(declaringTypeHandle))
                    result = TryGetFieldAccessor_Inner(moduleHandle, declaringTypeHandle, fieldTypeHandle, fieldHandle, CanonicalFormKind.Universal);

                if (result != null)
                    return result;
            }

            return null;
        }

        private unsafe FieldAccessor TryGetFieldAccessor_Inner(IntPtr mappingTableModule, RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle, FieldHandle fieldHandle, CanonicalFormKind canonFormKind)
        {
            NativeReader fieldMapReader;
            if (!TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.FieldAccessMap, out fieldMapReader))
                return null;

            NativeParser fieldMapParser = new NativeParser(fieldMapReader, 0);
            NativeHashtable fieldHashtable = new NativeHashtable(fieldMapParser);

            ExternalReferencesTable externalReferences = new ExternalReferencesTable(mappingTableModule, ReflectionMapBlob.CommonFixupsTable);

            CanonicallyEquivalentEntryLocator canonWrapper = new CanonicallyEquivalentEntryLocator(declaringTypeHandle, canonFormKind);
            var lookup = fieldHashtable.Lookup(canonWrapper.LookupHashCode);

            bool isDynamicType = RuntimeAugments.IsDynamicType(declaringTypeHandle);
            string fieldName = null;

            NativeParser entryParser;
            while (!(entryParser = lookup.GetNext()).IsNull)
            {
                // Grammar of a hash table entry:
                // Flags + DeclaringType + MdHandle or Name + Cookie or Ordinal or Offset

                FieldTableFlags entryFlags = (FieldTableFlags)entryParser.GetUnsigned();

                if ((canonFormKind == CanonicalFormKind.Universal) != entryFlags.HasFlag(FieldTableFlags.IsUniversalCanonicalEntry))
                    continue;

                RuntimeTypeHandle entryDeclaringTypeHandle = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!entryDeclaringTypeHandle.Equals(declaringTypeHandle)
                    && !(isDynamicType && canonWrapper.IsCanonicallyEquivalent(entryDeclaringTypeHandle)))
                    continue;

                if (entryFlags.HasFlag(FieldTableFlags.HasMetadataHandle))
                {
                    FieldHandle entryFieldHandle = (((int)HandleType.Field << 24) | (int)entryParser.GetUnsigned()).AsFieldHandle();
                    if (!fieldHandle.Equals(entryFieldHandle))
                        continue;
                }
                else
                {
                    if (fieldName == null)
                    {
                        MetadataReader mdReader;
                        TypeDefinitionHandle typeDefHandleUnused;
                        bool success = TryGetMetadataForNamedType(GetTypeDefinition(declaringTypeHandle), out mdReader, out typeDefHandleUnused);
                        Debug.Assert(success);

                        fieldName = fieldHandle.GetField(mdReader).Name.GetString(mdReader);
                    }

                    string entryFieldName = entryParser.GetString();

                    if (fieldName != entryFieldName)
                        continue;
                }

                int cookieOrOffsetOrOrdinal = (int)entryParser.GetUnsigned();
                int fieldOffset;
                int fieldOffsetDelta = RuntimeAugments.IsValueType(declaringTypeHandle) ? IntPtr.Size : 0;

                if (canonFormKind == CanonicalFormKind.Universal)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    fieldOffset = (int)externalReferences.GetRvaFromIndex((uint)cookieOrOffsetOrOrdinal);
                }

                switch (entryFlags & FieldTableFlags.StorageClass)
                {
                    case FieldTableFlags.Instance:
                        return RuntimeAugments.IsValueType(fieldTypeHandle) ?
                            (FieldAccessor)new ValueTypeFieldAccessorForInstanceFields(fieldOffset + fieldOffsetDelta, declaringTypeHandle, fieldTypeHandle) :
                            (FieldAccessor)new ReferenceTypeFieldAccessorForInstanceFields(fieldOffset + fieldOffsetDelta, declaringTypeHandle, fieldTypeHandle);

                    case FieldTableFlags.Static:
                        {
                            IntPtr fieldAddress;

                            if (RuntimeAugments.IsGenericType(declaringTypeHandle))
                            {
                                throw new NotImplementedException();
                            }
                            else
                            {
                                Debug.Assert(canonFormKind != CanonicalFormKind.Universal);
                                fieldAddress = RvaToNonGenericStaticFieldAddress(mappingTableModule, fieldOffset);
                            }

                            return RuntimeAugments.IsValueType(fieldTypeHandle) ?
                                (FieldAccessor)new ValueTypeFieldAccessorForStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), fieldAddress, fieldTypeHandle) :
                                (FieldAccessor)new ReferenceTypeFieldAccessorForStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), fieldAddress, fieldTypeHandle);
                        }

                    case FieldTableFlags.ThreadStatic:
                        if (canonFormKind == CanonicalFormKind.Universal)
                        {
                            return RuntimeAugments.IsValueType(fieldTypeHandle) ?
                                (FieldAccessor)new ValueTypeFieldAccessorForUniversalThreadStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), declaringTypeHandle, fieldOffset, fieldTypeHandle) :
                                (FieldAccessor)new ReferenceTypeFieldAccessorForUniversalThreadStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), declaringTypeHandle, fieldOffset, fieldTypeHandle);
                        }
                        else
                        {
                            IntPtr fieldAddressCookie = RvaToNonGenericStaticFieldAddress(mappingTableModule, fieldOffset);

                            return RuntimeAugments.IsValueType(fieldTypeHandle) ?
                                (FieldAccessor)new ValueTypeFieldAccessorForThreadStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), declaringTypeHandle, fieldAddressCookie, fieldTypeHandle) :
                                (FieldAccessor)new ReferenceTypeFieldAccessorForThreadStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), declaringTypeHandle, fieldAddressCookie, fieldTypeHandle);
                        }
                }
            }

            return null;
        }

        internal static bool CompareNativeLayoutMethodSignatureToMethodInfo(IntPtr signature, MethodBase methodBase)
        {
            IntPtr moduleHandle = RuntimeAugments.GetModuleFromPointer(signature);
            NativeReader reader = GetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.NativeLayoutInfo);
            NativeParser parser = new NativeParser(reader, reader.AddressToOffset(signature));

            return SigComparer.CompareMethodSigWithMethodInfo(parser, methodBase);
        }

        private bool TryGetMetadataForTypeMethodNameAndSignature(RuntimeTypeHandle declaringTypeHandle, MethodNameAndSignature nameAndSignature, out MethodHandle methodHandle)
        {
            MetadataReader reader;
            TypeDefinitionHandle typeDefinitionHandle;
            RuntimeTypeHandle metadataLookupTypeHandle = declaringTypeHandle;
            methodHandle = default(MethodHandle);

            // For generic instantiations, get the declaring type def to do the metadata lookup with, as TryGetMetadataForNamedType expects
            // this as a pre-condition
            if (RuntimeAugments.IsGenericType(declaringTypeHandle))
            {
                RuntimeTypeHandle[] components;
                if (!TryGetConstructedGenericTypeComponents(declaringTypeHandle, out metadataLookupTypeHandle, out components))
                    return false;
            }

            if (!TryGetMetadataForNamedType(metadataLookupTypeHandle, out reader, out typeDefinitionHandle))
                return false;

            TypeDefinition typeDefinition = typeDefinitionHandle.GetTypeDefinition(reader);
            foreach (MethodHandle mh in typeDefinition.Methods)
            {
                Method method = mh.GetMethod(reader);
                if (method.Name.StringEquals(nameAndSignature.Name, reader))
                {
                    MethodBase methodInfo = ReflectionCoreExecution.ExecutionDomain.GetMethod(metadataLookupTypeHandle, mh, Array.Empty<RuntimeTypeHandle>());
                    if (CompareNativeLayoutMethodSignatureToMethodInfo(nameAndSignature.Signature, methodInfo))
                    {
                        methodHandle = mh;
                        return true;
                    }
                }
            }

            return false;
        }
        
        //
        // This resolves RuntimeMethodHandles for methods declared on non-generic types (declaringTypeHandle is an output of this method.)
        //
        public unsafe sealed override bool TryGetMethodFromHandle(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodHandle methodHandle, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
            throw new NotImplementedException();
        }

        //
        // This resolves RuntimeMethodHandles for methods declared on generic types (declaringTypeHandle is an input of this method.)
        //
        public sealed override bool TryGetMethodFromHandleAndType(RuntimeMethodHandle runtimeMethodHandle, RuntimeTypeHandle declaringTypeHandle, out MethodHandle methodHandle, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
            RuntimeTypeHandle dummy;
            return TryGetMethodFromHandle(runtimeMethodHandle, out dummy, out methodHandle, out genericMethodTypeArgumentHandles);
        }

        //
        // This resolves RuntimeFieldHandles for fields declared on non-generic types (declaringTypeHandle is an output of this method.)
        //
        public unsafe sealed override bool TryGetFieldFromHandle(RuntimeFieldHandle runtimeFieldHandle, out RuntimeTypeHandle declaringTypeHandle, out FieldHandle fieldHandle)
        {
            throw new NotImplementedException();
        }

        //
        // This resolves RuntimeFieldHandles for fields declared on generic types (declaringTypeHandle is an input of this method.)
        //
        public sealed override bool TryGetFieldFromHandleAndType(RuntimeFieldHandle runtimeFieldHandle, RuntimeTypeHandle declaringTypeHandle, out FieldHandle fieldHandle)
        {
            RuntimeTypeHandle dummy;
            return TryGetFieldFromHandle(runtimeFieldHandle, out dummy, out fieldHandle);
        }

        internal IntPtr TryGetDefaultConstructorForType(RuntimeTypeHandle runtimeTypeHandle)
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

            return IntPtr.Zero;
        }

        public IntPtr TryGetDefaultConstructorForTypeUsingLocator(object canonEquivalentEntryLocator)
        {
            CanonicallyEquivalentEntryLocator canonLocatorLocal = (CanonicallyEquivalentEntryLocator)canonEquivalentEntryLocator;
            foreach (IntPtr module in ModuleList.Enumerate())
            {
                IntPtr result = TryGetDefaultConstructorForType_Inner(module, ref canonLocatorLocal);

                if (result != IntPtr.Zero)
                    return result;
            }

            return IntPtr.Zero;
        }

        private unsafe IntPtr TryGetDefaultConstructorForType_Inner(IntPtr mappingTableModule, ref CanonicallyEquivalentEntryLocator canonHelper)
        {
            NativeReader invokeMapReader;
            if (TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.InvokeMap, out invokeMapReader))
            {
                NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
                NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);

                ExternalReferencesTable externalReferences = new ExternalReferencesTable(mappingTableModule, ReflectionMapBlob.CommonFixupsTable);

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

                    return RvaToFunctionPointer(mappingTableModule, externalReferences.GetRvaFromIndex(entryParser.GetUnsigned()));
                }
            }

            // If not found in the invoke map, try the default constructor map
            NativeReader defaultCtorMapReader;
            if (TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.DefaultConstructorMap, out defaultCtorMapReader))
            {
                NativeParser defaultCtorMapParser = new NativeParser(defaultCtorMapReader, 0);
                NativeHashtable defaultCtorHashtable = new NativeHashtable(defaultCtorMapParser);

                ExternalReferencesTable externalReferencesForDefaultCtorMap = new ExternalReferencesTable(mappingTableModule, ReflectionMapBlob.CommonFixupsTable);
                var lookup = defaultCtorHashtable.Lookup(canonHelper.LookupHashCode);
                NativeParser defaultCtorParser;
                while (!(defaultCtorParser = lookup.GetNext()).IsNull)
                {
                    RuntimeTypeHandle entryType = externalReferencesForDefaultCtorMap.GetRuntimeTypeHandleFromIndex(defaultCtorParser.GetUnsigned());
                    if (!canonHelper.IsCanonicallyEquivalent(entryType))
                        continue;

                    return RvaToFunctionPointer(mappingTableModule, externalReferencesForDefaultCtorMap.GetRvaFromIndex(defaultCtorParser.GetUnsigned()));
                }
            }
            return IntPtr.Zero;
        }

        internal unsafe IntPtr TryGetStaticClassConstructionContext(RuntimeTypeHandle typeHandle)
        {
            if (RuntimeAugments.HasCctor(typeHandle))
            {
                if (RuntimeAugments.IsDynamicType(typeHandle))
                {
                    // For dynamic types, its always possible to get the non-gc static data section directly.
                    byte* ptr = (byte*)*(IntPtr*)RuntimeAugments.GetNonGcStaticFieldData(typeHandle);

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

                    MetadataTable cctorContextTable = new MetadataTable(moduleHandle, ReflectionMapBlob.CCtorContextMap, sizeof(CctorContextEntry));
                    foreach (CctorContextEntry* pCurrentEntry in cctorContextTable)
                    {
                        RuntimeTypeHandle entryTypeHandle = RvaToRuntimeTypeHandle(moduleHandle, pCurrentEntry->EETypeRva);
                        if (typeHandle.Equals(entryTypeHandle))
                        {
                            byte* ptr = (byte*)moduleHandle + pCurrentEntry->CctorContextRva;

                            // what we have now is the base address of the non-gc statics of the type
                            // what we need is the cctor context, which is just before that
                            ptr = ptr - sizeof(System.Runtime.CompilerServices.StaticClassConstructionContext);

                            return (IntPtr)ptr;
                        }
                    }
                }
            }

            return IntPtr.Zero;
        }

        //
        // Return MetadataReaders for each loaded module that has metadata associated with it.
        //
        // - The returned list may vary at different times (if, for example, new modules registered with RH since the last call.)
        //
        // - However, once this method has created a MetadataReader for a specific module, it must always return the same allocated instance (even in the face of multiple threads
        //   racing on this api.) This is because there's no meaningful way to define Equals() on a MetadataReader, hence Reflection uses reference equality to
        //   determine whether two MetadataReaders represent the "same module." (and in turn, this affects what Assembly.Equals() returns to app code using Reflection.)
        //
        internal IEnumerable<MetadataReader> MetadataReaders
        {
            get
            {
                foreach (var kvp in _moduleToMetadataReader)
                {
                    yield return kvp.Value;
                }
            }
        }

        class ModuleList
        {
            private static volatile IntPtr[] s_moduleList;

            private IntPtr _preferredModule;

            public static ModuleList Enumerate(IntPtr preferredModule = default(IntPtr))
            {
                return new ModuleList { _preferredModule = preferredModule };
            }

            public ModuleEnumerator GetEnumerator()
            {
                return new ModuleEnumerator(s_moduleList, _preferredModule);
            }

            /// <summary>
            /// Add a module to the module list table.
            ///
            /// The lock in ExecutionEnvironmentImplementation.RegisterModule guarantees that this method
            /// never gets called concurrently i.e. there's always at most one thread updating the module list.
            /// </summary>
            /// <param name="moduleHandle">Handle of module to register</param>
            public static void RegisterModule(IntPtr moduleHandle)
            {
                int existingModuleCount = (s_moduleList != null ? s_moduleList.Length : 0);
                IntPtr[] newModuleList = new IntPtr[existingModuleCount + 1];
                if (s_moduleList != null)
                {
                    Array.Copy(s_moduleList, newModuleList, existingModuleCount);
                }
                newModuleList[existingModuleCount] = moduleHandle;
                s_moduleList = newModuleList;
            }

            public struct ModuleEnumerator
            {
                private int _currentIndex;
                private IntPtr _preferredModule;

                /// <summary>
                /// We capture a snapshot of the module list when the enumeration commences
                /// to prevent it from changing under our hands due to module registration
                /// in the middle of enumeration.
                /// </summary>
                private IntPtr[] _moduleList;

                /// <summary>
                /// Construct the module enumerator based on the module list and preferred module.
                /// The preferred module goes first in the enumeration (unless it's null) and acts
                /// as an accelerator in situations where a given module is most likely to match.
                /// </summary>
                /// <param name="moduleList">Module list to enumerate</param>
                /// <param name="preferredModule">Module to prioritize in the enumeration</param>
                public ModuleEnumerator(IntPtr[] moduleList, IntPtr preferredModule)
                {
                    _currentIndex = preferredModule != IntPtr.Zero ? -2 : -1;
                    _preferredModule = preferredModule;
                    _moduleList = moduleList;
                }

                public IntPtr Current
                {
                    get
                    {
                        if (_currentIndex >= 0)
                            return _moduleList[_currentIndex];
                        return _preferredModule;
                    }
                }

                public bool MoveNext()
                {
                    _currentIndex++;

                    if (_currentIndex >= 0
                        && _currentIndex < _moduleList.Length
                        && _moduleList[_currentIndex] == _preferredModule)
                        _currentIndex++;

                    return _currentIndex < _moduleList.Length;
                }
            }
        }

        struct MethodParametersInfo
        {
            MetadataReader _metadataReader;
            MethodBase _methodBase;
            MethodHandle _methodHandle;

            Handle[] _returnTypeAndParametersHandlesCache;
            Type[] _returnTypeAndParametersTypesCache;

            public MethodParametersInfo(MethodBase methodBase)
            {
                _metadataReader = null;
                _methodBase = methodBase;
                _methodHandle = default(MethodHandle);
                _returnTypeAndParametersHandlesCache = null;
                _returnTypeAndParametersTypesCache = null;
            }

            public MethodParametersInfo(MetadataReader metadataReader, MethodBase methodBase, MethodHandle methodHandle)
            {
                _metadataReader = metadataReader;
                _methodBase = methodBase;
                _methodHandle = methodHandle;
                _returnTypeAndParametersHandlesCache = null;
                _returnTypeAndParametersTypesCache = null;
            }

            public LowLevelList<RuntimeTypeHandle> ParameterTypeHandles
            {
                get
                {
                    ParameterInfo[] parameters = _methodBase.GetParameters();
                    LowLevelList<RuntimeTypeHandle> result = new LowLevelList<RuntimeTypeHandle>(parameters.Length);

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Type parameterType = parameters[i].ParameterType;

                        // If the parameter is a pointer type, use IntPtr. Else use the actual parameter type.
                        if (parameterType.IsPointer)
                            result.Add(CommonRuntimeTypes.IntPtr.TypeHandle);
                        else if (parameterType.IsByRef)
                            result.Add(parameterType.GetElementType().TypeHandle);
                        else if (parameterType.GetTypeInfo().IsEnum)
                            result.Add(Enum.GetUnderlyingType(parameterType).TypeHandle);
                        else
                            result.Add(parameterType.TypeHandle);
                    }

                    return result;
                }
            }

            public LowLevelList<RuntimeTypeHandle> ReturnTypeAndParameterTypeHandles
            {
                get
                {
                    LowLevelList<RuntimeTypeHandle> result = ParameterTypeHandles;

                    MethodInfo reflectionMethodInfo = _methodBase as MethodInfo;
                    Type returnType = reflectionMethodInfo != null ? reflectionMethodInfo.ReturnType : CommonRuntimeTypes.Void;
                    result.Insert(0, returnType.TypeHandle);

                    return result;
                }
            }

            public bool[] ReturnTypeAndParametersByRefFlags
            {
                get
                {
                    ParameterInfo[] parameters = _methodBase.GetParameters();
                    bool[] result = new bool[parameters.Length + 1];

                    MethodInfo reflectionMethodInfo = _methodBase as MethodInfo;
                    Type returnType = reflectionMethodInfo != null ? reflectionMethodInfo.ReturnType : CommonRuntimeTypes.Void;
                    result[0] = returnType.IsByRef;

                    for (int i = 0; i < parameters.Length; i++)
                        result[i + 1] = parameters[i].ParameterType.IsByRef;

                    return result;
                }
            }

            public bool RequiresCallingConventionConverter(out bool[] forcedByRefParams)
            {
                Handle[] handles = null;
                Type[] types = null;
                GetReturnTypeAndParameterTypesAndMDHandles(ref handles, ref types);

                // Compute whether any of the parameters have generic vars in their signatures ...
                bool requiresCallingConventionConverter = false;
                forcedByRefParams = new bool[handles.Length];
                for (int i = 0; i < handles.Length; i++)
                    if ((forcedByRefParams[i] = TypeSignatureHasVarsNeedingCallingConventionConverter(handles[i], types[i])))
                        requiresCallingConventionConverter = true;

                return requiresCallingConventionConverter;
            }

            private void GetReturnTypeAndParameterTypesAndMDHandles(ref Handle[] handles, ref Type[] types)
            {
                if (_returnTypeAndParametersTypesCache == null)
                {
                    Debug.Assert(_metadataReader != null && !_methodHandle.Equals(default(MethodHandle)));

                    _returnTypeAndParametersHandlesCache = new Handle[_methodBase.GetParameters().Length + 1];
                    _returnTypeAndParametersTypesCache = new Type[_methodBase.GetParameters().Length + 1];

                    MethodSignature signature = _methodHandle.GetMethod(_metadataReader).Signature.GetMethodSignature(_metadataReader);

                    // Check the return type for generic vars
                    MethodInfo reflectionMethodInfo = _methodBase as MethodInfo;
                    _returnTypeAndParametersTypesCache[0] = reflectionMethodInfo != null ? reflectionMethodInfo.ReturnType : CommonRuntimeTypes.Void;
                    _returnTypeAndParametersHandlesCache[0] = signature.ReturnType.GetReturnTypeSignature(_metadataReader).Type;

                    // Check the method parameters for generic vars
                    int index = 1;
                    foreach (ParameterTypeSignatureHandle paramHandle in signature.Parameters)
                    {
                        _returnTypeAndParametersHandlesCache[index] = paramHandle.GetParameterTypeSignature(_metadataReader).Type;
                        _returnTypeAndParametersTypesCache[index] = _methodBase.GetParameters()[index - 1].ParameterType;
                        index++;
                    }
                }

                handles = _returnTypeAndParametersHandlesCache;
                types = _returnTypeAndParametersTypesCache;
                Debug.Assert(handles != null && types != null);
            }

            private bool TypeSignatureHasVarsNeedingCallingConventionConverter(Handle typeHandle, Type type)
            {
                if (typeHandle.HandleType == HandleType.TypeSpecification)
                {
                    TypeSpecification typeSpec = typeHandle.ToTypeSpecificationHandle(_metadataReader).GetTypeSpecification(_metadataReader);
                    Handle sigHandle = typeSpec.Signature;
                    HandleType sigHandleType = sigHandle.HandleType;
                    switch (sigHandleType)
                    {
                        case HandleType.TypeVariableSignature:
                        case HandleType.MethodTypeVariableSignature:
                            return true;

                        case HandleType.TypeInstantiationSignature:
                            {
                                Debug.Assert(type.IsConstructedGenericType);
                                TypeInstantiationSignature sig = sigHandle.ToTypeInstantiationSignatureHandle(_metadataReader).GetTypeInstantiationSignature(_metadataReader);

                                if (RuntimeAugments.IsValueType(type.TypeHandle))
                                {
                                    // This generic type is a struct (its base type is System.ValueType)
                                    int genArgIndex = 0;
                                    bool needsCallingConventionConverter = false;
                                    foreach (Handle genericTypeArgumentHandle in sig.GenericTypeArguments)
                                    {
                                        if (TypeSignatureHasVarsNeedingCallingConventionConverter(genericTypeArgumentHandle, type.GenericTypeArguments[genArgIndex++]))
                                        {
                                            needsCallingConventionConverter = true;
                                            break;
                                        }
                                    }

                                    if (needsCallingConventionConverter)
                                    {
                                        throw new NotImplementedException();
                                    }
                                }
                            }
                            return false;
                    }
                }

                return false;
            }
        }

        struct InvokeMapEntryDataEnumerator
        {
            // The field '_entryMethodInstantiation' is assigned but its value is never used
            // The field '_nameAndSignature' is assigned but its value is never used
            #pragma warning disable 414

            // Read-only inputs
            readonly private RuntimeTypeHandle _declaringTypeHandle;
            readonly private RuntimeTypeHandle[] _genericMethodTypeArgumentHandles;
            readonly private CanonicalFormKind _canonFormKind;
            readonly private IntPtr _moduleHandle;
            readonly private MethodHandle _methodHandle;
            readonly private MethodBase _methodInfo;

            // Computed inputs
            private MethodBase _methodInfoForDefinition;

            // Parsed data from entry in the hashtable
            public InvokeTableFlags _flags;
            public RuntimeTypeHandle _entryType;
            public IntPtr _methodEntrypoint;
            public uint _dynamicInvokeCookie;
            public uint _defaultValueStringIndex;
            public IntPtr _entryDictionary;
            public RuntimeTypeHandle[] _methodInstantiation;

            // Computed data
            private bool _hasEntryPoint;
            private bool _isMatchingMethodHandleAndDeclaringType;
            private MethodNameAndSignature _nameAndSignature;
            private RuntimeTypeHandle[] _entryMethodInstantiation;

            #pragma warning restore 414

            private MethodBase MethodInfoForDefinition
            {
                get
                {
                    if (_methodInfoForDefinition == null)
                    {
                        RuntimeTypeHandle definitionHandle = _declaringTypeHandle;
                        if (RuntimeAugments.IsGenericType(_declaringTypeHandle))
                        {
                            RuntimeTypeHandle[] dummyComponents;
                            if (!ReflectionExecution.ExecutionEnvironment.TryGetConstructedGenericTypeComponents(_declaringTypeHandle, out definitionHandle, out dummyComponents))
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        }

                        _methodInfoForDefinition = ReflectionCoreExecution.ExecutionDomain.GetMethod(definitionHandle, _methodHandle, Array.Empty<RuntimeTypeHandle>());
                    }

                    return _methodInfoForDefinition;
                }
            }


            public InvokeMapEntryDataEnumerator(RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle[] genericMethodArgs, CanonicalFormKind canonFormKind, IntPtr moduleHandle, MethodHandle methodHandle, MethodBase methodInfo)
            {
                _declaringTypeHandle = declaringTypeHandle;
                _genericMethodTypeArgumentHandles = genericMethodArgs;
                _canonFormKind = canonFormKind;
                _moduleHandle = moduleHandle;
                _methodHandle = methodHandle;
                _methodInfo = methodInfo;

                _flags = 0;
                _entryType = default(RuntimeTypeHandle);
                _methodEntrypoint = IntPtr.Zero;
                _dynamicInvokeCookie = 0xffffffff;
                _defaultValueStringIndex = 0xffffffff;
                _hasEntryPoint = false;
                _isMatchingMethodHandleAndDeclaringType = false;
                _entryDictionary = IntPtr.Zero;
                _methodInstantiation = null;
                _nameAndSignature = null;
                _entryMethodInstantiation = null;
                _methodInfoForDefinition = null;
            }

            public void GetNext(ref NativeParser entryParser, ref ExternalReferencesTable extRefTable, CanonicallyEquivalentEntryLocator canonHelper)
            {
                // Read flags and reset members data
                _flags = (InvokeTableFlags)entryParser.GetUnsigned();
                _hasEntryPoint = ((_flags & InvokeTableFlags.HasEntrypoint) != 0);
                _isMatchingMethodHandleAndDeclaringType = false;
                _entryType = default(RuntimeTypeHandle);
                _methodEntrypoint = IntPtr.Zero;
                _dynamicInvokeCookie = 0xffffffff;
                _defaultValueStringIndex = 0xffffffff;
                _entryDictionary = IntPtr.Zero;
                _methodInstantiation = null;
                _nameAndSignature = null;
                _entryMethodInstantiation = null;

                // If the current entry is not a canonical entry of the same canonical form kind we are looking for, then this cannot be a match
                if (((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0) != (_canonFormKind == CanonicalFormKind.Universal))
                    return;

                if ((_flags & InvokeTableFlags.HasMetadataHandle) != 0)
                {
                    MethodHandle entryMethodHandle = (((int)HandleType.Method << 24) | (int)entryParser.GetUnsigned()).AsMethodHandle();
                    if (!_methodHandle.Equals(entryMethodHandle))
                        return;
                }
                else
                {
                    throw new NotImplementedException();
                }

                _entryType = extRefTable.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!canonHelper.IsCanonicallyEquivalent(_entryType))
                    return;

                // Method handle and entry type match at this point. Continue reading data from the entry...
                _isMatchingMethodHandleAndDeclaringType = true;

                if (_hasEntryPoint)
                    _methodEntrypoint = RvaToFunctionPointer(_moduleHandle, extRefTable.GetRvaFromIndex(entryParser.GetUnsigned()));

                _dynamicInvokeCookie = entryParser.GetUnsigned();

                if ((_flags & InvokeTableFlags.HasDefaultParameters) != 0)
                    _defaultValueStringIndex = entryParser.GetUnsigned();

                if ((_flags & InvokeTableFlags.IsGenericMethod) == 0)
                    return;

                if ((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0)
                {
                    throw new NotImplementedException();
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
                    return SequenceEqual(_genericMethodTypeArgumentHandles, _methodInstantiation);

                throw new NotImplementedException();
            }

            public bool GetMethodEntryPoint(MetadataReader metadataReader, RuntimeTypeHandle declaringTypeHandle, out IntPtr methodEntrypoint, out IntPtr dictionaryComponent, out IntPtr rawMethodEntrypoint)
            {
                // Debug-only sanity check before proceeding (IsMatchingOrCompatibleEntry is called from TryGetDynamicMethodInvokeInfo)
                Debug.Assert(IsMatchingOrCompatibleEntry());

                rawMethodEntrypoint = _methodEntrypoint;
                methodEntrypoint = dictionaryComponent = IntPtr.Zero;

                if (!GetDictionaryComponent(out dictionaryComponent) || !GetMethodEntryPointComponent(dictionaryComponent, out methodEntrypoint))
                    return false;

                // Wrap the method entry point in a calling convention converter thunk if it's a universal canonical implementation
                if ((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0)
                {
                    Debug.Assert(_canonFormKind == CanonicalFormKind.Universal);
                    methodEntrypoint = GetCallingConventionConverterForMethodEntrypoint(metadataReader, declaringTypeHandle, methodEntrypoint, dictionaryComponent, _methodInfo, _methodHandle);
                }

                return true;
            }

            public string GetDefaultValueString(ref ExternalReferencesTable extRefTable)
            {
                if ((_flags & InvokeTableFlags.HasDefaultParameters) == 0)
                    return null;

                IntPtr defaultValueFrozenString = extRefTable.GetIntPtrFromIndex(_defaultValueStringIndex);
                return (string)RuntimeAugments.ConvertIntPtrToObjectReference(defaultValueFrozenString);
            }

            private bool GetDictionaryComponent(out IntPtr dictionaryComponent)
            {
                throw new NotImplementedException();
            }

            private bool GetMethodEntryPointComponent(IntPtr dictionaryComponent, out IntPtr methodEntrypoint)
            {
                methodEntrypoint = _methodEntrypoint;

                if (dictionaryComponent == IntPtr.Zero)
                    return true;

                // Do not use a fat function-pointer for universal canonical methods because the converter data block already holds the 
                // dictionary pointer so it serves as its own instantiating stub
                if ((_flags & InvokeTableFlags.IsUniversalCanonicalEntry) == 0)
                    methodEntrypoint = FunctionPointerOps.GetGenericMethodFunctionPointer(_methodEntrypoint, dictionaryComponent);

                return true;
            }

            private IntPtr GetCallingConventionConverterForMethodEntrypoint(MetadataReader metadataReader, RuntimeTypeHandle declaringType, IntPtr methodEntrypoint, IntPtr dictionary, MethodBase methodBase, MethodHandle mdHandle)
            {
                throw new NotImplementedException();
            }
        }
    }
}

