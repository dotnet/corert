// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class FieldFixupSignature : Signature
    {
        private readonly ReadyToRunFixupKind _fixupKind;

        private readonly FieldDesc _fieldDesc;

        private readonly ModuleTokenResolver _resolver;

        private readonly ModuleToken _fieldToken;

        public FieldFixupSignature(ModuleTokenResolver resolver, ReadyToRunFixupKind fixupKind, FieldDesc fieldDesc, ModuleToken fieldToken)
        {
            _resolver = resolver;
            _fixupKind = fixupKind;
            _fieldDesc = fieldDesc;
            _fieldToken = fieldToken;
        }

        public override int ClassCode => 271828182;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();

            if (!relocsOnly)
            {
                dataBuilder.AddSymbol(this);

                dataBuilder.EmitByte((byte)_fixupKind);
                dataBuilder.EmitFieldSignature(_fieldDesc, _fieldToken, _fieldToken.SignatureContext(_resolver));
            }

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"TypeFixupSignature({_fixupKind.ToString()}): {_fieldDesc.ToString()}; token: {_fieldToken})");
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _fieldToken.CompareTo(((FieldFixupSignature)other)._fieldToken);
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            dependencies.Add(factory.NecessaryTypeSymbol(_fieldDesc.OwningType), "Type referenced in a fixup signature");
            return dependencies;
        }
    }
}
