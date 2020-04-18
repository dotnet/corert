using System;
using System.Collections.Generic;
using ILCompiler.DependencyAnalysis;
using Internal.Text;

namespace ILCompiler.Compiler.DependencyAnalysis
{
    public class EHInfoNode : ObjectNode, ISymbolDefinitionNode
    {
        private readonly string _name;
        private ArrayBuilder<byte> _ehInfoBuilder;
        private readonly ObjectAndOffsetSymbolNode _endSymbol;
        private Relocation[] _relocs;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool IsShareable => false;

        public override int ClassCode => 354769872;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public EHInfoNode(string mangledName)
        {
            _name = mangledName + "___EHInfo";
            _ehInfoBuilder = new ArrayBuilder<byte>();
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, mangledName + "___EHInfo_End", true);
        }
        public int AddEHInfo(ObjectData ehInfo)
        {
            int offset = _ehInfoBuilder.Count;
            _ehInfoBuilder.Append(ehInfo.Data);
            _relocs = ehInfo.Relocs;
            return offset;
        }

        public int Count => _ehInfoBuilder.Count;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            // EH info node is a singleton in the R2R PE file
            sb.Append(_name);
        }

        public ISymbolDefinitionNode EndSymbol => _endSymbol;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            var byteArray = _ehInfoBuilder.ToArray();
            _endSymbol.SetSymbolOffset(byteArray.Length);
            return new ObjectData(byteArray, _relocs, alignment: 1, definedSymbols: new ISymbolDefinitionNode[] { this, _endSymbol });
        }

        protected override string GetName(NodeFactory context)
        {
            Utf8StringBuilder sb = new Utf8StringBuilder();
            AppendMangledName(context.NameMangler, sb);
            return sb.ToString();
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _name.CompareTo(((EHInfoNode)other)._name);
        }
    }
}
