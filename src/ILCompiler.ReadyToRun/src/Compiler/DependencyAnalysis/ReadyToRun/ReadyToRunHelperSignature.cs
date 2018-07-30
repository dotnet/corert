// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ReadyToRunHelperSignature : Signature
    {
        private readonly ReadyToRunHelper _helperID;

        public ReadyToRunHelperSignature(ReadyToRunHelper helper)
        {
            _helperID = helper;
        }

        protected override int ClassCode => 208107954;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            return new ObjectData(
                new[] { (byte) ReadyToRunFixupKind.READYTORUN_FIXUP_Helper, (byte) _helperID },
                Array.Empty<Relocation>(),
                1,
                new ISymbolDefinitionNode[] { this });
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("ReadyToRunHelper_");
            sb.Append(_helperID.ToString());
        }

        protected override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return _helperID.CompareTo(((ReadyToRunHelperSignature) other)._helperID);
        }
    }
}
