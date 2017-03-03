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
            DependencyList dependencies = new DependencyList();

            if (_targetMethod.IsAbstract && !_targetMethod.HasInstantiation)
            {
                dependencies.Add(new DependencyListEntry(factory.VirtualMethodUse(_targetMethod), "NONE"));
            }
            else if (!_targetMethod.IsAbstract)
            {
                if (_targetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific) != _targetMethod)
                {
                    dependencies.Add(new DependencyListEntry(factory.ShadowConcreteMethod(_targetMethod), "NONE"));
                }
                else
                {
                    dependencies.Add(new DependencyListEntry(factory.MethodEntrypoint(_targetMethod), "NONE"));
                }
            }

            if (_targetMethod.HasInstantiation && _targetMethod.IsVirtual)
            {
                dependencies.Add(new DependencyListEntry(factory.GVMDependencies(_targetMethod), "GVM dependencies for runtime method handle"));
                
            }

            return dependencies;
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
