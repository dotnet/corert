// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Internal.Text;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    class MethodEHInfoNode : ObjectNode, ISymbolDefinitionNode
    {
        private readonly MethodWithGCInfo _methodNode;

        public MethodEHInfoNode(MethodWithGCInfo methodNode)
        {
            _methodNode = methodNode;
        }

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool IsShareable => true;

        public override int ClassCode => 577012239;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("MethodEHInfoNode->");
            _methodNode.AppendMangledName(nameMangler, sb);
        }

        public override ObjectNode.ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectData sourceNode = _methodNode.EHInfo;
            if (sourceNode == null || sourceNode.Data.Length == 0)
            {
                return new ObjectNode.ObjectData(
                    Array.Empty<byte>(),
                    Array.Empty<Relocation>(),
                    1,
                    new ISymbolDefinitionNode[] { this });
            }
            ISymbolDefinitionNode[] augmentedSymbols = new ISymbolDefinitionNode[sourceNode.DefinedSymbols.Length + 1];
            Array.Copy(sourceNode.DefinedSymbols, 0, augmentedSymbols, 1, sourceNode.DefinedSymbols.Length);
            augmentedSymbols[0] = this;
            return new ObjectData(sourceNode.Data, sourceNode.Relocs, sourceNode.Alignment, augmentedSymbols);
        }

        protected override string GetName(NodeFactory context)
        {
            Utf8StringBuilder sb = new Utf8StringBuilder();
            sb.Append("MethodGCInfo->");
            _methodNode.AppendMangledName(context.NameMangler, sb);
            return sb.ToString();
        }
    }
}
