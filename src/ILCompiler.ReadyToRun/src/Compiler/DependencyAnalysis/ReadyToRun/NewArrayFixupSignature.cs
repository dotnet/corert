// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class NewArrayFixupSignature : Signature
    {
        private readonly ReadyToRunCodegenNodeFactory _factory;
        private readonly ArrayType _arrayType;
        private readonly ModuleToken _typeToken;

        public NewArrayFixupSignature(ReadyToRunCodegenNodeFactory factory, ArrayType arrayType, ModuleToken typeToken)
        {
            _factory = factory;
            _arrayType = arrayType;
            _typeToken = typeToken;
        }

        protected override int ClassCode => 815543321;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();
            dataBuilder.AddSymbol(this);

            dataBuilder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_NewArray);
            dataBuilder.EmitTypeSignature(_arrayType, _typeToken, _typeToken.SignatureContext(_factory));

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"NewArraySignature: {_arrayType.ToString()}; token: {_typeToken})");
        }

        protected override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return _typeToken.CompareTo(((NewArrayFixupSignature)other)._typeToken);
        }
    }
}
