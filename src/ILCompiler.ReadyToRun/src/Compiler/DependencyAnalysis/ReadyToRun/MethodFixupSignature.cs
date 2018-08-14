// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class MethodFixupSignature : Signature
    {
        private readonly ModuleTokenResolver _resolver;

        private readonly ReadyToRunFixupKind _fixupKind;

        private readonly MethodDesc _methodDesc;

        private readonly ModuleToken _methodToken;

        private readonly TypeDesc _constrainedType;

        private readonly bool _isUnboxingStub;

        public MethodFixupSignature(
            ModuleTokenResolver resolver, 
            ReadyToRunFixupKind fixupKind, 
            MethodDesc methodDesc, 
            ModuleToken methodToken,
            TypeDesc constrainedType,
            bool isUnboxingStub)
        {
            _resolver = resolver;
            _fixupKind = fixupKind;
            _methodDesc = methodDesc;
            _methodToken = methodToken;
            _constrainedType = constrainedType;
            _isUnboxingStub = isUnboxingStub;
        }

        public override int ClassCode => 150063499;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();
            dataBuilder.AddSymbol(this);

            dataBuilder.EmitByte((byte)_fixupKind);
            dataBuilder.EmitMethodSignature(_methodDesc, _methodToken, _constrainedType, _isUnboxingStub, _methodToken.SignatureContext(_resolver));

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"MethodFixupSignature({_fixupKind.ToString()} {_methodToken}): {_methodDesc.ToString()}");
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _methodToken.CompareTo(((MethodFixupSignature)other)._methodToken);
        }
    }
}
