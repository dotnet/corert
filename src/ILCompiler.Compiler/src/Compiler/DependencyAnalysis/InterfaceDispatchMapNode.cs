// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class InterfaceDispatchMapNode : ObjectNode, ISymbolDefinitionNode, ISortableSymbolNode
    {
        TypeDesc _type;

        public InterfaceDispatchMapNode(TypeDesc type)
        {
            _type = type;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__InterfaceDispatchMap_").Append(nameMangler.SanitizeName(nameMangler.GetMangledTypeName(_type)));
        }

        public int Offset => 0;
        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section
        {
            get
            {
                if (_type.Context.Target.IsWindows)
                    return ObjectNodeSection.FoldableReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }
        
        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            var result = new DependencyList();
            result.Add(factory.InterfaceDispatchMapIndirection(_type), "Interface dispatch map indirection node");

            // VTable slots of implemented interfaces are consulted during emission
            foreach (TypeDesc runtimeInterface in _type.RuntimeInterfaces)
            {
                result.Add(factory.VTable(runtimeInterface), "Interface for a dispatch map");
            }

            return result;
        }

        void EmitDispatchMap(ref ObjectDataBuilder builder, NodeFactory factory)
        {
            var entryCountReservation = builder.ReserveInt();
            int entryCount = 0;
            
            for (int interfaceIndex = 0; interfaceIndex < _type.RuntimeInterfaces.Length; interfaceIndex++)
            {
                var interfaceType = _type.RuntimeInterfaces[interfaceIndex];
                Debug.Assert(interfaceType.IsInterface);

                IReadOnlyList<MethodDesc> virtualSlots = factory.VTable(interfaceType).Slots;
                
                for (int interfaceMethodSlot = 0; interfaceMethodSlot < virtualSlots.Count; interfaceMethodSlot++)
                {
                    MethodDesc declMethod = virtualSlots[interfaceMethodSlot];
                    var implMethod = _type.GetClosestDefType().ResolveInterfaceMethodToVirtualMethodOnType(declMethod);

                    // Interface methods first implemented by a base type in the hierarchy will return null for the implMethod (runtime interface
                    // dispatch will walk the inheritance chain).
                    if (implMethod != null)
                    {
                        builder.EmitShort(checked((short)interfaceIndex));
                        builder.EmitShort(checked((short)(interfaceMethodSlot + (interfaceType.HasGenericDictionarySlot() ? 1 : 0))));
                        builder.EmitShort(checked((short)VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, implMethod)));
                        entryCount++;
                    }
                }
            }

            builder.EmitInt(entryCountReservation, entryCount);
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);
            objData.RequireInitialAlignment(16);
            objData.AddSymbol(this);

            if (!relocsOnly)
            {
                EmitDispatchMap(ref objData, factory);
            }

            return objData.ToObjectData();
        }

        protected internal override int ClassCode => 848664602;

        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_type, ((InterfaceDispatchMapNode)other)._type);
        }

        int ISortableSymbolNode.ClassCode => ClassCode;

        int ISortableSymbolNode.CompareToImpl(ISortableSymbolNode other, CompilerComparer comparer)
        {
            return CompareToImpl((ObjectNode)other, comparer);
        }
    }
}
