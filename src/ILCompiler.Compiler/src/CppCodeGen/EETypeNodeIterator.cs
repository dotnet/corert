using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using System.Collections.Generic;

namespace ILCompiler.Compiler.CppCodeGen
{
    public class EETypeNodeIterator
    {
        private List<DependencyNode> _typeNodes;
        private Dictionary<TypeDesc, EETypeNode> _typeToNodeMap;

        public EETypeNodeIterator(IEnumerable<DependencyNode> nodes)
        {
            _typeNodes = new List<DependencyNode>();
            _typeToNodeMap = new Dictionary<TypeDesc, EETypeNode>();
            foreach (var node in nodes)
            {
                var typeNode = node as EETypeNode;
                if(typeNode != null)
                {
                    _typeToNodeMap[typeNode.Type] = typeNode;
                }
            }

            foreach(var node in _typeToNodeMap.Values)
            {
                AddNode(node);
            }
        }

        private void AddNode(EETypeNode node)
        {
            if(node != null && !_typeNodes.Contains(node))
            {
                EETypeNode baseTypeNode;
                if (node.Type.BaseType != null )
                {
                    _typeToNodeMap.TryGetValue(node.Type.BaseType, out baseTypeNode);
                    AddNode(baseTypeNode);
                }
                this._typeNodes.Add(node);
            }

        }
        public List<DependencyNode> GetNodes()
        {
            return _typeNodes;
        }
    }
}
