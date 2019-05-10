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
        private readonly TypeDesc _delegateType;

        private readonly IMethodNode _targetMethod;

        private readonly ModuleToken _methodToken;

        private readonly SignatureContext _signatureContext;

        public DelegateCtorSignature(
            TypeDesc delegateType,
            IMethodNode targetMethod,
            ModuleToken methodToken,
            SignatureContext signatureContext)
        {
            _delegateType = delegateType;
            _targetMethod = targetMethod;
            _methodToken = methodToken;
            _signatureContext = signatureContext;
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
                    constrainedType: null,
                    methodToken: _methodToken,
                    enforceDefEncoding: false,
                    _signatureContext,
                    isUnboxingStub: false,
                    isInstantiatingStub: false);
                builder.EmitTypeSignature(_delegateType, _signatureContext);
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
            throw new NotImplementedException();
        }
    }
}
