// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

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

        private readonly MethodWithToken _methodArgument;

        private readonly FieldDesc _fieldArgument;

        private readonly GenericContext _methodContext;

        private readonly SignatureContext _signatureContext;

        public GenericLookupSignature(
            CORINFO_RUNTIME_LOOKUP_KIND runtimeLookupKind, 
            ReadyToRunFixupKind fixupKind, 
            TypeDesc typeArgument, 
            MethodWithToken methodArgument,
            FieldDesc fieldArgument,
            GenericContext methodContext,
            SignatureContext signatureContext)
        {
            Debug.Assert(typeArgument != null || methodArgument != null || fieldArgument != null);
            _runtimeLookupKind = runtimeLookupKind;
            _fixupKind = fixupKind;
            _typeArgument = typeArgument;
            _methodArgument = methodArgument;
            _fieldArgument = fieldArgument;
            _methodContext = methodContext;
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
                        dataBuilder.EmitTypeSignature(_methodContext.ContextType, _signatureContext);
                        break;

                    default:
                        throw new NotImplementedException();
                }

                dataBuilder.EmitByte((byte)_fixupKind);
                if (_methodArgument != null)
                {
                    dataBuilder.EmitMethodSignature(
                        method: _methodArgument.Method,
                        constrainedType: _typeArgument,
                        methodToken: _methodArgument.Token,
                        enforceDefEncoding: false,
                        context: _signatureContext,
                        isUnboxingStub: false,
                        isInstantiatingStub: true);
                }
                else if (_typeArgument != null)
                {
                    dataBuilder.EmitTypeSignature(_typeArgument, _signatureContext);
                }
                else if (_fieldArgument != null)
                {
                    dataBuilder.EmitFieldSignature(_fieldArgument, _signatureContext);
                }
                else
                {
                    throw new NotImplementedException();
                }
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
            if (_methodArgument != null)
            {
                RuntimeDeterminedTypeHelper.WriteTo(_methodArgument.Method, sb);
                if (!_methodArgument.Token.IsNull)
                {
                    sb.Append(" [");
                    sb.Append(_methodArgument.Token.MetadataReader.GetString(_methodArgument.Token.MetadataReader.GetAssemblyDefinition().Name));
                    sb.Append(":"); ;
                    sb.Append(((uint)_methodArgument.Token.Token).ToString("X8"));
                    sb.Append("]");
                }
            }
            if (_typeArgument != null)
            {
                RuntimeDeterminedTypeHelper.WriteTo(_typeArgument, sb);
            }
            if (_fieldArgument != null)
            {
                RuntimeDeterminedTypeHelper.WriteTo(_fieldArgument, sb);
            }
            sb.Append(" (");
            _methodContext.AppendMangledName(nameMangler, sb);
            sb.Append(")");
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            throw new NotImplementedException();
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            if (_typeArgument != null && !_typeArgument.IsRuntimeDeterminedSubtype)
            {
                dependencies.Add(factory.NecessaryTypeSymbol(_typeArgument), "Type referenced in a generic lookup signature");
            }
            return dependencies;
        }
    }
}
