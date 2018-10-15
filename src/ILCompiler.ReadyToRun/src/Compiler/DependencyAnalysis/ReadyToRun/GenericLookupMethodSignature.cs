// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class GenericLookupMethodSignature : Signature
    {
        private CORINFO_RUNTIME_LOOKUP_KIND _runtimeLookupKind;

        private readonly ReadyToRunFixupKind _fixupKind;

        private readonly MethodDesc _methodArgument;

        private readonly TypeDesc _contextType;

        private readonly SignatureContext _signatureContext;

        public GenericLookupMethodSignature(
            CORINFO_RUNTIME_LOOKUP_KIND runtimeLookupKind,
            ReadyToRunFixupKind fixupKind,
            MethodDesc methodArgument,
            TypeDesc contextType,
            SignatureContext signatureContext)
        {
            _runtimeLookupKind = runtimeLookupKind;
            _fixupKind = fixupKind;
            _methodArgument = methodArgument;
            _contextType = contextType;
            _signatureContext = signatureContext;
        }

        public override int ClassCode => 258609009;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();

            if (!relocsOnly)
            {
                dataBuilder.AddSymbol(this);

                switch (_runtimeLookupKind)
                {
                    case CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM:
                        dataBuilder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_TypeDictionaryLookup);
                        break;

                    case CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM:
                        dataBuilder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_MethodDictionaryLookup);
                        break;

                    case CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ:
                        dataBuilder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_ThisObjDictionaryLookup);
                        dataBuilder.EmitTypeSignature(_contextType, _signatureContext);
                        break;

                    default:
                        throw new NotImplementedException();
                }

                dataBuilder.EmitByte((byte)_fixupKind);
                dataBuilder.EmitMethodSignature(
                    _methodArgument, 
                    enforceDefEncoding: false,
                    constrainedType: null, 
                    isUnboxingStub: false, 
                    isInstantiatingStub: false, 
                    _signatureContext);
            }

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("GenericLookupMethodSignature(");
            sb.Append(_runtimeLookupKind.ToString());
            sb.Append(" / ");
            sb.Append(_fixupKind.ToString());
            sb.Append(": ");
            RuntimeDeterminedTypeHelper.WriteTo(_methodArgument, sb);
            if (_contextType != null)
            {
                sb.Append(" (");
                sb.Append(_contextType.ToString());
                sb.Append(")");
            }
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            throw new NotImplementedException();
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            // dependencies.Add(factory.NecessaryTypeSymbol(_methodArgument), "Method referenced in a generic lookup signature");
            return dependencies;
        }
    }
}
