// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ReadyToRunHelperSignature : Signature
    {
        private ReadyToRunHelper _helper;

        public ReadyToRunHelperSignature(ReadyToRunHelper helper)
        {
            _helper = helper;
        }

        protected override int ClassCode => 208107954;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder dataBuilder = new ObjectDataBuilder();
            dataBuilder.AddSymbol(this);

            dataBuilder.EmitByte((byte)ReadyToRunFixupKind.READYTORUN_FIXUP_Helper);
            dataBuilder.EmitByte((byte)_helper);

            return dataBuilder.ToObjectData();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("ReadyToRunHelper_");
            sb.Append(_helper.ToString());
        }

        protected override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return _helper.CompareTo(((ReadyToRunHelperSignature)other)._helper);
        }
    }
}
