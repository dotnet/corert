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
        private readonly ReadyToRunFixupKind _fixupKind;

        private readonly MethodDesc _methodDesc;

        private readonly TypeDesc _constrainedType;

        private readonly ModuleToken _methodToken;

        private readonly SignatureContext _signatureContext;

        private readonly bool _isUnboxingStub;

        private readonly bool _isInstantiatingStub;

        public MethodFixupSignature(
            ReadyToRunFixupKind fixupKind, 
            MethodDesc methodDesc, 
            TypeDesc constrainedType,
            ModuleToken methodToken,
            SignatureContext signatureContext,
            bool isUnboxingStub,
            bool isInstantiatingStub)
        {
            _fixupKind = fixupKind;
            _methodDesc = methodDesc;
            _methodToken = methodToken;
            _constrainedType = constrainedType;
            _signatureContext = signatureContext;
            _isUnboxingStub = isUnboxingStub;
            _isInstantiatingStub = isInstantiatingStub;
        }

        public MethodDesc Method => _methodDesc;

        public override int ClassCode => 150063499;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            if (relocsOnly)
            {
                // Method fixup signature doesn't contain any direct relocs
                return new ObjectData(data: Array.Empty<byte>(), relocs: null, alignment: 0, definedSymbols: null);
            }

            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();
            dataBuilder.AddSymbol(this);

            dataBuilder.EmitByte((byte)_fixupKind);
            dataBuilder.EmitMethodSignature(_methodDesc, _constrainedType, _methodToken, enforceDefEncoding: false,
                _signatureContext, _isUnboxingStub, _isInstantiatingStub);

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"MethodFixupSignature(");
            sb.Append(_fixupKind.ToString());
            if (_isUnboxingStub)
            {
                sb.Append(" [UNBOX]");
            }
            if (_isInstantiatingStub)
            {
                sb.Append(" [INST]");
            }
            sb.Append(": ");
            sb.Append(nameMangler.GetMangledMethodName(_methodDesc));
            if (_constrainedType != null)
            {
                sb.Append(" @ ");
                sb.Append(nameMangler.GetMangledTypeName(_constrainedType));
            }
            if (!_methodToken.IsNull)
            {
                sb.Append(" [");
                sb.Append(_methodToken.MetadataReader.GetString(_methodToken.MetadataReader.GetAssemblyDefinition().Name));
                sb.Append(":");
                sb.Append(((uint)_methodToken.Token).ToString("X8"));
                sb.Append("]");
            }
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            throw new NotImplementedException();
        }
    }
}
