// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using System;
using System.Diagnostics;

namespace ILCompiler.DependencyAnalysis
{
    class InterfaceDispatchCellNode : ObjectNode, ISymbolNode
    {
        MethodDesc _targetMethod;

        public InterfaceDispatchCellNode(MethodDesc targetMethod)
        {
            Debug.Assert(targetMethod.OwningType.IsInterface);
            _targetMethod = targetMethod;
        }

        public string MangledName
        {
            get
            {
                return "__InterfaceDispatchCell_" + NodeFactory.NameMangler.GetMangledMethodName(_targetMethod);
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public int Offset
        {
            get
            {
                return 0;
            }
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.DataSection;
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            // The interface dispatch cell has an alignment requirement of 2 * [Pointer size] as part of the 
            // synchronization mechanism of the two values in the runtime.
            objData.Alignment = _targetMethod.Context.Target.PointerSize * 2;
            objData.DefinedSymbols.Add(this);
            
            objData.EmitPointerReloc(factory.ExternSymbol("RhpInitialDynamicInterfaceDispatch"));
            
            // The second cell field uses the two lower-order bits to communicate the contents.
            // We add 1 to signal IDC_CachePointerIsInterfacePointer. See src\Native\Runtime\inc\rhbinder.h.
            objData.EmitPointerReloc(factory.NecessaryTypeSymbol(_targetMethod.OwningType), 1);
            
            // End the run of dispatch cells
            objData.EmitZeroPointer();

            // Avoid consulting VTable slots until they're guaranteed complete during final data emission
            if (!relocsOnly)
            {
                int interfaceMethodSlot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, _targetMethod);
                if (factory.Target.PointerSize == 8)
                {
                    objData.EmitLong(interfaceMethodSlot);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            return objData.ToObjectData();
        }
    }
}
