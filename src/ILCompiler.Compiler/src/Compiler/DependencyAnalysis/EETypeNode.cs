// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysisFramework;
using Internal.IL;
using Internal.Runtime;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;

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
    /// [Pointer Size]  | Pointer to containing Module indirection cell
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
    internal sealed partial class EETypeNode : ObjectNode, ISymbolNode, IEETypeNode
    {
        private TypeDesc _type;
        private bool _constructed;
        EETypeOptionalFieldsBuilder _optionalFieldsBuilder = new EETypeOptionalFieldsBuilder();

        public EETypeNode(TypeDesc type, bool constructed)
        {
            _type = type;
            _constructed = constructed;
        }

        public override string GetName()
        {
            if (_constructed)
            {
                return ((ISymbolNode)this).MangledName + " constructed";
            }
            else
            {
                return ((ISymbolNode)this).MangledName;
            }
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            if (!_constructed)
            {
                // If there is a constructed version of this node in the graph, emit that instead
                if (((DependencyNode)factory.ConstructedTypeSymbol(_type)).Marked)
                {
                    return true;
                }
            }

            return false;
        }

        public TypeDesc Type
        {
            get { return _type; }
        }

        public bool Constructed
        {
            get { return _constructed; }
        }

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

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return factory.CompilationModuleGroup.ShouldShareAcrossModules(_type);
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public void SetDispatchMapIndex(int index)
        {
            _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.DispatchMap, checked((uint)index));
        }

        int ISymbolNode.Offset
        {
            get
            {
                return GCDescSize;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return "__EEType_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.Alignment = 16;
            objData.DefinedSymbols.Add(this);

            if (!_type.IsGenericDefinition)
                ComputeOptionalEETypeFields(factory);

            OutputGCDesc(ref objData);
            OutputComponentSize(ref objData);
            OutputFlags(factory, ref objData);
            OutputBaseSize(ref objData);
            OutputRelatedType(factory, ref objData);

            // Avoid consulting VTable slots until they're guaranteed complete during final data emission
            if (!relocsOnly)
            {
                OutputVirtualSlotAndInterfaceCount(factory, ref objData);
            }

            objData.EmitInt(_type.GetHashCode());
            objData.EmitPointerReloc(factory.ModuleManagerIndirection);

            if (_constructed)
            {
                Debug.Assert(!_type.IsGenericDefinition);

                // Avoid consulting VTable slots until they're guaranteed complete during final data emission
                if (!relocsOnly)
                {
                    OutputVirtualSlots(factory, ref objData, _type, _type);
                }

                OutputInterfaceMap(factory, ref objData);
            }

            if (!_type.IsGenericDefinition)
            {
                OutputFinalizerMethod(factory, ref objData);
                OutputOptionalFields(factory, ref objData);
                OutputNullableTypeParameter(factory, ref objData);
                OutputGenericInstantiationDetails(factory, ref objData);
            }

            return objData.ToObjectData();
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            if (_constructed)
            {
                DependencyList dependencyList = new DependencyList();
                if (_type.RuntimeInterfaces.Length > 0)
                {
                    dependencyList.Add(factory.InterfaceDispatchMap(_type), "Interface dispatch map");

                    // If any of the implemented interfaces have variance, calls against compatible interface methods
                    // could result in interface methods of this type being used (e.g. IEnumberable<object>.GetEnumerator()
                    // can dispatch to an implementation of IEnumerable<string>.GetEnumerator()).
                    // For now, we will not try to optimize this and we will pretend all interface methods are necessary.
                    MetadataType mdType = _type.GetClosestMetadataType();
                    foreach (var implementedInterface in mdType.RuntimeInterfaces)
                    {
                        if (implementedInterface.HasVariance)
                        {
                            foreach (var interfaceMethod in implementedInterface.GetAllVirtualMethods())
                            {
                                MethodDesc implMethod = mdType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
                                if (implMethod != null)
                                {
                                    dependencyList.Add(factory.VirtualMethodUse(interfaceMethod), "Variant interface method");
                                    dependencyList.Add(factory.VirtualMethodUse(implMethod), "Variant interface method");
                                }
                            }
                        }
                    }
                }

                if (_type.IsArray)
                {
                    // Array EEType depends on System.Array's virtuals. Array EETypes don't point to
                    // their base type (i.e. there's no reloc based dependency making this "just work").
                    dependencyList.Add(factory.ConstructedTypeSymbol(_type.BaseType), "Array base type");
                }

                dependencyList.Add(factory.VTable(_type), "VTable");
                
                return dependencyList;
            }

            return null;
        }

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                // non constructed types don't have vtables
                if (!_constructed)
                    return false;

                // Since the vtable is dependency driven, generate conditional static dependencies for
                // all possible vtable entries
                if (_type.GetClosestMetadataType().GetAllVirtualMethods().GetEnumerator().MoveNext())
                {
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
            MetadataType mdType = _type.GetClosestMetadataType();

            foreach (MethodDesc decl in mdType.EnumAllVirtualSlots())
            {
                MethodDesc impl = mdType.FindVirtualFunctionTargetMethodOnObjectType(decl);
                if (impl.OwningType == mdType && !impl.IsAbstract)
                {
                    yield return new DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry(factory.MethodEntrypoint(impl, _type.IsValueType), factory.VirtualMethodUse(decl), "Virtual method");
                }
            }

            Debug.Assert(
                _type == mdType ||
                ((System.Collections.IStructuralEquatable)mdType.RuntimeInterfaces).Equals(_type.RuntimeInterfaces,
                EqualityComparer<DefType>.Default));

            // Add conditional dependencies for interface methods the type implements. For example, if the type T implements
            // interface IFoo which has a method M1, add a dependency on T.M1 dependent on IFoo.M1 being called, since it's
            // possible for any IFoo object to actually be an instance of T.
            foreach (DefType interfaceType in mdType.RuntimeInterfaces)
            {
                Debug.Assert(interfaceType.IsInterface);

                foreach (MethodDesc interfaceMethod in interfaceType.GetAllVirtualMethods())
                {
                    MethodDesc implMethod = mdType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
                    if (implMethod != null)
                    {
                        yield return new CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.ReadyToRunHelper(ReadyToRunHelperId.InterfaceDispatch, interfaceMethod), "Interface method");
                        yield return new CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.ReadyToRunHelper(ReadyToRunHelperId.ResolveVirtualFunction, interfaceMethod), "Interface method address");
                    }
                }
            }
        }

        /// <summary>
        /// Returns the offset within an EEType of the beginning of VTable entries
        /// </summary>
        /// <param name="pointerSize">The size of a pointer in bytes in the target architecture</param>
        public static int GetVTableOffset(int pointerSize)
        {
            return 16 + 2 * pointerSize;
        }

        private int GCDescSize
        {
            get
            {
                if (!_constructed || _type.IsGenericDefinition)
                    return 0;

                return GCDescEncoder.GetGCDescSize(_type);
            }
        }

        private void OutputGCDesc(ref ObjectDataBuilder builder)
        {
            if (!_constructed || _type.IsGenericDefinition)
            {
                Debug.Assert(GCDescSize == 0);
                return;
            }

            GCDescEncoder.EncodeGCDesc(ref builder, _type);
        }

        private void OutputComponentSize(ref ObjectDataBuilder objData)
        {
            if (_type.IsArray)
            {
                int elementSize = ((ArrayType)_type).ElementType.GetElementSize();
                if (elementSize >= 64 * 1024)
                {
                    // TODO: Array of type 'X' cannot be created because base value type is too large.
                    throw new TypeLoadException();
                }

                objData.EmitShort((short)elementSize);
            }
            else if (_type.IsString)
            {
                objData.EmitShort(2);
            }
            else if (_type.IsGenericDefinition)
            {
                objData.EmitShort((short)_type.Instantiation.Length);
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

            // Todo: RelatedTypeViaIATFlag when we support cross-module EETypes
            // Todo: Generic Type Definition EETypes

            if (_optionalFieldsBuilder.IsAtLeastOneFieldUsed())
            {
                flags |= (UInt16)EETypeFlags.OptionalFieldsFlag;
            }

            objData.EmitShort((short)flags);
        }

        private void OutputBaseSize(ref ObjectDataBuilder objData)
        {
            if (_type.IsGenericDefinition)
            {
                objData.EmitInt(0);
                return;
            }

            int pointerSize = _type.Context.Target.PointerSize;
            int minimumObjectSize = pointerSize * 3;
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
                if (!_type.IsSzArray)
                    objectSize +=
                        2 * _type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() * ((ArrayType)_type).Rank;
            }
            else if (_type.IsPointer)
            {
                // Object size 0 is a sentinel value in the runtime for parameterized types that means "Pointer Type"
                objData.EmitInt(0);
                return;
            }
            else
                throw new NotImplementedException();

            objectSize = AlignmentHelper.AlignUp(objectSize, pointerSize);
            objectSize = Math.Max(minimumObjectSize, objectSize);

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

        private void OutputRelatedType(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            ISymbolNode relatedTypeNode = null;

            if (_type.IsArray || _type.IsPointer)
            {
                var parameterType = ((ParameterizedType)_type).ParameterType;
                relatedTypeNode = factory.NecessaryTypeSymbol(parameterType);
            }
            else if (_type.IsGenericDefinition)
            {
                // Related type is not set for generic definitions
            }
            else
            {
                TypeDesc baseType = _type.BaseType;
                if (baseType != null)
                {
                    if (_constructed)
                    {
                        relatedTypeNode = factory.ConstructedTypeSymbol(baseType);
                    }
                    else
                    {
                        relatedTypeNode = factory.NecessaryTypeSymbol(baseType);
                    }
                }
            }

            if (relatedTypeNode != null)
            {
                objData.EmitPointerReloc(relatedTypeNode);
            }
            else
            {
                objData.EmitZeroPointer();
            }
        }

        private void OutputVirtualSlotAndInterfaceCount(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if (!_constructed)
            {
                objData.EmitShort(0);
                objData.EmitShort(0);
                return;
            }

            Debug.Assert(!_type.IsGenericDefinition);

            int virtualSlotCount = 0;
            TypeDesc currentTypeSlice = _type.GetClosestMetadataType();

            while (currentTypeSlice != null)
            {
                virtualSlotCount += factory.VTable(currentTypeSlice).Slots.Count;
                currentTypeSlice = currentTypeSlice.BaseType;
            }

            objData.EmitShort(checked((short)virtualSlotCount));
            objData.EmitShort(checked((short)_type.RuntimeInterfaces.Length));
        }

        private void OutputVirtualSlots(NodeFactory factory, ref ObjectDataBuilder objData, TypeDesc implType, TypeDesc declType)
        {
            declType = declType.GetClosestMetadataType();

            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(factory, ref objData, implType, baseType);

            IReadOnlyList<MethodDesc> virtualSlots = factory.VTable(declType).Slots;
            
            for (int i = 0; i < virtualSlots.Count; i++)
            {
                MethodDesc declMethod = virtualSlots[i];
                MethodDesc implMethod = implType.GetClosestMetadataType().FindVirtualFunctionTargetMethodOnObjectType(declMethod);

                if (declMethod.HasInstantiation)
                {
                    // Generic virtual methods will "compile", but will fail to link. Check for it here.
                    throw new NotImplementedException("VTable for " + _type + " has generic virtual methods.");
                }

                if (!implMethod.IsAbstract)
                    objData.EmitPointerReloc(factory.MethodEntrypoint(implMethod, implMethod.OwningType.IsValueType));
                else
                    objData.EmitZeroPointer();
            }
        }

        private void OutputInterfaceMap(NodeFactory factory, ref ObjectDataBuilder objData)
        {
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
                objData.EmitPointerReloc(factory.MethodEntrypoint(finalizerMethod));
            }
        }

        private void OutputOptionalFields(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            if(_optionalFieldsBuilder.IsAtLeastOneFieldUsed())
            {
                objData.EmitPointerReloc(factory.EETypeOptionalFields(_optionalFieldsBuilder));
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
        private void ComputeOptionalEETypeFields(NodeFactory factory)
        {
            ComputeRareFlags(factory);
            ComputeNullableValueOffset();
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

            if (factory.TypeInitializationManager.HasLazyStaticConstructor(_type))
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

            if (flags != 0)
            {
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.RareFlags, flags);
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
            _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.NullableValueOffset, (uint)field.Offset - 1);
        }

        /// <summary>
        /// ICastable is a special interface whose two methods are not invoked using regular interface dispatch.
        /// Instead, their VTable slots are recorded on the EEType of an object implementing ICastable and are
        /// called directly.
        /// </summary>
        void ComputeICastableVirtualMethodSlots(NodeFactory factory)
        {
            // TODO: This method is untested (we don't support interfaces yet)
            if (_type.IsInterface)
                return;
            
            foreach (DefType itf in _type.RuntimeInterfaces)
            {
                if (itf == factory.ICastableInterface)
                {
                    var isInstMethod = itf.GetKnownMethod("IsInstanceOfInterface", null);
                    var getImplTypeMethod = itf.GetKnownMethod("GetImplType", null);

                    int isInstMethodSlot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, isInstMethod);
                    int getImplTypeMethodSlot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, getImplTypeMethod);

                    if (isInstMethodSlot != -1 || getImplTypeMethodSlot != -1)
                    {
                        var rareFlags = _optionalFieldsBuilder.GetFieldValue(EETypeOptionalFieldsElement.RareFlags, 0);
                        rareFlags |= (uint)EETypeRareFlags.ICastableFlag;
                        _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.RareFlags, rareFlags);
                    }

                    if (isInstMethodSlot != -1)
                    {
                        _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.ICastableIsInstSlot, (uint)isInstMethodSlot);
                    }
                    if (getImplTypeMethodSlot != -1)
                    {
                        _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.ICastableGetImplTypeSlot, (uint)getImplTypeMethodSlot);
                    }
                }
            }
        }

        void ComputeValueTypeFieldPadding()
        {
            if (!_type.IsValueType)
                return;

            DefType defType = _type as DefType;
            Debug.Assert(defType != null);

            uint valueTypeFieldPadding = checked((uint)(defType.InstanceByteCount - defType.InstanceByteCountUnaligned));
            uint valueTypeFieldPaddingEncoded = EETypeBuilderHelpers.ComputeValueTypeFieldPaddingFieldValue(valueTypeFieldPadding, (uint)defType.InstanceFieldAlignment);

            if (valueTypeFieldPaddingEncoded != 0)
            {
                _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.ValueTypeFieldPadding, valueTypeFieldPaddingEncoded);
            }
        }
        
        public override bool HasDynamicDependencies
        {
            get
            {
                // This node's EETypeOptionalFields node may change if this EEType implements interfaces
                // that are used since the dispatch map table index is computed once we know the interface
                // layout later on in compilation.
                return _type.RuntimeInterfaces.Length > 0 && _constructed;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory)
        {
            List<CombinedDependencyListEntry> dynamicNodes = new List<DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry>();
            dynamicNodes.Add(new CombinedDependencyListEntry(factory.EETypeOptionalFields(_optionalFieldsBuilder), null, "EEType optional fields"));
            return dynamicNodes;
        }

        protected override void OnMarked(NodeFactory context)
        {
            Debug.Assert(_type.IsTypeDefinition || !_type.HasSameTypeDefinition(context.ArrayOfTClass), "Asking for Array<T> EEType");
        }
    }
}
