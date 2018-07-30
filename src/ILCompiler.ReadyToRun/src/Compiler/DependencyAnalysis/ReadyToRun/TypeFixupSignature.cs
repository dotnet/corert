﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class TypeFixupSignature : Signature
    {
        private readonly ReadyToRunFixupKind _fixupKind;

        private readonly TypeDesc _typeDesc;

        private readonly ReadyToRunCodegenNodeFactory _factory;

        private readonly ModuleToken _typeToken;

        public TypeFixupSignature(ReadyToRunCodegenNodeFactory factory, ReadyToRunFixupKind fixupKind, TypeDesc typeDesc, ModuleToken typeToken)
        {
            _factory = factory;
            _fixupKind = fixupKind;
            _typeDesc = typeDesc;
            _typeToken = typeToken;
        }

        protected override int ClassCode => 255607008;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();
            dataBuilder.AddSymbol(this);

            dataBuilder.EmitByte((byte)_fixupKind);
            dataBuilder.EmitTypeSignature(_typeDesc, _typeToken, _typeToken.SignatureContext(_factory));

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"TypeFixupSignature({_fixupKind.ToString()}): {_typeDesc.ToString()}; token: {_typeToken})");
        }

        protected override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return _typeToken.CompareTo(((TypeFixupSignature)other)._typeToken);
        }
    }
}
