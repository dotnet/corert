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
        MethodDesc _targetMethod;

        public RuntimeMethodHandleNode(NodeFactory factory, MethodDesc targetMethod)
        {
            Debug.Assert(!targetMethod.IsSharedByGenericInstantiations);
            _targetMethod = targetMethod;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix)
              .Append("__RuntimeMethodHandle_")
              .Append(NodeFactory.NameMangler.GetMangledMethodName(_targetMethod));
        }
        public int Offset => 0;
        protected override string GetName() => this.GetMangledName();
        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);

            objData.Alignment = objData.TargetPointerSize;
            objData.DefinedSymbols.Add(this);

            NativeLayoutMethodLdTokenVertexNode ldtokenSigNode = factory.NativeLayout.MethodLdTokenVertex(_targetMethod);
            objData.EmitPointerReloc(factory.NativeLayout.NativeLayoutSignature(ldtokenSigNode));

            return objData.ToObjectData();
        }
    }
}
