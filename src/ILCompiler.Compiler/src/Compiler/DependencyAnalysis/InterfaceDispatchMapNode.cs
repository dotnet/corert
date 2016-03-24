// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    internal struct DispatchMapEntry
    {
        public short InterfaceIndex;
        public short InterfaceMethodSlot;
        public short ImplementationMethodSlot;
    }

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

        DispatchMapEntry[] BuildDispatchMap(NodeFactory factory)
        {
            ArrayBuilder<DispatchMapEntry> dispatchMapEntries = new ArrayBuilder<DispatchMapEntry>();
            
            for (int i = 0; i < _type.RuntimeInterfaces.Length; i++)
            {
                var interfaceType = _type.RuntimeInterfaces[i];
                Debug.Assert(interfaceType.IsInterface);

                IReadOnlyList<MethodDesc> virtualSlots = factory.VTable(interfaceType).Slots;
                
                for (int j = 0; j < virtualSlots.Count; j++)
                {
                    MethodDesc declMethod = virtualSlots[j];
                    var implMethod = VirtualFunctionResolution.ResolveInterfaceMethodToVirtualMethodOnType(declMethod, _type.GetClosestMetadataType());

                    // Interface methods first implemented by a base type in the hierarchy will return null for the implMethod (runtime interface
                    // dispatch will walk the inheritance chain).
                    if (implMethod != null)
                    {
                        var entry = new DispatchMapEntry();
                        entry.InterfaceIndex = checked((short)i);
                        entry.InterfaceMethodSlot = checked((short)j);
                        entry.ImplementationMethodSlot = checked((short)VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, implMethod));
                        dispatchMapEntries.Add(entry);
                    }
                }
            }

            return dispatchMapEntries.ToArray();
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.Alignment = 16;
            objData.DefinedSymbols.Add(this);

            if (!relocsOnly)
            {
                var entries = BuildDispatchMap(factory);
                objData.EmitInt(entries.Length);
                foreach (var entry in entries)
                {
                    objData.EmitShort(entry.InterfaceIndex);
                    objData.EmitShort(entry.InterfaceMethodSlot);
                    objData.EmitShort(entry.ImplementationMethodSlot);
                }
            }

            return objData.ToObjectData();
        }
    }
}
