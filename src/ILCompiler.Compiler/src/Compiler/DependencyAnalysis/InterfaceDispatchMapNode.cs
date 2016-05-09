// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILCompiler.DependencyAnalysis
{
    public class InterfaceDispatchMapNode : ObjectNode, ISymbolNode
    {
        const int IndexNotSet = int.MaxValue;

        int _dispatchMapTableIndex;
        TypeDesc _type;

        public InterfaceDispatchMapNode(TypeDesc type)
        {
            _type = type;
            _dispatchMapTableIndex = IndexNotSet;
        }
        
        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
        
        string ISymbolNode.MangledName
        {
            get
            {
                if (_dispatchMapTableIndex == IndexNotSet)
                {
                    throw new InvalidOperationException("MangledName called before InterfaceDispatchMap index was initialized.");
                }
                    
                return NodeFactory.NameMangler.CompilationUnitPrefix + "__InterfaceDispatchMap_" + _dispatchMapTableIndex;
            }
        }
        
        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
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

        public void SetDispatchMapIndex(NodeFactory factory, int index)
        {
            _dispatchMapTableIndex = index;
            ((EETypeNode)factory.ConstructedTypeSymbol(_type)).SetDispatchMapIndex(_dispatchMapTableIndex);
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            var result = new DependencyList();
            result.Add(factory.InterfaceDispatchMapIndirection(_type), "Interface dispatch map indirection node");
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
                    var implMethod = _type.GetClosestMetadataType().ResolveInterfaceMethodToVirtualMethodOnType(declMethod);

                    // Interface methods first implemented by a base type in the hierarchy will return null for the implMethod (runtime interface
                    // dispatch will walk the inheritance chain).
                    if (implMethod != null)
                    {
                        builder.EmitShort(checked((short)interfaceIndex));
                        builder.EmitShort(checked((short)interfaceMethodSlot));
                        builder.EmitShort(checked((short)VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, implMethod)));
                        entryCount++;
                    }
                }
            }

            builder.EmitInt(entryCountReservation, entryCount);
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.Alignment = 16;
            objData.DefinedSymbols.Add(this);

            if (!relocsOnly)
            {
                EmitDispatchMap(ref objData, factory);
            }

            return objData.ToObjectData();
        }
    }
}
