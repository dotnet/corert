// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysisFramework;
using Internal.Runtime;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class ConstructedEETypeNode : EETypeNode, ISymbolNode
    {
        public ConstructedEETypeNode(TypeDesc type) : base(type)
        {
            Debug.Assert(!_type.IsGenericDefinition);
        }
        
        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName + " constructed";
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return false;
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = new DependencyList();
            if (_type.RuntimeInterfaces.Length > 0)
            {
                dependencyList.Add(factory.InterfaceDispatchMap(_type), "Interface dispatch map");

                // If any of the implemented interfaces have variance, calls against compatible interface methods
                // could result in interface methods of this type being used (e.g. IEnumberable<object>.GetEnumerator()
                // can dispatch to an implementation of IEnumerable<string>.GetEnumerator()).
                // For now, we will not try to optimize this and we will pretend all interface methods are necessary.
                DefType defType = _type.GetClosestDefType();
                foreach (var implementedInterface in defType.RuntimeInterfaces)
                {
                    if (implementedInterface.HasVariance)
                    {
                        foreach (var interfaceMethod in implementedInterface.GetAllVirtualMethods())
                        {
                            MethodDesc implMethod = defType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
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

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                // Since the vtable is dependency driven, generate conditional static dependencies for
                // all possible vtable entries
                if (_type.GetClosestDefType().GetAllVirtualMethods().GetEnumerator().MoveNext())
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
            DefType defType = _type.GetClosestDefType();

            foreach (MethodDesc decl in defType.EnumAllVirtualSlots())
            {
                MethodDesc impl = defType.FindVirtualFunctionTargetMethodOnObjectType(decl);
                if (impl.OwningType == defType && !impl.IsAbstract)
                {
                    yield return new DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry(factory.MethodEntrypoint(impl, _type.IsValueType), factory.VirtualMethodUse(decl), "Virtual method");
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

                foreach (MethodDesc interfaceMethod in interfaceType.GetAllVirtualMethods())
                {
                    MethodDesc implMethod = defType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
                    if (implMethod != null)
                    {
                        yield return new CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.ReadyToRunHelper(ReadyToRunHelperId.InterfaceDispatch, interfaceMethod), "Interface method");
                        yield return new CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.ReadyToRunHelper(ReadyToRunHelperId.ResolveVirtualFunction, interfaceMethod), "Interface method address");
                    }
                }
            }
        }

        public override bool HasDynamicDependencies
        {
            get
            {
                // This node's EETypeOptionalFields node may change if this EEType implements interfaces
                // that are used since the dispatch map table index is computed once we know the interface
                // layout later on in compilation.
                return _type.RuntimeInterfaces.Length > 0;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory)
        {
            List<CombinedDependencyListEntry> dynamicNodes = new List<DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry>();
            dynamicNodes.Add(new CombinedDependencyListEntry(factory.EETypeOptionalFields(_optionalFieldsBuilder), null, "EEType optional fields"));
            return dynamicNodes;
        }

        protected override ISymbolNode GetBaseTypeNode(NodeFactory factory)
        {
            return _type.BaseType != null ? factory.ConstructedTypeSymbol(_type.BaseType) : null;
        }

        protected override int GCDescSize => GCDescEncoder.GetGCDescSize(_type);

        protected override void OutputGCDesc(ref ObjectDataBuilder builder)
        {
            GCDescEncoder.EncodeGCDesc(ref builder, _type);
        }

        protected override void OutputVirtualSlots(NodeFactory factory, ref ObjectDataBuilder objData, TypeDesc implType, TypeDesc declType)
        {
            declType = declType.GetClosestDefType();

            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(factory, ref objData, implType, baseType);

            IReadOnlyList<MethodDesc> virtualSlots = factory.VTable(declType).Slots;

            for (int i = 0; i < virtualSlots.Count; i++)
            {
                MethodDesc declMethod = virtualSlots[i];
                MethodDesc implMethod = implType.GetClosestDefType().FindVirtualFunctionTargetMethodOnObjectType(declMethod);

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

        protected override void OutputInterfaceMap(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            foreach (var itf in _type.RuntimeInterfaces)
            {
                objData.EmitPointerReloc(factory.NecessaryTypeSymbol(itf));
            }
        }

        protected override void OutputVirtualSlotAndInterfaceCount(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            int virtualSlotCount = 0;
            TypeDesc currentTypeSlice = _type.GetClosestDefType();

            while (currentTypeSlice != null)
            {
                virtualSlotCount += factory.VTable(currentTypeSlice).Slots.Count;
                currentTypeSlice = currentTypeSlice.BaseType;
            }

            objData.EmitShort(checked((short)virtualSlotCount));
            objData.EmitShort(checked((short)_type.RuntimeInterfaces.Length));
        }
    }
}