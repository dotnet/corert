// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

using Internal.Text;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class DelegateCtorSignature : Signature
    {
        private readonly ModuleTokenResolver _resolver;

        private readonly TypeDesc _delegateType;

        private readonly ModuleToken _delegateTypeToken;

        private readonly IMethodNode _targetMethod;

        private readonly ModuleToken _targetMethodToken;

        public DelegateCtorSignature(
            ModuleTokenResolver resolver,
            TypeDesc delegateType, 
            ModuleToken delegateTypeToken,
            IMethodNode targetMethod,
            ModuleToken targetMethodToken)
        {
            _resolver = resolver;
            _delegateType = delegateType;
            _delegateTypeToken = delegateTypeToken;
            _targetMethod = targetMethod;
            _targetMethodToken = targetMethodToken;
        }

        public override int ClassCode => 99885741;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataSignatureBuilder builder = new ObjectDataSignatureBuilder();
            builder.AddSymbol(this);

            if (!relocsOnly)
            {
                builder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_DelegateCtor);
                builder.EmitMethodSignature(
                    _targetMethod.Method, 
                    _targetMethodToken, 
                    constrainedType: null, 
                    isUnboxingStub: false, 
                    _targetMethodToken.SignatureContext(_resolver));
                builder.EmitTypeSignature(_delegateType, _delegateTypeToken, _delegateTypeToken.SignatureContext(_resolver));
            }

            return builder.ToObjectData();
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            return new DependencyList(
                new DependencyListEntry[]
                {
                    new DependencyListEntry(_targetMethod, "Delegate target method")
                }
            );
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"DelegateCtor({_delegateType} -> {_targetMethod.Method})");
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
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
