// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class GenericLookupSignature : Signature
    {
        private CORINFO_RUNTIME_LOOKUP_KIND _runtimeLookupKind;

        private readonly ReadyToRunFixupKind _fixupKind;

        private readonly TypeDesc _typeArgument;

        private readonly TypeDesc _contextType;

        private readonly SignatureContext _signatureContext;

        public GenericLookupSignature(
            CORINFO_RUNTIME_LOOKUP_KIND runtimeLookupKind, 
            ReadyToRunFixupKind fixupKind, 
            TypeDesc typeArgument, 
            TypeDesc contextType,
            SignatureContext signatureContext)
        {
            _runtimeLookupKind = runtimeLookupKind;
            _fixupKind = fixupKind;
            _typeArgument = typeArgument;
            _contextType = contextType;
            _signatureContext = signatureContext;
        }

        public override int ClassCode => 258608008;

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
                dataBuilder.EmitTypeSignature(_typeArgument, _signatureContext);
            }

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("GenericLookupSignature(");
            sb.Append(_runtimeLookupKind.ToString());
            sb.Append(" / ");
            sb.Append(_fixupKind.ToString());
            sb.Append(": ");
            RuntimeDeterminedTypeHelper.WriteTo(_typeArgument, sb);
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
            if (!NodeFactory.TypeCannotHaveEEType(_typeArgument))
            {
                dependencies.Add(factory.NecessaryTypeSymbol(_typeArgument), "Type referenced in a generic lookup signature");
            }
            return dependencies;
        }
    }
}
