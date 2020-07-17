// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class WindowsDebugPseudoAssemblySection : ObjectNode, ISymbolDefinitionNode
    {
        private ManagedBinaryEmitter _pseudoAssembly;

        public WindowsDebugPseudoAssemblySection(TypeSystemContext typeSystemContext)
        {
            _pseudoAssembly = new ManagedBinaryEmitter(typeSystemContext, "PseudoAssembly");
        }

        private ObjectNodeSection _section = new ObjectNodeSection(".psdo-il", SectionType.ReadOnly);
        public override ObjectNodeSection Section => _section;

        public ManagedBinaryEmitter PseudoAssembly => _pseudoAssembly;
        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.WindowsDebugPseudoAssemblySectionNode;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            MemoryStream memoryStream = new MemoryStream(1000000);
            _pseudoAssembly.EmitToStream(memoryStream);
            _pseudoAssembly = null;
            return new ObjectData(memoryStream.ToArray(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugPseudoAssemblySection";
        }
    }
}
