// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    class NamedJumpStubNode : JumpStubNode
    {
        Utf8String _name;

        public NamedJumpStubNode(string name, ISymbolNode target) : base(target)
        {
            _name = new Utf8String(name);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(_name);
        }
    }
}
