// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

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
    public partial class EETypeNode : ObjectNode, IExportableSymbolNode, IEETypeNode, ISymbolDefinitionNode
    {
        protected TypeDesc _type;
        internal EETypeOptionalFieldsBuilder _optionalFieldsBuilder = new EETypeOptionalFieldsBuilder();
        internal EETypeOptionalFieldsNode _optionalFieldsNode;

        public EETypeNode(NodeFactory factory, TypeDesc type)
        {
            if (type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
                Debug.Assert(this is CanonicalDefinitionEETypeNode);
            else if (type.IsCanonicalSubtype(CanonicalFormKind.Any))
                Debug.Assert((this is CanonicalEETypeNode) || (this is NecessaryCanonicalEETypeNode));

            Debug.Assert(!type.IsRuntimeDeterminedSubtype);
            _type = type;
            _optionalFieldsNode = new EETypeOptionalFieldsNode(this);

            // Note: The fact that you can't create invalid EETypeNode is used from many places that grab
            // an EETypeNode from the factory with the sole purpose of making sure the validation has run
            // and that the result of the positive validation is "cached" (by the presence of an EETypeNode).
            CheckCanGenerateEEType(factory, type);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            // If there is a constructed version of this node in the graph, emit that instead
            if (ConstructedEETypeNode.CreationAllowed(_type))
                return factory.ConstructedTypeSymbol(_type).Marked;

            return false;
        }

        public override ObjectNode NodeForLinkage(NodeFactory factory)
        {
            return (ObjectNode)factory.NecessaryTypeSymbol(_type);
        }

        public virtual bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsType(Type);

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
            return nameMangler.NodeMangler.EEType(type);
        }

        public virtual void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.NodeMangler.EEType(_type));
        }

        int ISymbolNode.Offset => 0;
        int ISymbolDefinitionNode.Offset => GCDescSize;

        public override bool IsShareable => IsTypeNodeShareable(_type);

        public sealed override bool HasConditionalStaticDependencies
        {
            get
            {
                if (!EmitVirtualSlotsAndInterfaces)
                    return false;

                // Since the vtable is dependency driven, generate conditional static dependencies for
                // all possible vtable entries
                foreach (var method in _type.GetClosestDefType().GetAllMethods())
                {
                    if (method.IsVirtual)
                        return true;
                }

                // If the type implements at least one interface, calls against that interface could result in this type's
                // implementation being used.
                if (_type.RuntimeInterfaces.Length > 0)
                    return true;

                return false;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            Debug.Assert(EmitVirtualSlotsAndInterfaces);

            DefType defType = _type.GetClosestDefType();

            // If we're producing a full vtable, none of the dependencies are conditional.
            if (!factory.VTable(defType).HasFixedSlots)
            {
                foreach (MethodDesc decl in defType.EnumAllVirtualSlots())
                {
                    // Generic virtual methods are tracked by an orthogonal mechanism.
                    if (decl.HasInstantiation)
                        continue;

                    MethodDesc impl = defType.FindVirtualFunctionTargetMethodOnObjectType(decl);
                    if (impl.OwningType == defType && !impl.IsAbstract)
                    {
                        MethodDesc canonImpl = impl.GetCanonMethodTarget(CanonicalFormKind.Specific);
                        yield return new CombinedDependencyListEntry(factory.MethodEntrypoint(canonImpl, _type.IsValueType), factory.VirtualMethodUse(decl), "Virtual method");
                    }
                }

                Debug.Assert(
                    _type == defType ||
                    ((System.Collections.IStructuralEquatable)defType.RuntimeInterfaces).Equals(_type.RuntimeInterfaces,
                    EqualityComparer<DefType>.Default));

                // Add conditional dependencies for interface methods the type implements. For example, if the type T implements
                // interface IFoo which has a method M1, add a dependency on T.M1 dependent on IFoo.M1 being called, since it's
                // possible for any IFoo object to actually be an instance of T.
                foreach (DefType interfaceType in defType.RuntimeInterfaces)
                {
                    Debug.Assert(interfaceType.IsInterface);

                    foreach (MethodDesc interfaceMethod in interfaceType.GetAllMethods())
                    {
                        if (interfaceMethod.Signature.IsStatic)
                            continue;

                        // Generic virtual methods are tracked by an orthogonal mechanism.
                        if (interfaceMethod.HasInstantiation)
                            continue;

                        MethodDesc implMethod = defType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
                        if (implMethod != null)
                        {
                            yield return new CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.VirtualMethodUse(interfaceMethod), "Interface method");
                        }
                    }
                }
            }
        }

        public static bool IsTypeNodeShareable(TypeDesc type)
        {
            return type.IsParameterizedType || type.IsFunctionPointer || type is InstantiatedType;
        }

        private void AddVirtualMethodUseDependencies(DependencyList dependencyList, NodeFactory factory)
        {
            DefType closestDefType = _type.GetClosestDefType();

            if (_type.RuntimeInterfaces.Length > 0 && !factory.VTable(closestDefType).HasFixedSlots)
            {
                foreach (var implementedInterface in _type.RuntimeInterfaces)
                {
                    // If the type implements ICastable, the methods are implicitly necessary
                    if (implementedInterface == factory.ICastableInterface)
                    {
                        MethodDesc isInstDecl = implementedInterface.GetKnownMethod("IsInstanceOfInterface", null);
                        MethodDesc getImplTypeDecl = implementedInterface.GetKnownMethod("GetImplType", null);

                        MethodDesc isInstMethodImpl = _type.ResolveInterfaceMethodTarget(isInstDecl);
                        MethodDesc getImplTypeMethodImpl = _type.ResolveInterfaceMethodTarget(getImplTypeDecl);

                        if (isInstMethodImpl != null)
                            dependencyList.Add(factory.VirtualMethodUse(isInstMethodImpl), "ICastable IsInst");
                        if (getImplTypeMethodImpl != null)
                            dependencyList.Add(factory.VirtualMethodUse(getImplTypeMethodImpl), "ICastable GetImplType");
                    }

                    // If any of the implemented interfaces have variance, calls against compatible interface methods
                    // could result in interface methods of this type being used (e.g. IEnumberable<object>.GetEnumerator()
                    // can dispatch to an implementation of IEnumerable<string>.GetEnumerator()).
                    // For now, we will not try to optimize this and we will pretend all interface methods are necessary.
                    bool allInterfaceMethodsAreImplicitlyUsed = false;
                    if (implementedInterface.HasVariance)
                    {
                        TypeDesc interfaceDefinition = implementedInterface.GetTypeDefinition();
                        for (int i = 0; i < interfaceDefinition.Instantiation.Length; i++)
                        {
                            if (((GenericParameterDesc)interfaceDefinition.Instantiation[i]).Variance != 0 &&
                                !implementedInterface.Instantiation[i].IsValueType)
                            {
                                allInterfaceMethodsAreImplicitlyUsed = true;
                                break;
                            }
                        }
                    }
                    if (!allInterfaceMethodsAreImplicitlyUsed &&
                        (_type.IsArray || _type.GetTypeDefinition() == factory.ArrayOfTEnumeratorType) &&
                        implementedInterface.HasInstantiation)
                    {
                        // NOTE: we need to also do this for generic interfaces on arrays because they have a weird casting rule
                        // that doesn't require the implemented interface to be variant to consider it castable.
                        // For value types, we only need this when the array is castable by size (int[] and ICollection<uint>),
                        // or it's a reference type (Derived[] and ICollection<Base>).
                        TypeDesc elementType = _type.IsArray ? ((ArrayType)_type).ElementType : _type.Instantiation[0];
                        allInterfaceMethodsAreImplicitlyUsed =
                            CastingHelper.IsArrayElementTypeCastableBySize(elementType) ||
                            (elementType.IsDefType && !elementType.IsValueType);
                    }

                    if (allInterfaceMethodsAreImplicitlyUsed)
                    {
                        foreach (var interfaceMethod in implementedInterface.GetAllMethods())
                        {
                            if (interfaceMethod.Signature.IsStatic)
                                continue;

                            // Generic virtual methods are tracked by an orthogonal mechanism.
                            if (interfaceMethod.HasInstantiation)
                                continue;

                            MethodDesc implMethod = closestDefType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
                            if (implMethod != null)
                            {
                                dependencyList.Add(factory.VirtualMethodUse(interfaceMethod), "Variant interface method");
                                dependencyList.Add(factory.VirtualMethodUse(implMethod), "Variant interface method");
                            }
                        }
                    }
                }
            }
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();

            // Include the optional fields by default. We don't know if optional fields will be needed until
            // all of the interface usage has been stabilized. If we end up not needing it, the EEType node will not
            // generate any relocs to it, and the optional fields node will instruct the object writer to skip
            // emitting it.
            dependencies.Add(new DependencyListEntry(_optionalFieldsNode, "Optional fields"));

            StaticsInfoHashtableNode.AddStaticsInfoDependencies(ref dependencies, factory, _type);
            ReflectionFieldMapNode.AddReflectionFieldMapEntryDependencies(ref dependencies, factory, _type);

            if (EmitVirtualSlotsAndInterfaces)
            {
                AddVirtualMethodUseDependencies(dependencies, factory);
            }

            return dependencies;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);
            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            ComputeOptionalEETypeFields(factory, relocsOnly);

            OutputGCDesc(ref objData);
            OutputComponentSize(ref objData);
            OutputFlags(factory, ref objData);
            OutputBaseSize(ref objData);
            OutputRelatedType(factory, ref objData);

            // Number of vtable slots will be only known later. Reseve the bytes for it.
            var vtableSlotCountReservation = objData.ReserveShort();

            // Number of interfaces will only be known later. Reserve the bytes for it.
            var interfaceCountReservation = objData.ReserveShort();

            objData.EmitInt(_type.GetHashCode());
            objData.EmitPointerReloc(factory.TypeManagerIndirection);

            if (EmitVirtualSlotsAndInterfaces)
            {
                // Emit VTable
                Debug.Assert(objData.CountBytes - ((ISymbolDefinitionNode)this).Offset == GetVTableOffset(objData.TargetPointerSize));
                SlotCounter virtualSlotCounter = SlotCounter.BeginCounting(ref /* readonly */ objData);
                OutputVirtualSlots(factory, ref objData, _type, _type, relocsOnly);

                // Update slot count
                int numberOfVtableSlots = virtualSlotCounter.CountSlots(ref /* readonly */ objData);
                objData.EmitShort(vtableSlotCountReservation, checked((short)numberOfVtableSlots));

                // Emit interface map
                SlotCounter interfaceSlotCounter = SlotCounter.BeginCounting(ref /* readonly */ objData);
                OutputInterfaceMap(factory, ref objData);

                // Update slot count
                int numberOfInterfaceSlots = interfaceSlotCounter.CountSlots(ref /* readonly */ objData);
                objData.EmitShort(interfaceCountReservation, checked((short)numberOfInterfaceSlots));

            }
            else
            {
                // If we're not emitting any slots, the number of slots is zero.
                objData.EmitShort(vtableSlotCountReservation, 0);
                objData.EmitShort(interfaceCountReservation, 0);
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
                TypeDesc elementType = ((ArrayType)_type).ElementType;
                if (elementType == elementType.Context.UniversalCanonType)
                {
                    objData.EmitShort(0);
                }
                else
                {
                    int elementSize = elementType.GetElementSize().AsInt;
                    // We validated that this will fit the short when the node was constructed. No need for nice messages.
                    objData.EmitShort((short)checked((ushort)elementSize));
                }
            }
            else if (_type.IsString)
            {
                objData.EmitShort(StringComponentSize.Value);
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

            if (factory.TypeSystemContext.IsGenericArrayInterfaceType(_type))
            {
                // Runtime casting logic relies on all interface types implemented on arrays
                // to have the variant flag set (even if all the arguments are non-variant).
                // This supports e.g. casting uint[] to ICollection<int>
                flags |= (UInt16)EETypeFlags.GenericVarianceFlag;
            }

            if (!(this is CanonicalDefinitionEETypeNode))
            {
                foreach (DefType itf in _type.RuntimeInterfaces)
                {
                    if (itf == factory.ICastableInterface)
                    {
                        flags |= (UInt16)EETypeFlags.ICastableFlag;
                        break;
                    }
                }
            }               

            ISymbolNode relatedTypeNode = GetRelatedTypeNode(factory);

            // If the related type (base type / array element type / pointee type) is not part of this compilation group, and
            // the output binaries will be multi-file (not multiple object files linked together), indicate to the runtime
            // that it should indirect through the import address table
            if (relatedTypeNode != null && relatedTypeNode.RepresentsIndirectionCell)
            {
                flags |= (UInt16)EETypeFlags.RelatedTypeViaIATFlag;
            }

            if (HasOptionalFields)
            {
                flags |= (UInt16)EETypeFlags.OptionalFieldsFlag;
            }

            if (this is ClonedConstructedEETypeNode)
            {
                flags |= (UInt16)EETypeKind.ClonedEEType;
            }

            objData.EmitShort((short)flags);
        }

        protected virtual void OutputBaseSize(ref ObjectDataBuilder objData)
        {
            int pointerSize = _type.Context.Target.PointerSize;
            int objectSize;

            if (_type.IsDefType)
            {
                LayoutInt instanceByteCount = ((DefType)_type).InstanceByteCount;

                if (instanceByteCount.IsIndeterminate)
                {
                    // Some value must be put in, but the specific value doesn't matter as it
                    // isn't used for specific instantiations, and the universal canon eetype
                    // is never associated with an allocated object.
                    objectSize = pointerSize; 
                }
                else
                {
                    objectSize = pointerSize +
                        ((DefType)_type).InstanceByteCount.AsInt; // +pointerSize for SyncBlock
                }

                if (_type.IsValueType)
                    objectSize += pointerSize; // + EETypePtr field inherited from System.Object
            }
            else if (_type.IsArray)
            {
                objectSize = 3 * pointerSize; // SyncBlock + EETypePtr + Length
                if (_type.IsMdArray)
                    objectSize +=
                        2 * sizeof(int) * ((ArrayType)_type).Rank;
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
                    sizeof(int) +
                    StringComponentSize.Value;
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

        protected virtual void OutputVirtualSlots(NodeFactory factory, ref ObjectDataBuilder objData, TypeDesc implType, TypeDesc declType, bool relocsOnly)
        {
            Debug.Assert(EmitVirtualSlotsAndInterfaces);

            declType = declType.GetClosestDefType();

            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(factory, ref objData, implType, baseType, relocsOnly);

            // The generic dictionary pointer occupies the first slot of each type vtable slice
            if (declType.HasGenericDictionarySlot())
            {
                // All generic interface types have a dictionary slot, but only some of them have an actual dictionary.
                bool isInterfaceWithAnEmptySlot = declType.IsInterface &&
                    declType.ConvertToCanonForm(CanonicalFormKind.Specific) == declType;

                // Note: Canonical type instantiations always have a generic dictionary vtable slot, but it's empty
                if (declType.IsCanonicalSubtype(CanonicalFormKind.Any)
                    || factory.LazyGenericsPolicy.UsesLazyGenerics(declType)
                    || isInterfaceWithAnEmptySlot)
                    objData.EmitZeroPointer();
                else
                    objData.EmitPointerReloc(factory.TypeGenericDictionary(declType));
            }

            // It's only okay to touch the actual list of slots if we're in the final emission phase
            // or the vtable is not built lazily.
            if (relocsOnly && !factory.VTable(declType).HasFixedSlots)
                return;

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
        
        protected virtual IEETypeNode GetInterfaceTypeNode(NodeFactory factory, TypeDesc interfaceType)
        {
            return factory.NecessaryTypeSymbol(interfaceType);
        }

        protected virtual void OutputInterfaceMap(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            Debug.Assert(EmitVirtualSlotsAndInterfaces);

            foreach (var itf in _type.RuntimeInterfaces)
            {
                objData.EmitPointerRelocOrIndirectionReference(GetInterfaceTypeNode(factory, itf));
            }
        }

        private void OutputFinalizerMethod(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if (_type.HasFinalizer)
            {
                MethodDesc finalizerMethod = _type.GetFinalizer();
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
                objData.EmitPointerRelocOrIndirectionReference(factory.NecessaryTypeSymbol(_type.GetTypeDefinition()));

                GenericCompositionDetails details;
                if (_type.GetTypeDefinition() == factory.ArrayOfTEnumeratorType)
                {
                    // Generic array enumerators use special variance rules recognized by the runtime
                    details = new GenericCompositionDetails(_type.Instantiation, new[] { GenericVariance.ArrayCovariant });
                }
                else if (factory.TypeSystemContext.IsGenericArrayInterfaceType(_type))
                {
                    // Runtime casting logic relies on all interface types implemented on arrays
                    // to have the variant flag set (even if all the arguments are non-variant).
                    // This supports e.g. casting uint[] to ICollection<int>
                    details = new GenericCompositionDetails(_type, forceVarianceInfo: true);
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

            MetadataType metadataType = _type as MetadataType;

            if (_type.IsNullable)
            {
                flags |= (uint)EETypeRareFlags.IsNullableFlag;

                // If the nullable type is not part of this compilation group, and
                // the output binaries will be multi-file (not multiple object files linked together), indicate to the runtime
                // that it should indirect through the import address table
                if (factory.NecessaryTypeSymbol(_type.Instantiation[0]).RepresentsIndirectionCell)
                    flags |= (uint)EETypeRareFlags.NullableTypeViaIATFlag;
            }

            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type))
            {
                flags |= (uint)EETypeRareFlags.HasCctorFlag;
            }

            if (EETypeBuilderHelpers.ComputeRequiresAlign8(_type))
            {
                flags |= (uint)EETypeRareFlags.RequiresAlign8Flag;
            }

            if (metadataType != null && metadataType.IsHfa)
            {
                flags |= (uint)EETypeRareFlags.IsHFAFlag;
            }

            if (metadataType != null && !_type.IsInterface && metadataType.IsAbstract)
            {
                flags |= (uint)EETypeRareFlags.IsAbstractClassFlag;
            }

            if (metadataType != null && metadataType.IsByRefLike)
            {
                flags |= (uint)EETypeRareFlags.IsByRefLikeFlag;
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

            if (!_type.Instantiation[0].IsCanonicalSubtype(CanonicalFormKind.Universal))
            {
                var field = _type.GetKnownField("value");

                // In the definition of Nullable<T>, the first field should be the boolean representing "hasValue"
                Debug.Assert(field.Offset.AsInt > 0);

                // The contract with the runtime states the Nullable value offset is stored with the boolean "hasValue" size subtracted
                // to get a small encoding size win.
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.NullableValueOffset, (uint)field.Offset.AsInt - 1);
            }
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

        protected virtual void ComputeValueTypeFieldPadding()
        {
            // All objects that can have appreciable which can be derived from size compute ValueTypeFieldPadding. 
            // Unfortunately, the name ValueTypeFieldPadding is now wrong to avoid integration conflicts.

            // Interfaces, sealed types, and non-DefTypes cannot be derived from
            if (_type.IsInterface || !_type.IsDefType || (_type.IsSealed() && !_type.IsValueType))
                return;

            DefType defType = _type as DefType;
            Debug.Assert(defType != null);

            uint valueTypeFieldPaddingEncoded;

            if (defType.InstanceByteCount.IsIndeterminate)
            {
                valueTypeFieldPaddingEncoded = EETypeBuilderHelpers.ComputeValueTypeFieldPaddingFieldValue(0, 1, _type.Context.Target.PointerSize);
            }
            else
            {
                uint valueTypeFieldPadding = checked((uint)(defType.InstanceByteCount.AsInt - defType.InstanceByteCountUnaligned.AsInt));
                valueTypeFieldPaddingEncoded = EETypeBuilderHelpers.ComputeValueTypeFieldPaddingFieldValue(valueTypeFieldPadding, (uint)defType.InstanceFieldAlignment.AsInt, _type.Context.Target.PointerSize);
            }

            if (valueTypeFieldPaddingEncoded != 0)
            {
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldTag.ValueTypeFieldPadding, valueTypeFieldPaddingEncoded);
            }
        }

        protected override void OnMarked(NodeFactory context)
        {
            if (!context.IsCppCodegenTemporaryWorkaround)
            { 
                Debug.Assert(_type.IsTypeDefinition || !_type.HasSameTypeDefinition(context.ArrayOfTClass), "Asking for Array<T> EEType");
            }
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
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                }

                foreach (TypeDesc typeArg in defType.Instantiation)
                {
                    // ByRefs, pointers, function pointers, and System.Void are never valid instantiation arguments
                    if (typeArg.IsByRef
                        || typeArg.IsPointer
                        || typeArg.IsFunctionPointer
                        || typeArg.IsVoid
                        || (typeArg.IsValueType && ((DefType)typeArg).IsByRefLike))
                    {
                        ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
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
                    if (parameterType.IsFunctionPointer)
                    {
                        // Arrays of function pointers are not currently supported
                        ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                    }

                    LayoutInt elementSize = parameterType.GetElementSize();
                    if (!elementSize.IsIndeterminate && elementSize.AsInt >= ushort.MaxValue)
                    {
                        // Element size over 64k can't be encoded in the GCDesc
                        ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadValueClassTooLarge, parameterType);
                    }

                    if (((ArrayType)parameterizedType).Rank > 32)
                    {
                        ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadRankTooLarge, type);
                    }

                    if ((parameterType.IsDefType) && ((DefType)parameterType).IsByRefLike)
                    {
                        // Arrays of byref-like types are not allowed
                        ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                    }
                }

                // Validate we're not constructing a type over a ByRef
                if (parameterType.IsByRef)
                {
                    // CLR compat note: "ldtoken int32&&" will actually fail with a message about int32&; "ldtoken int32&[]"
                    // will fail with a message about being unable to create an array of int32&. This is a middle ground.
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
                }

                // It might seem reasonable to disallow array of void, but the CLR doesn't prevent that too hard.
                // E.g. "newarr void" will fail, but "newarr void[]" or "ldtoken void[]" will succeed.
            }

            // Function pointer EETypes are not currently supported
            if (type.IsFunctionPointer)
            {
                ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
            }
        }

        private struct SlotCounter
        {
            private int _startBytes;

            public static SlotCounter BeginCounting(ref /* readonly */ ObjectDataBuilder builder)
                => new SlotCounter { _startBytes = builder.CountBytes };

            public int CountSlots(ref /* readonly */ ObjectDataBuilder builder)
            {
                int bytesEmitted = builder.CountBytes - _startBytes;
                Debug.Assert(bytesEmitted % builder.TargetPointerSize == 0);
                return bytesEmitted / builder.TargetPointerSize;
            }

        }
    }
}
