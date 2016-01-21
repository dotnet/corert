// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ILCompiler.DependencyAnalysisFramework;
using Internal.Runtime;
using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;

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
    ///                 | UTF16 character encoding), and 0 for all other types.
    ///                 |
    /// UInt16          | EETypeKind (Normal, Array, Pointer type). Flags for: IsValueType, IsCrossModule, HasPointers,
    ///                 | HasOptionalFields, IsInterface, IsGeneric. Top 5 bits are used for enum CorElementType to
    ///                 | record whether it's back by an Int32, Int16 etc
    ///                 |
    /// [Pointer Size]  | Related type. Base type for regular types. Element type for arrays / pointer types.
    ///                 |
    /// UInt16          | Number of VTable slots (X)
    ///                 |
    /// UInt16          | Number of interfaces implemented by type (Y)
    ///                 |
    /// UInt32          | Hash code
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
    /// 
    /// </summary>
    internal class EETypeNode : ObjectNode, ISymbolNode
    {
        private TypeDesc _type;
        private bool _constructed;
        EETypeOptionalFieldsBuilder _optionalFieldsBuilder = new EETypeOptionalFieldsBuilder();
        EETypeOptionalFieldsNode _optionalFieldsNode;

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

        public override string Section
        {
            get
            {
                return "data";
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public void SetDispatchMapIndex(uint index)
        {
            _optionalFieldsBuilder.SetFieldValue(EETypeOptionalFieldsElement.DispatchMap, index);
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
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

            // Todo: Generic Type Definition EETypes
            //       Early-out just to prevent crashing at compile time...
            if (_type.HasInstantiation && _type.IsTypeDefinition)
            {
                objData.EmitZeroPointer();
                return objData.ToObjectData();
            }

            ComputeOptionalEETypeFields(factory);

            if (null == _optionalFieldsNode)
            {
                _optionalFieldsNode = factory.EETypeOptionalFields(_optionalFieldsBuilder);
            }

            OutputComponentSize(ref objData);
            OutputFlags(factory, ref objData);
            OutputBaseSize(ref objData);
            OutputRelatedType(factory, ref objData);
            OutputVirtualSlotAndInterfaceCount(factory, ref objData);

            objData.EmitInt(_type.GetHashCode());

            if (_constructed)
            {
                OutputVirtualSlots(factory, ref objData, _type, _type);
                OutputInterfaceMap(factory, ref objData);
            }

            OutputFinalizerMethod(factory, ref objData);
            OutputOptionalFields(factory, ref objData);
            OutputNullableTypeParameter(factory, ref objData);

            return objData.ToObjectData();
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = new DependencyNodeCore<NodeFactory>.DependencyList();
            if (_type is MetadataType && _constructed)
            {
                dependencyList.Add(factory.InterfaceDispatchMap(_type), "Interface dispatch map");
            }

            dependencyList.Add(factory.EETypeOptionalFields(_optionalFieldsBuilder), "EEType optional fields");
            return dependencyList;
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
                foreach (MethodDesc method in _type.GetMethods())
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
            if (_type is MetadataType)
            {
                foreach (MethodDesc decl in VirtualFunctionResolution.EnumAllVirtualSlots((MetadataType)_type))
                {
                    MethodDesc impl = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(decl, (MetadataType)_type);
                    if (impl.OwningType == _type && !impl.IsAbstract)
                    {
                        yield return new DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry(factory.MethodEntrypoint(impl), factory.VirtualMethodUse(decl), "Virtual method");
                    }
                }

                // Add conditional dependencies for interface methods the type implements. For example, if the type T implements
                // interface IFoo which has a method M1, add a dependency on T.M1 dependent on IFoo.M1 being called, since it's
                // possible for any IFoo object to actually be an instance of T.
                foreach (DefType interfaceType in _type.RuntimeInterfaces)
                {
                    Debug.Assert(interfaceType.IsInterface);

                    foreach (MethodDesc interfaceMethod in interfaceType.GetMethods())
                    {
                        Debug.Assert(interfaceMethod.IsVirtual);
                        MethodDesc implMethod = VirtualFunctionResolution.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod, _type.GetClosestMetadataType());
                        if (implMethod != null)
                        {
                            yield return new DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.ReadyToRunHelper(ReadyToRunHelperId.InterfaceDispatch, interfaceMethod), "Interface method");
                        }
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
            return 16 + pointerSize;
        }

        private void OutputComponentSize(ref ObjectDataBuilder objData)
        {
            if (_type.IsArray)
            {
                objData.EmitShort((short)((ArrayType)_type).ElementType.GetElementSize());
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

            // Todo: RelatedTypeViaIATFlag when we support cross-module EETypes
            // Todo: GenericVarianceFlag when we support variance
            // Todo: Generic Type Definition EETypes

            if (_optionalFieldsBuilder.IsAtLeastOneFieldUsed())
            {
                flags |= (UInt16)EETypeFlags.OptionalFieldsFlag;
            }

            objData.EmitShort((short)flags);
        }

        private void OutputBaseSize(ref ObjectDataBuilder objData)
        {
            int pointerSize = _type.Context.Target.PointerSize;
            int minimumObjectSize = pointerSize * 3;
            int objectSize;

            if (_type is MetadataType)
            {
                objectSize = pointerSize +
                    ((MetadataType)_type).InstanceByteCount; // +pointerSize for SyncBlock
            }
            else if (_type is ArrayType)
            {
                objectSize = 3 * pointerSize; // SyncBlock + EETypePtr + Length
                int rank = ((ArrayType)_type).Rank;
                if (rank > 1)
                    objectSize +=
                        2 * _type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() * rank;
            }
            else if (_type is PointerType)
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

            int virtualSlotCount = 0;
            TypeDesc currentTypeSlice = _type;

            while (currentTypeSlice != null)
            {
                List<MethodDesc> virtualSlots;
                factory.VirtualSlots.TryGetValue(currentTypeSlice, out virtualSlots);
                if (virtualSlots != null)
                {
                    virtualSlotCount += virtualSlots.Count;
                }

                currentTypeSlice = currentTypeSlice.BaseType;
            }

            objData.EmitShort(checked((short)virtualSlotCount));
            objData.EmitShort(checked((short)_type.RuntimeInterfaces.Length));
        }

        private void OutputVirtualSlots(NodeFactory factory, ref ObjectDataBuilder objData, TypeDesc implType, TypeDesc declType)
        {
            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(factory, ref objData, implType, baseType);

            List<MethodDesc> virtualSlots;
            factory.VirtualSlots.TryGetValue(declType, out virtualSlots);

            if (virtualSlots != null)
            {
                for (int i = 0; i < virtualSlots.Count; i++)
                {
                    MethodDesc declMethod = virtualSlots[i];
                    MethodDesc implMethod = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(declMethod, implType.GetClosestMetadataType());

                    if (!implMethod.IsAbstract)
                        objData.EmitPointerReloc(factory.MethodEntrypoint(implMethod));
                    else
                        objData.EmitZeroPointer();
                }
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

        /// <summary>
        /// Populate the OptionalFieldsRuntimeBuilder if any optional fields are required.
        /// </summary>
        private void ComputeOptionalEETypeFields(NodeFactory factory)
        {
            ComputeRareFlags();
            ComputeNullableValueOffset();
            ComputeICastableVirtualMethodSlots(factory);
            ComputeValueTypeFieldPadding();
        }

        void ComputeRareFlags()
        {
            uint flags = 0;

            if (_type.IsNullable)
            {
                flags |= (uint)EETypeRareFlags.IsNullableFlag;
            }

            if (_type.HasStaticConstructor)
            {
                flags |= (uint)EETypeRareFlags.HasCctorFlag;
            }
            
            if (EETypeBuilderHelpers.ComputeRequiresAlign8(_type))
            {
                flags |= (uint)EETypeRareFlags.RequiresAlign8Flag;
            }

            if (_type is DefType && ((DefType)_type).IsHFA())
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

            var field = _type.GetField("value");

            // Ensure the definition of Nullable<T> didn't change on us
            Debug.Assert(field != null);

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
                    var isInstMethod = itf.GetMethod("IsInstanceOfInterface", null);
                    var getImplTypeMethod = itf.GetMethod("GetImplType", null);
                    Debug.Assert(isInstMethod != null && getImplTypeMethod != null);

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
    }
}
