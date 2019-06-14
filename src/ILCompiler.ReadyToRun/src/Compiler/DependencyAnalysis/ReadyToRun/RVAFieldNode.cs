// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysis;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class RVAFieldNode : ObjectNode, ISymbolDefinitionNode
    {
        private ISymbolNode _sectionStartNode;

        private int _sectionRelativeOffset;

        public RVAFieldNode(ISymbolNode sectionStartNode, int sectionRelativeOffset)
        {
            _sectionStartNode = sectionStartNode;
            _sectionRelativeOffset = sectionRelativeOffset;
        }

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool IsShareable => false;

        public override int ClassCode => 543876258;

        public override bool RepresentsIndirectionCell => true;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("RVAFieldNode->");
            _sectionStartNode.AppendMangledName(nameMangler, sb);
            sb.Append($":{_sectionRelativeOffset:X4}");
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            byte[] data;
            RelocType relocType;
            switch (factory.Target.PointerSize)
            {
                case 4:
                    data = BitConverter.GetBytes(_sectionRelativeOffset);
                    relocType = RelocType.IMAGE_REL_BASED_HIGHLOW;
                    break;

                case 8:
                    data = BitConverter.GetBytes((long)_sectionRelativeOffset);
                    relocType = RelocType.IMAGE_REL_BASED_DIR64;
                    break;

                default:
                    throw new NotImplementedException();
            }

            return new ObjectData(
                data: data,
                relocs: new Relocation[] { new Relocation(relocType, 0, _sectionStartNode) },
                alignment: factory.Target.PointerSize,
                definedSymbols: new ISymbolDefinitionNode[] { this } 
            );
        }

        protected override string GetName(NodeFactory context)
        {
            Utf8StringBuilder sb = new Utf8StringBuilder();
            AppendMangledName(context.NameMangler, sb);
            return sb.ToString();
        }
    }
}
