// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    class RuntimeMethodHandleNode : ObjectNode, ISymbolNode
    {
        private MethodDesc _targetMethod;

        public RuntimeMethodHandleNode(MethodDesc targetMethod)
        {
            Debug.Assert(!targetMethod.IsSharedByGenericInstantiations);
            Debug.Assert(!targetMethod.IsRuntimeDeterminedExactMethod);
            _targetMethod = targetMethod;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix)
              .Append("__RuntimeMethodHandle_")
              .Append(nameMangler.GetMangledMethodName(_targetMethod));
        }
        public int Offset => 0;
        protected override string GetName() => this.GetMangledName();
        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            if (_targetMethod.HasInstantiation && _targetMethod.IsVirtual)
            {
                DependencyList dependencies = new DependencyList();
                dependencies.Add(new DependencyListEntry(factory.GVMDependencies(_targetMethod), "GVM dependencies for runtime method handle"));
                return dependencies;
            }
            return null;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);

            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            NativeLayoutMethodLdTokenVertexNode ldtokenSigNode = factory.NativeLayout.MethodLdTokenVertex(_targetMethod);
            objData.EmitPointerReloc(factory.NativeLayout.NativeLayoutSignature(ldtokenSigNode));

            return objData.ToObjectData();
        }
    }
}
