// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public override int ClassCode => 815543321;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();
            dataBuilder.AddSymbol(this);

            dataBuilder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_NewArray);
            dataBuilder.EmitTypeSignature(_arrayType, _typeToken, _typeToken.SignatureContext(_factory.Resolver));

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"NewArraySignature: {_arrayType.ToString()}; token: {_typeToken})");
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _typeToken.CompareTo(((NewArrayFixupSignature)other)._typeToken);
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            dependencies.Add(factory.NecessaryTypeSymbol(_arrayType.ElementType), "Type used as array element");
            return dependencies;
        }
    }
}
