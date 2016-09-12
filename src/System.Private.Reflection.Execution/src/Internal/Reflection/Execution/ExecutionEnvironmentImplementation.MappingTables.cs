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
using ThunkKind = Internal.Runtime.TypeLoader.CallConverterThunk.ThunkKind;
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
        private struct MethodTargetAndDictionary { public IntPtr TargetPointer; public IntPtr DictionaryPointer; }

        private LowLevelDictionary<IntPtr, MethodTargetAndDictionary> _callConverterWrappedMethodEntrypoints = new LowLevelDictionary<IntPtr, MethodTargetAndDictionary>();

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
                RemoteStaticFieldDescriptor* descriptor = (RemoteStaticFieldDescriptor*)(moduleHandle +
                   (staticFieldRVA & ~FieldAccessFlags.RemoteStaticFieldRVA));
                staticFieldAddress = *descriptor->IndirectionCell + descriptor->Offset;
            }
            else
                staticFieldAddress = (IntPtr)(moduleHandle + staticFieldRVA);

            return staticFieldAddress;
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
        public unsafe sealed override bool TryGetMetadataForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeDefinitionHandle typeDefHandle)
        {
            Debug.Assert(!RuntimeAugments.IsGenericType(runtimeTypeHandle));
            return TypeLoaderEnvironment.Instance.TryGetMetadataForNamedType(runtimeTypeHandle, out metadataReader, out typeDefHandle);
        }

        //
        // Return true for a TypeDef if the policy has decided this type is blocked from reflection.
        //
        // Preconditions:
        //    runtimeTypeHandle is a typedef or a generic type instance (not a constructed type such as an array)
        //
        public unsafe sealed override bool IsReflectionBlocked(RuntimeTypeHandle runtimeTypeHandle)
        {
            // CORERT-TODO: reflection blocking
#if !CORERT
            // For generic types, use the generic type definition
            runtimeTypeHandle = GetTypeDefinition(runtimeTypeHandle);

            var moduleHandle = RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle);

            NativeReader blockedReflectionReader = GetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.BlockReflectionTypeMap);
            NativeParser blockedReflectionParser = new NativeParser(blockedReflectionReader, 0);
            NativeHashtable blockedReflectionHashtable = new NativeHashtable(blockedReflectionParser);
            ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
            externalReferences.InitializeCommonFixupsTable(moduleHandle);

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
#endif
            return false;
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
        public unsafe sealed override bool TryGetNamedTypeForMetadata(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, out RuntimeTypeHandle runtimeTypeHandle)
        {
            return TypeLoaderEnvironment.Instance.TryGetNamedTypeForMetadata(metadataReader, typeDefHandle, out runtimeTypeHandle);
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
        public unsafe sealed override bool TryGetTypeReferenceForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeReferenceHandle typeRefHandle)
        {
            return TypeLoaderEnvironment.TryGetTypeReferenceForNamedType(runtimeTypeHandle, out metadataReader, out typeRefHandle);
        }

        /// <summary>
        /// Return the RuntimeTypeHandle for the named type referenced by another type that pay-for-play denotes as browsable (for example,
        /// in a member signature.) Typically, the type itself is *not* browsable (or it would have appeared in the TypeDef table.)
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
        public unsafe sealed override bool TryGetNamedTypeForTypeReference(MetadataReader metadataReader, TypeReferenceHandle typeRefHandle, out RuntimeTypeHandle runtimeTypeHandle)
        {
            return TypeLoaderEnvironment.TryGetNamedTypeForTypeReference(metadataReader, typeRefHandle, out runtimeTypeHandle);
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

            // For non-dynamic arrays try to look up the array type in the ArrayMap blobs;
            // attempt to dynamically create a new one if that doesn't succeeed.
            return TypeLoaderEnvironment.Instance.TryGetArrayTypeForElementType(elementTypeHandle, false, -1, out arrayTypeHandle);
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
            if (RuntimeAugments.IsUnmanagedPointerType(elementTypeHandle))
            {
                throw new PlatformNotSupportedException(SR.PlatformNotSupported_PointerArray);
            }

            if (RuntimeAugments.IsGenericTypeDefinition(elementTypeHandle))
            {
                throw new NotSupportedException(SR.NotSupported_OpenType);
            }
            
            if ((rank < MDArray.MinRank) || (rank > MDArray.MaxRank))
            {
                throw new PlatformNotSupportedException(SR.Format(SR.PlatformNotSupported_NoMultiDims, rank));
            }

            return TypeLoaderEnvironment.Instance.TryGetArrayTypeForElementType(elementTypeHandle, true, rank, out arrayTypeHandle);
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
            return TypeLoaderEnvironment.Instance.TryGetPointerTypeForTargetType(targetTypeHandle, out pointerTypeHandle);
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
            return TypeLoaderEnvironment.Instance.TryGetConstructedGenericTypeComponents(runtimeTypeHandle, out genericTypeDefinitionHandle, out genericTypeArgumentHandles);
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
            return TypeLoaderEnvironment.Instance.TryGetConstructedGenericTypeComponents(runtimeTypeHandle, out genericTypeDefinitionHandle, out genericTypeArgumentHandles);
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
            if (TypeLoaderEnvironment.Instance.TryLookupConstructedGenericTypeForComponents(genericTypeDefinitionHandle, genericTypeArgumentHandles, out runtimeTypeHandle))
            {
                return true;
            }

            TypeInfo typeDefinition = Type.GetTypeFromHandle(genericTypeDefinitionHandle).GetTypeInfo();

            TypeInfo[] typeArguments = new TypeInfo[genericTypeArgumentHandles.Length];
            for (int i = 0; i < genericTypeArgumentHandles.Length; i++)
                typeArguments[i] = Type.GetTypeFromHandle(genericTypeArgumentHandles[i]).GetTypeInfo();

            ConstraintValidator.EnsureSatisfiesClassConstraints(typeDefinition, typeArguments);

            return TypeLoaderEnvironment.Instance.TryGetConstructedGenericTypeForComponents(genericTypeDefinitionHandle, genericTypeArgumentHandles, out runtimeTypeHandle);
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

            MethodBase methodInfo = ReflectionCoreExecution.ExecutionDomain.GetMethod(declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles);

            // Validate constraints first. This is potentially useless work if the method already exists, but it prevents bad
            // inputs to reach the type loader (we don't have support to e.g. represent pointer types within the type loader)
            if (genericMethodTypeArgumentHandles != null && genericMethodTypeArgumentHandles.Length > 0)
                ConstraintValidator.EnsureSatisfiesClassConstraints((MethodInfo)methodInfo);

            MethodSignatureComparer methodSignatureComparer = new MethodSignatureComparer(reader, methodHandle);

            MethodInvokeInfo methodInvokeInfo;
#if GENERICS_FORCE_USG
            // Stress mode to force the usage of universal canonical method targets for reflection invokes.
            // It is recommended to use "/SharedGenericsMode GenerateAllUniversalGenerics" NUTC command line argument when
            // compiling the application in order to effectively use the GENERICS_FORCE_USG mode.

            // If we are just trying to invoke a non-generic method on a non-generic type, we won't force the universal lookup
            if (!RuntimeAugments.IsGenericType(declaringTypeHandle) && (genericMethodTypeArgumentHandles == null || genericMethodTypeArgumentHandles.Length == 0))
                methodInvokeInfo = TryGetMethodInvokeInfo(reader, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles,
                    methodInfo, ref methodSignatureComparer, CanonicalFormKind.Specific);
            else
                methodInvokeInfo = TryGetMethodInvokeInfo(reader, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles,
                    methodInfo, ref methodSignatureComparer, CanonicalFormKind.Universal);
#else
            methodInvokeInfo = TryGetMethodInvokeInfo(reader, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles,
                methodInfo, ref methodSignatureComparer, CanonicalFormKind.Specific);

            // If we failed to get a MethodInvokeInfo for an exact method, or a canonically equivalent method, check if there is a universal canonically
            // equivalent entry that could be used (it will be much slower, and require a calling convention converter)
            if (methodInvokeInfo == null)
                methodInvokeInfo = TryGetMethodInvokeInfo(reader, declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles,
                    methodInfo, ref methodSignatureComparer, CanonicalFormKind.Universal);
#endif

            if (methodInvokeInfo == null)
                return null;

            return MethodInvokerWithMethodInvokeInfo.CreateMethodInvoker(reader, declaringTypeHandle, methodHandle, methodInvokeInfo);
        }

        // Get the pointers necessary to call a dynamic method invocation function
        //
        // This is either a function pointer to call, or a function pointer and template token.
        private unsafe void GetDynamicMethodInvokeMethodInfo(IntPtr moduleHandle, uint cookie, RuntimeTypeHandle[] argHandles,
            out IntPtr dynamicInvokeMethod, out IntPtr dynamicInvokeMethodGenericDictionary)
        {
            if ((cookie & 1) == 1)
            {
                // If the dynamic invoke method is a generic method, we need to consult the DynamicInvokeTemplateData table to locate
                // the matching template so that we can instantiate it. The DynamicInvokeTemplateData table starts with a single UINT
                // with the RVA of the type that hosts all DynamicInvoke methods. The table then follows with list of [Token, FunctionPointer]
                // pairs. The cookie parameter is an index into this table and points to a single pair.
                uint* pBlob;
                uint cbBlob;
                bool success = RuntimeAugments.FindBlob(moduleHandle, (int)ReflectionMapBlob.DynamicInvokeTemplateData, (IntPtr)(&pBlob), (IntPtr)(&cbBlob));
                Debug.Assert(success && cbBlob > 4);

                byte* pNativeLayoutInfoBlob;
                uint cbNativeLayoutInfoBlob;
                success = RuntimeAugments.FindBlob(moduleHandle, (int)ReflectionMapBlob.NativeLayoutInfo, new IntPtr(&pNativeLayoutInfoBlob), new IntPtr(&cbNativeLayoutInfoBlob));
                Debug.Assert(success);

                // All methods referred from this blob are contained in the same type. The first UINT in the blob is the RVA of that EEType
                RuntimeTypeHandle declaringTypeHandle = RvaToRuntimeTypeHandle(moduleHandle, pBlob[0]);

                // The index points to two entries: the token of the dynamic invoke method and the function pointer to the canonical method
                // Now have the type loader build or locate a dictionary for this method
                uint index = cookie >> 1;

                MethodNameAndSignature nameAndSignature;
                IntPtr nameAndSigSignature = (IntPtr)(pNativeLayoutInfoBlob + pBlob[index]);
                success = TypeLoaderEnvironment.Instance.TryGetMethodNameAndSignatureFromNativeLayoutSignature(ref nameAndSigSignature, out nameAndSignature);
                Debug.Assert(success);

                success = TypeLoaderEnvironment.Instance.TryGetGenericMethodDictionaryForComponents(declaringTypeHandle, argHandles, nameAndSignature, out dynamicInvokeMethodGenericDictionary);
                Debug.Assert(success);

                dynamicInvokeMethod = RvaToFunctionPointer(moduleHandle, pBlob[index + 1]);
            }
            else
            {
                // Nongeneric DynamicInvoke method. This is used to DynamicInvoke methods that have parameters that
                // cannot be shared (or if there are no parameters to begin with).
                ExternalReferencesTable extRefs = default(ExternalReferencesTable);
                extRefs.InitializeCommonFixupsTable(moduleHandle);

                dynamicInvokeMethod = RvaToFunctionPointer(moduleHandle, extRefs.GetRvaFromIndex(cookie >> 1));
                dynamicInvokeMethodGenericDictionary = IntPtr.Zero;
            }
        }

        private IntPtr GetDynamicMethodInvokerThunk(RuntimeTypeHandle[] argHandles, MethodBase methodInfo)
        {
            ParameterInfo[] parameters = methodInfo.GetParametersNoCopy();
            // last entry in argHandles is the return type if the type is not typeof(void)
            Debug.Assert(parameters.Length == argHandles.Length || parameters.Length == (argHandles.Length - 1));

            bool[] byRefParameters = new bool[parameters.Length + 1];
            RuntimeTypeHandle[] parameterTypeHandles = new RuntimeTypeHandle[parameters.Length + 1];

            // This is either a constructor ("returns" void) or an instance method
            MethodInfo reflectionMethodInfo = methodInfo as MethodInfo;
            parameterTypeHandles[0] = (reflectionMethodInfo != null ? reflectionMethodInfo.ReturnType.TypeHandle : CommonRuntimeTypes.Void.TypeHandle);
            byRefParameters[0] = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypeHandles[i + 1] = argHandles[i];
                byRefParameters[i + 1] = parameters[i].ParameterType.IsByRef;
            }

            return CallConverterThunk.MakeThunk(ThunkKind.ReflectionDynamicInvokeThunk, IntPtr.Zero, IntPtr.Zero, false, parameterTypeHandles, byRefParameters, null);
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

        private static RuntimeTypeHandle[] GetTypeSequence(ref ExternalReferencesTable extRefs, ref NativeParser parser)
        {
            uint count = parser.GetUnsigned();
            RuntimeTypeHandle[] result = new RuntimeTypeHandle[count];
            for (uint i = 0; i < count; i++)
            {
                result[i] = extRefs.GetRuntimeTypeHandleFromIndex(parser.GetUnsigned());
            }
            return result;
        }

        private IntPtr TryGetVirtualResolveData(IntPtr moduleHandle,
            RuntimeTypeHandle methodHandleDeclaringType, MethodHandle methodHandle, RuntimeTypeHandle[] genericArgs,
            ref MethodSignatureComparer methodSignatureComparer)
        {
            NativeReader invokeMapReader = GetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.VirtualInvokeMap);
            NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
            NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);
            ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
            externalReferences.InitializeCommonFixupsTable(moduleHandle);

            RuntimeTypeHandle definitionType = GetTypeDefinition(methodHandleDeclaringType);

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

                if (!methodSignatureComparer.IsMatchingNativeLayoutMethodNameAndSignature(nameAndSig.Name, nameAndSig.Signature))
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
                        return IntPtr.Zero;
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
                        return IntPtr.Zero;
                    }

                    RuntimeMethodHandle gvmSlot;
                    if (!TypeLoaderEnvironment.Instance.TryGetRuntimeMethodHandleForComponents(declaringTypeOfVirtualInvoke, methodName, methodSignature, genericArgs, out gvmSlot))
                    {
                        return IntPtr.Zero;
                    }

                    return (new OpenMethodResolver(declaringTypeOfVirtualInvoke, gvmSlot, methodHandle.AsInt())).ToIntPtr();
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

                            RuntimeTypeHandle genericDefinition;
                            RuntimeTypeHandle[] genericTypeArgs;
                            bool success = TypeLoaderEnvironment.Instance.TryGetConstructedGenericTypeComponents(searchForSharedGenericTypesInParentHierarchy,
                                                                                                                out genericDefinition,
                                                                                                                out genericTypeArgs);
                            if (TypeLoaderEnvironment.Instance.ConversionToCanonFormIsAChange(genericTypeArgs, CanonicalFormKind.Specific))
                            {
                                // Shared generic types have a slot dedicated to holding the generic dictionary.
                                slot++;
                            }

                            Debug.Assert(success);
                        }

                        // Walk to parent
                        if (!RuntimeAugments.TryGetBaseType(searchForSharedGenericTypesInParentHierarchy, out searchForSharedGenericTypesInParentHierarchy))
                        {
                            break;
                        }
                    }


                    return (new OpenMethodResolver(declaringTypeOfVirtualInvoke, checked((ushort)slot), methodHandle.AsInt())).ToIntPtr();
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Try to look up method invoke info in metadata for all registered modules, construct
        /// the calling convention converter as appropriate and fill in MethodInvokeInfo.
        /// </summary>
        /// <param name="metadataReader">Metadata reader for the declaring type</param>
        /// <param name="declaringTypeHandle">Runtime handle of declaring type for the method</param>
        /// <param name="methodHandle">Handle of method to look up</param>
        /// <param name="genericMethodTypeArgumentHandles">Runtime handles of generic method arguments</param>
        /// <param name="methodSignatureComparer">Helper structure used for comparing signatures</param>
        /// <param name="canonFormKind">Requested canon form</param>
        /// <returns>Constructed method invoke info, null on failure</returns>
        private unsafe MethodInvokeInfo TryGetMethodInvokeInfo(
            MetadataReader metadataReader,
            RuntimeTypeHandle declaringTypeHandle,
            MethodHandle methodHandle,
            RuntimeTypeHandle[] genericMethodTypeArgumentHandles,
            MethodBase methodInfo,
            ref MethodSignatureComparer methodSignatureComparer,
            CanonicalFormKind canonFormKind)
        {
            MethodInvokeMetadata methodInvokeMetadata;

            if (!TypeLoaderEnvironment.TryGetMethodInvokeMetadata(
                metadataReader,
                declaringTypeHandle,
                methodHandle,
                genericMethodTypeArgumentHandles,
                ref methodSignatureComparer,
                canonFormKind,
                out methodInvokeMetadata))
            {
                // Method invoke info not found
                return null;
            }

            if ((methodInvokeMetadata.InvokeTableFlags & InvokeTableFlags.IsUniversalCanonicalEntry) != 0)
            {
                // Wrap the method entry point in a calling convention converter thunk if it's a universal canonical implementation
                Debug.Assert(canonFormKind == CanonicalFormKind.Universal);
                methodInvokeMetadata.MethodEntryPoint = GetCallingConventionConverterForMethodEntrypoint(
                    metadataReader,
                    declaringTypeHandle,
                    methodInvokeMetadata.MethodEntryPoint,
                    methodInvokeMetadata.DictionaryComponent,
                    methodInfo,
                    methodHandle);
            }

            if (methodInvokeMetadata.MethodEntryPoint != methodInvokeMetadata.RawMethodEntryPoint &&
                !FunctionPointerOps.IsGenericMethodPointer(methodInvokeMetadata.MethodEntryPoint))
            {
                // Keep track of the raw method entrypoints for the cases where they get wrapped into a calling convention converter thunk.
                // This is needed for reverse lookups, like in TryGetMethodForOriginalLdFtnResult
                Debug.Assert(canonFormKind == CanonicalFormKind.Universal);
                lock (_callConverterWrappedMethodEntrypoints)
                {
                    _callConverterWrappedMethodEntrypoints.LookupOrAdd(methodInvokeMetadata.MethodEntryPoint, new MethodTargetAndDictionary
                    {
                        TargetPointer = methodInvokeMetadata.RawMethodEntryPoint,
                        DictionaryPointer = methodInvokeMetadata.DictionaryComponent
                    });
                }
            }

            RuntimeTypeHandle[] dynInvokeMethodArgs = GetDynamicInvokeInstantiationArguments(methodInfo);

            IntPtr dynamicInvokeMethod;
            IntPtr dynamicInvokeMethodGenericDictionary;
            if ((methodInvokeMetadata.InvokeTableFlags & InvokeTableFlags.NeedsParameterInterpretation) != 0)
            {
                dynamicInvokeMethod = GetDynamicMethodInvokerThunk(dynInvokeMethodArgs, methodInfo);
                dynamicInvokeMethodGenericDictionary = IntPtr.Zero;
            }
            else
            {
                GetDynamicMethodInvokeMethodInfo(
                    methodInvokeMetadata.MappingTableModule,
                    methodInvokeMetadata.DynamicInvokeCookie,
                    dynInvokeMethodArgs,
                    out dynamicInvokeMethod,
                    out dynamicInvokeMethodGenericDictionary);
            }

            IntPtr resolver = IntPtr.Zero;
            if ((methodInvokeMetadata.InvokeTableFlags & InvokeTableFlags.HasVirtualInvoke) != 0)
            {
                resolver = TryGetVirtualResolveData(ModuleList.Instance.GetModuleForMetadataReader(metadataReader),
                    declaringTypeHandle, methodHandle, genericMethodTypeArgumentHandles,
                    ref methodSignatureComparer);

                // Unable to find virtual resolution information, cannot return valid MethodInvokeInfo
                if (resolver == IntPtr.Zero)
                    return null;
            }

            var methodInvokeInfo = new MethodInvokeInfo
            {
                LdFtnResult = methodInvokeMetadata.MethodEntryPoint,
                DynamicInvokeMethod = dynamicInvokeMethod,
                DynamicInvokeGenericDictionary = dynamicInvokeMethodGenericDictionary,
                MethodInfo = methodInfo,
                VirtualResolveData = resolver,
            };
            return methodInvokeInfo;
        }

        private static IntPtr GetCallingConventionConverterForMethodEntrypoint(MetadataReader metadataReader, RuntimeTypeHandle declaringType, IntPtr methodEntrypoint, IntPtr dictionary, MethodBase methodBase, MethodHandle mdHandle)
        {
            MethodParametersInfo methodParamsInfo = new MethodParametersInfo(metadataReader, methodBase, mdHandle);

            bool[] forcedByRefParameters;
            if (methodParamsInfo.RequiresCallingConventionConverter(out forcedByRefParameters))
            {
                RuntimeTypeHandle[] parameterTypeHandles = methodParamsInfo.ReturnTypeAndParameterTypeHandles.ToArray();
                bool[] byRefParameters = methodParamsInfo.ReturnTypeAndParametersByRefFlags;

                Debug.Assert(parameterTypeHandles.Length == byRefParameters.Length && byRefParameters.Length == forcedByRefParameters.Length);

                bool isMethodOnStructure = RuntimeAugments.IsValueType(declaringType);

                return CallConverterThunk.MakeThunk(
                    (methodBase.IsGenericMethod || isMethodOnStructure ? ThunkKind.StandardToGenericInstantiating : ThunkKind.StandardToGenericInstantiatingIfNotHasThis),
                    methodEntrypoint,
                    dictionary,
                    !methodBase.IsStatic,
                    parameterTypeHandles,
                    byRefParameters,
                    forcedByRefParameters);
            }
            else
            {
                return FunctionPointerOps.GetGenericMethodFunctionPointer(methodEntrypoint, dictionary);
            }
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

        private struct FunctionPointerOffsetPair : IComparable<FunctionPointerOffsetPair>
        {
            public FunctionPointerOffsetPair(IntPtr functionPointer, uint offset)
            {
                FunctionPointer = functionPointer;
                Offset = offset;
            }

            public int CompareTo(FunctionPointerOffsetPair other)
            {
                unsafe
                {
                    void* fptr = FunctionPointer.ToPointer();
                    void* otherFptr = other.FunctionPointer.ToPointer();

                    if (fptr < otherFptr)
                        return -1;
                    else if (fptr == otherFptr)
                        return Offset.CompareTo(other.Offset);
                    else
                        return 1;
                }
            }

            public readonly IntPtr FunctionPointer;
            public readonly uint Offset;
        }

        private struct FunctionPointersToOffsets
        {
            public FunctionPointerOffsetPair[] Data;

            public bool TryGetOffsetsRange(IntPtr functionPointer, out int firstParserOffsetIndex, out int lastParserOffsetIndex)
            {
                firstParserOffsetIndex = -1;
                lastParserOffsetIndex = -1;

                if (Data == null)
                    return false;

                int binarySearchIndex = Array.BinarySearch(Data, new FunctionPointerOffsetPair(functionPointer, 0));

                // Array.BinarySearch will return either a positive number which is the first index in a range
                // or a negative number which is the bitwise complement of the start of the range
                // or a negative number which doesn't correspond to the range at all.
                if (binarySearchIndex < 0)
                    binarySearchIndex = ~binarySearchIndex;

                if (binarySearchIndex >= Data.Length || Data[binarySearchIndex].FunctionPointer != functionPointer)
                    return false;

                // binarySearchIndex now contains the index of the start of a range of matching function pointers and offsets
                firstParserOffsetIndex = binarySearchIndex;
                lastParserOffsetIndex = binarySearchIndex;
                while ((lastParserOffsetIndex < (Data.Length - 1)) && Data[lastParserOffsetIndex + 1].FunctionPointer == functionPointer)
                {
                    lastParserOffsetIndex++;
                }
                return true;
            }
        }

        // ldftn reverse lookup hash. Must be cleared and reset if the module list changes. (All sets to
        // this variable must happen under a lock)
        private volatile KeyValuePair<IntPtr, FunctionPointersToOffsets>[] _ldftnReverseLookup = null;

        private KeyValuePair<IntPtr, FunctionPointersToOffsets>[] GetLdFtnReverseLookups()
        {
            KeyValuePair<IntPtr, FunctionPointersToOffsets>[] ldFtnReverseLookup = _ldftnReverseLookup;

            if (ldFtnReverseLookup != null)
                return ldFtnReverseLookup;
            else
            {
                lock (this)
                {
                    ldFtnReverseLookup = _ldftnReverseLookup;

                    // double checked lock, safe due to use of volatile on s_ldftnReverseHashes
                    if (ldFtnReverseLookup != null)
                        return ldFtnReverseLookup;

                    // FUTURE: add a module load callback to invalidate this cache if a new module is loaded.
                    while (true)
                    {
                        int size = 0;
                        foreach (IntPtr module in ModuleList.Enumerate())
                        {
                            size++;
                        }

                        ldFtnReverseLookup = new KeyValuePair<IntPtr, FunctionPointersToOffsets>[size];
                        int index = 0;
                        foreach (IntPtr module in ModuleList.Enumerate())
                        {
                            // If the module list changes during execution of this code, rebuild from scratch
                            if (index >= ldFtnReverseLookup.Length)
                                continue;

                            ldFtnReverseLookup[index] = new KeyValuePair<IntPtr, FunctionPointersToOffsets>(module, ComputeLdftnReverseLookupLookup(module));
                            index++;
                        }

                        // unless we need to repeat the module enumeration, only execute the body of this while loop once.
                        break;
                    }

                    _ldftnReverseLookup = ldFtnReverseLookup;
                    return ldFtnReverseLookup;
                }
            }
        }

        internal unsafe bool TryGetMethodForOriginalLdFtnResult(IntPtr originalLdFtnResult, ref RuntimeTypeHandle declaringTypeHandle, out MethodHandle methodHandle, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
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

            foreach (KeyValuePair<IntPtr, FunctionPointersToOffsets> perModuleLookup in GetLdFtnReverseLookups())
            {
                int startIndex;
                int endIndex;

                if (perModuleLookup.Value.TryGetOffsetsRange(canonOriginalLdFtnResult, out startIndex, out endIndex))
                {
                    for (int curIndex = startIndex; curIndex <= endIndex; curIndex++)
                    {
                        uint parserOffset = perModuleLookup.Value.Data[curIndex].Offset;
                        if (TryGetMethodForOriginalLdFtnResult_Inner(perModuleLookup.Key, canonOriginalLdFtnResult, instantiationArgument, parserOffset, ref declaringTypeHandle, out methodHandle, out genericMethodTypeArgumentHandles))
                            return true;
                    }
                }
            }

            methodHandle = default(MethodHandle);
            genericMethodTypeArgumentHandles = null;
            return false;
        }

        private FunctionPointersToOffsets ComputeLdftnReverseLookupLookup(IntPtr mappingTableModule)
        {
            FunctionPointersToOffsets functionPointerToOffsetInInvokeMap = new FunctionPointersToOffsets();

            NativeReader invokeMapReader;
            if (!TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.InvokeMap, out invokeMapReader))
            {
                return functionPointerToOffsetInInvokeMap;
            }

            ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
            externalReferences.InitializeCommonFixupsTable(mappingTableModule);

            NativeParser invokeMapParser = new NativeParser(invokeMapReader, 0);
            NativeHashtable invokeHashtable = new NativeHashtable(invokeMapParser);

            LowLevelList<FunctionPointerOffsetPair> functionPointers = new LowLevelList<FunctionPointerOffsetPair>();

            var lookup = invokeHashtable.EnumerateAllEntries();
            NativeParser entryParser;
            while (!(entryParser = lookup.GetNext()).IsNull)
            {
                uint parserOffset = entryParser.Offset;
                Debug.Assert(entryParser.Reader == invokeMapParser.Reader);

                InvokeTableFlags entryFlags = (InvokeTableFlags)entryParser.GetUnsigned();

                bool hasEntrypoint = ((entryFlags & InvokeTableFlags.HasEntrypoint) != 0);
                if (!hasEntrypoint)
                    continue;

                uint entryMethodHandleOrNameAndSigRaw = entryParser.GetUnsigned();
                uint entryDeclaringTypeRaw = entryParser.GetUnsigned();

                IntPtr entryMethodEntrypoint = RvaToFunctionPointer(mappingTableModule, externalReferences.GetRvaFromIndex(entryParser.GetUnsigned()));
                functionPointers.Add(new FunctionPointerOffsetPair(entryMethodEntrypoint, parserOffset));
            }

            functionPointerToOffsetInInvokeMap.Data = functionPointers.ToArray();
            Array.Sort(functionPointerToOffsetInInvokeMap.Data);

            return functionPointerToOffsetInInvokeMap;
        }

        private unsafe bool TryGetMethodForOriginalLdFtnResult_Inner(IntPtr mappingTableModule, IntPtr canonOriginalLdFtnResult, IntPtr instantiationArgument, uint parserOffset, ref RuntimeTypeHandle declaringTypeHandle, out MethodHandle methodHandle, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
            methodHandle = default(MethodHandle);
            genericMethodTypeArgumentHandles = null;

            NativeReader invokeMapReader;
            if (!TryGetNativeReaderForBlob(mappingTableModule, ReflectionMapBlob.InvokeMap, out invokeMapReader))
            {
                // This should have succeeded otherwise, how did we get a parser offset as an input parameter?
                Debug.Assert(false);
                return false;
            }

            ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
            externalReferences.InitializeCommonFixupsTable(mappingTableModule);

            NativeParser entryParser = new NativeParser(invokeMapReader, parserOffset);

            InvokeTableFlags entryFlags = (InvokeTableFlags)entryParser.GetUnsigned();

            // If the passed in method was a fat function pointer, but the entry in the mapping table doesn't need
            // an instantiation argument (or the other way around), trivially reject it.
            if ((instantiationArgument == IntPtr.Zero) != ((entryFlags & InvokeTableFlags.RequiresInstArg) == 0))
                return false;

            Debug.Assert((entryFlags & InvokeTableFlags.HasEntrypoint) != 0);

            uint entryMethodHandleOrNameAndSigRaw = entryParser.GetUnsigned();
            uint entryDeclaringTypeRaw = entryParser.GetUnsigned();

            IntPtr entryMethodEntrypoint = RvaToFunctionPointer(mappingTableModule, externalReferences.GetRvaFromIndex(entryParser.GetUnsigned()));

            if ((entryFlags & InvokeTableFlags.NeedsParameterInterpretation) == 0)
                entryParser.GetUnsigned(); // skip dynamic invoke cookie

            Debug.Assert(entryMethodEntrypoint == canonOriginalLdFtnResult);

            if ((entryFlags & InvokeTableFlags.RequiresInstArg) == 0 && declaringTypeHandle.IsNull())
                declaringTypeHandle = externalReferences.GetRuntimeTypeHandleFromIndex(entryDeclaringTypeRaw);

            if ((entryFlags & InvokeTableFlags.IsGenericMethod) != 0)
            {
                if ((entryFlags & InvokeTableFlags.RequiresInstArg) != 0)
                {
                    MethodNameAndSignature dummyNameAndSignature;
                    bool success = TypeLoaderEnvironment.Instance.TryGetGenericMethodComponents(instantiationArgument, out declaringTypeHandle, out dummyNameAndSignature, out genericMethodTypeArgumentHandles);
                    Debug.Assert(success);
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
                uint nameAndSigOffset = externalReferences.GetRvaFromIndex(entryMethodHandleOrNameAndSigRaw);
                MethodNameAndSignature nameAndSig;
                if (!TypeLoaderEnvironment.Instance.TryGetMethodNameAndSignatureFromNativeLayoutOffset(mappingTableModule, nameAndSigOffset, out nameAndSig))
                {
                    Debug.Assert(false);
                    return false;
                }

                if (!TryGetMetadataForTypeMethodNameAndSignature(declaringTypeHandle, nameAndSig, out methodHandle))
                {
                    Debug.Assert(false);
                    return false;
                }
            }

            return true;
        }

        public sealed override FieldAccessor TryGetFieldAccessor(RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle, FieldHandle fieldHandle)
        {
            foreach (var moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(declaringTypeHandle)))
            {
                FieldAccessor result = TryGetFieldAccessor_Inner(moduleHandle, declaringTypeHandle, fieldTypeHandle, fieldHandle, CanonicalFormKind.Specific);

                if (result != null)
                    return result;
            }

            // If we can't find an specific canonical match, look for a universal match
            foreach (var moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(declaringTypeHandle)))
            {
                FieldAccessor result = TryGetFieldAccessor_Inner(moduleHandle, declaringTypeHandle, fieldTypeHandle, fieldHandle, CanonicalFormKind.Universal);

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

            ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
            externalReferences.InitializeCommonFixupsTable(mappingTableModule);

            CanonicallyEquivalentEntryLocator canonWrapper = new CanonicallyEquivalentEntryLocator(declaringTypeHandle, canonFormKind);
            var lookup = fieldHashtable.Lookup(canonWrapper.LookupHashCode);

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
                    && !(canonWrapper.IsCanonicallyEquivalent(entryDeclaringTypeHandle)))
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
                    if (!TypeLoaderEnvironment.Instance.TryGetFieldOffset(declaringTypeHandle, (uint)cookieOrOffsetOrOrdinal, out fieldOffset))
                    {
                        Debug.Assert(false);
                        return null;
                    }
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
                                if (entryFlags.HasFlag(FieldTableFlags.IsGcSection))
                                    fieldAddress = *(IntPtr*)TypeLoaderEnvironment.Instance.TryGetGcStaticFieldData(declaringTypeHandle) + fieldOffset;
                                else
                                    fieldAddress = *(IntPtr*)TypeLoaderEnvironment.Instance.TryGetNonGcStaticFieldData(declaringTypeHandle) + fieldOffset;
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
                        bool useFieldOffsetAccessor = false;
                        IntPtr fieldAddressCookie = IntPtr.Zero;

                        if (canonFormKind != CanonicalFormKind.Universal)
                        {
                            fieldAddressCookie = RvaToNonGenericStaticFieldAddress(mappingTableModule, fieldOffset);
                        }

                        if (!entryDeclaringTypeHandle.Equals(declaringTypeHandle))
                        {
                            // In this case we didn't find an exact match, but we did find a canonically equivalent match
                            // We might be in the dynamic type case, or the canonically equivalent, but not the same case.

                            if (RuntimeAugments.IsDynamicType(declaringTypeHandle))
                            {
                                if (canonFormKind == CanonicalFormKind.Universal)
                                {
                                    // If the declaring type is dynamic, and we found a universal canon match, we should use the universal canon path as fieldOffset will be meaningful
                                    useFieldOffsetAccessor = true;
                                }
                                else
                                {
                                    // We can use the non-universal path, as the fieldAddressCookie has two fields (field offset, and type offset), and for dynamic types, the type offset is ignored
                                    useFieldOffsetAccessor = false;
                                }
                            }
                            else
                            {
                                // We're working with a statically generated type, but we didn't find an exact match in the tables
                                if (canonFormKind != CanonicalFormKind.Universal)
                                    fieldOffset = checked((int)TypeLoaderEnvironment.GetThreadStaticTypeOffsetFromThreadStaticCookie(fieldAddressCookie));

                                fieldAddressCookie = TypeLoaderEnvironment.Instance.TryGetThreadStaticFieldOffsetCookieForTypeAndFieldOffset(declaringTypeHandle, checked((uint)fieldOffset));
                                useFieldOffsetAccessor = false;
                            }
                        }

                        if (useFieldOffsetAccessor)
                        {
                            return RuntimeAugments.IsValueType(fieldTypeHandle) ?
                                (FieldAccessor)new ValueTypeFieldAccessorForUniversalThreadStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), declaringTypeHandle, fieldOffset, fieldTypeHandle) :
                                (FieldAccessor)new ReferenceTypeFieldAccessorForUniversalThreadStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), declaringTypeHandle, fieldOffset, fieldTypeHandle);
                        }
                        else
                        {
                            return RuntimeAugments.IsValueType(fieldTypeHandle) ?
                                (FieldAccessor)new ValueTypeFieldAccessorForThreadStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), declaringTypeHandle, fieldAddressCookie, fieldTypeHandle) :
                                (FieldAccessor)new ReferenceTypeFieldAccessorForThreadStaticFields(TryGetStaticClassConstructionContext(declaringTypeHandle), declaringTypeHandle, fieldAddressCookie, fieldTypeHandle);
                        }
                }
            }

            return null;
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
            IntPtr nativeLayoutSignature = nameAndSignature.Signature;

            foreach (MethodHandle mh in typeDefinition.Methods)
            {
                Method method = mh.GetMethod(reader);
                if (method.Name.StringEquals(nameAndSignature.Name, reader))
                {
                    MethodSignatureComparer methodSignatureComparer = new MethodSignatureComparer(reader, mh);
                    if (methodSignatureComparer.IsMatchingNativeLayoutMethodSignature(nativeLayoutSignature))
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
            MethodNameAndSignature nameAndSignature;
            methodHandle = default(MethodHandle);
            if (!TypeLoaderEnvironment.Instance.TryGetRuntimeMethodHandleComponents(runtimeMethodHandle, out declaringTypeHandle, out nameAndSignature, out genericMethodTypeArgumentHandles))
                return false;

            return TryGetMetadataForTypeMethodNameAndSignature(declaringTypeHandle, nameAndSignature, out methodHandle);
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
            declaringTypeHandle = default(RuntimeTypeHandle);
            fieldHandle = default(FieldHandle);

            string fieldName;
            if (!TypeLoaderEnvironment.Instance.TryGetRuntimeFieldHandleComponents(runtimeFieldHandle, out declaringTypeHandle, out fieldName))
                return false;

            MetadataReader reader;
            TypeDefinitionHandle typeDefinitionHandle;
            RuntimeTypeHandle metadataLookupTypeHandle = declaringTypeHandle;

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
            foreach (FieldHandle fh in typeDefinition.Fields)
            {
                Field field = fh.GetField(reader);
                if (field.Name.StringEquals(fieldName, reader))
                {
                    fieldHandle = fh;
                    return true;
                }
            }

            return false;
        }

        //
        // This resolves RuntimeFieldHandles for fields declared on generic types (declaringTypeHandle is an input of this method.)
        //
        public sealed override bool TryGetFieldFromHandleAndType(RuntimeFieldHandle runtimeFieldHandle, RuntimeTypeHandle declaringTypeHandle, out FieldHandle fieldHandle)
        {
            RuntimeTypeHandle dummy;
            return TryGetFieldFromHandle(runtimeFieldHandle, out dummy, out fieldHandle);
        }

        /// <summary>
        /// Locate the static constructor context given the runtime type handle (EEType) for the type in question.
        /// </summary>
        /// <param name="typeHandle">EEtype of the type to look up</param>
        internal unsafe IntPtr TryGetStaticClassConstructionContext(RuntimeTypeHandle typeHandle)
        {
            return TypeLoaderEnvironment.TryGetStaticClassConstructionContext(typeHandle);
        }

        private struct MethodParametersInfo
        {
            private MetadataReader _metadataReader;
            private MethodBase _methodBase;
            private MethodHandle _methodHandle;

            private Handle[] _returnTypeAndParametersHandlesCache;
            private Type[] _returnTypeAndParametersTypesCache;

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
                    ParameterInfo[] parameters = _methodBase.GetParametersNoCopy();
                    LowLevelList<RuntimeTypeHandle> result = new LowLevelList<RuntimeTypeHandle>(parameters.Length);

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Type parameterType = parameters[i].ParameterType;

                        // If the parameter is a pointer type, use IntPtr. Else use the actual parameter type.
                        if (parameterType.IsPointer)
                            result.Add(CommonRuntimeTypes.IntPtr.TypeHandle);
                        else if (parameterType.IsByRef)
                            result.Add(parameterType.GetElementType().TypeHandle);
                        else if (parameterType.GetTypeInfo().IsEnum && !parameters[i].HasDefaultValue)
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
                    ParameterInfo[] parameters = _methodBase.GetParametersNoCopy();
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

                    _returnTypeAndParametersHandlesCache = new Handle[_methodBase.GetParametersNoCopy().Length + 1];
                    _returnTypeAndParametersTypesCache = new Type[_methodBase.GetParametersNoCopy().Length + 1];

                    MethodSignature signature = _methodHandle.GetMethod(_metadataReader).Signature.GetMethodSignature(_metadataReader);

                    // Check the return type for generic vars
                    MethodInfo reflectionMethodInfo = _methodBase as MethodInfo;
                    _returnTypeAndParametersTypesCache[0] = reflectionMethodInfo != null ? reflectionMethodInfo.ReturnType : CommonRuntimeTypes.Void;
                    _returnTypeAndParametersHandlesCache[0] = signature.ReturnType;

                    // Check the method parameters for generic vars
                    int index = 1;
                    foreach (Handle paramSigHandle in signature.Parameters)
                    {
                        _returnTypeAndParametersHandlesCache[index] = paramSigHandle;
                        _returnTypeAndParametersTypesCache[index] = _methodBase.GetParametersNoCopy()[index - 1].ParameterType;
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
                                        if (!TypeLoaderEnvironment.Instance.TryComputeHasInstantiationDeterminedSize(type.TypeHandle, out needsCallingConventionConverter))
                                            Environment.FailFast("Unable to setup calling convention converter correctly");
                                        return needsCallingConventionConverter;
                                    }
                                }
                            }
                            return false;
                    }
                }

                return false;
            }
        }
    }
}

