// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class DelegateCtorSignature : Signature
    {
        private readonly ReadyToRunCodegenNodeFactory _factory;

        private readonly TypeDesc _delegateType;

        private readonly ModuleToken _delegateTypeToken;

        private readonly IMethodNode _targetMethod;

        private readonly ModuleToken _targetMethodToken;

        public DelegateCtorSignature(
            ReadyToRunCodegenNodeFactory factory,
            TypeDesc delegateType, 
            ModuleToken delegateTypeToken,
            IMethodNode targetMethod,
            ModuleToken targetMethodToken)
        {
            _factory = factory;
            _delegateType = delegateType;
            _delegateTypeToken = delegateTypeToken;
            _targetMethod = targetMethod;
            _targetMethodToken = targetMethodToken;
        }

        protected override int ClassCode => 99885741;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataSignatureBuilder builder = new ObjectDataSignatureBuilder();
            builder.AddSymbol(this);

            if (relocsOnly)
            {
                builder.EmitReloc(_targetMethod, RelocType.IMAGE_REL_BASED_REL32);
            }
            else
            {
                builder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_DelegateCtor);
                builder.EmitMethodSignature(_targetMethod.Method, _targetMethodToken, _targetMethodToken.SignatureContext(_factory));
                builder.EmitTypeSignature(_delegateType, _delegateTypeToken, _delegateTypeToken.SignatureContext(_factory));
            }

            return builder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"DelegateCtor({_delegateType} -> {_targetMethod.Method})");
        }

        protected override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            DelegateCtorSignature otherSignature = (DelegateCtorSignature)other;
            int result = _delegateTypeToken.CompareTo(otherSignature._delegateTypeToken);
            if (result == 0)
            {
                result = _targetMethodToken.CompareTo(otherSignature._targetMethodToken);
            }
            return result;
        }
    }
}
