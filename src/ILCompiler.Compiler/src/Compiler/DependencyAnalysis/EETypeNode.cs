// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using GenericVariance = Internal.Runtime.GenericVariance;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Given a type, EETypeNode writes an EEType data structure in the format expected by the runtime.
    /// 
    /// Format of an EEType:
    /// 
    /// Field Size      | Contents
    /// ----------------+-----------------------------------
    /// UInt16          | Component Size. For arrays this is the element type size, for strings it is 2 (.NET uses 
    ///                 | UTF16 character encoding), for generic type definitions it is the number of generic parameters,
    ///                 | and 0 for all other types.
    ///                 |
    /// UInt16          | EETypeKind (Normal, Array, Pointer type). Flags for: IsValueType, IsCrossModule, HasPointers,
    ///                 | HasOptionalFields, IsInterface, IsGeneric. Top 5 bits are used for enum CorElementType to
    ///                 | record whether it's back by an Int32, Int16 etc
    ///                 |
    /// Uint32          | Base size.
    ///                 |
    /// [Pointer Size]  | Related type. Base type for regular types. Element type for arrays / pointer types.
    ///                 |
    /// UInt16          | Number of VTable slots (X)
    ///                 |
    /// UInt16          | Number of interfaces implemented by type (Y)
    ///                 |
    /// UInt32          | Hash code
    ///                 |
    /// [Pointer Size]  | Pointer to containing TypeManager indirection cell
    ///                 |
    /// X * [Ptr Size]  | VTable entries (optional)
    ///                 |
    /// Y * [Ptr Size]  | Pointers to interface map data structures (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to finalizer method (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to optional fields (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to the generic type argument of a Nullable&lt;T&gt; (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to the generic type definition EEType (optional)
    ///                 |
    /// [Pointer Size]  | Pointer to the generic argument and variance info (optional)
    /// </summary>
    public partial class EETypeNode : ObjectNode, ISymbolNode, IEETypeNode
    {
        protected TypeDesc _type;
        internal EETypeOptionalFieldsBuilder _optionalFieldsBuilder = new EETypeOptionalFieldsBuilder();
        internal EETypeOptionalFieldsNode _optionalFieldsNode;

        public EETypeNode(NodeFactory factory, TypeDesc type)
        {
            if (type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
                Debug.Assert(this is CanonicalDefinitionEETypeNode);
            else if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                Debug.Assert(this is CanonicalEETypeNode);

            Debug.Assert(!type.IsRuntimeDeterminedSubtype);
            _type = type;
            _optionalFieldsNode = new EETypeOptionalFieldsNode(this);

            // Note: The fact that you can't create invalid EETypeNode is used from many places that grab
            // an EETypeNode from the factory with the sole purpose of making sure the validation has run
            // and that the result of the positive validation is "cached" (by the presence of an EETypeNode).
            CheckCanGenerateEEType(factory, type);
        }

        protected override string GetName() => this.GetMangledName();

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            // If there is a constructed version of this node in the graph, emit that instead
                if (ConstructedEETypeNode.CreationAllowed(_type))
                return ((DependencyNode)factory.ConstructedTypeSymbol(_type)).Marked;

            return false;
        }

        public TypeDesc Type => _type;

        public override ObjectNodeSection Section
        {
            get
            {
                if (_type.Context.Target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public int MinimumObjectSize => _type.Context.Target.PointerSize * 3;

        protected virtual bool EmitVirtualSlotsAndInterfaces => false;

        internal bool HasOptionalFields
        {
            get { return _optionalFieldsBuilder.IsAtLeastOneFieldUsed(); }
        }

        internal byte[] GetOptionalFieldsData(NodeFactory factory)
        {
            return _optionalFieldsBuilder.GetBytes();
        }
        
        public override bool StaticDependenciesAreComputed => true;

        public void SetDispatchMapIndex(int index)
        {
            _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.DispatchMap, checked((uint)index));
        }

        public static string GetMangledName(TypeDesc type, NameMangler nameMangler)
        {
            return "__EEType_" + nameMangler.GetMangledTypeName(type);
        }

        public virtual void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__EEType_").Append(nameMangler.GetMangledTypeName(_type));
        }

        public int Offset => GCDescSize;
        public override bool IsShareable => IsTypeNodeShareable(_type);

        public static bool IsTypeNodeShareable(TypeDesc type)
        {
            return type.IsParameterizedType || type.IsFunctionPointer || type is InstantiatedType;
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();

            // Include the optional fields by default. We don't know if optional fields will be needed until
            // all of the interface usage has been stabilized. If we end up not needing it, the EEType node will not
            // generate any relocs to it, and the optional fields node will instruct the object writer to skip
            // emitting it.
            dependencies.Add(new DependencyListEntry(_optionalFieldsNode, "Optional fields"));

            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type) && !_type.IsCanonicalSubtype(CanonicalFormKind.Any))
            {
                // The fact that we generated an EEType means that someone can call RuntimeHelpers.RunClassConstructor.
                // We need to make sure this is possible.
                dependencies.Add(new DependencyListEntry(factory.TypeNonGCStaticsSymbol((MetadataType)_type), "Class constructor"));
            }

            return dependencies;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            ComputeOptionalEETypeFields(factory, relocsOnly);

            OutputGCDesc(ref objData);
            OutputComponentSize(ref objData);
            OutputFlags(factory, ref objData);
            OutputBaseSize(ref objData);
            OutputRelatedType(factory, ref objData);

            // Avoid consulting VTable slots until they're guaranteed complete during final data emission
            if (EmitVirtualSlotsAndInterfaces && !relocsOnly)
            {
                OutputVirtualSlotAndInterfaceCount(factory, ref objData);
            }
            else
            {
                objData.EmitShort(0);
                objData.EmitShort(0);
            }

            objData.EmitInt(_type.GetHashCode());
            objData.EmitPointerReloc(factory.TypeManagerIndirection);

            if (EmitVirtualSlotsAndInterfaces)
            {
                // Avoid consulting VTable slots until they're guaranteed complete during final data emission
                if (!relocsOnly)
                {
                    OutputVirtualSlots(factory, ref objData, _type, _type);
                }

                OutputInterfaceMap(factory, ref objData);
            }

            OutputFinalizerMethod(factory, ref objData);
            OutputOptionalFields(factory, ref objData);
            OutputNullableTypeParameter(factory, ref objData);
            OutputGenericInstantiationDetails(factory, ref objData);

            return objData.ToObjectData();
        }

        /// <summary>
        /// Returns the offset within an EEType of the beginning of VTable entries
        /// </summary>
        /// <param name="pointerSize">The size of a pointer in bytes in the target architecture</param>
        public static int GetVTableOffset(int pointerSize)
        {
            return 16 + 2 * pointerSize;
        }

        protected virtual int GCDescSize => 0;

        protected virtual void OutputGCDesc(ref ObjectDataBuilder builder)
        {
            // Non-constructed EETypeNodes get no GC Desc
            Debug.Assert(GCDescSize == 0);
        }
        
        private void OutputComponentSize(ref ObjectDataBuilder objData)
        {
            if (_type.IsArray)
            {
                int elementSize = ((ArrayType)_type).ElementType.GetElementSize();
                // We validated that this will fit the short when the node was constructed. No need for nice messages.
                objData.EmitShort((short)checked((ushort)elementSize));
            }
            else if (_type.IsString)
            {
                objData.EmitShort(2);
            }
            else
            {
                objData.EmitShort(0);
            }
        }

        private void OutputFlags(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            UInt16 flags = EETypeBuilderHelpers.ComputeFlags(_type);

            if (_type.GetTypeDefinition() == factory.ArrayOfTEnumeratorType)
            {
                // Generic array enumerators use special variance rules recognized by the runtime
                flags |= (UInt16)EETypeFlags.GenericVarianceFlag;
            }

            ISymbolNode relatedTypeNode = GetRelatedTypeNode(factory);

            // If the related type (base type / array element type / pointee type) is not part of this compilation group, and
            // the output binaries will be multi-file (not multiple object files linked together), indicate to the runtime
            // that it should indirect through the import address table
            if (relatedTypeNode != null && relatedTypeNode.RepresentsIndirectionCell)
            {
                flags |= (UInt16)EETypeFlags.RelatedTypeViaIATFlag;
            }

            // Todo: Generic Type Definition EETypes

            if (HasOptionalFields)
            {
                flags |= (UInt16)EETypeFlags.OptionalFieldsFlag;
            }

            objData.EmitShort((short)flags);
        }

        protected virtual void OutputBaseSize(ref ObjectDataBuilder objData)
        {
            int pointerSize = _type.Context.Target.PointerSize;
            int objectSize;

            if (_type.IsDefType)
            {
                objectSize = pointerSize +
                    ((DefType)_type).InstanceByteCount; // +pointerSize for SyncBlock

                if (_type.IsValueType)
                    objectSize += pointerSize; // + EETypePtr field inherited from System.Object
            }
            else if (_type.IsArray)
            {
                objectSize = 3 * pointerSize; // SyncBlock + EETypePtr + Length
                if (_type.IsMdArray)
                    objectSize +=
                        2 * _type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() * ((ArrayType)_type).Rank;
            }
            else if (_type.IsPointer)
            {
                // These never get boxed and don't have a base size. Use a sentinel value recognized by the runtime.
                objData.EmitInt(ParameterizedTypeShapeConstants.Pointer);
                return;
            }
            else if (_type.IsByRef)
            {
                // These never get boxed and don't have a base size. Use a sentinel value recognized by the runtime.
                objData.EmitInt(ParameterizedTypeShapeConstants.ByRef);
                return;
            }
            else
                throw new NotImplementedException();

            objectSize = AlignmentHelper.AlignUp(objectSize, pointerSize);
            objectSize = Math.Max(MinimumObjectSize, objectSize);

            if (_type.IsString)
            {
                // If this is a string, throw away objectSize we computed so far. Strings are special.
                // SyncBlock + EETypePtr + length + firstChar
                objectSize = 2 * pointerSize +
                    _type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() +
                    _type.Context.GetWellKnownType(WellKnownType.Char).GetElementSize();
            }

            objData.EmitInt(objectSize);
        }

        protected static TypeDesc GetFullCanonicalTypeForCanonicalType(TypeDesc type)
        {
            if (type.IsCanonicalSubtype(CanonicalFormKind.Specific))
            {
                return type.ConvertToCanonForm(CanonicalFormKind.Specific);
            }
            else if (type.IsCanonicalSubtype(CanonicalFormKind.Universal))
            {
                return type.ConvertToCanonForm(CanonicalFormKind.Universal);
            }
            else
            {
                return type;
            }
        }

        protected virtual ISymbolNode GetBaseTypeNode(NodeFactory factory)
        {
            return _type.BaseType != null ? factory.NecessaryTypeSymbol(_type.BaseType) : null;
        }

        private ISymbolNode GetRelatedTypeNode(NodeFactory factory)
        {
            ISymbolNode relatedTypeNode = null;

            if (_type.IsArray || _type.IsPointer || _type.IsByRef)
            {
                var parameterType = ((ParameterizedType)_type).ParameterType;
                relatedTypeNode = factory.NecessaryTypeSymbol(parameterType);
            }
            else
            {
                TypeDesc baseType = _type.BaseType;
                if (baseType != null)
                {
                    relatedTypeNode = GetBaseTypeNode(factory);
                }
            }

            return relatedTypeNode;
        }

        protected virtual void OutputRelatedType(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            ISymbolNode relatedTypeNode = GetRelatedTypeNode(factory);

            if (relatedTypeNode != null)
            {
                objData.EmitPointerReloc(relatedTypeNode);
            }
            else
            {
                objData.EmitZeroPointer();
            }
        }

        protected virtual void OutputVirtualSlotAndInterfaceCount(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            Debug.Assert(EmitVirtualSlotsAndInterfaces);

            int virtualSlotCount = 0;
            TypeDesc currentTypeSlice = _type.GetClosestDefType();

            while (currentTypeSlice != null)
            {
                if (currentTypeSlice.HasGenericDictionarySlot())
                    virtualSlotCount++;

                virtualSlotCount += factory.VTable(currentTypeSlice).Slots.Count;
                currentTypeSlice = currentTypeSlice.BaseType;
            }

            objData.EmitShort(checked((short)virtualSlotCount));
            objData.EmitShort(checked((short)_type.RuntimeInterfaces.Length));
        }

        protected virtual void OutputVirtualSlots(NodeFactory factory, ref ObjectDataBuilder objData, TypeDesc implType, TypeDesc declType)
        {
            Debug.Assert(EmitVirtualSlotsAndInterfaces);

            declType = declType.GetClosestDefType();

            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(factory, ref objData, implType, baseType);

            // The generic dictionary pointer occupies the first slot of each type vtable slice
            if (declType.HasGenericDictionarySlot())
            {
                // Note: Canonical type instantiations always have a generic dictionary vtable slot, but it's empty
                if (declType.IsCanonicalSubtype(CanonicalFormKind.Any))
                    objData.EmitZeroPointer();
                else
                    objData.EmitPointerReloc(factory.TypeGenericDictionary(declType));
            }

            // Actual vtable slots follow
            IReadOnlyList<MethodDesc> virtualSlots = factory.VTable(declType).Slots;

            for (int i = 0; i < virtualSlots.Count; i++)
            {
                MethodDesc declMethod = virtualSlots[i];

                // No generic virtual methods can appear in the vtable!
                Debug.Assert(!declMethod.HasInstantiation);

                MethodDesc implMethod = implType.GetClosestDefType().FindVirtualFunctionTargetMethodOnObjectType(declMethod);

                if (!implMethod.IsAbstract)
                {
                    MethodDesc canonImplMethod = implMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                    objData.EmitPointerReloc(factory.MethodEntrypoint(canonImplMethod, implMethod.OwningType.IsValueType));
                }
                else
                {
                    objData.EmitZeroPointer();
                }
            }
        }
        
        protected virtual void OutputInterfaceMap(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            Debug.Assert(EmitVirtualSlotsAndInterfaces);

            foreach (var itf in _type.RuntimeInterfaces)
            {
                objData.EmitPointerReloc(factory.NecessaryTypeSymbol(itf));
            }
        }

        private void OutputFinalizerMethod(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            MethodDesc finalizerMethod = _type.GetFinalizer();

            if (finalizerMethod != null)
            {
                MethodDesc canonFinalizerMethod = finalizerMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                objData.EmitPointerReloc(factory.MethodEntrypoint(canonFinalizerMethod));
            }
        }

        private void OutputOptionalFields(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if (HasOptionalFields)
            {
                objData.EmitPointerReloc(_optionalFieldsNode);
            }
        }

        private void OutputNullableTypeParameter(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if (_type.IsNullable)
            {
                objData.EmitPointerReloc(factory.NecessaryTypeSymbol(_type.Instantiation[0]));
            }
        }

        private void OutputGenericInstantiationDetails(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if (_type.HasInstantiation && !_type.IsTypeDefinition)
            {
                objData.EmitPointerReloc(factory.NecessaryTypeSymbol(_type.GetTypeDefinition()));

                GenericCompositionDetails details;
                if (_type.GetTypeDefinition() == factory.ArrayOfTEnumeratorType)
                {
                    // Generic array enumerators use special variance rules recognized by the runtime
                    details = new GenericCompositionDetails(_type.Instantiation, new[] { GenericVariance.ArrayCovariant });
                }
                else
                    details = new GenericCompositionDetails(_type);

                objData.EmitPointerReloc(factory.GenericComposition(details));
            }
        }

        /// <summary>
        /// Populate the OptionalFieldsRuntimeBuilder if any optional fields are required.
        /// </summary>
        protected internal virtual void ComputeOptionalEETypeFields(NodeFactory factory, bool relocsOnly)
        {
            ComputeRareFlags(factory);
            ComputeNullableValueOffset();
            if (!relocsOnly)
                ComputeICastableVirtualMethodSlots(factory);
            ComputeValueTypeFieldPadding();
        }

        void ComputeRareFlags(NodeFactory factory)
        {
            uint flags = 0;

            if (_type.IsNullable)
            {
                flags |= (uint)EETypeRareFlags.IsNullableFlag;
            }

            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type))
            {
                flags |= (uint)EETypeRareFlags.HasCctorFlag;
            }

            if (EETypeBuilderHelpers.ComputeRequiresAlign8(_type))
            {
                flags |= (uint)EETypeRareFlags.RequiresAlign8Flag;
            }

            if (_type.IsDefType && ((DefType)_type).IsHfa)
            {
                flags |= (uint)EETypeRareFlags.IsHFAFlag;
            }

            foreach (DefType itf in _type.RuntimeInterfaces)
            {
                if (itf == factory.ICastableInterface)
                {
                    flags |= (uint)EETypeRareFlags.ICastableFlag;
                    break;
                }
            }

            if (flags != 0)
            {
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.RareFlags, flags);
            }
        }

        /// <summary>
        /// To support boxing / unboxing, the offset of the value field of a Nullable type is recorded on the EEType.
        /// This is variable according to the alignment requirements of the Nullable&lt;T&gt; type parameter.
        /// </summary>
        void ComputeNullableValueOffset()
        {
            if (!_type.IsNullable)
                return;

            var field = _type.GetKnownField("value");

            // In the definition of Nullable<T>, the first field should be the boolean representing "hasValue"
            Debug.Assert(field.Offset > 0);

            // The contract with the runtime states the Nullable value offset is stored with the boolean "hasValue" size subtracted
            // to get a small encoding size win.
            _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.NullableValueOffset, (uint)field.Offset - 1);
        }

        /// <summary>
        /// ICastable is a special interface whose two methods are not invoked using regular interface dispatch.
        /// Instead, their VTable slots are recorded on the EEType of an object implementing ICastable and are
        /// called directly.
        /// </summary>
        protected virtual void ComputeICastableVirtualMethodSlots(NodeFactory factory)
        {
            if (_type.IsInterface || !EmitVirtualSlotsAndInterfaces)
                return;

            foreach (DefType itf in _type.RuntimeInterfaces)
            {
                if (itf == factory.ICastableInterface)
                {
                    MethodDesc isInstDecl = itf.GetKnownMethod("IsInstanceOfInterface", null);
                    MethodDesc getImplTypeDecl = itf.GetKnownMethod("GetImplType", null);

                    MethodDesc isInstMethodImpl = _type.ResolveInterfaceMethodTarget(isInstDecl);
                    MethodDesc getImplTypeMethodImpl = _type.ResolveInterfaceMethodTarget(getImplTypeDecl);

                    int isInstMethodSlot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, isInstMethodImpl);
                    int getImplTypeMethodSlot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, getImplTypeMethodImpl);

                    Debug.Assert(isInstMethodSlot != -1 && getImplTypeMethodSlot != -1);

                    _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.ICastableIsInstSlot, (uint)isInstMethodSlot);
                    _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.ICastableGetImplTypeSlot, (uint)getImplTypeMethodSlot);
                }
            }
        }

        void ComputeValueTypeFieldPadding()
        {
            // All objects that can have appreciable which can be derived from size compute ValueTypeFieldPadding. 
            // Unfortunately, the name ValueTypeFieldPadding is now wrong to avoid integration conflicts.

            // Interfaces, sealed types, and non-DefTypes cannot be derived from
            if (_type.IsInterface || !_type.IsDefType || (_type.IsSealed() && !_type.IsValueType))
                return;

            DefType defType = _type as DefType;
            Debug.Assert(defType != null);

            uint valueTypeFieldPadding = checked((uint)(defType.InstanceByteCount - defType.InstanceByteCountUnaligned));
            uint valueTypeFieldPaddingEncoded = EETypeBuilderHelpers.ComputeValueTypeFieldPaddingFieldValue(valueTypeFieldPadding, (uint)defType.InstanceFieldAlignment);

            if (valueTypeFieldPaddingEncoded != 0)
            {
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.ValueTypeFieldPadding, valueTypeFieldPaddingEncoded);
            }
        }

        protected override void OnMarked(NodeFactory context)
        {
            //Debug.Assert(_type.IsTypeDefinition || !_type.HasSameTypeDefinition(context.ArrayOfTClass), "Asking for Array<T> EEType");
        }

        /// <summary>
        /// Validates that it will be possible to create an EEType for '<paramref name="type"/>'.
        /// </summary>
        public static void CheckCanGenerateEEType(NodeFactory factory, TypeDesc type)
        {
            // Don't validate generic definitons
            if (type.IsGenericDefinition)
            {
                return;
            }

            // System.__Canon or System.__UniversalCanon
            if(type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
            {
                return;
            }

            // It must be possible to create an EEType for the base type of this type
            TypeDesc baseType = type.BaseType;
            if (baseType != null)
            {
                // Make sure EEType can be created for this.
                factory.NecessaryTypeSymbol(GetFullCanonicalTypeForCanonicalType(baseType));
            }
            
            // We need EETypes for interfaces
            foreach (var intf in type.RuntimeInterfaces)
            {
                // Make sure EEType can be created for this.
                factory.NecessaryTypeSymbol(GetFullCanonicalTypeForCanonicalType(intf));
            }

            // Validate classes, structs, enums, interfaces, and delegates
            DefType defType = type as DefType;
            if (defType != null)
            {
                // Ensure we can compute the type layout
                defType.ComputeInstanceLayout(InstanceLayoutKind.TypeAndFields);

                //
                // The fact that we generated an EEType means that someone can call RuntimeHelpers.RunClassConstructor.
                // We need to make sure this is possible.
                //
                if (factory.TypeSystemContext.HasLazyStaticConstructor(defType))
                {
                    defType.ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizesAndFields);
                }

                // Make sure instantiation length matches the expectation
                // TODO: it might be more resonable for the type system to enforce this (also for methods)
                if (defType.Instantiation.Length != defType.GetTypeDefinition().Instantiation.Length)
                {
                    throw new TypeSystemException.TypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                }

                foreach (TypeDesc typeArg in defType.Instantiation)
                {
                    // ByRefs, pointers, function pointers, and System.Void are never valid instantiation arguments
                    if (typeArg.IsByRef || typeArg.IsPointer || typeArg.IsFunctionPointer || typeArg.IsVoid)
                    {
                        throw new TypeSystemException.TypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                    }

                    // TODO: validate constraints
                }

                // Check the type doesn't have bogus MethodImpls or overrides and we can get the finalizer.
                defType.GetFinalizer();
            }

            // Validate parameterized types
            ParameterizedType parameterizedType = type as ParameterizedType;
            if (parameterizedType != null)
            {
                TypeDesc parameterType = parameterizedType.ParameterType;

                // Make sure EEType can be created for this.
                factory.NecessaryTypeSymbol(parameterType);

                if (parameterizedType.IsArray)
                {
                    if (parameterType.IsPointer || parameterType.IsFunctionPointer)
                    {
                        // Arrays of pointers and function pointers are not currently supported
                        throw new TypeSystemException.TypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                    }

                    int elementSize = parameterType.GetElementSize();
                    if (elementSize >= ushort.MaxValue)
                    {
                        // Element size over 64k can't be encoded in the GCDesc
                        throw new TypeSystemException.TypeLoadException(ExceptionStringID.ClassLoadValueClassTooLarge, parameterType);
                    }

                    if (((ArrayType)parameterizedType).Rank > 32)
                    {
                        throw new TypeSystemException.TypeLoadException(ExceptionStringID.ClassLoadRankTooLarge, type);
                    }
                }

                // Validate we're not constructing a type over a ByRef
                if (parameterType.IsByRef)
                {
                    // CLR compat note: "ldtoken int32&&" will actually fail with a message about int32&; "ldtoken int32&[]"
                    // will fail with a message about being unable to create an array of int32&. This is a middle ground.
                    throw new TypeSystemException.TypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                }

                // It might seem reasonable to disallow array of void, but the CLR doesn't prevent that too hard.
                // E.g. "newarr void" will fail, but "newarr void[]" or "ldtoken void[]" will succeed.
            }

            // Function pointer EETypes are not currently supported
            if (type.IsFunctionPointer)
            {
                throw new TypeSystemException.TypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
            }
        }
    }
}
