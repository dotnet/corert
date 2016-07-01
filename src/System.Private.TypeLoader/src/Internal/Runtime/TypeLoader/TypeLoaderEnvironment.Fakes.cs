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

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.TypeLoader
{
    [EagerOrderedStaticConstructor(EagerStaticConstructorOrder.TypeLoaderEnvironment)]
    public sealed partial class TypeLoaderEnvironment
    {
        public static readonly TypeLoaderEnvironment Instance;

        /// <summary>
        /// List of loaded binary modules is typically used to locate / process various metadata blobs
        /// and other per-module information.
        /// </summary>
        public readonly ModuleList ModuleList;

        static TypeLoaderEnvironment()
        {
            Instance = new TypeLoaderEnvironment();
        }

        public TypeLoaderEnvironment()
        {
            ModuleList = new ModuleList();
        }

        public bool GetTypeFromSignatureAndContext(IntPtr signature, RuntimeTypeHandle[] typeArgs, RuntimeTypeHandle[] methodArgs, out RuntimeTypeHandle createdType, out IntPtr remainingSignature)
        {
            throw new NotImplementedException();
        }

        public bool TryGetGenericVirtualMethodPointer(RuntimeTypeHandle targetTypeHandle, MethodNameAndSignature nameAndSignature, RuntimeTypeHandle[] genericMethodArgumentHandles, out IntPtr methodPointer, out IntPtr dictionaryPointer)
        {
            throw new NotImplementedException();
        }

        public unsafe bool TryGetMetadataForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeDefinitionHandle typeDefHandle)
        {
            // Iterate over all modules, starting with the module that defines the EEType
            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle)))
            {
                MetadataTable mapTable = MetadataTable.CreateTypeMapTable(moduleHandle);
                foreach (var ptrEntry in mapTable)
                {
                    var pCurrentEntry = (TypeMapEntry*)ptrEntry;
                    RuntimeTypeHandle entryTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(pCurrentEntry->EEType);
                    Handle entryMetadataHandle = pCurrentEntry->TypeDefinitionHandle.AsHandle();
                    if (entryTypeHandle.Equals(runtimeTypeHandle) &&
                        entryMetadataHandle.HandleType == HandleType.TypeDefinition)
                    {
                        metadataReader = ModuleList.Instance.GetMetadataReaderForModule(moduleHandle);
                        typeDefHandle = entryMetadataHandle.ToTypeDefinitionHandle(metadataReader);
                        return true;
                    }
                }
            }

            metadataReader = null;
            typeDefHandle = default(TypeDefinitionHandle);

            return false;
        }

        public unsafe bool TryGetNamedTypeForMetadata(MetadataReader metadataReader, TypeDefinitionHandle typeDefHandle, out RuntimeTypeHandle runtimeTypeHandle)
        {
            IntPtr moduleHandle = ModuleList.Instance.GetModuleForMetadataReader(metadataReader);
            MetadataTable mapTable = MetadataTable.CreateTypeMapTable(moduleHandle);
            foreach (var ptrEntry in mapTable)
            {
                TypeMapEntry* pCurrentEntry = (TypeMapEntry*)ptrEntry;
                if (pCurrentEntry->TypeDefinitionHandle.AsHandle().Equals(typeDefHandle))
                {
                    runtimeTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(pCurrentEntry->EEType);
                    return true;
                }
            }

            runtimeTypeHandle = default(RuntimeTypeHandle);
            return false;
        }

        public unsafe static bool TryGetTypeReferenceForNamedType(RuntimeTypeHandle runtimeTypeHandle, out MetadataReader metadataReader, out TypeReferenceHandle typeRefHandle)
        {
            // Iterate over all modules, starting with the module that defines the EEType
            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(runtimeTypeHandle)))
            {
                MetadataTable mapTable = MetadataTable.CreateTypeMapTable(moduleHandle);
                foreach (var ptrEntry in mapTable)
                {
                    var pCurrentEntry = (TypeMapEntry*)ptrEntry;
                    RuntimeTypeHandle entryTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(pCurrentEntry->EEType);
                    Handle entryMetadataHandle = pCurrentEntry->TypeDefinitionHandle.AsHandle();
                    if (entryTypeHandle.Equals(runtimeTypeHandle) &&
                        entryMetadataHandle.HandleType == HandleType.TypeReference)
                    {
                        metadataReader = ModuleList.Instance.GetMetadataReaderForModule(moduleHandle);
                        typeRefHandle = entryMetadataHandle.ToTypeReferenceHandle(metadataReader);
                        return true;
                    }
                }
            }

            metadataReader = null;
            typeRefHandle = default(TypeReferenceHandle);

            return false;
        }

        public unsafe static bool TryGetNamedTypeForTypeReference(MetadataReader metadataReader, TypeReferenceHandle typeRefHandle, out RuntimeTypeHandle runtimeTypeHandle)
        {
            IntPtr moduleHandle = ModuleList.Instance.GetModuleForMetadataReader(metadataReader);
            MetadataTable mapTable = MetadataTable.CreateTypeMapTable(moduleHandle);
            foreach (var ptrEntry in mapTable)
            {
                TypeMapEntry* pCurrentEntry = (TypeMapEntry*)ptrEntry;
                if (pCurrentEntry->TypeDefinitionHandle.AsHandle().Equals(typeRefHandle))
                {
                    runtimeTypeHandle = RuntimeAugments.CreateRuntimeTypeHandle(pCurrentEntry->EEType);
                    return true;
                }
            }

            runtimeTypeHandle = default(RuntimeTypeHandle);
            return false;
        }

        public bool TryGetArrayTypeForElementType(RuntimeTypeHandle elementTypeHandle, out RuntimeTypeHandle arrayTypeHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryGetPointerTypeForTargetType(RuntimeTypeHandle pointeeTypeHandle, out RuntimeTypeHandle pointerTypeHandle)
        {
            throw new NotImplementedException();
        }

        public IntPtr TryGetDefaultConstructorForType(RuntimeTypeHandle runtimeTypeHandle)
        {
            throw new NotImplementedException();
        }

        public IntPtr TryGetDefaultConstructorForTypeUsingLocator(object canonEquivalentEntryLocator)
        {
            throw new NotImplementedException();
        }

        public bool TryGetConstructedGenericTypeComponents(RuntimeTypeHandle runtimeTypeHandle, out RuntimeTypeHandle genericTypeDefinitionHandle, out RuntimeTypeHandle[] genericTypeArgumentHandles)
        {
            genericTypeDefinitionHandle = RuntimeAugments.GetGenericInstantiation(runtimeTypeHandle, out genericTypeArgumentHandles);
            return true;
        }

        public bool TryLookupConstructedGenericTypeForComponents(RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles, out RuntimeTypeHandle runtimeTypeHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryGetConstructedGenericTypeForComponents(RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles, out RuntimeTypeHandle runtimeTypeHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryGetMethodNameAndSignatureFromNativeLayoutSignature(ref IntPtr signature, out MethodNameAndSignature nameAndSignature)
        {
            throw new NotImplementedException();
        }

        public bool TryGetGenericMethodDictionaryForComponents(RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle[] genericMethodArgHandles, MethodNameAndSignature nameAndSignature, out IntPtr methodDictionary)
        {
            throw new NotImplementedException();
        }

        public bool TryGetMethodNameAndSignatureFromNativeLayoutOffset(IntPtr moduleHandle, uint nativeLayoutOffset, out MethodNameAndSignature nameAndSignature)
        {
            throw new NotImplementedException();
        }

        public bool TryGetMethodNameAndSignaturePointersFromNativeLayoutSignature(IntPtr module, uint methodNameAndSigToken, out IntPtr methodNameSigPtr, out IntPtr methodSigPtr)
        {
            throw new NotImplementedException();
        }

        public unsafe bool TryGetRuntimeMethodHandleForComponents(RuntimeTypeHandle declaringTypeHandle, IntPtr methodName, IntPtr methodSignature, RuntimeTypeHandle[] genericMethodArgs, out RuntimeMethodHandle handle)
        {
            throw new NotImplementedException();
        }

        public bool ConversionToCanonFormIsAChange(RuntimeTypeHandle[] genericArgHandles, CanonicalFormKind kind)
        {
            throw new NotImplementedException();
        }

        public bool TryComputeHasInstantiationDeterminedSize(RuntimeTypeHandle typeHandle, out bool hasInstantiationDeterminedSize)
        {
            throw new NotImplementedException();
        }

        public unsafe static IntPtr TryGetStaticClassConstructionContext(RuntimeTypeHandle typeHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryGetRuntimeFieldHandleComponents(RuntimeFieldHandle runtimeFieldHandle, out RuntimeTypeHandle declaringTypeHandle, out string fieldName)
        {
            throw new NotImplementedException();
        }

        public bool TryGetRuntimeMethodHandleComponents(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodArgs)
        {
            throw new NotImplementedException();
        }

        public IntPtr TryGetNonGcStaticFieldData(RuntimeTypeHandle runtimeTypeHandle)
        {
            throw new NotImplementedException();
        }

        public IntPtr TryGetGcStaticFieldData(RuntimeTypeHandle runtimeTypeHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryGetFieldOffset(RuntimeTypeHandle declaringTypeHandle, uint fieldOrdinal, out int fieldOffset)
        {
            throw new NotImplementedException();
        }

        public bool TryGetGenericMethodComponents(IntPtr methodDictionary, out RuntimeTypeHandle declaringType, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodArgumentHandles)
        {
            throw new NotImplementedException();
        }

        public static bool TryGetMethodInvokeMetadata(
            MetadataReader metadataReader,
            RuntimeTypeHandle declaringTypeHandle,
            MethodHandle methodHandle,
            RuntimeTypeHandle[] genericMethodTypeArgumentHandles,
            ref MethodSignatureComparer methodSignatureComparer,
            CanonicalFormKind canonFormKind,
            out MethodInvokeMetadata methodInvokeMetadata)
        {
            throw new NotImplementedException();
        }

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

        private const uint RVAIsIndirect = 0x80000000u;

        internal static unsafe RuntimeTypeHandle RvaToRuntimeTypeHandle(IntPtr moduleHandle, uint rva)
        {
            if ((rva & RVAIsIndirect) != 0)
            {
                return RuntimeAugments.CreateRuntimeTypeHandle(*(IntPtr*)((byte*)moduleHandle.ToPointer() + (rva & ~RVAIsIndirect)));
            }
            return RuntimeAugments.CreateRuntimeTypeHandle((IntPtr)((byte*)moduleHandle.ToPointer() + rva));
        }
    }

    internal static class RuntimeTypeHandleEETypeExtensions
    {
        public static unsafe IntPtr ToIntPtr(this RuntimeTypeHandle rtth)
        {
            return *(IntPtr*)&rtth;
        }
    }
}

namespace Internal.TypeSystem
{
    public enum CanonicalFormKind
    {
        Specific,
        Universal
    }
}

namespace Internal.Runtime.TypeLoader
{
    public class CallConverterThunk
    {
        public enum ThunkKind
        {
            StandardToGenericInstantiating,
            StandardToGenericInstantiatingIfNotHasThis,
            StandardToGeneric,
            GenericToStandard,
            StandardUnboxing,
            StandardUnboxingAndInstantiatingGeneric,
            GenericToStandardWithTargetPointerArg,
            GenericToStandardWithTargetPointerArgAndParamArg,
            GenericToStandardWithTargetPointerArgAndMaybeParamArg,
            DelegateInvokeOpenStaticThunk,
            DelegateInvokeClosedStaticThunk,
            DelegateInvokeOpenInstanceThunk,
            DelegateInvokeInstanceClosedOverGenericMethodThunk,
            DelegateMulticastThunk,
            DelegateObjectArrayThunk,
            DelegateDynamicInvokeThunk,
            ReflectionDynamicInvokeThunk,
        }

        public unsafe static IntPtr MakeThunk(ThunkKind thunkKind,
                                              IntPtr targetPointer,
                                              IntPtr instantiatingArg,
                                              bool hasThis, RuntimeTypeHandle[] parameters,
                                              bool[] byRefParameters,
                                              bool[] paramsByRefForced)
        {
            throw new NotImplementedException();
        }
    }

    public struct CanonicallyEquivalentEntryLocator
    {
        public CanonicallyEquivalentEntryLocator(RuntimeTypeHandle typeToFind, CanonicalFormKind kind)
        {
            throw new NotImplementedException();
        }

        public int LookupHashCode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsCanonicallyEquivalent(RuntimeTypeHandle other)
        {
            throw new NotImplementedException();
        }
    }

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
        /// Default value
        /// </summary>
        public string DefaultValueString;

        /// <summary>
        /// Dynamic invoke cookie
        /// </summary>
        public uint DynamicInvokeCookie;

        /// <summary>
        /// Invoke flags
        /// </summary>
        public InvokeTableFlags InvokeTableFlags;
    }
}
