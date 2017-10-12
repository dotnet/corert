// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;
using System.Collections.Generic;
using System.Threading;

using Internal.Metadata.NativeFormat;
using Internal.TypeSystem;

namespace Internal.Runtime.TypeLoader
{
    internal static class RuntimeTypeHandleEETypeExtensions
    {
        public static unsafe EEType* ToEETypePtr(this RuntimeTypeHandle rtth)
        {
            return (EEType*)(*(IntPtr*)&rtth);
        }

        public static unsafe IntPtr ToIntPtr(this RuntimeTypeHandle rtth)
        {
            return *(IntPtr*)&rtth;
        }

        public static unsafe bool IsDynamicType(this RuntimeTypeHandle rtth)
        {
            return rtth.ToEETypePtr()->IsDynamicType;
        }

        public static unsafe int GetNumVtableSlots(this RuntimeTypeHandle rtth)
        {
            return rtth.ToEETypePtr()->NumVtableSlots;
        }

        public static unsafe IntPtr GetDictionary(this RuntimeTypeHandle rtth)
        {
            return EETypeCreator.GetDictionary(rtth.ToEETypePtr());
        }

        public static unsafe void SetDictionary(this RuntimeTypeHandle rtth, int dictionarySlot, IntPtr dictionary)
        {
            Debug.Assert(rtth.ToEETypePtr()->IsDynamicType && dictionarySlot < rtth.GetNumVtableSlots());
            *(IntPtr*)((byte*)rtth.ToEETypePtr() + sizeof(EEType) + dictionarySlot * IntPtr.Size) = dictionary;
        }

        public static unsafe void SetInterface(this RuntimeTypeHandle rtth, int interfaceIndex, RuntimeTypeHandle interfaceType)
        {
            rtth.ToEETypePtr()->InterfaceMap[interfaceIndex].InterfaceType = interfaceType.ToEETypePtr();
        }

        public static unsafe void SetGenericDefinition(this RuntimeTypeHandle rtth, RuntimeTypeHandle genericDefinitionHandle)
        {
            rtth.ToEETypePtr()->GenericDefinition = genericDefinitionHandle.ToEETypePtr();
        }

        public static unsafe void SetGenericArgument(this RuntimeTypeHandle rtth, int argumentIndex, RuntimeTypeHandle argumentType)
        {
            rtth.ToEETypePtr()->GenericArguments[argumentIndex].Value = argumentType.ToEETypePtr();
        }

        public static unsafe void SetNullableType(this RuntimeTypeHandle rtth, RuntimeTypeHandle T_typeHandle)
        {
            rtth.ToEETypePtr()->NullableType = T_typeHandle.ToEETypePtr();
        }

        public static unsafe void SetRelatedParameterType(this RuntimeTypeHandle rtth, RuntimeTypeHandle relatedTypeHandle)
        {
            rtth.ToEETypePtr()->RelatedParameterType = relatedTypeHandle.ToEETypePtr();
        }

        public static unsafe void SetParameterizedTypeShape(this RuntimeTypeHandle rtth, uint value)
        {
            rtth.ToEETypePtr()->ParameterizedTypeShape = value;
        }

        public static unsafe void SetBaseType(this RuntimeTypeHandle rtth, RuntimeTypeHandle baseTypeHandle)
        {
            rtth.ToEETypePtr()->BaseType = baseTypeHandle.ToEETypePtr();
        }

        public static unsafe void SetComponentSize(this RuntimeTypeHandle rtth, UInt16 componentSize)
        {
            rtth.ToEETypePtr()->ComponentSize = componentSize;
        }
    }

    internal class MemoryHelpers
    {
        public static int AlignUp(int val, int alignment)
        {
            Debug.Assert(val >= 0 && alignment >= 0);

            // alignment must be a power of 2 for this implementation to work (need modulo otherwise)
            Debug.Assert(0 == (alignment & (alignment - 1)));
            int result = (val + (alignment - 1)) & ~(alignment - 1);
            Debug.Assert(result >= val);      // check for overflow

            return result;
        }

        public static unsafe void Memset(IntPtr destination, int length, byte value)
        {
            byte* pbDest = (byte*)destination.ToPointer();
            while (length > 0)
            {
                *pbDest = value;
                pbDest++;
                length--;
            }
        }

        public static IntPtr AllocateMemory(int cbBytes)
        {
            return PInvokeMarshal.MemAlloc(new IntPtr(cbBytes));
        }

        public static void FreeMemory(IntPtr memoryPtrToFree)
        {
            PInvokeMarshal.MemFree(memoryPtrToFree);
        }
    }

    internal unsafe class EETypeCreator
    {
        private static IntPtr s_emptyGCDesc;

        private static void CreateEETypeWorker(EEType* pTemplateEEType, UInt32 hashCodeOfNewType,
            int arity, bool requireVtableSlotMapping, TypeBuilderState state)
        {
            bool successful = false;
            IntPtr eeTypePtrPlusGCDesc = IntPtr.Zero;
            IntPtr dynamicDispatchMapPtr = IntPtr.Zero;
            DynamicModule* dynamicModulePtr = null;
            IntPtr gcStaticData = IntPtr.Zero;
            IntPtr gcStaticsIndirection = IntPtr.Zero;

            try
            {
                Debug.Assert((pTemplateEEType != null) || (state.TypeBeingBuilt as MetadataType != null));

                // In some situations involving arrays we can find as a template a dynamically generated type.
                // In that case, the correct template would be the template used to create the dynamic type in the first
                // place.
                if (pTemplateEEType != null && pTemplateEEType->IsDynamicType)
                {
                    pTemplateEEType = pTemplateEEType->DynamicTemplateType;
                }

                ModuleInfo moduleInfo = TypeLoaderEnvironment.GetModuleInfoForType(state.TypeBeingBuilt);
                dynamicModulePtr = moduleInfo.DynamicModulePtr;
                Debug.Assert(dynamicModulePtr != null);

                bool requiresDynamicDispatchMap = requireVtableSlotMapping && (pTemplateEEType != null) && pTemplateEEType->HasDispatchMap;

                uint valueTypeFieldPaddingEncoded = 0;
                int baseSize = 0;

                bool isValueType;
                bool hasFinalizer;
                bool isNullable;
                bool isArray;
                bool isGeneric;
                ushort componentSize = 0;
                ushort flags;
                ushort runtimeInterfacesLength = 0;
                bool isGenericEETypeDef = false;
                bool isAbstractClass;
                bool isByRefLike;
#if EETYPE_TYPE_MANAGER
                IntPtr typeManager = IntPtr.Zero;
#endif

                if (state.RuntimeInterfaces != null)
                {
                    runtimeInterfacesLength = checked((ushort)state.RuntimeInterfaces.Length);
                }

                if (pTemplateEEType != null)
                {
                    valueTypeFieldPaddingEncoded = EETypeBuilderHelpers.ComputeValueTypeFieldPaddingFieldValue(
                        pTemplateEEType->ValueTypeFieldPadding, 
                        (uint)pTemplateEEType->FieldAlignmentRequirement,
                        IntPtr.Size);
                    baseSize = (int)pTemplateEEType->BaseSize;
                    isValueType = pTemplateEEType->IsValueType;
                    hasFinalizer = pTemplateEEType->IsFinalizable;
                    isNullable = pTemplateEEType->IsNullable;
                    componentSize = pTemplateEEType->ComponentSize;
                    flags = pTemplateEEType->Flags;
                    isArray = pTemplateEEType->IsArray;
                    isGeneric = pTemplateEEType->IsGeneric;
                    isAbstractClass = pTemplateEEType->IsAbstract && !pTemplateEEType->IsInterface;
                    isByRefLike = pTemplateEEType->IsByRefLike;
#if EETYPE_TYPE_MANAGER
                    typeManager = pTemplateEEType->PointerToTypeManager;
#endif
                    Debug.Assert(pTemplateEEType->NumInterfaces == runtimeInterfacesLength);
                }
                else if (state.TypeBeingBuilt.IsGenericDefinition)
                {
                    flags = (ushort)EETypeKind.GenericTypeDefEEType;
                    isValueType = state.TypeBeingBuilt.IsValueType;
                    if (isValueType)
                        flags |= (ushort)EETypeFlags.ValueTypeFlag;

                    if (state.TypeBeingBuilt.IsInterface)
                        flags |= (ushort)EETypeFlags.IsInterfaceFlag;
                    hasFinalizer = false;
                    isArray = false;
                    isNullable = false;
                    isGeneric = false;
                    isGenericEETypeDef = true;
                    isAbstractClass = false;
                    isByRefLike = false;
                    componentSize = checked((ushort)state.TypeBeingBuilt.Instantiation.Length);
                    baseSize = 0;
                }
                else
                {
                    isValueType = state.TypeBeingBuilt.IsValueType;
                    hasFinalizer = state.TypeBeingBuilt.HasFinalizer;
                    isNullable = state.TypeBeingBuilt.GetTypeDefinition().IsNullable;
                    flags = EETypeBuilderHelpers.ComputeFlags(state.TypeBeingBuilt);
                    isArray = false;
                    isGeneric = state.TypeBeingBuilt.HasInstantiation;

                    isAbstractClass = (state.TypeBeingBuilt is MetadataType)
                        && ((MetadataType)state.TypeBeingBuilt).IsAbstract
                        && !state.TypeBeingBuilt.IsInterface;

                    isByRefLike = (state.TypeBeingBuilt is DefType) && ((DefType)state.TypeBeingBuilt).IsByRefLike;

                    if (state.TypeBeingBuilt.HasVariance)
                    {
                        state.GenericVarianceFlags = new int[state.TypeBeingBuilt.Instantiation.Length];
                        int i = 0;

                        foreach (GenericParameterDesc gpd in state.TypeBeingBuilt.GetTypeDefinition().Instantiation)
                        {
                            state.GenericVarianceFlags[i] = (int)gpd.Variance;
                            i++;
                        }
                        Debug.Assert(i == state.GenericVarianceFlags.Length);
                    }
                }

                // TODO! Change to if template is Universal or non-Existent
                if (state.TypeSize.HasValue)
                {
                    baseSize = state.TypeSize.Value;

                    int baseSizeBeforeAlignment = baseSize;

                    baseSize = MemoryHelpers.AlignUp(baseSize, IntPtr.Size);

                    if (isValueType)
                    {
                        // Compute the valuetype padding size based on size before adding the object type pointer field to the size
                        uint cbValueTypeFieldPadding = (uint)(baseSize - baseSizeBeforeAlignment);

                        // Add Object type pointer field to base size
                        baseSize += IntPtr.Size;

                        valueTypeFieldPaddingEncoded = (uint)EETypeBuilderHelpers.ComputeValueTypeFieldPaddingFieldValue(cbValueTypeFieldPadding, (uint)state.FieldAlignment.Value, IntPtr.Size);
                    }

                    // Minimum base size is 3 pointers, and requires us to bump the size of an empty class type
                    if (baseSize <= IntPtr.Size)
                    {
                        // ValueTypes should already have had their size bumped up by the normal type layout process
                        Debug.Assert(!isValueType);
                        baseSize += IntPtr.Size;
                    }

                    // Add sync block skew
                    baseSize += IntPtr.Size;

                    // Minimum basesize is 3 pointers
                    Debug.Assert(baseSize >= (IntPtr.Size * 3));
                }

                // Optional fields encoding
                int cbOptionalFieldsSize;
                OptionalFieldsRuntimeBuilder optionalFields;
                {
                    optionalFields = new OptionalFieldsRuntimeBuilder(pTemplateEEType != null ? pTemplateEEType->OptionalFieldsPtr : null);

                    UInt32 rareFlags = optionalFields.GetFieldValue(EETypeOptionalFieldTag.RareFlags, 0);
                    rareFlags |= (uint)EETypeRareFlags.IsDynamicTypeFlag;          // Set the IsDynamicTypeFlag
                    rareFlags &= ~(uint)EETypeRareFlags.NullableTypeViaIATFlag;    // Remove the NullableTypeViaIATFlag flag

                    if (state.NumSealedVTableEntries > 0)
                        rareFlags |= (uint)EETypeRareFlags.HasSealedVTableEntriesFlag;

                    if (requiresDynamicDispatchMap)
                        rareFlags |= (uint)EETypeRareFlags.HasDynamicallyAllocatedDispatchMapFlag;

                    if (state.NonGcDataSize != 0)
                        rareFlags |= (uint)EETypeRareFlags.IsDynamicTypeWithNonGcStatics;

                    if (state.GcDataSize != 0)
                        rareFlags |= (uint)EETypeRareFlags.IsDynamicTypeWithGcStatics;

                    if (state.ThreadDataSize != 0)
                        rareFlags |= (uint)EETypeRareFlags.IsDynamicTypeWithThreadStatics;

#if ARM
                    if (state.FieldAlignment == 8)
                        rareFlags |= (uint)EETypeRareFlags.RequiresAlign8Flag;
                    else
                        rareFlags &= ~(uint)EETypeRareFlags.RequiresAlign8Flag;

                    if (state.IsHFA)
                        rareFlags |= (uint)EETypeRareFlags.IsHFAFlag;
                    else
                        rareFlags &= ~(uint)EETypeRareFlags.IsHFAFlag;
#endif
                    if (state.HasStaticConstructor)
                        rareFlags |= (uint)EETypeRareFlags.HasCctorFlag;
                    else
                        rareFlags &= ~(uint)EETypeRareFlags.HasCctorFlag;

                    if (isAbstractClass)
                        rareFlags |= (uint)EETypeRareFlags.IsAbstractClassFlag;
                    else
                        rareFlags &= ~(uint)EETypeRareFlags.IsAbstractClassFlag;

                    if (isByRefLike)
                        rareFlags |= (uint)EETypeRareFlags.IsByRefLikeFlag;
                    else
                        rareFlags &= ~(uint)EETypeRareFlags.IsByRefLikeFlag;

                    if (isNullable)
                    {
                        rareFlags |= (uint)EETypeRareFlags.IsNullableFlag;
                        uint nullableValueOffset = state.NullableValueOffset;

                        // The stored offset is never zero (Nullable has a boolean there indicating whether the value is valid). 
                        // If the real offset is one, then the field isn't set. Otherwise the offset is encoded - 1 to save space.
                        if (nullableValueOffset == 1)
                            optionalFields.ClearField(EETypeOptionalFieldTag.NullableValueOffset);
                        else
                            optionalFields.SetFieldValue(EETypeOptionalFieldTag.NullableValueOffset, checked(nullableValueOffset - 1));
                    }
                    else
                    {
                        rareFlags &= ~(uint)EETypeRareFlags.IsNullableFlag;
                        optionalFields.ClearField(EETypeOptionalFieldTag.NullableValueOffset);
                    }

                    rareFlags |= (uint)EETypeRareFlags.HasDynamicModuleFlag;

                    optionalFields.SetFieldValue(EETypeOptionalFieldTag.RareFlags, rareFlags);

                    // Dispatch map is fetched either from template type, or from the dynamically allocated DispatchMap field
                    optionalFields.ClearField(EETypeOptionalFieldTag.DispatchMap);

                    optionalFields.ClearField(EETypeOptionalFieldTag.ValueTypeFieldPadding);

                    if (valueTypeFieldPaddingEncoded != 0)
                        optionalFields.SetFieldValue(EETypeOptionalFieldTag.ValueTypeFieldPadding, valueTypeFieldPaddingEncoded);

                    // Compute size of optional fields encoding
                    cbOptionalFieldsSize = optionalFields.Encode();
                    Debug.Assert(cbOptionalFieldsSize > 0);
                }

                // Note: The number of vtable slots on the EEType to create is not necessary equal to the number of
                // vtable slots on the template type for universal generics (see ComputeVTableLayout)
                ushort numVtableSlots = state.NumVTableSlots;

                // Compute the EEType size and allocate it
                EEType* pEEType;
                {
                    // In order to get the size of the EEType to allocate we need the following information 
                    // 1) The number of VTable slots (from the TypeBuilderState)
                    // 2) The number of Interfaces (from the template)
                    // 3) Whether or not there is a finalizer (from the template)
                    // 4) Optional fields size
                    // 5) Whether or not the type is nullable (from the template)
                    // 6) Whether or not the type has sealed virtuals (from the TypeBuilderState)
                    int cbEEType = (int)EEType.GetSizeofEEType(
                        numVtableSlots,
                        runtimeInterfacesLength,
                        hasFinalizer,
                        true,
                        isNullable,
                        state.NumSealedVTableEntries > 0,
                        isGeneric,
                        state.NonGcDataSize != 0,
                        state.GcDataSize != 0,
                        state.ThreadDataSize != 0);

                    // Dynamic types have an extra pointer-sized field that contains a pointer to their template type
                    cbEEType += IntPtr.Size;

                    // Check if we need another pointer sized field for a dynamic DispatchMap
                    cbEEType += (requiresDynamicDispatchMap ? IntPtr.Size : 0);

                    // Add another pointer sized field for a DynamicModule
                    cbEEType += IntPtr.Size;

                    int cbGCDesc = GetInstanceGCDescSize(state, pTemplateEEType, isValueType, isArray);
                    int cbGCDescAligned = MemoryHelpers.AlignUp(cbGCDesc, IntPtr.Size);

                    // Allocate enough space for the EEType + gcDescSize
                    eeTypePtrPlusGCDesc = MemoryHelpers.AllocateMemory(cbGCDescAligned + cbEEType + cbOptionalFieldsSize);

                    // Get the EEType pointer, and the template EEType pointer
                    pEEType = (EEType*)(eeTypePtrPlusGCDesc + cbGCDescAligned);
                    state.HalfBakedRuntimeTypeHandle = pEEType->ToRuntimeTypeHandle();

                    // Set basic EEType fields
                    pEEType->ComponentSize = componentSize;
                    pEEType->Flags = flags;
                    pEEType->BaseSize = (uint)baseSize;
                    pEEType->NumVtableSlots = numVtableSlots;
                    pEEType->NumInterfaces = runtimeInterfacesLength;
                    pEEType->HashCode = hashCodeOfNewType;
#if EETYPE_TYPE_MANAGER
                    pEEType->PointerToTypeManager = typeManager;
#endif

                    // Write the GCDesc
                    bool isSzArray = isArray ? state.ArrayRank < 1 : false;
                    int arrayRank = isArray ? state.ArrayRank.Value : 0;
                    CreateInstanceGCDesc(state, pTemplateEEType, pEEType, baseSize, cbGCDesc, isValueType, isArray, isSzArray, arrayRank);
                    Debug.Assert(pEEType->HasGCPointers == (cbGCDesc != 0));

#if GENERICS_FORCE_USG
                    if (state.NonUniversalTemplateType != null)
                    {
                        Debug.Assert(state.NonUniversalInstanceGCDescSize == cbGCDesc, "Non-universal instance GCDesc size not matching with universal GCDesc size!");
                        Debug.Assert(cbGCDesc == 0 || pEEType->HasGCPointers);

                        // The TestGCDescsForEquality helper will compare 2 GCDescs for equality, 4 bytes at a time (GCDesc contents treated as integers), and will read the 
                        // GCDesc data in *reverse* order for instance GCDescs (subtracts 4 from the pointer values at each iteration).
                        //    - For the first GCDesc, we use (pEEType - 4) to point to the first 4-byte integer directly preceeding the EEType
                        //    - For the second GCDesc, given that the state.NonUniversalInstanceGCDesc already points to the first byte preceeding the template EEType, we 
                        //      subtract 3 to point to the first 4-byte integer directly preceeding the template EEType
                        TestGCDescsForEquality(new IntPtr((byte*)pEEType - 4), state.NonUniversalInstanceGCDesc - 3, cbGCDesc, true);
                    }
#endif

                    // Copy the encoded optional fields buffer to the newly allocated memory, and update the OptionalFields field on the EEType
                    // It is important to set the optional fields first on the newly created EEType, because all other 'setters' 
                    // will assert that the type is dynamic, just to make sure we are not making any changes to statically compiled types
                    pEEType->OptionalFieldsPtr = (byte*)pEEType + cbEEType;
                    optionalFields.WriteToEEType(pEEType, cbOptionalFieldsSize);

#if CORERT
                    pEEType->PointerToTypeManager = PermanentAllocatedMemoryBlobs.GetPointerToIntPtr(moduleInfo.Handle.GetIntPtrUNSAFE());
#endif
                    pEEType->DynamicModule = dynamicModulePtr;

                    // Copy VTable entries from template type
                    int numSlotsFilled = 0;
                    IntPtr* pVtable = (IntPtr*)((byte*)pEEType + sizeof(EEType));
                    if (pTemplateEEType != null)
                    {
                        IntPtr* pTemplateVtable = (IntPtr*)((byte*)pTemplateEEType + sizeof(EEType));
                        for (int i = 0; i < pTemplateEEType->NumVtableSlots; i++)
                        {
                            int vtableSlotInDynamicType = requireVtableSlotMapping ? state.VTableSlotsMapping.GetVTableSlotInTargetType(i) : i;
                            if (vtableSlotInDynamicType != -1)
                            {
                                Debug.Assert(vtableSlotInDynamicType < numVtableSlots);

                                IntPtr dictionaryPtrValue;
                                if (requireVtableSlotMapping && state.VTableSlotsMapping.IsDictionarySlot(i, out dictionaryPtrValue))
                                {
                                    // This must be the dictionary pointer value of one of the base types of the 
                                    // current universal generic type being constructed.
                                    pVtable[vtableSlotInDynamicType] = dictionaryPtrValue;

                                    // Assert that the current template vtable slot is also a NULL value since all 
                                    // universal generic template types have NULL dictionary slot values in their vtables
                                    Debug.Assert(pTemplateVtable[i] == IntPtr.Zero);
                                }
                                else
                                {
                                    pVtable[vtableSlotInDynamicType] = pTemplateVtable[i];
                                }
                                numSlotsFilled++;
                            }
                        }
                    }
                    else if (isGenericEETypeDef)
                    {
                        // If creating a Generic Type Definition
                        Debug.Assert(pEEType->NumVtableSlots == 0);
                    }
                    else
                    {
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
                        // Dynamically loaded type

                        // Fill the vtable with vtable resolution thunks in all slots except for
                        // the dictionary slots, which should be filled with dictionary pointers if those
                        // dictionaries are already published.

                        TypeDesc nextTypeToExamineForDictionarySlot = state.TypeBeingBuilt;
                        TypeDesc typeWithDictionary;
                        int nextDictionarySlot = GetMostDerivedDictionarySlot(ref nextTypeToExamineForDictionarySlot, out typeWithDictionary);

                        for (int iSlot = pEEType->NumVtableSlots - 1; iSlot >= 0; iSlot--)
                        {
                            bool isDictionary = iSlot == nextDictionarySlot;
                            if (!isDictionary)
                            {
                                pVtable[iSlot] = LazyVTableResolver.GetThunkForSlot(iSlot);
                            }
                            else
                            {
                                if (typeWithDictionary.RetrieveRuntimeTypeHandleIfPossible())
                                {
                                    pVtable[iSlot] = typeWithDictionary.RuntimeTypeHandle.GetDictionary();
                                }
                                nextDictionarySlot = GetMostDerivedDictionarySlot(ref nextTypeToExamineForDictionarySlot, out typeWithDictionary);
                            }
                            numSlotsFilled++;
                        }
#else
                        Environment.FailFast("Template type loader is null, but metadata based type loader is not in use");
#endif
                    }

                    Debug.Assert(numSlotsFilled == numVtableSlots);

                    // Copy Pointer to finalizer method from the template type
                    if (hasFinalizer)
                    {
                        if (pTemplateEEType != null)
                        {
                            pEEType->FinalizerCode = pTemplateEEType->FinalizerCode;
                        }
                        else
                        {
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
                            pEEType->FinalizerCode = LazyVTableResolver.GetFinalizerThunk();
#else
                            Environment.FailFast("Template type loader is null, but metadata based type loader is not in use");
#endif
                        }
                    }
                }

                // Copy the sealed vtable entries if they exist on the template type
                if (state.NumSealedVTableEntries > 0)
                {
                    state.HalfBakedSealedVTable = MemoryHelpers.AllocateMemory((int)state.NumSealedVTableEntries * IntPtr.Size);

                    UInt32 cbSealedVirtualSlotsTypeOffset = pEEType->GetFieldOffset(EETypeField.ETF_SealedVirtualSlots);
                    *((IntPtr*)((byte*)pEEType + cbSealedVirtualSlotsTypeOffset)) = state.HalfBakedSealedVTable;

                    for (UInt16 i = 0; i < state.NumSealedVTableEntries; i++)
                    {
                        IntPtr value = pTemplateEEType->GetSealedVirtualSlot(i);
                        pEEType->SetSealedVirtualSlot(value, i);
                    }
                }

                // Create a new DispatchMap for the type
                if (requiresDynamicDispatchMap)
                {
                    DispatchMap* pTemplateDispatchMap = (DispatchMap*)RuntimeAugments.GetDispatchMapForType(pTemplateEEType->ToRuntimeTypeHandle());

                    dynamicDispatchMapPtr = MemoryHelpers.AllocateMemory(pTemplateDispatchMap->Size);

                    UInt32 cbDynamicDispatchMapOffset = pEEType->GetFieldOffset(EETypeField.ETF_DynamicDispatchMap);
                    *((IntPtr*)((byte*)pEEType + cbDynamicDispatchMapOffset)) = dynamicDispatchMapPtr;

                    DispatchMap* pDynamicDispatchMap = (DispatchMap*)dynamicDispatchMapPtr;
                    pDynamicDispatchMap->NumEntries = pTemplateDispatchMap->NumEntries;

                    for (int i = 0; i < pTemplateDispatchMap->NumEntries; i++)
                    {
                        DispatchMap.DispatchMapEntry* pTemplateEntry = (*pTemplateDispatchMap)[i];
                        DispatchMap.DispatchMapEntry* pDynamicEntry = (*pDynamicDispatchMap)[i];

                        pDynamicEntry->_usInterfaceIndex = pTemplateEntry->_usInterfaceIndex;
                        pDynamicEntry->_usInterfaceMethodSlot = pTemplateEntry->_usInterfaceMethodSlot;
                        if (pTemplateEntry->_usImplMethodSlot < pTemplateEEType->NumVtableSlots)
                        {
                            pDynamicEntry->_usImplMethodSlot = (ushort)state.VTableSlotsMapping.GetVTableSlotInTargetType(pTemplateEntry->_usImplMethodSlot);
                            Debug.Assert(pDynamicEntry->_usImplMethodSlot < numVtableSlots);
                        }
                        else
                        {
                            // This is an entry in the sealed vtable. We need to adjust the slot number based on the number of vtable slots
                            // in the dynamic EEType
                            pDynamicEntry->_usImplMethodSlot = (ushort)(pTemplateEntry->_usImplMethodSlot - pTemplateEEType->NumVtableSlots + numVtableSlots);
                            Debug.Assert(state.NumSealedVTableEntries > 0 &&
                                pDynamicEntry->_usImplMethodSlot >= numVtableSlots &&
                                (pDynamicEntry->_usImplMethodSlot - numVtableSlots) < state.NumSealedVTableEntries);
                        }
                    }
                }

                if (pTemplateEEType != null)
                {
                    pEEType->DynamicTemplateType = pTemplateEEType;
                }
                else
                {
                    // Use object as the template type for non-template based EETypes. This will
                    // allow correct Module identification for types.

                    if (state.TypeBeingBuilt.HasVariance)
                    {
                        // TODO! We need to have a variant EEType here if the type has variance, as the 
                        // CreateGenericInstanceDescForType requires it. However, this is a ridiculous api surface
                        // When we remove GenericInstanceDescs from the product, get rid of this weird special
                        // case
                        pEEType->DynamicTemplateType = typeof(IEnumerable<int>).TypeHandle.ToEETypePtr();
                    }
                    else
                    {
                        pEEType->DynamicTemplateType = typeof(object).TypeHandle.ToEETypePtr();
                    }
                }

                int nonGCStaticDataOffset = 0;

                if (!isArray && !isGenericEETypeDef)
                {
                    nonGCStaticDataOffset = state.HasStaticConstructor ? -TypeBuilder.ClassConstructorOffset : 0;

                    // create GC desc
                    if (state.GcDataSize != 0 && state.GcStaticDesc == IntPtr.Zero)
                    {
                        if (state.GcStaticEEType != IntPtr.Zero)
                        {
                            // CoreRT Abi uses managed heap-allocated GC statics
                            object obj = RuntimeAugments.NewObject(((EEType*)state.GcStaticEEType)->ToRuntimeTypeHandle());
                            gcStaticData = RuntimeAugments.RhHandleAlloc(obj, GCHandleType.Normal);

                            // CoreRT references statics through an extra level of indirection (a table in the image).
                            gcStaticsIndirection = MemoryHelpers.AllocateMemory(IntPtr.Size);

                            *((IntPtr*)gcStaticsIndirection) = gcStaticData;
                            pEEType->DynamicGcStaticsData = gcStaticsIndirection;
                        }
                        else
                        {
                            int cbStaticGCDesc;
                            state.GcStaticDesc = CreateStaticGCDesc(state.StaticGCLayout, out state.AllocatedStaticGCDesc, out cbStaticGCDesc);
#if GENERICS_FORCE_USG
                            TestGCDescsForEquality(state.GcStaticDesc, state.NonUniversalStaticGCDesc, cbStaticGCDesc, false);
#endif
                        }
                    }

                    if (state.ThreadDataSize != 0 && state.ThreadStaticDesc == IntPtr.Zero)
                    {
                        int cbThreadStaticGCDesc;
                        state.ThreadStaticDesc = CreateStaticGCDesc(state.ThreadStaticGCLayout, out state.AllocatedThreadStaticGCDesc, out cbThreadStaticGCDesc);
#if GENERICS_FORCE_USG
                        TestGCDescsForEquality(state.ThreadStaticDesc, state.NonUniversalThreadStaticGCDesc, cbThreadStaticGCDesc, false);
#endif
                    }

                    // If we have a class constructor, our NonGcDataSize MUST be non-zero
                    Debug.Assert(!state.HasStaticConstructor || (state.NonGcDataSize != 0));
                }

                if (isGeneric)
                {
                    if (!RuntimeAugments.CreateGenericInstanceDescForType(*(RuntimeTypeHandle*)&pEEType, arity, state.NonGcDataSize, nonGCStaticDataOffset,
                        state.GcDataSize, (int)state.ThreadStaticOffset, state.GcStaticDesc, state.ThreadStaticDesc, state.GenericVarianceFlags))
                    {
                        throw new OutOfMemoryException();
                    }
                }
                else
                {
                    Debug.Assert(arity == 0 || isGenericEETypeDef);
                    // We don't need to report the non-gc and gc static data regions and allocate them for non-generics, 
                    // as we currently place these fields directly into the image
                    if (!isGenericEETypeDef && state.ThreadDataSize != 0)
                    {
                        // Types with thread static fields ALWAYS get a GID. The GID is used to perform GC 
                        // and lifetime management of the thread static data. However, these GIDs are only used for that
                        // so the specified GcDataSize, etc are 0
                        if (!RuntimeAugments.CreateGenericInstanceDescForType(*(RuntimeTypeHandle*)&pEEType, 0, 0, 0, 0, (int)state.ThreadStaticOffset, IntPtr.Zero, state.ThreadStaticDesc, null))
                        {
                            throw new OutOfMemoryException();
                        }
                    }
                }

                if (state.Dictionary != null)
                    state.HalfBakedDictionary = state.Dictionary.Allocate();

                Debug.Assert(!state.HalfBakedRuntimeTypeHandle.IsNull());
                Debug.Assert((state.NumSealedVTableEntries == 0 && state.HalfBakedSealedVTable == IntPtr.Zero) || (state.NumSealedVTableEntries > 0 && state.HalfBakedSealedVTable != IntPtr.Zero));
                Debug.Assert((state.Dictionary == null && state.HalfBakedDictionary == IntPtr.Zero) || (state.Dictionary != null && state.HalfBakedDictionary != IntPtr.Zero));

                successful = true;
            }
            finally
            {
                if (!successful)
                {
                    if (eeTypePtrPlusGCDesc != IntPtr.Zero)
                        MemoryHelpers.FreeMemory(eeTypePtrPlusGCDesc);
                    if (dynamicDispatchMapPtr != IntPtr.Zero)
                        MemoryHelpers.FreeMemory(dynamicDispatchMapPtr);
                    if (state.HalfBakedSealedVTable != IntPtr.Zero)
                        MemoryHelpers.FreeMemory(state.HalfBakedSealedVTable);
                    if (state.HalfBakedDictionary != IntPtr.Zero)
                        MemoryHelpers.FreeMemory(state.HalfBakedDictionary);
                    if (state.AllocatedStaticGCDesc)
                        MemoryHelpers.FreeMemory(state.GcStaticDesc);
                    if (state.AllocatedThreadStaticGCDesc)
                        MemoryHelpers.FreeMemory(state.ThreadStaticDesc);
                    if (gcStaticData != IntPtr.Zero)
                        RuntimeAugments.RhHandleFree(gcStaticData);
                    if (gcStaticsIndirection != IntPtr.Zero)
                        MemoryHelpers.FreeMemory(gcStaticsIndirection);
                }
            }
        }

        private static IntPtr CreateStaticGCDesc(List<bool> gcBitfield, out bool allocated, out int cbGCDesc)
        {
            if (gcBitfield != null)
            {
                int series = CreateGCDesc(gcBitfield, 0, false, true, null);
                if (series > 0)
                {
                    cbGCDesc = sizeof(int) + series * sizeof(int) * 2;
                    IntPtr result = MemoryHelpers.AllocateMemory(cbGCDesc);
                    CreateGCDesc(gcBitfield, 0, false, true, (void**)result.ToPointer());
                    allocated = true;
                    return result;
                }
            }

            allocated = false;

            if (s_emptyGCDesc == IntPtr.Zero)
            {
                IntPtr ptr = MemoryHelpers.AllocateMemory(8);

                long* gcdesc = (long*)ptr.ToPointer();
                *gcdesc = 0;

                if (Interlocked.CompareExchange(ref s_emptyGCDesc, ptr, IntPtr.Zero) != IntPtr.Zero)
                    MemoryHelpers.FreeMemory(ptr);
            }

            cbGCDesc = IntPtr.Size;
            return s_emptyGCDesc;
        }

        private static void CreateInstanceGCDesc(TypeBuilderState state, EEType* pTemplateEEType, EEType* pEEType, int baseSize, int cbGCDesc, bool isValueType, bool isArray, bool isSzArray, int arrayRank)
        {
            var gcBitfield = state.InstanceGCLayout;
            if (isArray)
            {
                if (cbGCDesc != 0)
                {
                    pEEType->HasGCPointers = true;
                    if (state.IsArrayOfReferenceTypes)
                    {
                        IntPtr* gcDescStart = (IntPtr*)((byte*)pEEType - cbGCDesc);
                        gcDescStart[0] = new IntPtr(-baseSize);
                        gcDescStart[1] = new IntPtr(baseSize - sizeof(IntPtr));
                        gcDescStart[2] = new IntPtr(1);
                    }
                    else
                    {
                        CreateArrayGCDesc(gcBitfield, arrayRank, isSzArray, ((void**)pEEType) - 1);
                    }
                }
                else
                {
                    pEEType->HasGCPointers = false;
                }
            }
            else if (gcBitfield != null)
            {
                if (cbGCDesc != 0)
                {
                    pEEType->HasGCPointers = true;
                    CreateGCDesc(gcBitfield, baseSize, isValueType, false, ((void**)pEEType) - 1);
                }
                else
                {
                    pEEType->HasGCPointers = false;
                }
            }
            else if (pTemplateEEType != null)
            {
                Buffer.MemoryCopy((byte*)pTemplateEEType - cbGCDesc, (byte*)pEEType - cbGCDesc, cbGCDesc, cbGCDesc);
                pEEType->HasGCPointers = pTemplateEEType->HasGCPointers;
            }
            else
            {
                pEEType->HasGCPointers = false;
            }
        }

        private static unsafe int GetInstanceGCDescSize(TypeBuilderState state, EEType* pTemplateEEType, bool isValueType, bool isArray)
        {
            var gcBitfield = state.InstanceGCLayout;
            if (isArray)
            {
                if (state.IsArrayOfReferenceTypes)
                {
                    // Reference type arrays have a GC desc the size of 3 pointers
                    return 3 * sizeof(IntPtr);
                }
                else
                {
                    int series = 0;
                    if (gcBitfield != null)
                        series = CreateArrayGCDesc(gcBitfield, 1, true, null);

                    return series > 0 ? (series + 2) * IntPtr.Size : 0;
                }
            }
            else if (gcBitfield != null)
            {
                int series = CreateGCDesc(gcBitfield, 0, isValueType, false, null);
                return series > 0 ? (series * 2 + 1) * IntPtr.Size : 0;
            }
            else if (pTemplateEEType != null)
            {
                return RuntimeAugments.GetGCDescSize(pTemplateEEType->ToRuntimeTypeHandle());
            }
            else
            {
                return 0;
            }
        }

        private static unsafe int CreateArrayGCDesc(List<bool> bitfield, int rank, bool isSzArray, void* gcdesc)
        {
            if (bitfield == null)
                return 0;

            void** baseOffsetPtr = (void**)gcdesc - 1;

#if WIN64
            int* ptr = (int*)baseOffsetPtr - 1;
#else
            short* ptr = (short*)baseOffsetPtr - 1;
#endif
            int baseOffset = 2;
            if (!isSzArray)
            {
                baseOffset += 2 * rank / (sizeof(IntPtr) / sizeof(int));
            }

            int numSeries = 0;
            int i = 0;

            bool first = true;
            int last = 0;
            short numPtrs = 0;
            while (i < bitfield.Count)
            {
                if (bitfield[i])
                {
                    if (first)
                    {
                        baseOffset += i;
                        first = false;
                    }
                    else if (gcdesc != null)
                    {
                        *ptr-- = (short)((i - last) * IntPtr.Size);
                        *ptr-- = numPtrs;
                    }

                    numSeries++;
                    numPtrs = 0;

                    while ((i < bitfield.Count) && (bitfield[i]))
                    {
                        numPtrs++;
                        i++;
                    }

                    last = i;
                }
                else
                {
                    i++;
                }
            }

            if (gcdesc != null)
            {
                if (numSeries > 0)
                {
                    *ptr-- = (short)((bitfield.Count - last + baseOffset - 2) * IntPtr.Size);
                    *ptr-- = numPtrs;

                    *(void**)gcdesc = (void*)-numSeries;
                    *baseOffsetPtr = (void*)(baseOffset * IntPtr.Size);
                }
            }

            return numSeries;
        }

        private static unsafe int CreateGCDesc(List<bool> bitfield, int size, bool isValueType, bool isStatic, void* gcdesc)
        {
            int offs = 0;
            // if this type is a class we have to account for the gcdesc.
            if (isValueType)
                offs = IntPtr.Size;

            if (bitfield == null)
                return 0;

            void** ptr = (void**)gcdesc - 1;

            int* staticPtr = isStatic ? ((int*)gcdesc + 1) : null;

            int numSeries = 0;
            int i = 0;
            while (i < bitfield.Count)
            {
                if (bitfield[i])
                {
                    numSeries++;
                    int seriesOffset = i * IntPtr.Size + offs;
                    int seriesSize = 0;

                    while ((i < bitfield.Count) && (bitfield[i]))
                    {
                        seriesSize += IntPtr.Size;
                        i++;
                    }


                    if (gcdesc != null)
                    {
                        if (staticPtr != null)
                        {
                            *staticPtr++ = seriesSize;
                            *staticPtr++ = seriesOffset;
                        }
                        else
                        {
                            seriesSize = seriesSize - size;
                            *ptr-- = (void*)seriesOffset;
                            *ptr-- = (void*)seriesSize;
                        }
                    }
                }
                else
                {
                    i++;
                }
            }

            if (gcdesc != null)
            {
                if (staticPtr != null)
                    *(int*)gcdesc = numSeries;
                else
                    *(void**)gcdesc = (void*)numSeries;
            }

            return numSeries;
        }

        [Conditional("GENERICS_FORCE_USG")]
        unsafe private static void TestGCDescsForEquality(IntPtr dynamicGCDesc, IntPtr templateGCDesc, int cbGCDesc, bool isInstanceGCDesc)
        {
            if (templateGCDesc == IntPtr.Zero)
                return;

            Debug.Assert(dynamicGCDesc != IntPtr.Zero);
            Debug.Assert(cbGCDesc == MemoryHelpers.AlignUp(cbGCDesc, 4));

            uint* pMem1 = (uint*)dynamicGCDesc.ToPointer();
            uint* pMem2 = (uint*)templateGCDesc.ToPointer();
            bool foundDifferences = false;

            for (int i = 0; i < cbGCDesc; i += 4)
            {
                if (*pMem1 != *pMem2)
                {
                    // Log all the differences before the assert
                    Debug.WriteLine("ERROR: GCDesc comparison failed at byte #" + i.LowLevelToString() + " while comparing " +
                        dynamicGCDesc.LowLevelToString() + " with " + templateGCDesc.LowLevelToString() +
                        ": [" + (*pMem1).LowLevelToString() + "]/[" + (*pMem2).LowLevelToString() + "]");
                    foundDifferences = true;
                }
                if (isInstanceGCDesc)
                {
                    pMem1--;
                    pMem2--;
                }
                else
                {
                    pMem1++;
                    pMem2++;
                }
            }

            Debug.Assert(!foundDifferences);
        }

        public static RuntimeTypeHandle CreatePointerEEType(UInt32 hashCodeOfNewType, RuntimeTypeHandle pointeeTypeHandle, TypeDesc pointerType)
        {
            TypeBuilderState state = new TypeBuilderState(pointerType);

            CreateEETypeWorker(typeof(void*).TypeHandle.ToEETypePtr(), hashCodeOfNewType, 0, false, state);
            Debug.Assert(!state.HalfBakedRuntimeTypeHandle.IsNull());

            state.HalfBakedRuntimeTypeHandle.ToEETypePtr()->RelatedParameterType = pointeeTypeHandle.ToEETypePtr();

            return state.HalfBakedRuntimeTypeHandle;
        }

        public static RuntimeTypeHandle CreateByRefEEType(UInt32 hashCodeOfNewType, RuntimeTypeHandle pointeeTypeHandle, TypeDesc byRefType)
        {
            TypeBuilderState state = new TypeBuilderState(byRefType);

            // ByRef and pointer types look similar enough that we can use void* as a template.
            // Ideally this should be typeof(void&) but C# doesn't support that syntax. We adjust for this below.
            CreateEETypeWorker(typeof(void*).TypeHandle.ToEETypePtr(), hashCodeOfNewType, 0, false, state);
            Debug.Assert(!state.HalfBakedRuntimeTypeHandle.IsNull());

            state.HalfBakedRuntimeTypeHandle.ToEETypePtr()->RelatedParameterType = pointeeTypeHandle.ToEETypePtr();

            // We used a pointer as a template. We need to make this a byref.
            Debug.Assert(state.HalfBakedRuntimeTypeHandle.ToEETypePtr()->ParameterizedTypeShape == ParameterizedTypeShapeConstants.Pointer);
            state.HalfBakedRuntimeTypeHandle.ToEETypePtr()->ParameterizedTypeShape = ParameterizedTypeShapeConstants.ByRef;

            return state.HalfBakedRuntimeTypeHandle;
        }

        public static RuntimeTypeHandle CreateEEType(TypeDesc type, TypeBuilderState state)
        {
            Debug.Assert(type != null && state != null);

            EEType* pTemplateEEType = null;
            bool requireVtableSlotMapping = false;

            if (type is PointerType || type is ByRefType)
            {
                Debug.Assert(0 == state.NonGcDataSize);
                Debug.Assert(false == state.HasStaticConstructor);
                Debug.Assert(0 == state.GcDataSize);
                Debug.Assert(0 == state.ThreadStaticOffset);
                Debug.Assert(0 == state.NumSealedVTableEntries);
                Debug.Assert(IntPtr.Zero == state.GcStaticDesc);
                Debug.Assert(IntPtr.Zero == state.ThreadStaticDesc);

                // Pointers and ByRefs only differ by the ParameterizedTypeShape value.
                RuntimeTypeHandle templateTypeHandle = typeof(void*).TypeHandle;

                pTemplateEEType = templateTypeHandle.ToEETypePtr();
            }
            else if ((type is MetadataType) && (state.TemplateType == null || !state.TemplateType.RetrieveRuntimeTypeHandleIfPossible()))
            {
                requireVtableSlotMapping = true;
                pTemplateEEType = null;
            }
            else if (type.IsMdArray || (type.IsSzArray && ((ArrayType)type).ElementType.IsPointer))
            {
                // Multidimensional arrays and szarrays of pointers don't implement generic interfaces and
                // we don't need to do much for them in terms of type building. We can pretty much just take
                // the EEType for any of those, massage the bits that matter (GCDesc, element type,
                // component size,...) to be of the right shape and we're done.
                pTemplateEEType = typeof(object[,]).TypeHandle.ToEETypePtr();
                requireVtableSlotMapping = false;
            }
            else
            {
                Debug.Assert(state.TemplateType != null && !state.TemplateType.RuntimeTypeHandle.IsNull());
                requireVtableSlotMapping = state.TemplateType.IsCanonicalSubtype(CanonicalFormKind.Universal);
                RuntimeTypeHandle templateTypeHandle = state.TemplateType.RuntimeTypeHandle;
                pTemplateEEType = templateTypeHandle.ToEETypePtr();
            }

            DefType typeAsDefType = type as DefType;
            // Use a checked typecast to 'ushort' for the arity to ensure its value never exceeds 65535 and cause integer
            // overflows later when computing size of memory blocks to allocate for the type and its GenericInstanceDescriptor structures
            int arity = checked((ushort)((typeAsDefType != null && typeAsDefType.HasInstantiation ? typeAsDefType.Instantiation.Length : 0)));

            CreateEETypeWorker(pTemplateEEType, (uint)type.GetHashCode(), arity, requireVtableSlotMapping, state);

            return state.HalfBakedRuntimeTypeHandle;
        }

        public static IntPtr GetDictionary(EEType* pEEType)
        {
            // Dictionary slot is the first vtable slot

            EEType* pBaseType = pEEType->BaseType;
            int dictionarySlot = (pBaseType == null ? 0 : pBaseType->NumVtableSlots);
            return *(IntPtr*)((byte*)pEEType + sizeof(EEType) + dictionarySlot * IntPtr.Size);
        }

        public static int GetDictionarySlotInVTable(TypeDesc type)
        {
            if (!type.CanShareNormalGenericCode())
                return -1;

            // Dictionary slot is the first slot in the vtable after the base type's vtable entries
            return type.BaseType != null ? type.BaseType.GetOrCreateTypeBuilderState().NumVTableSlots : 0;
        }

        private static int GetMostDerivedDictionarySlot(ref TypeDesc nextTypeToExamineForDictionarySlot, out TypeDesc typeWithDictionary)
        {
            while (nextTypeToExamineForDictionarySlot != null)
            {
                if (nextTypeToExamineForDictionarySlot.GetOrCreateTypeBuilderState().HasDictionarySlotInVTable)
                {
                    typeWithDictionary = nextTypeToExamineForDictionarySlot;
                    nextTypeToExamineForDictionarySlot = nextTypeToExamineForDictionarySlot.BaseType;
                    return GetDictionarySlotInVTable(typeWithDictionary);
                }

                nextTypeToExamineForDictionarySlot = nextTypeToExamineForDictionarySlot.BaseType;
            }

            typeWithDictionary = null;
            return -1;
        }
    }
}
