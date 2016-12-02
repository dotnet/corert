// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    class InterfaceDispatchCellNode : ObjectNode, ISymbolNode
    {
        MethodDesc _targetMethod;

        public InterfaceDispatchCellNode(MethodDesc targetMethod)
        {
            Debug.Assert(targetMethod.OwningType.IsInterface);
            Debug.Assert(!targetMethod.IsSharedByGenericInstantiations);
            _targetMethod = targetMethod;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__InterfaceDispatchCell_");
            sb.Append(NodeFactory.NameMangler.GetMangledMethodName(_targetMethod));
        }
        public int Offset => 0;
        public override bool IsShareable => true;

        protected override string GetName() => this.GetMangledName();

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

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
