// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;

using Internal.Metadata.NativeFormat;
using Internal.NativeFormat;
using Internal.TypeSystem;
using Internal.TypeSystem.NativeFormat;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.TypeLoader
{
    internal class Callbacks : TypeLoaderCallbacks
    {
        public override bool TryGetConstructedGenericTypeForComponents(RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles, out RuntimeTypeHandle runtimeTypeHandle)
        {
            return TypeLoaderEnvironment.Instance.TryGetConstructedGenericTypeForComponents(genericTypeDefinitionHandle, genericTypeArgumentHandles, out runtimeTypeHandle);
        }

        public override int GetThreadStaticsSizeForDynamicType(int index, out int numTlsCells)
        {
            return TypeLoaderEnvironment.Instance.TryGetThreadStaticsSizeForDynamicType(index, out numTlsCells);
        }

        public override IntPtr GenericLookupFromContextAndSignature(IntPtr context, IntPtr signature, out IntPtr auxResult)
        {
            return TypeLoaderEnvironment.Instance.GenericLookupFromContextAndSignature(context, signature, out auxResult);
        }

        public override bool GetRuntimeMethodHandleComponents(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodArgs)
        {
            return TypeLoaderEnvironment.Instance.TryGetRuntimeMethodHandleComponents(runtimeMethodHandle, out declaringTypeHandle, out nameAndSignature, out genericMethodArgs);
        }

        public override bool CompareMethodSignatures(RuntimeSignature signature1, RuntimeSignature signature2)
        {
            return TypeLoaderEnvironment.Instance.CompareMethodSignatures(signature1, signature2);
        }

        public override IntPtr TryGetDefaultConstructorForType(RuntimeTypeHandle runtimeTypeHandle)
        {
            return TypeLoaderEnvironment.Instance.TryGetDefaultConstructorForType(runtimeTypeHandle);
        }

        public override IntPtr GetDelegateThunk(Delegate delegateObject, int thunkKind)
        {
            return CallConverterThunk.GetDelegateThunk(delegateObject, thunkKind);
        }

        public override bool TryGetGenericVirtualTargetForTypeAndSlot(RuntimeTypeHandle targetHandle, ref RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, ref string methodName, ref RuntimeSignature methodSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer, out bool slotUpdated)
        {
            return TypeLoaderEnvironment.Instance.TryGetGenericVirtualTargetForTypeAndSlot(targetHandle, ref declaringType, genericArguments, ref methodName, ref methodSignature, out methodPointer, out dictionaryPointer, out slotUpdated);
        }

        /// <summary>
        /// Register a new runtime-allocated code thunk in the diagnostic stream.
        /// </summary>
        /// <param name="thunkAddress">Address of thunk to register</param>
        public override void RegisterThunk(IntPtr thunkAddress)
        {
            SerializedDebugData.RegisterTailCallThunk(thunkAddress);
        }
    }

    public static class RuntimeSignatureExtensions
    {
        public static IntPtr NativeLayoutSignature(this RuntimeSignature signature)
        {
            if (!signature.IsNativeLayoutSignature)
                return IntPtr.Zero;

            NativeReader reader = TypeLoaderEnvironment.Instance.GetNativeLayoutInfoReader(signature.ModuleHandle);
            return reader.OffsetToAddress((uint)signature.Token);
        }
    }

    [EagerOrderedStaticConstructor(EagerStaticConstructorOrder.TypeLoaderEnvironment)]
    public sealed partial class TypeLoaderEnvironment
    {
        [ThreadStatic]
        private static bool t_isReentrant;

        public static readonly TypeLoaderEnvironment Instance;

        /// <summary>
        /// List of loaded binary modules is typically used to locate / process various metadata blobs
        /// and other per-module information.
        /// </summary>
        public readonly ModuleList ModuleList;

        // Cache the NativeReader in each module to avoid looking up the NativeLayoutInfo blob each
        // time we call GetNativeLayoutInfoReader(). The dictionary is a thread static variable to ensure
        // thread safety. Using ThreadStatic instead of a lock is ok as long as the NativeReader class is 
        // small enough in size (which is the case today).
        [ThreadStatic]
        private static LowLevelDictionary<IntPtr, NativeReader> t_moduleNativeReaders;

        static TypeLoaderEnvironment()
        {
            Instance = new TypeLoaderEnvironment();
            RuntimeAugments.InitializeLookups(new Callbacks());
        }

        public TypeLoaderEnvironment()
        {
            ModuleList = new ModuleList();
        }

        // To keep the synchronization simple, we execute all type loading under a global lock
        private Lock _typeLoaderLock = new Lock();

        public void VerifyTypeLoaderLockHeld()
        {
            if (!_typeLoaderLock.IsAcquired)
                Environment.FailFast("TypeLoaderLock not held");
        }

        public IntPtr GenericLookupFromContextAndSignature(IntPtr context, IntPtr signature, out IntPtr auxResult)
        {
            IntPtr result;

            using (LockHolder.Hold(_typeLoaderLock))
            {
                try
                {
                    if (t_isReentrant)
                        Environment.FailFast("Reentrant lazy generic lookup");
                    t_isReentrant = true;

                    result = TypeBuilder.BuildGenericLookupTarget(context, signature, out auxResult);

                    t_isReentrant = false;
                }
                catch
                {
                    // Catch and rethrow any exceptions instead of using finally block. Otherwise, filters that are run during 
                    // the first pass of exception unwind may hit the re-entrancy fail fast above.

                    // TODO: Convert this to filter for better diagnostics once we switch to Roslyn

                    t_isReentrant = false;
                    throw;
                }
            }

            return result;
        }

        private bool EnsureTypeHandleForType(TypeDesc type)
        {
            if (type.RuntimeTypeHandle.IsNull())
            {
                using (LockHolder.Hold(_typeLoaderLock))
                {
                    // Now that we hold the lock, we may find that existing types can now find
                    // their associated RuntimeTypeHandle. Flush the type builder states as a way
                    // to force the reresolution of RuntimeTypeHandles which couldn't be found before.
                    type.Context.FlushTypeBuilderStates();
                    try
                    {
                        new TypeBuilder().BuildType(type);
                    }
                    catch (TypeBuilder.MissingTemplateException)
                    {
                        return false;
                    }
                }
            }

            // Returned type has to have a valid type handle value, unless it's a byref type
            // (byref types don't have any associated EETypes in the runtime)
            Debug.Assert(!type.RuntimeTypeHandle.IsNull() || (type is ByRefType));
            return !type.RuntimeTypeHandle.IsNull() || (type is ByRefType);
        }

        private TypeDesc GetConstructedTypeFromParserAndNativeLayoutContext(ref NativeParser parser, NativeLayoutInfoLoadContext nativeLayoutContext)
        {
            TypeDesc parsedType = nativeLayoutContext.GetType(ref parser);
            if (parsedType == null)
                return null;

            if (!EnsureTypeHandleForType(parsedType))
                return null;

            return parsedType;
        }

        //
        // Parse a native layout signature pointed to by "signature" in the executable image, optionally using
        // "typeArgs" and "methodArgs" for generic type parameter substitution.  The first field in "signature"
        // must be an encoded type but any data beyond that is user-defined and returned in "remainingSignature"
        //
        internal bool GetTypeFromSignatureAndContext(ref RuntimeSignature signature, RuntimeTypeHandle[] typeArgs, RuntimeTypeHandle[] methodArgs, out RuntimeTypeHandle createdType, out RuntimeSignature remainingSignature)
        {
            NativeReader reader = GetNativeLayoutInfoReader(signature.ModuleHandle);
            NativeParser parser = new NativeParser(reader, (uint)signature.Token);

            bool result = GetTypeFromSignatureAndContext(ref parser, signature.ModuleHandle, typeArgs, methodArgs, out createdType);

            remainingSignature = RuntimeSignature.CreateFromNativeLayoutSignature(signature.ModuleHandle, (int)parser.Offset);

            return result;
        }

        internal bool GetTypeFromSignatureAndContext(ref NativeParser parser, IntPtr moduleHandle, RuntimeTypeHandle[] typeArgs, RuntimeTypeHandle[] methodArgs, out RuntimeTypeHandle createdType)
        {
            createdType = default(RuntimeTypeHandle);
            TypeSystemContext context = TypeSystemContextFactory.Create();

            TypeDesc parsedType = TryParseNativeSignatureWorker(context, moduleHandle, ref parser, typeArgs, methodArgs, false) as TypeDesc;
            if (parsedType == null)
                return false;

            if (!EnsureTypeHandleForType(parsedType))
                return false;

            createdType = parsedType.RuntimeTypeHandle;

            TypeSystemContextFactory.Recycle(context);
            return true;
        }

        //
        // Parse a native layout signature pointed to by "signature" in the executable image, optionally using
        // "typeArgs" and "methodArgs" for generic type parameter substitution.  The first field in "signature"
        // must be an encoded method but any data beyond that is user-defined and returned in "remainingSignature"
        //
        public bool GetMethodFromSignatureAndContext(ref RuntimeSignature signature, RuntimeTypeHandle[] typeArgs, RuntimeTypeHandle[] methodArgs, out RuntimeTypeHandle createdType, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles, out RuntimeSignature remainingSignature)
        {
            NativeReader reader = GetNativeLayoutInfoReader(signature.ModuleHandle);
            NativeParser parser = new NativeParser(reader, (uint)signature.Token);

            bool result = GetMethodFromSignatureAndContext(ref parser, signature.ModuleHandle, typeArgs, methodArgs, out createdType, out nameAndSignature, out genericMethodTypeArgumentHandles);

            remainingSignature = RuntimeSignature.CreateFromNativeLayoutSignature(signature.ModuleHandle, (int)parser.Offset);

            return result;
        }

        internal bool GetMethodFromSignatureAndContext(ref NativeParser parser, IntPtr moduleHandle, RuntimeTypeHandle[] typeArgs, RuntimeTypeHandle[] methodArgs, out RuntimeTypeHandle createdType, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodTypeArgumentHandles)
        {
            createdType = default(RuntimeTypeHandle);
            nameAndSignature = null;
            genericMethodTypeArgumentHandles = null;

            TypeSystemContext context = TypeSystemContextFactory.Create();

            MethodDesc parsedMethod = TryParseNativeSignatureWorker(context, moduleHandle, ref parser, typeArgs, methodArgs, true) as MethodDesc;
            if (parsedMethod == null)
                return false;

            if (!EnsureTypeHandleForType(parsedMethod.OwningType))
                return false;

            createdType = parsedMethod.OwningType.RuntimeTypeHandle;
            nameAndSignature = parsedMethod.NameAndSignature;
            if (parsedMethod.Instantiation.Length > 0)
            {
                genericMethodTypeArgumentHandles = new RuntimeTypeHandle[parsedMethod.Instantiation.Length];
                for (int i = 0; i < parsedMethod.Instantiation.Length; ++i)
                {
                    if (!EnsureTypeHandleForType(parsedMethod.Instantiation[i]))
                        return false;

                    genericMethodTypeArgumentHandles[i] = parsedMethod.Instantiation[i].RuntimeTypeHandle;
                }
            }

            TypeSystemContextFactory.Recycle(context);

            return true;
        }

        //
        // Returns the native layout info reader
        //
        internal unsafe NativeReader GetNativeLayoutInfoReader(IntPtr moduleHandle)
        {
            Debug.Assert(moduleHandle != IntPtr.Zero);

            if (t_moduleNativeReaders == null)
                t_moduleNativeReaders = new LowLevelDictionary<IntPtr, NativeReader>();

            NativeReader result = null;
            if (t_moduleNativeReaders.TryGetValue(moduleHandle, out result))
                return result;

            byte* pBlob;
            uint cbBlob;
            if (RuntimeAugments.FindBlob(moduleHandle, (int)ReflectionMapBlob.NativeLayoutInfo, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
                result = new NativeReader(pBlob, cbBlob);

            t_moduleNativeReaders.Add(moduleHandle, result);
            return result;
        }

        private static RuntimeTypeHandle[] GetTypeSequence(ref ExternalReferencesTable extRefs, ref NativeParser parser)
        {
            uint count = parser.GetUnsigned();
            RuntimeTypeHandle[] result = new RuntimeTypeHandle[count];
            for (uint i = 0; i < count; i++)
                result[i] = extRefs.GetRuntimeTypeHandleFromIndex(parser.GetUnsigned());

            return result;
        }

        private static RuntimeTypeHandle[] TypeDescsToRuntimeHandles(Instantiation types)
        {
            var result = new RuntimeTypeHandle[types.Length];
            for (int i = 0; i < types.Length; i++)
                result[i] = types[i].RuntimeTypeHandle;

            return result;
        }

        public bool TryGetConstructedGenericTypeForComponents(RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles, out RuntimeTypeHandle runtimeTypeHandle)
        {
            if (TryLookupConstructedGenericTypeForComponents(genericTypeDefinitionHandle, genericTypeArgumentHandles, out runtimeTypeHandle))
                return true;

            using (LockHolder.Hold(_typeLoaderLock))
            {
                return TypeBuilder.TryBuildGenericType(genericTypeDefinitionHandle, genericTypeArgumentHandles, out runtimeTypeHandle);
            }
        }

        // Get an array RuntimeTypeHandle given an element's RuntimeTypeHandle and rank. Pass false for isMdArray, and rank == -1 for SzArrays
        public bool TryGetArrayTypeForElementType(RuntimeTypeHandle elementTypeHandle, bool isMdArray, int rank, out RuntimeTypeHandle arrayTypeHandle)
        {
            if (TryGetArrayTypeForElementType_LookupOnly(elementTypeHandle, isMdArray, rank, out arrayTypeHandle))
            {
                return true;
            }

            using (LockHolder.Hold(_typeLoaderLock))
            {
                if (isMdArray && (rank < MDArray.MinRank) && (rank > MDArray.MaxRank))
                {
                    arrayTypeHandle = default(RuntimeTypeHandle);
                    return false;
                }

                if (TypeSystemContext.GetArrayTypesCache(isMdArray, rank).TryGetValue(elementTypeHandle, out arrayTypeHandle))
                    return true;

                return TypeBuilder.TryBuildArrayType(elementTypeHandle, isMdArray, rank, out arrayTypeHandle);
            }
        }

        // Looks up an array RuntimeTypeHandle given an element's RuntimeTypeHandle and rank. A rank of -1 indicates SzArray
        internal bool TryGetArrayTypeForElementType_LookupOnly(RuntimeTypeHandle elementTypeHandle, bool isMdArray, int rank, out RuntimeTypeHandle arrayTypeHandle)
        {
            if (isMdArray && (rank < MDArray.MinRank) && (rank > MDArray.MaxRank))
            {
                arrayTypeHandle = default(RuntimeTypeHandle);
                return false;
            }

            if (TypeSystemContext.GetArrayTypesCache(isMdArray, rank).TryGetValue(elementTypeHandle, out arrayTypeHandle))
                return true;

            if (!isMdArray &&
                !RuntimeAugments.IsDynamicType(elementTypeHandle) &&
                TryGetArrayTypeForNonDynamicElementType(elementTypeHandle, out arrayTypeHandle))
            {
                TypeSystemContext.GetArrayTypesCache(isMdArray, rank).AddOrGetExisting(arrayTypeHandle);
                return true;
            }

            return false;
        }

        public bool TryGetPointerTypeForTargetType(RuntimeTypeHandle pointeeTypeHandle, out RuntimeTypeHandle pointerTypeHandle)
        {
            // There are no lookups for pointers in static modules. All pointer EETypes will be created at this level.
            // It's possible to have multiple pointer EETypes representing the same pointer type with the same element type
            // The caching of pointer types is done at the reflection layer (in the RuntimeTypeUnifier) and
            // here in the TypeSystemContext layer

            if (TypeSystemContext.PointerTypesCache.TryGetValue(pointeeTypeHandle, out pointerTypeHandle))
                return true;

            using (LockHolder.Hold(_typeLoaderLock))
            {
                return TypeBuilder.TryBuildPointerType(pointeeTypeHandle, out pointerTypeHandle);
            }
        }

        public int GetCanonicalHashCode(RuntimeTypeHandle typeHandle, CanonicalFormKind kind)
        {
            TypeSystemContext context = TypeSystemContextFactory.Create();
            TypeDesc type = context.ResolveRuntimeTypeHandle(typeHandle);
            int hashCode = type.ConvertToCanonForm(kind).GetHashCode();
            TypeSystemContextFactory.Recycle(context);

            return hashCode;
        }

        private object TryParseNativeSignatureWorker(TypeSystemContext typeSystemContext, IntPtr moduleHandle, ref NativeParser parser, RuntimeTypeHandle[] typeGenericArgumentHandles, RuntimeTypeHandle[] methodGenericArgumentHandles, bool isMethodSignature)
        {
            Instantiation typeGenericArguments = typeSystemContext.ResolveRuntimeTypeHandles(typeGenericArgumentHandles ?? Array.Empty<RuntimeTypeHandle>());
            Instantiation methodGenericArguments = typeSystemContext.ResolveRuntimeTypeHandles(methodGenericArgumentHandles ?? Array.Empty<RuntimeTypeHandle>());

            NativeLayoutInfoLoadContext nativeLayoutContext = new NativeLayoutInfoLoadContext();
            nativeLayoutContext._moduleHandle = moduleHandle;
            nativeLayoutContext._typeSystemContext = typeSystemContext;
            nativeLayoutContext._typeArgumentHandles = typeGenericArguments;
            nativeLayoutContext._methodArgumentHandles = methodGenericArguments;

            if (isMethodSignature)
                return nativeLayoutContext.GetMethod(ref parser);
            else
                return nativeLayoutContext.GetType(ref parser);
        }

        public bool TryGetGenericMethodDictionaryForComponents(RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle[] genericMethodArgHandles, MethodNameAndSignature nameAndSignature, out IntPtr methodDictionary)
        {
            if (TryLookupGenericMethodDictionaryForComponents(declaringTypeHandle, nameAndSignature, genericMethodArgHandles, out methodDictionary))
                return true;

            using (LockHolder.Hold(_typeLoaderLock))
            {
                return TypeBuilder.TryBuildGenericMethod(declaringTypeHandle, genericMethodArgHandles, nameAndSignature, out methodDictionary);
            }
        }

        public bool TryGetFieldOffset(RuntimeTypeHandle declaringTypeHandle, uint fieldOrdinal, out int fieldOffset)
        {
            fieldOffset = int.MinValue;

            // No use going further for non-generic types... TypeLoader doesn't have offset answers for non-generic types!
            if (!declaringTypeHandle.IsGenericType())
                return false;

            using (LockHolder.Hold(_typeLoaderLock))
            {
                return TypeBuilder.TryGetFieldOffset(declaringTypeHandle, fieldOrdinal, out fieldOffset);
            }
        }

        public bool CanInstantiationsShareCode(RuntimeTypeHandle[] genericArgHandles1, RuntimeTypeHandle[] genericArgHandles2, CanonicalFormKind kind)
        {
            if (genericArgHandles1.Length != genericArgHandles2.Length)
                return false;

            bool match = true;

            TypeSystemContext context = TypeSystemContextFactory.Create();

            for (int i = 0; i < genericArgHandles1.Length; i++)
            {
                TypeDesc genericArg1 = context.ResolveRuntimeTypeHandle(genericArgHandles1[i]);
                TypeDesc genericArg2 = context.ResolveRuntimeTypeHandle(genericArgHandles2[i]);

                if (context.ConvertToCanon(genericArg1, kind) != context.ConvertToCanon(genericArg2, kind))
                {
                    match = false;
                    break;
                }
            }

            TypeSystemContextFactory.Recycle(context);

            return match;
        }

        public bool ConversionToCanonFormIsAChange(RuntimeTypeHandle[] genericArgHandles, CanonicalFormKind kind)
        {
            // Todo: support for universal canon type?

            TypeSystemContext context = TypeSystemContextFactory.Create();

            Instantiation genericArgs = context.ResolveRuntimeTypeHandles(genericArgHandles);
            bool result;
            context.ConvertInstantiationToCanonForm(genericArgs, kind, out result);

            TypeSystemContextFactory.Recycle(context);

            return result;
        }

        // get the generics hash table and external references table for a module
        // TODO multi-file: consider whether we want to cache this info
        private unsafe bool GetHashtableFromBlob(IntPtr moduleHandle, ReflectionMapBlob blobId, out NativeHashtable hashtable, out ExternalReferencesTable externalReferencesLookup)
        {
            byte* pBlob;
            uint cbBlob;

            hashtable = default(NativeHashtable);
            externalReferencesLookup = default(ExternalReferencesTable);

            if (!RuntimeAugments.FindBlob(moduleHandle, (int)blobId, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
                return false;

            NativeReader reader = new NativeReader(pBlob, cbBlob);
            NativeParser parser = new NativeParser(reader, 0);

            hashtable = new NativeHashtable(parser);

            return externalReferencesLookup.InitializeNativeReferences(moduleHandle);
        }

        public static unsafe void GetFieldAlignmentAndSize(RuntimeTypeHandle fieldType, out int alignment, out int size)
        {
            EEType* typePtr = fieldType.ToEETypePtr();
            if (typePtr->IsValueType)
            {
                size = (int)typePtr->ValueTypeSize;
            }
            else
            {
                size = IntPtr.Size;
            }

            alignment = (int)typePtr->FieldAlignmentRequirement;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UnboxingAndInstantiatingStubMapEntry
        {
            public uint StubMethodRva;
            public uint MethodRva;
        }

        public static unsafe bool TryGetTargetOfUnboxingAndInstantiatingStub(IntPtr maybeInstantiatingAndUnboxingStub, out IntPtr targetMethod)
        {
            targetMethod = IntPtr.Zero;

            // Get module
            IntPtr associatedModule = RuntimeAugments.GetModuleFromPointer(maybeInstantiatingAndUnboxingStub);
            if (associatedModule == IntPtr.Zero)
            {
                return false;
            }

            // Get UnboxingAndInstantiatingTable
            UnboxingAndInstantiatingStubMapEntry* pBlob;
            uint cbBlob;

            if (!RuntimeAugments.FindBlob(associatedModule, (int)ReflectionMapBlob.UnboxingAndInstantiatingStubMap, (IntPtr)(&pBlob), (IntPtr)(&cbBlob)))
            {
                return false;
            }

            uint cStubs = cbBlob / (uint)sizeof(UnboxingAndInstantiatingStubMapEntry);

            for (uint i = 0; i < cStubs; ++i)
            {
                if (RvaToFunctionPointer(associatedModule, pBlob[i].StubMethodRva) == maybeInstantiatingAndUnboxingStub)
                {
                    // We found a match, create pointer from RVA and move on.
                    targetMethod = RvaToFunctionPointer(associatedModule, pBlob[i].MethodRva);
                    return true;
                }
            }

            // Stub not found.
            return false;
        }

        public bool TryComputeHasInstantiationDeterminedSize(RuntimeTypeHandle typeHandle, out bool hasInstantiationDeterminedSize)
        {
            TypeSystemContext context = TypeSystemContextFactory.Create();
            bool success = TryComputeHasInstantiationDeterminedSize(typeHandle, context, out hasInstantiationDeterminedSize);
            TypeSystemContextFactory.Recycle(context);

            return success;
        }

        public bool TryComputeHasInstantiationDeterminedSize(RuntimeTypeHandle typeHandle, TypeSystemContext context, out bool hasInstantiationDeterminedSize)
        {
            Debug.Assert(RuntimeAugments.IsGenericType(typeHandle) || RuntimeAugments.IsGenericTypeDefinition(typeHandle));
            DefType type = (DefType)context.ResolveRuntimeTypeHandle(typeHandle);

            return TryComputeHasInstantiationDeterminedSize(type, out hasInstantiationDeterminedSize);
        }

        internal bool TryComputeHasInstantiationDeterminedSize(DefType type, out bool hasInstantiationDeterminedSize)
        {
            Debug.Assert(type.HasInstantiation);

            NativeLayoutInfoLoadContext loadContextUniversal;
            NativeLayoutInfo universalLayoutInfo;
            NativeParser parser = type.GetOrCreateTypeBuilderState().GetParserForUniversalNativeLayoutInfo(out loadContextUniversal, out universalLayoutInfo);
            if (parser.IsNull)
            {
                hasInstantiationDeterminedSize = false;
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
                // TODO, Add logic which uses type loader to identify which types are affected by loading the 
                // universal generic type, and checking its size. At this time, the type loader cannot correctly
                // compute sizes of generic types that are instantiated over UniversalCanon
                Environment.FailFast("Unable to determine if a generic has an instantiation determined size.");
#endif
                return false;
            }

            int? flags = (int?)parser.GetUnsignedForBagElementKind(BagElementKind.TypeFlags);

            hasInstantiationDeterminedSize = flags.HasValue ?
                (((NativeFormat.TypeFlags)flags) & NativeFormat.TypeFlags.HasInstantiationDeterminedSize) != 0 :
                false;

            return true;
        }

        public bool TryResolveSingleMetadataFixup(IntPtr module, int metadataToken, MetadataFixupKind fixupKind, out IntPtr fixupResolution)
        {
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
            using (LockHolder.Hold(_typeLoaderLock))
            {
                try
                {
                    return TypeBuilder.TryResolveSingleMetadataFixup(module, metadataToken, fixupKind, out fixupResolution);
                }
                catch (Exception ex)
                {
                    Environment.FailFast("Failed to resolve metadata token " +
                        ((uint)metadataToken).LowLevelToString() + ": " + ex.Message);
#else
                    Environment.FailFast("Failed to resolve metadata token " +
                        ((uint)metadataToken).LowLevelToString());
#endif
                    fixupResolution = IntPtr.Zero;
                    return false;
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
                }
            }
#endif
        }

        public bool TryDispatchMethodOnTarget(IntPtr module, int metadataToken, RuntimeTypeHandle targetInstanceType, out IntPtr methodAddress)
        {
            using (LockHolder.Hold(_typeLoaderLock))
            {
                return TryDispatchMethodOnTarget_Inner(
                    module,
                    metadataToken,
                    targetInstanceType,
                    out methodAddress);
            }
        }

#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
        internal DispatchCellInfo ConvertDispatchCellInfo(IntPtr module, DispatchCellInfo cellInfo)
        {
            using (LockHolder.Hold(_typeLoaderLock))
            {
                return ConvertDispatchCellInfo_Inner(
                    module,
                    cellInfo);
            }
        }
#endif

        internal bool TryResolveTypeSlotDispatch(IntPtr targetTypeAsIntPtr, IntPtr interfaceTypeAsIntPtr, ushort slot, out IntPtr methodAddress)
        {
            using (LockHolder.Hold(_typeLoaderLock))
            {
                return TryResolveTypeSlotDispatch_Inner(targetTypeAsIntPtr, interfaceTypeAsIntPtr, slot, out methodAddress);
            }
        }

        public unsafe bool TryGetOrCreateNamedTypeForMetadata(
            MetadataReader metadataReader,
            TypeDefinitionHandle typeDefHandle,
            out RuntimeTypeHandle runtimeTypeHandle)
        {
            if (TryGetNamedTypeForMetadata(metadataReader, typeDefHandle, out runtimeTypeHandle))
            {
                return true;
            }
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
            IntPtr moduleHandle = ModuleList.Instance.GetModuleForMetadataReader(metadataReader);
            IntPtr runtimeTypeHandleAsIntPtr;
            if (TryResolveSingleMetadataFixup(
                moduleHandle,
                typeDefHandle.ToHandle(metadataReader).ToInt(),
                MetadataFixupKind.TypeHandle,
                out runtimeTypeHandleAsIntPtr))
            {
                runtimeTypeHandle = *(RuntimeTypeHandle*)&runtimeTypeHandleAsIntPtr;
                return true;
            }
#endif
            return false;
        }
    }
}
