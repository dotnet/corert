// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class CoreCLRReadyToRunTypesTableNode : ObjectNode, ISymbolDefinitionNode
    {
        TargetDetails _target;
        
        List<(int Rid, EETypeNode Node)> _eeTypeNodes;
        
        public CoreCLRReadyToRunTypesTableNode(TargetDetails target)
        {
            _target = target;
            _eeTypeNodes = new List<(int Rid, EETypeNode Node)>();
        }
        
        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunAvailableTypesTable");
        }

        public int Offset => 0;

        public override bool IsShareable => false;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section
        {
            get
            {
                if (_target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public int Add(int rid, EETypeNode eeTypeNode)
        {
            int eeTypeIndex = _eeTypeNodes.Count;
            _eeTypeNodes.Add((Rid: rid, Node: eeTypeNode));
            return eeTypeIndex;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            NativeWriter writer = new NativeWriter();
            Section section = writer.NewSection();

            VertexHashtable typesHashtable = new VertexHashtable();
            section.Place(typesHashtable);
            
            foreach ((int Rid, EETypeNode Node) eeTypeNode in _eeTypeNodes)
            {
                int hashCode = eeTypeNode.Node.Type.GetHashCode();
                typesHashtable.Append(unchecked((uint)hashCode), section.Place(new UnsignedConstant((uint)eeTypeNode.Rid << 1)));
            }

            MemoryStream writerContent = new MemoryStream();
            writer.Save(writerContent);

            return new ObjectData(
                data: writerContent.ToArray(),
                relocs: null,
                alignment: 8,
                definedSymbols: new ISymbolDefinitionNode[] { this });
        }

        protected override int ClassCode => -944318825;
    }
}
