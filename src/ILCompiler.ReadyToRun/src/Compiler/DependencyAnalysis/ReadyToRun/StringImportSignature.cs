// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.JitInterface;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class StringImportSignature : Signature
    {
        private readonly mdToken _token;

        public StringImportSignature(mdToken token)
        {
            _token = token;
        }

        protected override int ClassCode => 324832559;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ObjectDataSignatureBuilder dataBuilder = new ObjectDataSignatureBuilder();
            dataBuilder.AddSymbol(this);

            dataBuilder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_StringHandle);
            dataBuilder.EmitUInt(SignatureBuilder.RidFromToken(_token));

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("StringImportSignature: " + ((uint)_token).ToString("X8"));
        }

        protected override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return _token.CompareTo(((StringImportSignature)other)._token);
        }
    }
}
