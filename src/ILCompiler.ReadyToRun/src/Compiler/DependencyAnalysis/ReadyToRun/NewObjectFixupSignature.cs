// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class NewObjectFixupSignature : Signature
    {
        private readonly ReadyToRunCodegenNodeFactory _factory;
        private readonly TypeDesc _typeDesc;
        private readonly ModuleToken _typeToken;

        public NewObjectFixupSignature(ReadyToRunCodegenNodeFactory factory, TypeDesc typeDesc, ModuleToken typeToken)
        {
            _factory = factory;
            _typeDesc = typeDesc;
            _typeToken = typeToken;
        }

        public override int ClassCode => 551247760;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();

            if (!relocsOnly)
            {
                dataBuilder.AddSymbol(this);

                dataBuilder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_NewObject);
                dataBuilder.EmitTypeSignature(_typeDesc, _typeToken, _typeToken.SignatureContext(_factory.Resolver));
            }

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append($@"NewObjectSignature: {_typeDesc.ToString()}; token: {_typeToken})");
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _typeToken.CompareTo(((NewObjectFixupSignature)other)._typeToken);
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            dependencies.Add(factory.ConstructedTypeSymbol(_typeDesc), "Type constructed through new object fixup");
            return dependencies;
        }
    }
}
