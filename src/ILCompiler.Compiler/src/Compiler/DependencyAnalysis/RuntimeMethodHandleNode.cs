// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    class RuntimeMethodHandleNode : ObjectNode, ISymbolNode
    {
        MethodDesc _targetMethod;
        NativeLayoutSignatureNode _nativeSignatureNode;

        public RuntimeMethodHandleNode(NodeFactory factory, MethodDesc targetMethod)
        {
            Debug.Assert(!targetMethod.IsSharedByGenericInstantiations);

            Vertex nativeSignature = factory.MetadataManager.NativeLayoutInfo.GetNativeLayoutInfoSignatureForLdToken(factory, targetMethod);

            _targetMethod = targetMethod;
            _nativeSignatureNode = factory.NativeLayoutInfoSignature(nativeSignature);
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__RuntimeMethodHandle_");
            sb.Append(NodeFactory.NameMangler.GetMangledMethodName(_targetMethod));
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

            objData.EmitPointerReloc(_nativeSignatureNode);

            return objData.ToObjectData();
        }
    }
}
