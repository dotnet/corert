// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    class InterfaceDispatchCellNode : ObjectNode, ISymbolNode
    {
        MethodDesc _targetMethod;
        string _callSiteIdentifier;

        public InterfaceDispatchCellNode(MethodDesc targetMethod, string callSiteIdentifier)
        {
            Debug.Assert(targetMethod.OwningType.IsInterface);
            Debug.Assert(!targetMethod.IsSharedByGenericInstantiations);
            _targetMethod = targetMethod;
            _callSiteIdentifier = callSiteIdentifier;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetMangledName(_targetMethod, _callSiteIdentifier));
        }
        public int Offset => 0;

        public override bool IsShareable => false;

        public static string GetMangledName(MethodDesc method, string callSiteIdentifier)
        {
            string name = NodeFactory.NameMangler.CompilationUnitPrefix + "__InterfaceDispatchCell_" + NodeFactory.NameMangler.GetMangledMethodName(method);

            if (!string.IsNullOrEmpty(callSiteIdentifier))
            {
                name += "_" + callSiteIdentifier;
            }

            return name;
        }

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

            if (factory.Target.Architecture == TargetArchitecture.ARM)
            {
                objData.EmitPointerReloc(factory.InitialInterfaceDispatchStub);
            }
            else
            {
                objData.EmitPointerReloc(factory.ExternSymbol("RhpInitialDynamicInterfaceDispatch"));
            }

            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                // TODO: Enable Indirect Pointer for Interface Dispatch Cell. See https://github.com/dotnet/corert/issues/2542
                objData.EmitPointerReloc(factory.NecessaryTypeSymbol(_targetMethod.OwningType),
                    (int)InterfaceDispatchCellCachePointerFlags.CachePointerIsInterfacePointerOrMetadataToken);
            }
            else
            {
                if (factory.CompilationModuleGroup.ContainsType(_targetMethod.OwningType))
                {
                    objData.EmitReloc(factory.NecessaryTypeSymbol(_targetMethod.OwningType), RelocType.IMAGE_REL_BASED_RELPTR32,
                        (int)InterfaceDispatchCellCachePointerFlags.CachePointerIsInterfaceRelativePointer);
                }
                else
                {
                    objData.EmitReloc(factory.NecessaryTypeSymbol(_targetMethod.OwningType), RelocType.IMAGE_REL_BASED_RELPTR32,
                        (int)InterfaceDispatchCellCachePointerFlags.CachePointerIsIndirectedInterfaceRelativePointer);
                }

                if (objData.TargetPointerSize == 8)
                {
                    // IMAGE_REL_BASED_RELPTR is a 32-bit relocation. However, the cell needs a full pointer 
                    // width there since a pointer to the cache will be written into the cell. Emit additional
                    // 32 bits on targets whose pointer size is 64 bit. 
                    objData.EmitInt(0);
                }
            }

            // End the run of dispatch cells
            objData.EmitZeroPointer();

            // Avoid consulting VTable slots until they're guaranteed complete during final data emission
            if (!relocsOnly)
            {
                objData.EmitNaturalInt(VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, _targetMethod));
            }

            return objData.ToObjectData();
        }
    }
}
