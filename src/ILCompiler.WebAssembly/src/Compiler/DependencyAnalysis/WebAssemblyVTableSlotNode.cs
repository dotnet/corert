﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class WebAssemblyVTableSlotNode : ObjectNode, ISymbolDefinitionNode
    {
        private readonly MethodDesc _targetMethod;

        public WebAssemblyVTableSlotNode(MethodDesc targetMethod)
        {
            Debug.Assert(targetMethod.IsVirtual);
            Debug.Assert(!targetMethod.IsSharedByGenericInstantiations);
            _targetMethod = targetMethod;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetMangledName(nameMangler, _targetMethod));
        }
        public int Offset => 0;

        public override bool IsShareable => false;

        public static string GetMangledName(NameMangler nameMangler, MethodDesc method)
        {
            return "__getslot__" + nameMangler.GetMangledMethodName(method);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList result = new DependencyList();

            if (!factory.VTable(_targetMethod.OwningType).HasFixedSlots)
            {
                result.Add(factory.VirtualMethodUse(_targetMethod), "VTable method use");
            }

            return result;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            Debug.Assert((EETypeNode.GetVTableOffset(factory.Target.PointerSize) % factory.Target.PointerSize) == 0, "vtable offset must be aligned");
            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);

            objData.AddSymbol(this);

            if (!relocsOnly)
            {
                var tableOffset = EETypeNode.GetVTableOffset(factory.Target.PointerSize) / factory.Target.PointerSize;
                objData.EmitInt(tableOffset + VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, _targetMethod));
            }
            return objData.ToObjectData();
        }

        protected override int ClassCode => 0;

        protected override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_targetMethod, ((WebAssemblyVTableSlotNode)other)._targetMethod);
        }
    }
}
